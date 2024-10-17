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

    private Node? _prev;
    private Node? _current;

    internal Layout(int width)
    {
        _width = width;
        _styleStack.Push(new());
    }

    internal static DisplayList CreateLayout(int width, Node rootnode)
    {
        Layout layout = new(width);

        layout.Recurse(rootnode);
        layout.Flush();

        return layout.DisplayList;
    }

    private void Recurse(Node tree)
    {
        _prev = _current;
        _current = tree;
        switch (tree)
        {
            case Text text:
            {
                var textContent = WebUtility.HtmlDecode(text.TextContent).AsSpan();
                Word(textContent);
                break;
            }
            case Element elem:
            {
                OpenTag(elem);
                foreach (Node child in elem.Children)
                {
                    Recurse(child);
                }

                CloseTag(elem);
                break;
            }
        }
    }

    private void OpenTag(Element element)
    {
        string tagName = element.Tag.ToLower();
        switch (tagName)
        {
            case "i":
            {
                _styleStack.Push(GetStyle() with
                {
                    FontStyle = "italic"
                });
                break;
            }
            case "b":
            {
                _styleStack.Push(GetStyle() with
                {
                    FontWeight = "bold"
                });
                break;
            }
            case "small":
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    FontSize = parentStyle.FontSize - Constants.SmallStep
                });
                break;
            }
            case "big":
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    FontSize = parentStyle.FontSize + Constants.BigStep
                });
                break;
            }
            case "br" or "br/":
            {
                Flush();
                break;
            }
            case "h1":
            {
                Flush();

                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    TextAlignment = element.Attributes.ContainsKey("title")
                        ? "center"
                        : "left",
                    FontSize = parentStyle.FontSize + Constants.BigStep * 2
                });
                break;
            }
            case "sup":
            {
                var parentStyle = GetStyle();
                _styleStack.Push(parentStyle with
                {
                    VerticalAlignment = "super",
                    FontSize = parentStyle.FontSize - Constants.BigStep
                });
                break;
            }
        }
    }

    private void CloseTag(Element elem)
    {
        string tag = elem.Tag.ToLower();
        switch (tag)
        {
            case "i":
            case "b":
            case "small":
            case "big":
            case "sup":
            {
                _styleStack.TryPop(out _);
                break;
            }
            case "p":
            {
                Flush();
                _cursorY += Constants.Vstep;
                break;
            }
            case "h1":
            {
                Flush();
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
                int lastBreak = text[..(int)chars].LastIndexOfAny(" \n");
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
