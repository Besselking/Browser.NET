using System.Text;

namespace Browser;

internal abstract record Node(Node? Parent, List<Node> Children)
{
    public void PrintTree()
    {
        StringBuilder sb = new();
        PrintTree(sb, 0);
        Console.WriteLine(sb.ToString());
    }

    public void PrintTree(StringBuilder sb, int indent)
    {
        sb.Append(' ', indent).AppendLine(ToString());
        foreach (Node child in Children)
        {
            child.PrintTree(sb, indent + 2);
        }
    }
}

internal sealed record Text(Node Parent, List<Node> Children, string TextContent) : Node(Parent, Children)
{
    public override string ToString()
    {
        return TextContent;
    }
}

internal sealed record Element(Node? Parent, List<Node> Children, string Tag, Dictionary<string, string> Attributes)
    : Node(Parent, Children)
{
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(Tag.Length + 2)
            .Append('<')
            .Append(Tag);

        if (Attributes.Count > 0)
        {
            sb.Append(' ')
                .AppendJoin(' ', Attributes.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
        }

        return sb
            .Append('>')
            .ToString();
    }
}