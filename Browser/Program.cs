using System.Text;

namespace Browser;

public static class Program
{
    private static readonly HttpClient SharedClient = new()
    {
        // BaseAddress = new Uri("https://example.com"),
    };

    private static Node? _rootnode = null;

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

        _rootnode = response.Content.Parse();
    }

    private static void FileScheme(Uri url)
    {
        _rootnode = new FileInfo(url.LocalPath).Parse();
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
        _rootnode = reader.Parse();

        return true;
    }

    private static void OpenTkMain(string host)
    {
        ArgumentNullException.ThrowIfNull(_rootnode);
        using Window wnd = new Window(host, _rootnode);
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

    internal static Node Parse(this HttpContent content)
    {
        using Stream stream = content.ReadAsStream();
        using StreamReader reader = new(stream);
        return Parse(reader);
    }

    internal static Node Parse(this FileInfo fileInfo)
    {
        using FileStream stream = fileInfo.Open(FileMode.Open);
        using StreamReader reader = new(stream);

        return Parse(reader);
    }

    public static Node Parse(this TextReader reader)
    {
        var parser = new HtmlParser(reader);
        var node = parser.Parse();
        node.PrintTree();
        return node;
    }
}
