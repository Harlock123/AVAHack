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
    public void Creatures_stand_in_the_view_and_behind_the_portcullis()
    {
        var (g, r) = Make();
        g.Stand(new Cell(6, 6), Dir.East);
        g.AddMonster("gutterGoblin", new Cell(9, 6), Dir.West);   // beyond the portcullis, seen through its bars
        g.AddMonster("cellarRat", new Cell(8, 6), Dir.West);
        var art = new SpriteFactory();
        r.Render(g.Level, g.Camera(), 0, Scene.Build(g, g.Camera(), art));
        GoldenImage.Check("creatures-portcullis", r);

        g.Stand(new Cell(2, 3), Dir.East);
        g.AddMonster("boneSentry", new Cell(4, 3), Dir.West);
        g.AddMonster("shambler", new Cell(3, 1), Dir.South); // round the corner: hidden by the wall
        r.Render(g.Level, g.Camera(), 0, Scene.Build(g, g.Camera(), art));
        GoldenImage.Check("creatures-room", r);
        Assert.NotNull(r.Project(4.5, 3.5, 0.5));
        Assert.Null(r.Project(1.5, 3.5, 0.5)); // behind the eye
    }

    [Fact]
    public void Pits_plates_and_stairs_have_their_own_floors()
    {
        var level = LevelLoader.Parse("""
            start: 1 2 north
            pit: 1 1 -> 3 2
            plate: 2 1 toggle -> 2 3
            map:
            #####
            #^o>#
            #...#
            ##+##
            #####
            """);
        var g = new GameEngine(level);
        var r = new SoftwareViewRenderer(TextureFactory.Cellar());
        g.Stand(new Cell(2, 2), Dir.North);
        r.Render(level, g.Camera(), 0);
        GoldenImage.Check("floors", r);
    }

    [Theory]
    [InlineData("works")]
    [InlineData("choir")]
    [InlineData("heart")]
    [InlineData("stacks")]
    [InlineData("rootdark")]
    [InlineData("glass")]
    public void Each_region_draws_with_its_own_art(string theme)
    {
        var (g, _) = Make();
        var r = new SoftwareViewRenderer(TextureFactory.ForTheme(theme));
        g.Stand(new Cell(3, 3), Dir.East);
        Draw(g, r);
        GoldenImage.Check($"region-{theme}", r);
    }

    [Theory]
    [InlineData("cellar")]
    [InlineData("works")]
    [InlineData("choir")]
    [InlineData("heart")]
    [InlineData("stacks")]
    [InlineData("rootdark")]
    [InlineData("glass")]
    public void The_art_pack_gives_every_region_its_walls_floors_and_ceilings(string theme)
    {
        var set = TextureFactory.ForTheme(theme);
        foreach (var t in new[] { set.Wall, set.Floor, set.Ceiling })
        {
            Assert.Equal(128, t.Side);
            Assert.All(t.Pixels, p => Assert.Equal(0xFFu, p >> 24)); // opaque
        }
        // The markings are drawn over the art at its size, and the lever on its wall.
        Assert.Equal(128, set.TrapMark!.Side);
        Assert.Equal(128, set.LeverUp.Side);
        Assert.NotEqual(set.Wall.Pixels, set.LeverUp.Pixels);
    }

    [Fact]
    public void A_hall_of_the_Ossuary_Stacks()
    {
        var db = AVAHack.Content.ContentDb.LoadBuiltIn();
        var level = LevelLoader.Parse("""
            start: 2 3 east
            theme: stacks
            map:
            ##########
            #........#
            #........#
            #........#
            #........#
            #........#
            ##########
            """);
        for (int x = 1; x < 9; x++) { level.Shelves.Add(new Cell(x, 0)); level.Shelves.Add(new Cell(x, 6)); }
        for (int y = 1; y < 6; y++) level.Shelves.Add(new Cell(9, y));
        level.Stations[new Cell(5, 1)] = "candle";
        level.Stations[new Cell(7, 5)] = "candle";
        var g = new GameEngine(level, 1, new AVAHack.Characters.Party(AVAHack.Characters.CharacterBuilder.QuickStartParty(db).ToArray()), db);
        g.AddMonster("candlewight", new Cell(6, 3), Dir.West);
        g.AddMonster("pageSwarm", new Cell(8, 2), Dir.West);
        var r = new SoftwareViewRenderer(TextureFactory.ForTheme("stacks"));
        r.Render(level, g.Camera(), 0, Scene.Build(g, g.Camera(), new SpriteFactory()));
        GoldenImage.Check("stacks-hall", r);
    }

    [Fact]
    public void A_cavern_of_the_Rootdark()
    {
        var db = AVAHack.Content.ContentDb.LoadBuiltIn();
        var level = LevelLoader.Parse("""
            start: 2 3 east
            theme: rootdark
            map:
            ##########
            #........#
            #........#
            #........#
            #........#
            #........#
            ##########
            """);
        for (int x = 1; x < 9; x += 2) { level.RootWalls.Add(new Cell(x, 0)); level.RootWalls.Add(new Cell(x + 1, 6)); }
        for (int y = 1; y < 6; y += 2) level.RootWalls.Add(new Cell(9, y));
        level.Stations[new Cell(5, 1)] = "pod";
        level.Stations[new Cell(4, 3)] = "roots";
        level.Stations[new Cell(7, 5)] = "pod";
        var g = new GameEngine(level, 1, new AVAHack.Characters.Party(AVAHack.Characters.CharacterBuilder.QuickStartParty(db).ToArray()), db);
        g.LevelFlags["burst:" + new Cell(7, 5)] = "1";
        g.AddMonster("thralledDelver", new Cell(7, 2), Dir.West);
        g.AddMonster("rootWyrm", new Cell(8, 4), Dir.West);
        var r = new SoftwareViewRenderer(TextureFactory.ForTheme("rootdark"));
        r.Render(level, g.Camera(), 0, Scene.Build(g, g.Camera(), new SpriteFactory()));
        GoldenImage.Check("rootdark-cavern", r);
    }

    [Fact]
    public void A_hall_of_the_Glass_Roads()
    {
        var db = AVAHack.Content.ContentDb.LoadBuiltIn();
        var level = LevelLoader.Parse("""
            start: 2 3 east
            theme: glass
            map:
            ##########
            #........#
            #........#
            #........#
            #........#
            #........#
            ##########
            """);
        for (int x = 1; x < 9; x++) { if (x % 3 != 0) level.MirrorWalls.Add(new Cell(x, 0)); if (x % 2 == 0) level.MirrorWalls.Add(new Cell(x, 6)); }
        for (int y = 1; y < 6; y++) level.MirrorWalls.Add(new Cell(9, y));
        foreach (var c in new[] { new Cell(3, 3), new Cell(4, 2), new Cell(5, 4), new Cell(6, 3) }) level.Shards.Add(c);
        level.Stations[new Cell(7, 1)] = "rift";
        var g = new GameEngine(level, 1, new AVAHack.Characters.Party(AVAHack.Characters.CharacterBuilder.QuickStartParty(db).ToArray()), db);
        g.AddMonster("prismEye", new Cell(6, 2), Dir.West);
        g.AddMonster("hollowTemplar", new Cell(8, 4), Dir.West);
        g.AddMonster("shardling", new Cell(5, 3), Dir.West);
        var r = new SoftwareViewRenderer(TextureFactory.ForTheme("glass"));
        r.Render(level, g.Camera(), 0, Scene.Build(g, g.Camera(), new SpriteFactory()));
        GoldenImage.Check("glass-hall", r);
    }

    [Fact]
    public void Every_region_in_the_content_has_its_art()
    {
        foreach (var r in AVAHack.Content.ContentDb.LoadBuiltIn().Regions)
            Assert.NotNull(TextureFactory.Asset($"{r.Id}-wall"));
    }

    [Fact]
    public void The_code_drawn_set_is_still_there()
    {
        var set = TextureFactory.Drawn();
        Assert.Equal(Texture.Size, set.Wall.Side);
        Assert.Null(TextureFactory.Asset("no-such-texture"));
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
