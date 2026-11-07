namespace AVAHack.Render;

/// <summary>
/// The rest of the bestiary (M7), drawn from a few generic shapes: people, beasts on four legs, blobs, floating
/// things, bugs, fungi, machines, serpents and swarms. Each sprite id picks a shape and its colours.
/// </summary>
public sealed partial class SpriteFactory
{
    private static uint C(int r, int g, int b) => Paint.Rgb(r, g, b);

    private static readonly Dictionary<string, Look> People = new()
    {
        ["thrall"] = new(C(150, 140, 120), C(84, 76, 66), C(60, 54, 46), 0.95, false, "none", false, false, C(240, 240, 200)),
        ["miner"] = new(C(170, 150, 130), C(96, 82, 60), C(70, 60, 40), 0.85, false, "pick", false, true, C(240, 200, 120)),
        ["overseer"] = new(C(200, 196, 190), C(60, 56, 66), C(110, 90, 50), 1.0, false, "whip", false, false, C(200, 230, 255)),
        ["speaker"] = new(C(160, 150, 140), C(110, 100, 90), C(80, 74, 66), 1.0, false, "staff", false, true, C(170, 240, 220)),
        ["hexer"] = new(C(98, 142, 62), C(90, 50, 90), C(60, 34, 60), 0.8, true, "staff", false, true, C(200, 120, 255)),
        ["chief"] = new(C(90, 130, 56), C(140, 40, 30), C(200, 170, 60), 0.9, true, "axe", false, false, C(255, 220, 60)),
        ["bugbear"] = new(C(140, 100, 60), C(90, 70, 50), C(60, 46, 34), 1.0, true, "club", false, false, C(255, 200, 80)),
        ["cultist"] = new(C(120, 150, 110), C(40, 90, 80), C(160, 140, 60), 0.95, false, "knife", false, true, C(240, 230, 90)),
        ["cultistGuard"] = new(C(120, 150, 110), C(60, 70, 74), C(40, 100, 90), 1.0, false, "spear", false, false, C(240, 230, 90)),
        ["delver"] = new(C(200, 160, 130), C(100, 70, 50), C(70, 90, 60), 0.95, false, "sword", false, true, C(230, 230, 230)),
        ["archerSkel"] = new(C(214, 206, 184), C(214, 206, 184), C(150, 140, 120), 1.0, false, "bow", true, false, C(255, 70, 40)),
        ["knight"] = new(C(200, 196, 180), C(120, 120, 130), C(90, 80, 70), 1.0, false, "sword", false, false, C(255, 90, 40)),
        ["king"] = new(C(214, 206, 184), C(90, 30, 40), C(220, 180, 60), 1.0, false, "sword", true, false, C(120, 255, 160)),
        ["wight"] = new(C(170, 190, 180), C(50, 60, 60), C(40, 50, 50), 1.0, false, "lantern", false, true, C(120, 255, 160)),
        ["bishop"] = new(C(140, 170, 160), C(40, 70, 90), C(200, 200, 220), 1.0, false, "staff", false, true, C(140, 220, 255)),
        ["saint"] = new(C(30, 24, 34), C(220, 214, 200), C(230, 200, 90), 1.0, false, "none", false, true, C(255, 255, 255)),
        ["singer"] = new(C(210, 190, 200), C(120, 70, 120), C(200, 160, 200), 0.95, false, "none", false, true, C(255, 220, 255)),
    };

    private static readonly IReadOnlyList<string> MoreKnown =
    [
        "bat", "horror", "swarm", "beetle", "spiderBig", "hound", "warg", "houndVoid",
        "slimeGrey", "slimeAcid", "slimeMirror", "slimeGlutton", "cube", "wisp", "imp", "ghost", "ghostFrost", "moths",
        "brain", "unborn", "unmaker", "mushroom", "walker", "walkerTall", "colossus", "sentinel", "crawler", "golem",
        "golemMagma", "juggernaut", "engine", "naga", "nagaQueen", "tentacles", "stalker", "mimic",
    ];

