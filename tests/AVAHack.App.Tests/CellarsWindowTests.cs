using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVAHack.App.Views;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App.Tests;

/// <summary>The M2 game in a headless window: The Cellars, the hero bar, the hands and the end screens.</summary>
public sealed class CellarsWindowTests
{
    private static GameEngine NewGame() => new(LevelLoader.LoadBuiltIn("the-cellars"), 7);

    private static (Window W, GameView V) Open()
    {
        var view = new GameView(NewGame(), startLoop: false, restart: NewGame);
        var w = new Window { Width = 1280, Height = 800, Content = view };
        w.Show();
        Pump();
        return (w, view);
    }

    private static void Pump()
    {
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Run(GameView v, double seconds)
    {
        for (double t = 0; t < seconds; t += 0.05) v.Advance(0.05);
    }

    private static void Shot(Window w, string name)
    {
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAHack-{name}.png"), PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_cellars_open_with_the_hero_bar_and_the_first_note()
    {
        var (w, v) = Open();
        Assert.Contains("booms shut", v.Engine.LastMessage);
        Shot(w, "cellars-start");
    }

    [AvaloniaFact]
    public void Key_2_swings_and_key_1_blocks()
    {
        var (w, v) = Open();
        w.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.None);
        w.KeyReleaseQwerty(PhysicalKey.Digit2, RawInputModifiers.None);
        Assert.False(v.Engine.RightHand.Ready);
        w.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
        Run(v, 0.4);
        Assert.True(v.Engine.Blocking);
        w.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
        Assert.False(v.Engine.Blocking);
    }

    [AvaloniaFact]
    public void Clicking_a_hand_box_uses_that_hand()
    {
        var (w, v) = Open();
        var box = v.HandRect(HandSide.Right).Center;
        w.MouseDown(box, MouseButton.Left);
        w.MouseUp(box, MouseButton.Left);
        Assert.False(v.Engine.RightHand.Ready);
    }

    [AvaloniaFact]
    public void Space_pauses()
    {
        var (w, v) = Open();
        w.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.True(v.Engine.Paused);
        long ticks = v.Engine.TickCount;
        Run(v, 0.5);
        Assert.Equal(ticks, v.Engine.TickCount);
        Shot(w, "paused");
        w.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.False(v.Engine.Paused);
    }

    [AvaloniaFact]
    public void A_fight_in_the_rat_nest()
    {
        var (w, v) = Open();
        var g = v.Engine;
        g.Stand(new Cell(12, 3), Dir.East); // in the doorway of the rat nest
        Run(v, 1.2);                        // the rats come
        for (int i = 0; i < 3; i++)
        {
            g.HandDown(HandSide.Right);
            g.HandUp(HandSide.Right);
            Run(v, 0.25);
        }
        Shot(w, "rat-fight");
        Assert.True(g.InFight);
    }

    [AvaloniaFact]
    public void The_crypt_and_the_warren()
    {
        var (w, v) = Open();
        var g = v.Engine;
        g.Stand(new Cell(21, 4), Dir.East); // facing the first bone sentry
        Run(v, 0.6);
        Shot(w, "crypt");
        g.Stand(new Cell(22, 12), Dir.East); // inside the warren's gate
        Run(v, 1.0);
        Shot(w, "warren");
    }

    [AvaloniaFact]
    public void Dying_shows_the_end_and_enter_starts_again()
    {
        var (w, v) = Open();
        var g = v.Engine;
        g.Hero.TakeDamage(1000);
        g.Tick();
        Assert.Equal(GameMode.Dead, g.Mode);
        Shot(w, "fallen");
        w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.NotSame(g, v.Engine);
        Assert.Equal(GameMode.Playing, v.Engine.Mode);
    }
}
