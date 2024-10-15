namespace Browser;

internal sealed record Style(
    string FontWeight = "normal",
    string FontStyle = "roman",
    string TextAlignment = "left",
    string VerticalAlignment = "baseline",
    int FontSize = Constants.FontSize
);