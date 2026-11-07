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
/// The game screen, laid out on a fixed 1280×800 canvas scaled to fit the window: the first-person view (the
/// software renderer's framebuffer, scaled 1.5×) with floating numbers over it, a side panel with the compass,
/// automap and message log, and the hero's bar with HP, Stamina, Mana and the two hands. Keys go through
/// <see cref="InputHandler"/>; a click in the view asks the renderer what is under the pointer.
/// </summary>
public sealed partial class GameView : Control, ICustomHitTest
{
    public const double CanvasWidth = 1280, CanvasHeight = 800;
    public static readonly Rect ViewArea = new(0, 0, 960, 600);
    public static readonly Rect SideArea = new(960, 0, 320, 600);
    public static readonly Rect BarArea = new(0, 600, 1280, 200);
    public static readonly Rect LeftHandBox = new(560, 624, 96, 96);
    public static readonly Rect RightHandBox = new(672, 624, 96, 96);

    private static readonly Typeface Face = new("Inter");
    private static readonly Typeface Bold = new("Inter", FontStyle.Normal, FontWeight.Bold);

    private readonly WriteableBitmap _bitmap;
    private readonly bool _loop;
    private readonly Func<GameEngine>? _restart;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SpriteFactory _art = new();
    private readonly List<(FloatText F, double Age)> _floats = new();
    private TimeSpan _last;
    private bool _attached;
    private double _fps;
    private HandSide? _mouseHand;

    public GameEngine Engine { get; private set; }
    public SoftwareViewRenderer Renderer { get; }

    /// <summary>F3 shows frames per second and the render time.</summary>
    public bool ShowStats { get; set; }
    public double LastRenderMs { get; private set; }

    /// <param name="startLoop">False for tests: nothing moves until <see cref="Advance"/> is called.</param>
    /// <param name="restart">Makes a fresh game when the player starts again after dying or winning.</param>
    public GameView(GameEngine engine, bool startLoop, Func<GameEngine>? restart = null)
    {
        Engine = engine;
        _restart = restart;
        Engine.Float += OnFloat;
        Renderer = new SoftwareViewRenderer(TextureFactory.Cellar());
        _bitmap = new WriteableBitmap(new PixelSize(Renderer.Width, Renderer.Height), new Vector(96, 96),
                                      PixelFormats.Bgra8888, AlphaFormat.Opaque);
        _loop = startLoop;
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        DrawView();
    }

