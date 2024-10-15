using System.Net;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Browser;

internal sealed class Layout
{
    internal DisplayList DisplayList { get; } = [];

    private readonly DisplayList _line = [];
    private float _cursorX = Constants.Hstep;
    private float _cursorY = Constants.Vstep;

    private readonly int _width;

    private readonly Stack<Style> _styleStack = [];
    private SKTypeface? _normalFont;
    private SKTypeface? _boldFont;
    private SKTypeface? _italicFont;
    private SKTypeface? _boldItalicFont;

    private Token? _prev;
    private Token? _current;

    internal Layout(int width)
    {
        _width = width;
        _styleStack.Push(new());
    }

    internal static DisplayList CreateLayout(int width, List<Token> tokens)
    {
        // Console.WriteLine("Relayout");
        Layout layout = new(width);

        foreach (Token tok in tokens)
        {
            // await Task.Delay(10);
            layout.Token(tok);
        }

        layout.Flush();

        return layout.DisplayList;
    }

    private void Token(Token tok)
    {
        _prev = _current;
        _current = tok;
        // Console.WriteLine(tok);
        switch (tok)
        {
            case Text textToken:
            {
                string whitespaceNormalized = Regex.Replace(textToken.TextContent, "[ \t]*\n[ \t]", "\n");
                var textContent = WebUtility.HtmlDecode(whitespaceNormalized).AsSpan();
                Word(textContent);
                break;
            }
            case Tag { TagName: "i" }:
            {
                _styleStack.Push(GetStyle() with
                {
                    FontStyle = "italic"
                });
                break;
            }
            case Tag { TagName: "/i" }:
            {
                _styleStack.TryPop(out _);
                break;
            }
            case Tag { TagName: "b" }:
            {
                _styleStack.Push(GetStyle() with
                {
                    FontWeight = "bold"
                });
                break;
            }
            case Tag { TagName: "/b" }:
            {
                _styleStack.TryPop(out _);
                break;
            }
            case Tag { TagName: "small" }:
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    FontSize = parentStyle.FontSize - Constants.SmallStep
                });
                break;
            }
            case Tag { TagName: "/small" }:
            {
                _styleStack.TryPop(out _);
                break;
            }
            case Tag { TagName: "big" }:
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    FontSize = parentStyle.FontSize + Constants.BigStep
                });
                break;
            }
            case Tag { TagName: "/big" }:
            {
                _styleStack.TryPop(out _);
                break;
            }
            case Tag { TagName: "br" or "br/" }:
            {
                Flush();
                break;
            }
            case Tag { TagName: "/p" }:
            {
                Flush();
                _cursorY += Constants.Vstep;
                break;
            }
            case Tag { TagName: "h1" } h1Tag:
            {
                Flush();

                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    TextAlignment = h1Tag.Attributes.Contains("class=\"title\"")
                        ? "center"
                        : "left",
                    FontSize = parentStyle.FontSize + Constants.BigStep * 2
                });
                break;
            }
            case Tag { TagName: "/h1" }:
            {
                Flush();
                _styleStack.TryPop(out _);
                break;
            }
            case Tag { TagName: "sup" }:
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    VerticalAlignment = "super",
                    FontSize = parentStyle.FontSize - Constants.BigStep
                });
                break;
            }
            case Tag { TagName: "/sup" }:
            {
                _styleStack.TryPop(out _);
                break;
            }
        }
    }

    private SKTypeface GetTypeface()
    {
        var style = GetStyle();
        if (style.FontStyle == "roman")
        {
            if (style.FontWeight == "normal")
            {
                return _normalFont ??= SKFontManager.Default.MatchFamily("SF Pro Display", SKFontStyle.Normal);
            }

            if (style.FontWeight == "bold")
            {
                return _boldFont ??= SKFontManager.Default.MatchFamily("SF Pro Display", SKFontStyle.Bold);
            }
        }
        else if (style.FontStyle == "italic")
        {
            if (style.FontWeight == "normal")
            {
                return _italicFont ??= SKFontManager.Default.MatchFamily("SF Pro Display", SKFontStyle.Italic);
            }

            if (style.FontWeight == "bold")
            {
                return _boldItalicFont ??= SKFontManager.Default.MatchFamily("SF Pro Display", SKFontStyle.BoldItalic);
            }
        }

        return _normalFont ??= SKFontManager.Default.MatchFamily("SF Pro Display", SKFontStyle.Normal);
    }

    private void Word(ReadOnlySpan<char> textContent)
    {
        if (textContent.IsEmpty) return;

        SKTypeface typeface = GetTypeface();

        var style = GetStyle();

        SKPaint paint = new SKPaint(typeface.ToFont())
        {
            TextSize = style.FontSize,
            IsAntialias = true,
        };

        ReadOnlySpan<char> text = textContent;

        while (!text.IsEmpty)
        {
            long chars = paint.BreakText(text, _width - _cursorX - Constants.Hstep, out float measuredWidth);
            if (chars != text.Length)
            {
                int lastBreak = text[..((int)chars)].LastIndexOfAny(" \n");
                if (lastBreak != -1)
                {
                    var brokenText = text[..lastBreak];

                    Words displayWord = new(style, paint, new(_cursorX, _cursorY), brokenText.ToString());
                    _line.Add(displayWord);
                    Flush();
                    text = text[(lastBreak + 1)..];
                }
                else
                {
                    Flush();
                    text = text[1..];
                    continue;
                }
            }
            else
            {
                Words displayWord = new(style, paint, new(_cursorX, _cursorY), text.ToString());
                _line.Add(displayWord);
                text = [];
                _cursorX += measuredWidth;
            }
        }
    }

    private void Flush()
    {
        if (_line.Count == 0) return;

        float maxSpacing = _line.Max(di => di.Paint.FontSpacing);
        float baseline = _cursorY + maxSpacing;

        float lineCenter = (_cursorX - Constants.Hstep) / 2f;
        float windowCenter = _width / 2f;
        float centerOffset = windowCenter - lineCenter;

        var style = GetStyle();
        bool alignCenter = style.TextAlignment == "center";

        foreach (DisplayItem item in _line.AsSpan())
        {
            var x = alignCenter ? item.Pos.X + centerOffset : item.Pos.X;
            var y = GetTextAlignmentPos(item, baseline);
            DisplayList.Add(item with { Pos = new(x, y) });
        }

        _cursorY = baseline;
        _cursorX = Constants.Hstep;
        _line.Clear();
    }

    private float GetTextAlignmentPos(DisplayItem item, float baseline)
    {
        return item.Style.VerticalAlignment switch
        {
            "super" => item.Paint.FontMetrics.Ascent + baseline,
            _ => baseline,
        };
    }

    public Style GetStyle()
    {
        if (!_styleStack.TryPeek(out Style? style))
        {
            Console.WriteLine("Stack underflow!");
            Console.WriteLine($"Previous: {_prev}");
            Console.WriteLine($"Current: {_current}");
            style = new();
        }

        return style;
    }
}
