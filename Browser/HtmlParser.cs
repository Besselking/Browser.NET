using System.Buffers;
using System.Text;

namespace Browser;

internal class HtmlParser
{
    private readonly TextReader _body;
    private readonly Stack<Element> _unfinished;

    private static readonly SearchValues<string> SelfClosingTags = SearchValues.Create([
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    ], StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<string> HeadTags = SearchValues.Create([
        "base", "basefont", "bgsound", "noscript", "link", "meta", "title", "style", "script"
    ], StringComparison.OrdinalIgnoreCase);

    internal HtmlParser(TextReader body)
    {
        _body = body;
        _unfinished = [];
    }

    internal Node Parse()
    {
        StringBuilder text = new();
        bool inTag = false;

        int c;
        while ((c = _body.Read()) != -1)
        {
            switch (c)
            {
                case '<':
                    inTag = true;
                    if (text.Length > 0)
                    {
                        AddText(text);
                    }

                    break;
                case '>':
                    inTag = false;
                    AddTag(text);
                    break;
                default:
                    text.Append((char)c);
                    break;
            }
        }

        if (!inTag && text.Length > 0)
        {
            AddText(text);
        }

        return Finish();
    }

    private void AddText(StringBuilder text)
    {
        string textContent = text.ToString();
        text.Clear();

        if (String.IsNullOrWhiteSpace(textContent))
        {
            return;
        }

        ImplicitTags(null);

        var parent = _unfinished.Peek();
        var node = new Text(parent, [], textContent);
        parent.Children.Add(node);
    }

    private void AddTag(StringBuilder text)
    {
        AddTag(text.ToString());
        text.Clear();
    }

    private void AddTag(string text)
    {
        string[] tagInfo = text.Split(' ');

        string tag = tagInfo[0];
        if (tag.StartsWith('!'))
        {
            return;
        }

        ImplicitTags(tag);

        Dictionary<string, string> attributes = GetAttributes(tagInfo[1..]);

        if (tag.StartsWith('/'))
        {
            if (_unfinished.Count == 1)
            {
                return;
            }

            var node = _unfinished.Pop();
            var parent = _unfinished.Peek();
            parent.Children.Add(node);
        }
        else if (SelfClosingTags.Contains(tag))
        {
            var parent = _unfinished.Peek();
            var node = new Element(parent, [], tag, attributes);
            parent.Children.Add(node);
        }
        else
        {
            var parent = _unfinished.TryPeek(out Element? parentNode) ? parentNode : null;
            var node = new Element(parent, [], tag, attributes);
            _unfinished.Push(node);
        }
    }

    private Dictionary<string, string> GetAttributes(string[] attrpairs)
    {
        if (attrpairs.Length == 0)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        if (attrpairs[^1] is "/")
        {
            attrpairs = attrpairs[..^1];
        }

        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
        foreach (string attrpair in attrpairs)
        {
            if (attrpair.Contains('='))
            {
                string[] keyValueStrings = attrpair.Split('=', 2);
                var key = keyValueStrings[0];
                var value = keyValueStrings[1];

                if (value.Length > 2 && value[0] is ('\'' or '"'))
                {
                    value = value[1..^1];
                }

                attributes[key] = value;
            }
            else
            {
                attributes[attrpair] = String.Empty;
            }
        }

        return attributes;
    }

    private Node Finish()
    {
        if (_unfinished.Count == 0)
        {
            ImplicitTags(null);
        }

        while (_unfinished.Count > 1)
        {
            var node = _unfinished.Pop();
            var parent = _unfinished.Peek();
            parent.Children.Add(node);
        }

        return _unfinished.Pop();
    }

    private void ImplicitTags(string? tag)
    {
        tag = tag?.ToLower();
        while (true)
        {
            var openTags = _unfinished.Select(node => node.Tag).ToArray();

            if (openTags.Length == 0 && tag is not "html")
            {
                AddTag("html");
            }
            else if (openTags is ["html"] && tag is not ("head" or "body" or "/html"))
            {
                AddTag(HeadTags.Contains(tag) ? "head" : "body");
            }
            else if (openTags is ["html", "head"] && (tag is not "/head" || HeadTags.Contains(tag)))
            {
                AddTag("/head");
            }
            else
            {
                break;
            }
        }
    }
}