using System.Diagnostics;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Render.Tests;

public sealed class ViewTests(ITestOutputHelper output)
{
    private static (GameEngine G, SoftwareViewRenderer R) Make()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("test-hall"));
        return (g, new SoftwareViewRenderer(TextureFactory.Cellar()));
    }

    private static void Draw(GameEngine g, SoftwareViewRenderer r, CameraPose? pose = null) =>
        r.Render(g.Level, pose ?? g.Camera(), 0);

    [Fact]
    public void The_start_room()
    {
        var (g, r) = Make();
        Draw(g, r);
        GoldenImage.Check("start-room", r);
    }

    [Fact]
    public void A_shut_door_ahead()
    {
        var (g, r) = Make();
        g.Stand(new Cell(3, 3), Dir.East); // two cells west of the hand door at (5,3)
        Draw(g, r);
        GoldenImage.Check("door-shut", r);
        Assert.IsType<DoorTarget>(r.Pick(r.Width / 2, r.Height / 2));
    }

    [Fact]
    public void A_door_half_raised_shows_the_room_beyond_under_it()
    {
        var (g, r) = Make();
        g.Stand(new Cell(4, 3), Dir.East);
        g.Press(GameAction.Use);
        for (int i = 0; i < 30; i++) g.Tick();
        Assert.InRange(g.Level.DoorAt(5, 3)!.Open, 0.45, 0.55);
        Draw(g, r);
        GoldenImage.Check("door-half", r);
        // Low down in the middle you see past the door; high up you see the door itself.
        Assert.IsType<DoorTarget>(r.Pick(r.Width / 2, r.Height / 4));
        Assert.IsNotType<DoorTarget>(r.Pick(r.Width / 2, r.Height * 7 / 10));
    }

    [Fact]
    public void The_portcullis_is_seen_through_and_the_lever_beside_it()
    {
        var (g, r) = Make();
        g.Stand(new Cell(6, 6), Dir.East); // in front of the portcullis at (7,6)
        Draw(g, r);
        GoldenImage.Check("portcullis", r);

        g.Stand(new Cell(6, 4), Dir.West); // facing the lever
        Draw(g, r);
        GoldenImage.Check("lever-up", r);
        var target = r.Pick(r.Width / 2, r.Height / 2);
        Assert.Equal(new WallFaceTarget(6, 4, Dir.West), target);
    }

    [Fact]
    public void Halfway_through_a_turn_and_a_step()
    {
        var (g, r) = Make();
        g.Press(GameAction.TurnRight);
        for (int i = 0; i < GameEngine.TurnTicks / 2; i++) g.Tick();
        Draw(g, r);
        GoldenImage.Check("mid-turn", r);

        g.Stand(new Cell(2, 2), Dir.South);
        g.Press(GameAction.StepForward);
        for (int i = 0; i < GameEngine.StepTicks / 2; i++) g.Tick();
        Draw(g, r);
        GoldenImage.Check("mid-step", r);
    }

    [Fact]
    public void Every_pixel_is_drawn_from_every_cell_and_facing()
    {
        var (g, r) = Make();
        var level = g.Level;
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            if (level.KindAt(x, y) != CellKind.Floor) continue;
            foreach (var d in Enum.GetValues<Dir>())
            {
                Array.Fill(r.Pixels, 0u);
                g.Stand(new Cell(x, y), d);
                Draw(g, r);
                Assert.DoesNotContain(0u, r.Pixels);
            }
        }
    }

    [Fact]
    public void A_frame_renders_fast_enough()
    {
        var (g, r) = Make();
        g.Stand(new Cell(10, 13), Dir.North); // a long view up the middle corridor
        for (int i = 0; i < 10; i++) r.Render(g.Level, g.Camera(), i * 0.016);
        const int frames = 60;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++) r.Render(g.Level, g.Camera(), i * 0.016);
        double ms = sw.Elapsed.TotalMilliseconds / frames;
        output.WriteLine($"{ms:F2} ms per {r.Width}×{r.Height} frame");
        // The budget is 8 ms (spec §15.2); the test allows slow, shared CI machines and Debug builds.
        Assert.True(ms < 40, $"{ms:F2} ms per frame");
    }
}
