using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using AVAHack.App.Input;
using AVAHack.Game;
using AVAHack.Render;
using AVAHack.World;

namespace AVAHack.App.Views;

/// <summary>
/// The game screen. Each frame it runs the engine's clock, has the software renderer draw the first-person
/// view into its framebuffer, copies that into a bitmap and scales it to fit the window at 16:10, with a
/// compass and the latest message on top. Keys go through <see cref="InputHandler"/>; a click in the view
/// asks the renderer what is under the pointer and hands that to the engine.
/// </summary>
public sealed class GameView : Control, ICustomHitTest
{
    private static readonly Typeface Face = new("Inter");
    private readonly WriteableBitmap _bitmap;
    private readonly bool _loop;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _last;
    private bool _attached;
    private double _fps;

    public GameEngine Engine { get; }
    public SoftwareViewRenderer Renderer { get; }

    /// <summary>F3 shows frames per second and the render time.</summary>
    public bool ShowStats { get; set; }
    public double LastRenderMs { get; private set; }

    /// <param name="startLoop">False for tests: nothing moves until <see cref="Advance"/> is called.</param>
    public GameView(GameEngine engine, bool startLoop)
    {
        Engine = engine;
        Renderer = new SoftwareViewRenderer(TextureFactory.Cellar());
        _bitmap = new WriteableBitmap(new PixelSize(Renderer.Width, Renderer.Height), new Vector(96, 96),
                                      PixelFormats.Bgra8888, AlphaFormat.Opaque);
        _loop = startLoop;
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        DrawView();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Focus();
        if (_loop)
        {
            _last = _clock.Elapsed;
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_attached) return;
        var now = _clock.Elapsed;
        double dt = (now - _last).TotalSeconds;
        _last = now;
        if (dt > 0) _fps = _fps * 0.9 + 0.1 / dt;
        Advance(dt);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    /// <summary>Runs the game forward <paramref name="seconds"/> and redraws.</summary>
    public void Advance(double seconds)
    {
        Engine.Update(seconds);
        DrawView();
        InvalidateVisual();
    }

    private unsafe void DrawView()
    {
        var t0 = _clock.Elapsed;
        double alpha = Engine.Alpha;
        Renderer.Render(Engine.Level, Engine.Camera(alpha), Engine.Time + alpha * GameEngine.TickSeconds);
        using (var fb = _bitmap.Lock())
        {
            var src = Renderer.Pixels;
            int w = Renderer.Width, h = Renderer.Height;
            fixed (uint* p = src)
            {
                for (int y = 0; y < h; y++)
                    Buffer.MemoryCopy(p + y * w, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, w * 4L);
            }
        }
        LastRenderMs = (_clock.Elapsed - t0).TotalMilliseconds;
    }

    /// <summary>Where the view is drawn in the control: as large as fits at the framebuffer's shape.</summary>
    public Rect ViewRect()
    {
        double s = Math.Min(Bounds.Width / Renderer.Width, Bounds.Height / Renderer.Height);
        double w = Renderer.Width * s, h = Renderer.Height * s;
        return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
    }

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var view = ViewRect();
        if (view.Width <= 0 || view.Height <= 0) return;
        dc.DrawImage(_bitmap, new Rect(0, 0, Renderer.Width, Renderer.Height), view);

        double scale = view.Height / Renderer.Height;
        DrawCompass(dc, view, scale);
        if (Engine.LastMessage is { } msg)
            DrawText(dc, msg, view.Center.X, view.Bottom - 26 * scale, 11 * scale, Brushes.Wheat, centre: true);
        if (ShowStats)
            DrawText(dc, string.Create(CultureInfo.InvariantCulture, $"{_fps:F0} fps  ·  view {LastRenderMs:F1} ms  ·  {Engine.PartyCell} {Engine.Facing}"),
                     view.X + 8 * scale, view.Y + 6 * scale, 9 * scale, Brushes.LightGreen, centre: false);
    }

    private void DrawCompass(DrawingContext dc, Rect view, double scale)
    {
        double cx = view.Center.X, y = view.Y + 8 * scale;
        var plate = new Rect(cx - 22 * scale, y, 44 * scale, 22 * scale);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 20, 16, 12)), new Pen(new SolidColorBrush(Color.FromRgb(120, 96, 60)), 1.5 * scale), plate, 4 * scale, 4 * scale);
        DrawText(dc, Engine.Facing.Letter().ToString(), cx, y + 2 * scale, 14 * scale, Brushes.Goldenrod, centre: true);
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, double size, IBrush brush, bool centre)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, Math.Max(6, size), brush);
        var at = new Point(centre ? x - ft.Width / 2 : x, y);
        var shadow = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, Math.Max(6, size), Brushes.Black);
        dc.DrawText(shadow, at + new Point(1, 1));
        dc.DrawText(ft, at);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.PhysicalKey == PhysicalKey.F3)
        {
            ShowStats = !ShowStats;
            InvalidateVisual();
        }
        else if (InputHandler.Map(e.PhysicalKey) is { } action)
        {
            Engine.Press(action);
        }
        else
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
    }

    /// <summary>The whole control takes clicks, not only the parts it has drawn on.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var view = ViewRect();
        var p = e.GetPosition(this);
        if (!view.Contains(p)) return;
        int fx = (int)((p.X - view.X) / view.Width * Renderer.Width);
        int fy = (int)((p.Y - view.Y) / view.Height * Renderer.Height);
        if (Renderer.Pick(fx, fy) is { } target && Engine.ClickWorld(target)) InvalidateVisual();
        e.Handled = true;
    }
}
