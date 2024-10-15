using System.Numerics;
using SkiaSharp;

namespace Browser;

internal abstract record DisplayItem(Style Style, SKPaint Paint, Vector2 Pos);

internal sealed record Words(Style Style, SKPaint Paint, Vector2 Pos, string Text) : DisplayItem(Style, Paint, Pos);