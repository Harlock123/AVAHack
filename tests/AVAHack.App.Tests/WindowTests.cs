using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVAHack.App.Input;
using AVAHack.App.Views;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App.Tests;

/// <summary>
/// The real game view in a headless window: keys and clicks go in the way a player's would. Set AVAHACK_SHOTS to a
/// folder to save what the window showed.
/// </summary>
public sealed class WindowTests
{
    private static (Window W, GameView V) Open()
    {
        var view = new GameView(new GameEngine(LevelLoader.LoadBuiltIn("test-hall")), startLoop: false);
        var w = new Window { Width = 1280, Height = 800, Content = view };
        w.Show();
        Pump();
        return (w, view);
    }

    private static void Pump()
    {
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
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
    public void Keys_walk_and_turn_the_party()
    {
        var (w, v) = Open();
        Shot(w, "start");
        w.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
        v.Advance(0.3);
        Assert.Equal(new Cell(3, 2), v.Engine.PartyCell);
        w.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);
        v.Advance(0.3);
        Assert.Equal(Dir.South, v.Engine.Facing);
        w.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        v.Advance(0.3);
        Assert.Equal(Dir.East, v.Engine.Facing);
        w.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None); // strafe right: south
        v.Advance(0.3);
        Assert.Equal(new Cell(3, 3), v.Engine.PartyCell);
    }

    [AvaloniaFact]
    public void Clicking_the_lever_raises_the_portcullis()
    {
        var (w, v) = Open();
        v.Engine.Stand(new Cell(6, 4), Dir.West);
        v.Advance(0);
        var centre = v.ViewRect().Center;
        w.MouseDown(centre, MouseButton.Left);
        w.MouseUp(centre, MouseButton.Left);
        Assert.True(Assert.Single(v.Engine.Level.Levers).Down);
        for (int i = 0; i < 5; i++) v.Advance(0.25); // one update runs at most a quarter second
        Assert.True(v.Engine.Level.DoorAt(7, 6)!.IsPassable);
        Shot(w, "lever-pulled");

        v.Engine.Stand(new Cell(6, 6), Dir.East);
        v.Advance(0);
        Shot(w, "portcullis-raised");
    }

    [AvaloniaFact]
    public void Use_opens_the_door_ahead_and_F3_shows_the_stats()
    {
        var (w, v) = Open();
        v.Engine.Stand(new Cell(4, 3), Dir.East);
        w.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
        v.Advance(0.5);
        Assert.True(v.Engine.Level.DoorAt(5, 3)!.Opening);
        Shot(w, "door-opening");
        w.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
        Assert.True(v.ShowStats);
    }

    [AvaloniaFact]
    public void The_view_keeps_its_shape_in_any_window()
    {
        var (w, v) = Open();
        w.Width = 1000;
        w.Height = 900;
        Pump();
        var r = v.ViewRect();
        Assert.Equal(1.6, r.Width / r.Height, 3);
        Assert.Equal(1000 * 960 / 1280.0, r.Width, 1); // the canvas fills the width; the view is its left 960
    }

    [Theory]
    [InlineData(PhysicalKey.W, GameAction.StepForward)]
    [InlineData(PhysicalKey.ArrowUp, GameAction.StepForward)]
    [InlineData(PhysicalKey.NumPad8, GameAction.StepForward)]
    [InlineData(PhysicalKey.S, GameAction.StepBack)]
    [InlineData(PhysicalKey.ArrowDown, GameAction.StepBack)]
    [InlineData(PhysicalKey.A, GameAction.StrafeLeft)]
    [InlineData(PhysicalKey.D, GameAction.StrafeRight)]
    [InlineData(PhysicalKey.Q, GameAction.TurnLeft)]
    [InlineData(PhysicalKey.ArrowLeft, GameAction.TurnLeft)]
    [InlineData(PhysicalKey.E, GameAction.TurnRight)]
    [InlineData(PhysicalKey.ArrowRight, GameAction.TurnRight)]
    [InlineData(PhysicalKey.F, GameAction.Use)]
    public void Keys_map_to_actions(PhysicalKey key, GameAction action) => Assert.Equal(action, InputHandler.Map(key));

    [Theory]
    [InlineData(PhysicalKey.Digit1, 0, HandSide.Left)]
    [InlineData(PhysicalKey.Digit2, 0, HandSide.Right)]
    [InlineData(PhysicalKey.Digit5, 2, HandSide.Left)]
    [InlineData(PhysicalKey.Digit8, 3, HandSide.Right)]
    public void Keys_1_to_8_are_the_heroes_hands(PhysicalKey key, int hero, HandSide side) => Assert.Equal((hero, side), InputHandler.MapHand(key));

    [Fact]
    public void Caps_lock_and_shift_do_nothing() // Shift's release can arrive as CapsLock on some desktops
    {
        Assert.Null(InputHandler.Map(PhysicalKey.CapsLock));
        Assert.Null(InputHandler.Map(PhysicalKey.ShiftLeft));
    }
}