    /// <summary>Draws the M7 creatures. False if the id isn't one of them.</summary>
    private static bool DrawMore(Paint p, string sprite, SpriteView v, SpriteFrame f)
    {
        if (People.TryGetValue(sprite, out var look))
        {
            Humanoid(p, v, f, look);
            if (f != SpriteFrame.Dead && v != SpriteView.Back) Extras(p, sprite, look);
            return true;
        }
        switch (sprite)
        {
            case "swarm": for (int i = 0; i < 3; i++) Shifted(p, (i - 1) * 14, i % 2 * 4 - 4, q => Rat(q, v, f)); return true;
            case "bat": Floater(p, f, C(60, 50, 50), C(200, 60, 60), "wings"); return true;
            case "horror": Blob(p, f, C(70, 40, 80), C(255, 120, 200), 26, 22, true); return true;
            case "beetle": Bug(p, v, f, C(50, 70, 60), C(20, 30, 26), 1.0, 6); return true;
            case "crawler": Bug(p, v, f, C(170, 130, 60), C(110, 80, 40), 0.8, 8); return true;
            case "spiderBig": Bug(p, v, f, C(70, 40, 60), C(40, 24, 34), 1.15, 8); return true;
            case "hound": Beast(p, v, f, C(150, 140, 120), C(220, 60, 40), true); return true;
            case "warg": Beast(p, v, f, C(90, 80, 74), C(250, 220, 60), false, rider: true); return true;
            case "houndVoid": Beast(p, v, f, C(24, 20, 34), C(170, 120, 255), false); return true;
            case "slimeGrey": Blob(p, f, C(120, 124, 120), C(220, 220, 220), 20, 12, false); return true;
            case "slimeAcid": Blob(p, f, C(110, 170, 50), C(240, 255, 120), 22, 16, false); return true;
            case "slimeMirror": Blob(p, f, C(150, 160, 170), C(250, 250, 255), 24, 8, false); return true;
            case "slimeGlutton": Blob(p, f, C(130, 90, 70), C(230, 200, 120), 30, 28, true); return true;
            case "cube": Cube(p, f); return true;
            case "wisp": Floater(p, f, C(240, 120, 30), C(255, 240, 140), "flame"); return true;
            case "imp": Floater(p, f, C(190, 60, 40), C(255, 220, 80), "imp"); return true;
            case "ghost": Floater(p, f, C(200, 200, 190), C(255, 140, 60), "ghost"); return true;
            case "ghostFrost": Floater(p, f, C(170, 210, 240), C(240, 250, 255), "ghost"); return true;
            case "moths": for (int i = 0; i < 3; i++) Shifted(p, (i - 1) * 14, (i % 2) * -8, q => Floater(q, f, C(150, 130, 90), C(80, 160, 255), "moth")); return true;
            case "brain": Floater(p, f, C(220, 150, 160), C(255, 255, 140), "brain"); return true;
            case "unborn": Floater(p, f, C(40, 30, 50), C(255, 60, 60), "ghost"); return true;
            case "unmaker": Floater(p, f, C(16, 12, 24), C(200, 140, 255), "unmaker"); return true;
            case "mushroom": Fungus(p, f, C(200, 80, 60), C(240, 220, 200), 0.6); return true;
            case "walker": Fungus(p, f, C(140, 110, 160), C(210, 200, 190), 0.85); return true;
            case "walkerTall": Fungus(p, f, C(100, 140, 90), C(220, 210, 180), 1.0); return true;
            case "colossus": Fungus(p, f, C(110, 100, 70), C(160, 150, 110), 1.0, heap: true); return true;
            case "sentinel": Machine(p, v, f, C(180, 140, 70), C(120, 220, 255), 0.95, false); return true;
            case "golem": Machine(p, v, f, C(90, 90, 96), C(255, 140, 40), 1.0, true); return true;
            case "golemMagma": Machine(p, v, f, C(60, 40, 36), C(255, 110, 20), 1.0, true, cracks: true); return true;
            case "juggernaut": Cart(p, f); return true;
            case "engine": Engine(p, f); return true;
            case "naga": Serpent(p, v, f, C(60, 120, 90), C(220, 200, 120), 0.9); return true;
            case "nagaQueen": Serpent(p, v, f, C(40, 90, 110), C(230, 190, 60), 1.0, crown: true); return true;
            case "tentacles": Tentacles(p, f); return true;
            case "stalker": Beast(p, v, f, C(36, 40, 44), C(140, 255, 140), false, lean: true); return true;
            case "mimic": Mimic(p, f); return true;
        }
        return false;
    }

    /// <summary>Draws a creature at two thirds size, moved sideways by <paramref name="dx"/> grid units (a swarm is three of them).</summary>
    private static void Shifted(Paint p, int dx, int dy, Action<Paint> draw)
    {
        var scratch = new SpriteImage(Size, Size);
        var q = new Paint(scratch, Scale);
        draw(q);
        q.Finish();
        int small = Size * 2 / 3, ox = (Size - small) / 2 + (int)(dx * Scale), oy = Size - small + (int)(dy * Scale);
        for (int y = 0; y < small; y++)
        for (int x = 0; x < small; x++)
            p.Bake(ox + x, oy + y, scratch[x * Size / small, y * Size / small]);
    }
}
