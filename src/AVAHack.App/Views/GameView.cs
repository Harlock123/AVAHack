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
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Render;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.App.Views;

public enum Screen { Title, Create, Load, Play }

public enum Overlay { None, Inventory, Menu, Character, Spellbook, Help }

/// <summary>
/// The game window's one control. Everything is drawn on a fixed 1280×800 canvas scaled to fit: the title, the
/// party creation screens, and the game itself (the first-person view, a side panel with automap and log, the
/// party bar) with its overlays (inventory, menu, character sheet). Buttons are rectangles registered while
/// drawing; a click goes to the topmost one under the pointer, or else to the game.
/// </summary>
public sealed partial class GameView : Control, ICustomHitTest
{
    public const double CanvasWidth = 1280, CanvasHeight = 800;
    public static readonly Rect ViewArea = new(0, 0, 960, 600);
    public static readonly Rect SideArea = new(960, 0, 320, 600);
    public static readonly Rect BarArea = new(0, 600, 1280, 200);

    private static readonly Typeface Face = new("Inter");
    private static readonly Typeface Bold = new("Inter", FontStyle.Normal, FontWeight.Bold);

    private readonly WriteableBitmap _bitmap;
    private readonly bool _loop;
    private readonly Func<GameEngine>? _restart;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SpriteFactory _art = new();
    private readonly List<(FloatText F, double Age)> _floats = new();
    private readonly List<(Rect R, Action Left, Action? Right)> _buttons = new();
    private TimeSpan _last;
    private bool _attached;
    private double _fps;
    private (Character Hero, HandSide Side)? _mouseHand;
    private Point _pointer;

    public ContentDb Content { get; }
    public SaveStore Store { get; }
    public GameEngine Engine { get; private set; }
    public SoftwareViewRenderer Renderer { get; }
    public Screen Screen { get; private set; }
    public Overlay Overlay { get; set; }

    /// <summary>F3 shows frames per second and the render time.</summary>
    public bool ShowStats { get; set; }
    public double LastRenderMs { get; private set; }

