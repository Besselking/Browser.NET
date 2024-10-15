using System.Numerics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;

namespace Browser;

public class Window : GameWindow
{
    private readonly List<Token> _tokens;
    private GRContext? _skiaCtx;
    private SKSurface? _skSurface;
    private DisplayList _displayList;

    public Window(string title, List<Token> tokens)
        : base(GetDefaultDws(), GetDefaultNws(title))
    {
        _tokens = tokens;
        _displayList = [];
    }

    // private int WindowWidth = 1280, WindowHeight = 720;

    private static NativeWindowSettings GetDefaultNws(string host)
    {
        var nws = new NativeWindowSettings
        {
            WindowBorder = WindowBorder.Resizable,
            WindowState = WindowState.Normal,
            Title = $"Browser - {host}",
        };

        var monitors = Monitors.GetMonitors();
        if (monitors.Count > 0)
        {
            var info = monitors[0];
            var area = info.ClientArea;
            nws.WindowState = WindowState.Normal;
            nws.Location = area.Min;
            nws.ClientSize = area.Size;
            nws.Profile = ContextProfile.Core;
        }

        return nws;
    }

    private static GameWindowSettings GetDefaultDws()
    {
        return new GameWindowSettings
        {
            UpdateFrequency = 60.0
        };
    }

    private int ScollStop => FramebufferSize.Y / 2;

    protected override void OnLoad()
    {
        base.OnLoad();
        SkiaInit();

        _displayList = Layout.CreateLayout(FramebufferSize.X, _tokens);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        Vector2 deltaScroll = new Vector2(e.OffsetX, e.OffsetY);
        _scroll -= deltaScroll * 2;
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        SkiaResize(e.Width, e.Height);

        _displayList = Layout.CreateLayout(e.Width, _tokens);
        Draw();

        base.OnFramebufferResize(e);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (_scroll.Y < 0)
        {
            _scroll += (float)args.Time * 5 * (_scroll with { Y = 0 } - _scroll);
        }

        if (_scroll.X != 0)
        {
            _scroll += (float)args.Time * 5 * (_scroll with { X = 0 } - _scroll);
        }

        if (_scroll.Y > _displayList.MaxY - ScollStop)
        {
            if (FramebufferSize.Y > _displayList.MaxY)
            {
                _scroll += (float)args.Time * 5 * (Vector2.Zero - _scroll);
            }
            else
            {
                _scroll += (float)args.Time * 5 * (new Vector2(0, _displayList.MaxY - ScollStop) - _scroll);
            }
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        Draw();
        base.OnRenderFrame(args);
    }

    private void Draw()
    {
        SkiaRender();
        SwapBuffers();
    }

    private void SkiaInit()
    {
        _skiaCtx = GRContext.CreateGl();
        SkiaResize(FramebufferSize.X, FramebufferSize.Y);
    }

    private void SkiaCleanUp()
    {
        _skiaCtx?.Dispose();
        _skSurface?.Dispose();
    }

    private void SkiaResize(int w, int h)
    {
        GL.Viewport(0, 0, w, h);

        if (_skiaCtx == null) SkiaInit();

        GRGlFramebufferInfo fbi = new GRGlFramebufferInfo(0, (uint)InternalFormat.Rgba8);
        var ctype = SKColorType.Rgba8888;
        var beTarget = new GRBackendRenderTarget(w, h, 0, 0, fbi);

        // Dispose Previous Surface
        _skSurface?.Dispose();
        _skSurface = SKSurface.Create(_skiaCtx, beTarget, GRSurfaceOrigin.BottomLeft, ctype, null, null);
        if (_skSurface == null)
        {
            Close();
        }
    }

    protected override void OnUnload()
    {
        SkiaCleanUp();
        base.OnUnload();
    }

    private void SkiaRender()
    {
        var ctx = _skSurface!.Canvas;
        // Put draw code here
        ctx.DrawColor(SKColors.White);

        DrawLayout(ctx);
        DrawScrollbar(ctx);

        _skiaCtx!.Flush();
    }

    private void DrawScrollbar(SKCanvas ctx)
    {
        var frameSize = FramebufferSize;

        if (_displayList.MaxY < frameSize.Y)
        {
            return;
        }

        float factorStart = _scroll.Y / _displayList.MaxY;
        float barYStart = frameSize.Y * factorStart;

        float factorEnd = (_scroll.Y + ScollStop) / _displayList.MaxY;
        float barYEnd = frameSize.Y * factorEnd;

        SKRect rect = new SKRect(frameSize.X - Constants.Hstep, barYStart, frameSize.X, barYEnd);
        SKRoundRect roundRect = new SKRoundRect(rect, 50f);
        ctx.DrawRoundRect(roundRect, new SKPaint
        {
            Color = SKColors.Gray,
        });
    }

    private void DrawLayout(SKCanvas ctx)
    {
        var displaySpan = _displayList.AsSpan();

        foreach (var item in displaySpan)
        {
            RenderDisplayItem(ctx, FramebufferSize.Y, item);
        }
    }

    private static Vector2 _scroll;

    private static void RenderDisplayItem(SKCanvas ctx, int screenHeight, DisplayItem item)
    {
        switch (item)
        {
            case Words word:
            {
                RenderWord(ctx, screenHeight, word);
                break;
            }
        }
    }

    private static void RenderWord(SKCanvas ctx, int screenHeight, Words word)
    {
        if (word.Pos.Y - word.Paint.TextSize > _scroll.Y + screenHeight
            || word.Pos.Y + Constants.Vstep < _scroll.Y)
        {
            return;
        }

        Vector2 cursor = word.Pos - _scroll;

        ctx.DrawText(word.Text, cursor.X, cursor.Y, word.Paint);
    }
}