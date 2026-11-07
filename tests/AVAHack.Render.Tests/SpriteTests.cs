using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Render.Tests;

public sealed class SpriteTests
{
    [Fact]
    public void Every_creature_has_every_view_and_frame()
    {
        var art = new SpriteFactory();
        var views = Enum.GetValues<SpriteView>();
        var frames = Enum.GetValues<SpriteFrame>();
        int cols = views.Length * frames.Length, cell = SpriteFactory.Size + 4;
        var sheet = new uint[cols * cell * SpriteFactory.Known.Count * cell];
        Array.Fill(sheet, 0xFF2A2620u);
        int sheetW = cols * cell;
        for (int r = 0; r < SpriteFactory.Known.Count; r++)
        {
            string id = SpriteFactory.Known[r];
            int c = 0;
            foreach (var v in views)
            foreach (var f in frames)
            {
                var img = art.Get(id, v, f);
                int opaque = img.Pixels.Count(p => p >> 24 != 0);
                Assert.True(opaque > 60, $"{id} {v} {f} is nearly empty ({opaque} pixels)");
                // Feet on the ground: something in the bottom rows.
                Assert.True(Enumerable.Range(0, img.Width).Any(x => img[x, img.Height - 2] >> 24 != 0 || img[x, img.Height - 4] >> 24 != 0 || img[x, img.Height - 6] >> 24 != 0),
                    $"{id} {v} {f} floats above the floor");
                for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    uint p = img[x, y];
                    if (p >> 24 != 0) sheet[(r * cell + 2 + y) * sheetW + c * cell + 2 + x] = p;
                }
                c++;
            }
        }
        var mirrored = art.Get("goblin", SpriteView.Side, SpriteFrame.Idle, mirror: true);
        Assert.Equal(art.Get("goblin", SpriteView.Side, SpriteFrame.Idle)[10, 40], mirrored[SpriteFactory.Size - 11, 40]);
        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is { Length: > 0 } dir)
            Png.Save(Path.Combine(dir, "sprites.png"), sheet, sheetW, SpriteFactory.Known.Count * cell);
    }

    [Fact]
    public void The_scene_shows_each_creature_from_the_side_the_camera_sees()
    {
        var level = LevelLoader.Parse("start: 1 1 east\nmap:\n#######\n#.....#\n#######");
        var g = new GameEngine(level);
        var art = new SpriteFactory();
        g.AddMonster("gutterGoblin", new Cell(4, 1), Dir.West); // facing the party
        g.AddMonster("gutterGoblin", new Cell(5, 1), Dir.East); // facing away
        g.AddMonster("gutterGoblin", new Cell(3, 1), Dir.North); // side on
        var s = Scene.Build(g, g.Camera(), art);
        Assert.Same(art.Get("goblin", SpriteView.Front, SpriteFrame.Idle), s[0].Image);
        Assert.Same(art.Get("goblin", SpriteView.Back, SpriteFrame.Idle), s[1].Image);
        // Facing north, seen from the west: it looks to the camera's left, so the side view is mirrored.
        Assert.Same(art.Get("goblin", SpriteView.Side, SpriteFrame.Idle, mirror: true), s[2].Image);
    }
}
