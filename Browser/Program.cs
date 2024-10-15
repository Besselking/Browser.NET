using System.Text;

namespace Browser;

public static class Program
{
    private static readonly HttpClient SharedClient = new()
    {
        // BaseAddress = new Uri("https://example.com"),
    };

    private static List<Token> _tokens = [];

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: Browser.NET <uri>");
            return;
        }

        if (!Uri.TryCreate(args[0], UriKind.RelativeOrAbsolute, out Uri? url))
        {
            Console.Error.WriteLine("URI is invalid.");
            return;
        }

        if (url.Scheme is "https" or "http")
        {
            HttpScheme(url);
        }
        else if (url.IsFile)
        {
            FileScheme(url);
        }
        else if (url.Scheme is "data")
        {
            if (!DataScheme(url))
            {
                return;
            }
        }

        OpenTkMain(url.Host);
    }

    private static void HttpScheme(Uri url)
    {
        using HttpResponseMessage response = SharedClient.GetAsync(url).Result;

        response.EnsureSuccessStatusCode()
            .WriteRequestToConsole();

        _tokens = response.Content.Lex();
    }

    private static void FileScheme(Uri url)
    {
        _tokens = new FileInfo(url.LocalPath).Lex();
    }

    private static bool DataScheme(Uri url)
    {
        Span<Range> splitDataUrl = stackalloc Range[2];
        var localPath = url.LocalPath.AsSpan();
        int splitCount = localPath.Split(splitDataUrl, ',');

        var contentType = localPath[splitDataUrl[0]];
        if (contentType is not "text/html")
        {
            Console.Error.Write($"Unexpected content-type \"{contentType}\"");
            return false;
        }

        if (splitCount != 2)
        {
            Console.Error.Write("No content in data-url");
            return false;
        }

        var htmlString = localPath[splitDataUrl[1]].ToString();
        using StringReader reader = new(htmlString);
        _tokens = reader.Lex();

        return true;
    }

    private static void OpenTkMain(string host)
    {
        using Window wnd = new Window(host, _tokens);
        wnd.Run();
    }
}

static class HttpResponseMessageExtensions
{
    internal static void WriteRequestToConsole(this HttpResponseMessage response)
    {
        var request = response.RequestMessage;
        Console.Write($"{request?.Method} ");
        Console.Write($"{request?.RequestUri} ");
        Console.WriteLine($"HTTP/{request?.Version}");
    }

    internal static List<Token> Lex(this HttpContent content)
    {
        using Stream stream = content.ReadAsStream();
        using StreamReader reader = new(stream);
        return Lex(reader);
    }

    internal static List<Token> Lex(this FileInfo fileInfo)
    {
        using FileStream stream = fileInfo.Open(FileMode.Open);
        using StreamReader reader = new(stream);

        return Lex(reader);
    }

    public static List<Token> Lex(this TextReader reader)
    {
        List<Token> tokens = [];
        StringBuilder buffer = new StringBuilder();

        bool inTag = false;

        int c;
        while ((c = reader.Read()) != -1)
        {
            switch (c)
            {
                case '<':
                    inTag = true;
                    if (buffer.Length > 0)
                    {
                        tokens.Add(new Text(buffer.ToString()));
                    }

                    buffer.Clear();
                    break;
                case '>':
                    inTag = false;
                    string[] tagInfo = buffer.ToString().Split(' ');
                    tokens.Add(new Tag(tagInfo[0], tagInfo[1..]));
                    buffer.Clear();
                    break;
                default:
                    buffer.Append((char)c);
                    break;
            }
        }

        if (!inTag && buffer.Length > 0)
        {
            tokens.Add(new Text(buffer.ToString()));
        }

        return tokens;
    }
}