    /// <summary>Straight into a game (tests, and starting again).</summary>
    public GameView(GameEngine engine, bool startLoop, Func<GameEngine>? restart = null, SaveStore? store = null)
    {
        Content = engine.Content;
        Store = store ?? new SaveStore();
        Engine = engine;
        _restart = restart;
        Screen = Screen.Play;
        Renderer = new SoftwareViewRenderer(TextureFactory.Cellar());
        _bitmap = new WriteableBitmap(new PixelSize(Renderer.Width, Renderer.Height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);
        _loop = startLoop;
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Hook(engine);
        DrawView();
    }

    /// <summary>The title screen, with the cellars dimly behind it.</summary>
    public GameView(ContentDb content, bool startLoop, SaveStore? store = null)
        : this(Backdrop(content), startLoop, null, store)
    {
        Screen = Screen.Title;
    }

    private static GameEngine Backdrop(ContentDb content)
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 1, DefaultParty(content), content);
        g.Stand(new Cell(13, 3), Dir.East);
        return g;
    }

    /// <summary>The quick-start party with its kit.</summary>
    public static Party DefaultParty(ContentDb content)
    {
        var heroes = CharacterBuilder.QuickStartParty(content);
        foreach (var h in heroes) StartingKit.Give(content, h);
        return new Party(heroes.ToArray());
    }

    private void Hook(GameEngine g) => g.Float += OnFloat;
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

    /// <summary>Runs the game forward <paramref name="seconds"/> and redraws. Menus and the inventory pause it.</summary>
    public void Advance(double seconds)
    {
        if (Screen == Screen.Play && Overlay is Overlay.None or Overlay.Help) Engine.Update(seconds);
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

    /// <summary>Plays <paramref name="g"/> from now on.</summary>
    public void StartGame(GameEngine g)
    {
        Engine.Float -= OnFloat;
        Engine = g;
        Hook(g);
        _floats.Clear();
        Screen = Screen.Play;
        Overlay = Overlay.None;
        DrawView();
        InvalidateVisual();
    }

    /// <summary>After dying or winning: the restart given, or the title screen.</summary>
    public void Restart()
    {
        if (_restart is not null) StartGame(_restart());
        else
        {
            Screen = Screen.Title;
            Overlay = Overlay.None;
            InvalidateVisual();
        }
    }

    public void QuickSave()
    {
        if (Screen != Screen.Play || Engine.Mode != GameMode.Playing) return;
        Store.Save(SaveStore.QuickSlot, SaveNow());
        Engine.Say("Game saved.");
    }

    public void QuickLoad() => LoadSlot(SaveStore.QuickSlot);

    /// <summary>What a save of the current game holds; a Delve adds its levels.</summary>
    public Func<GameEngine, SaveGame>? Saver { get; set; }
    public Func<SaveGame, GameEngine>? Loader { get; set; }

    private SaveGame SaveNow() => Saver?.Invoke(Engine) ?? Engine.ToSave();

    public bool LoadSlot(string slot)
    {
        var save = Store.Load(slot);
        if (save is null) return false;
        StartGame(Loader?.Invoke(save) ?? GameEngine.FromSave(save, Content));
        Engine.Say("Game loaded.");
        return true;
    }

    private unsafe void DrawView()
    {
        var t0 = _clock.Elapsed;
        double alpha = Engine.Alpha;
        var cam = Engine.Camera(alpha);
        Renderer.TorchStrength = Engine.LightStrength;
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

    /// <summary>A hero's hand box in the party bar, in control coordinates (hero 0 by default).</summary>
    public Rect HandRect(HandSide side, int hero = 0) => ToControl(HandBox(hero, side));

    public override void Render(DrawingContext dc)
    {
        _buttons.Clear();
        dc.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        var (s, ox, oy) = Fit();
        if (s <= 0) return;
        using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(ox, oy)))
        using (dc.PushClip(new Rect(0, 0, CanvasWidth, CanvasHeight)))
        {
            switch (Screen)
            {
                case Screen.Title: DrawTitle(dc); break;
                case Screen.Create: DrawCreate(dc); break;
                case Screen.Load: DrawLoad(dc); break;
                default:
                    DrawViewArea(dc);
                    DrawSidePanel(dc);
                    DrawBar(dc);
                    DrawOverlays(dc);
                    break;
            }
        }
    }

    // ---- Buttons ----

    private void Button(DrawingContext dc, Rect r, string label, Action onClick, bool enabled = true, bool selected = false,
                        double size = 16, Action? onRight = null)
    {
        bool hover = r.Contains(_pointer) && enabled;
        var fill = !enabled ? DisabledBrush : selected ? SelectedBrush : hover ? HoverBrush : ButtonBrush;
        dc.DrawRectangle(fill, new Pen(selected ? Gold : PanelEdge, selected ? 2 : 1.5), r, 4, 4);
        var ft = Text(label, size, enabled ? Parchment : Dim, Bold, r.Width - 12);
        dc.DrawText(ft, new Point(r.Center.X - ft.Width / 2, r.Center.Y - ft.Height / 2));
        if (enabled) _buttons.Add((r, onClick, onRight));
    }

    /// <summary>A click that no button wants: registered last, so buttons drawn later take precedence over it.</summary>
    private void ClickArea(Rect r, Action onClick, Action? onRight = null) => _buttons.Add((r, onClick, onRight));

    private bool DispatchClick(Point p, bool right)
    {
        for (int i = _buttons.Count - 1; i >= 0; i--)
        {
            var (r, left, rightAction) = _buttons[i];
            if (!r.Contains(p)) continue;
            if (right) rightAction?.Invoke();
            else left();
            InvalidateVisual();
            return true;
        }
        return false;
    }

    // ---- Input ----

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        if (Screen == Screen.Create && CreateKey(e)) return;
        if (Screen != Screen.Play) return;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.PhysicalKey)
        {
            case PhysicalKey.F3: ShowStats = !ShowStats; return;
            case PhysicalKey.F5: QuickSave(); return;
            case PhysicalKey.F9: QuickLoad(); return;
            case PhysicalKey.Escape:
                Overlay = Overlay == Overlay.None ? Overlay.Menu : Overlay.None;
                return;
            case PhysicalKey.I:
                Overlay = Overlay == Overlay.Inventory ? Overlay.None : Overlay.Inventory;
                return;
            case PhysicalKey.Tab:
                CycleSelected();
                return;
            case PhysicalKey.Space:
                Engine.TogglePause();
                return;
            case PhysicalKey.Enter or PhysicalKey.NumPadEnter when Engine.Mode != GameMode.Playing:
                Restart();
                return;
        }
        if (OverlayKey(e)) return;
        if (Overlay is not (Overlay.None or Overlay.Help)) return;
        if (ctrl && InputHandler.MapSlot(e.PhysicalKey) is { } slot)
        {
            Engine.Party.Swap(Engine.Party.SlotOf(Engine.Selected), slot);
            return;
        }
        if (InputHandler.MapHand(e.PhysicalKey) is { } hand)
        {
            if (Engine.Party.At(hand.Hero) is { } c) Engine.HandDown(c, hand.Side);
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
        if (Screen == Screen.Play && InputHandler.MapHand(e.PhysicalKey) is { } hand)
        {
            if (Engine.Party.At(hand.Hero) is { } c) Engine.HandUp(c, hand.Side);
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Screen == Screen.Create && e.Text is { } t) CreateText(t);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = ToCanvas(e.GetPosition(this));
        if (Screen != Screen.Play || Overlay != Overlay.None) InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        var p = ToCanvas(e.GetPosition(this));
        _pointer = p;
        e.Handled = true;
        bool right = props.IsRightButtonPressed;

        if (Screen == Screen.Play && Overlay == Overlay.None && !right && Engine.Mode == GameMode.Playing)
        {
            // Hands are held, not clicked: they get the press and the release.
            for (int i = 0; i < 4; i++)
                foreach (var side in new[] { HandSide.Left, HandSide.Right })
                    if (Engine.Party.At(i) is { } c && HandBox(i, side).Contains(p))
                    {
                        _mouseHand = (c, side);
                        Engine.HandDown(c, side);
                        return;
                    }
        }
        if (DispatchClick(p, right)) return;
        if (Screen != Screen.Play) return;
        if (Engine.Mode != GameMode.Playing && !right) { Restart(); return; }
        if (Overlay != Overlay.None || !ViewArea.Contains(p)) return;
        if (right)
        {
            // Right-click in the view swings the selected hero's weapon, held for a power attack.
            _mouseHand = (Engine.Selected, HandSide.Right);
            Engine.HandDown(Engine.Selected, HandSide.Right);
            return;
        }
        int fx = (int)((p.X - ViewArea.X) / ViewArea.Width * Renderer.Width);
        int fy = (int)((p.Y - ViewArea.Y) / ViewArea.Height * Renderer.Height);
        if (Renderer.Pick(fx, fy) is { } target && Engine.ClickWorld(target)) InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_mouseHand is { } h)
        {
            Engine.HandUp(h.Hero, h.Side);
            _mouseHand = null;
            e.Handled = true;
        }
    }

    private void CycleSelected()
    {
        var list = Engine.Party.Members.ToList();
        int i = list.IndexOf(Engine.Selected);
        Engine.Selected = list[(i + 1) % list.Count];
    }

    // ---- Text ----

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
