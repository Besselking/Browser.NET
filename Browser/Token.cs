namespace Browser;

public abstract record Token;

public sealed record Text(string TextContent) : Token;
public sealed record Tag(string TagName, string[] Attributes) : Token;