    private void OnFloat(FloatText f) => _floats.Add((f, 0));

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
        if (!Engine.Paused)
        {
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var (f, age) = _floats[i];
                age += Math.Min(seconds, 0.25);
                if (age > 1.2) _floats.RemoveAt(i);
                else _floats[i] = (f, age);
            }
        }
        DrawView();
        InvalidateVisual();
    }

    /// <summary>Starts a fresh game (after dying or winning).</summary>
    public void Restart()
    {
        if (_restart is null) return;
        Engine.Float -= OnFloat;
        Engine = _restart();
        Engine.Float += OnFloat;
        _floats.Clear();
        DrawView();
        InvalidateVisual();
    }

    private unsafe void DrawView()
    {
        var t0 = _clock.Elapsed;
        double alpha = Engine.Alpha;
        var cam = Engine.Camera(alpha);
        Renderer.Render(Engine.Level, cam, Engine.Time + alpha * GameEngine.TickSeconds, Scene.Build(Engine, cam, _art, alpha));
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

    // ---- Layout ----

    /// <summary>The canvas's scale and offset in the control.</summary>
    private (double Scale, double X, double Y) Fit()
    {
        double s = Math.Min(Bounds.Width / CanvasWidth, Bounds.Height / CanvasHeight);
        return (s, (Bounds.Width - CanvasWidth * s) / 2, (Bounds.Height - CanvasHeight * s) / 2);
    }

    private Rect ToControl(Rect r)
    {
        var (s, ox, oy) = Fit();
        return new Rect(ox + r.X * s, oy + r.Y * s, r.Width * s, r.Height * s);
    }

    private Point ToCanvas(Point p)
    {
        var (s, ox, oy) = Fit();
        return s <= 0 ? default : new Point((p.X - ox) / s, (p.Y - oy) / s);
    }

    /// <summary>Where the first-person view is drawn, in control coordinates.</summary>
    public Rect ViewRect() => ToControl(ViewArea);

    public Rect HandRect(HandSide side) => ToControl(side == HandSide.Left ? LeftHandBox : RightHandBox);

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var (s, ox, oy) = Fit();
        if (s <= 0) return;
        using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy)))
        using (dc.PushClip(new Rect(0, 0, CanvasWidth, CanvasHeight)))
        {
            DrawViewArea(dc);
            DrawSidePanel(dc);
            DrawBar(dc);
            DrawOverlays(dc);
        }
    }

    // ---- Input ----

    /// <summary>The whole control takes clicks, not only the parts it has drawn on.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        switch (e.PhysicalKey)
        {
            case PhysicalKey.F3:
                ShowStats = !ShowStats;
                InvalidateVisual();
                return;
            case PhysicalKey.Space:
                Engine.TogglePause();
                InvalidateVisual();
                return;
            case PhysicalKey.Enter or PhysicalKey.NumPadEnter when Engine.Mode != GameMode.Playing:
                Restart();
                return;
        }
        if (InputHandler.MapHand(e.PhysicalKey) is { } hand)
        {
            Engine.HandDown(hand);
            return;
        }
        if (InputHandler.Map(e.PhysicalKey) is { } action)
        {
            Engine.Press(action);
            return;
        }
        e.Handled = false;
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (InputHandler.MapHand(e.PhysicalKey) is { } hand)
        {
            Engine.HandUp(hand);
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        var p = ToCanvas(e.GetPosition(this));
        e.Handled = true;
        if (props.IsLeftButtonPressed)
        {
            if (LeftHandBox.Contains(p)) { _mouseHand = HandSide.Left; Engine.HandDown(HandSide.Left); return; }
            if (RightHandBox.Contains(p)) { _mouseHand = HandSide.Right; Engine.HandDown(HandSide.Right); return; }
            if (Engine.Mode != GameMode.Playing) { Restart(); return; }
            if (!ViewArea.Contains(p)) return;
            int fx = (int)((p.X - ViewArea.X) / ViewArea.Width * Renderer.Width);
            int fy = (int)((p.Y - ViewArea.Y) / ViewArea.Height * Renderer.Height);
            if (Renderer.Pick(fx, fy) is { } target && Engine.ClickWorld(target)) InvalidateVisual();
        }
        else if (props.IsRightButtonPressed && ViewArea.Contains(p))
        {
            // Right-click in the view swings the weapon, held for a power attack.
            _mouseHand = HandSide.Right;
            Engine.HandDown(HandSide.Right);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_mouseHand is { } hand)
        {
            Engine.HandUp(hand);
            _mouseHand = null;
            e.Handled = true;
        }
    }

    // ---- Drawing helpers ----

    private static FormattedText Text(string text, double size, IBrush brush, Typeface? face = null, double maxWidth = 0)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face ?? Face, size, brush);
        if (maxWidth > 0) ft.MaxTextWidth = maxWidth;
        return ft;
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, double size, IBrush brush,
                                 bool centre = false, Typeface? face = null, double maxWidth = 0)
    {
        var ft = Text(text, size, brush, face, maxWidth);
        var at = new Point(centre ? x - ft.Width / 2 : x, y);
        dc.DrawText(Text(text, size, Brushes.Black, face, maxWidth), at + new Point(1, 1));
        dc.DrawText(ft, at);
    }

    private static string Fmt(double v) => v.ToString("0", CultureInfo.InvariantCulture);
}
