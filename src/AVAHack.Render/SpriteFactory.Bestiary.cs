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

    /// <summary>Draws a creature at two thirds size, moved sideways by <paramref name="dx"/> (a swarm is three of them).</summary>
    private static void Shifted(Paint p, int dx, int dy, Action<Paint> draw)
    {
        var scratch = new SpriteImage(Size, Size);
        draw(new Paint(scratch));
        int small = Size * 2 / 3, ox = (Size - small) / 2 + dx, oy = Size - small + dy;
        for (int y = 0; y < small; y++)
        for (int x = 0; x < small; x++)
        {
            uint c = scratch[x * Size / small, y * Size / small];
            int tx = ox + x, ty = oy + y;
            if (c >> 24 != 0 && tx >= 0 && ty >= 0 && tx < Size && ty < Size) p.Image[tx, ty] = c;
        }
    }

    private static void Extras(Paint p, string sprite, Look k)
    {
        switch (sprite)
        {
            case "king":
                p.Rect(25, 2, 39, 6, k.Trim);
                p.Triangle(25, 2, 27, -2, 29, 2, k.Trim); p.Triangle(30, 2, 32, -3, 34, 2, k.Trim); p.Triangle(35, 2, 37, -2, 39, 2, k.Trim);
                break;
            case "saint":
            case "singer":
                p.Ellipse(32, 0 + 4, 9, 2.5, C(250, 230, 140), false);
                break;
            case "chief":
                p.Rect(26, 3, 38, 6, k.Trim);
                break;
            case "knight":
                p.Rect(26, 6, 38, 12, k.Clothes);
                p.Rect(28, 9, 36, 10, C(20, 16, 14), false);
                break;
        }
    }

    // ---- Beasts on four legs ----

    private static void Beast(Paint p, SpriteView v, SpriteFrame f, uint fur, uint eye, bool bony, bool rider = false, bool lean = false)
    {
        uint dark = Paint.Shade(fur, -30);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 57, 22, 6, fur);
            p.Ellipse(54, 55, 7, 5, fur);
            for (int i = 0; i < 4; i++) p.Line(18 + i * 8, 58, 14 + i * 8, 62, 2, dark);
            return;
        }
        double rear = f == SpriteFrame.Windup ? -5 : 0, lunge = f == SpriteFrame.Strike ? 5 : 0;
        double body = lean ? 8 : 10;
        if (v == SpriteView.Side)
        {
            p.Line(10, 40 + rear, 2, 30 + rear, 3, fur);
            foreach (var x in new[] { 16, 22, 40, 46 }) p.Line(x, 46, x + (f == SpriteFrame.Strike ? 2 : 0), 63, 3.5, dark);
            p.Ellipse(30, 42 + rear / 2, 18, body, fur);
            if (bony) for (int i = 0; i < 4; i++) p.Line(20 + i * 6, 36, 20 + i * 6, 48, 1, C(230, 220, 200));
            p.Ellipse(50 + lunge, 34 + rear, 9, 7, fur);
            p.Triangle(46 + lunge, 28 + rear, 48 + lunge, 20 + rear, 51 + lunge, 28 + rear, dark);
            p.Rect(54 + lunge, 36 + rear, 62 + lunge, 40 + rear, fur);
            p.Dot(53 + lunge, 32 + rear, eye); p.Dot(54 + lunge, 32 + rear, eye);
            if (f != SpriteFrame.Idle) p.Line(55 + lunge, 40 + rear, 61 + lunge, 40 + rear, 1, C(240, 240, 230));
            if (rider) { p.Rect(26, 18, 34, 34, C(98, 142, 62)); p.Ellipse(30, 14, 5, 5, C(98, 142, 62)); p.Line(34, 24, 44, 14, 2, C(170, 170, 176)); }
            return;
        }
        // Front and back: chest, two front legs, head on top.
        p.Line(24, 46, 22, 63, 4, dark); p.Line(40, 46, 42, 63, 4, dark);
        p.Ellipse(32, 44, 14, body + 2, fur);
        if (v == SpriteView.Back) { p.Line(32, 50, 32, 62, 3, fur); if (rider) p.Rect(26, 20, 38, 36, C(98, 142, 62)); return; }
        p.Ellipse(32, 30 + rear, 9 + lunge / 2, 8, fur);
        p.Triangle(24, 26 + rear, 22, 16 + rear, 28, 24 + rear, dark);
        p.Triangle(40, 26 + rear, 42, 16 + rear, 36, 24 + rear, dark);
        p.Ellipse(32, 36 + rear, 5, 4, dark);
        p.Dot(28, 29 + rear, eye); p.Dot(29, 29 + rear, eye); p.Dot(35, 29 + rear, eye); p.Dot(36, 29 + rear, eye);
        if (f != SpriteFrame.Idle) p.Rect(29, 38 + rear, 35, 40 + rear, C(40, 10, 10), false);
        if (rider) { p.Rect(27, 6, 37, 20, C(98, 142, 62)); p.Ellipse(32, 4, 5, 4, C(98, 142, 62)); }
    }

    // ---- Oozes ----

    private static void Blob(Paint p, SpriteFrame f, uint body, uint shine, double rx, double ry, bool eyes)
    {
        if (f == SpriteFrame.Dead) { p.Ellipse(32, 60, rx + 6, 4, Paint.Shade(body, -20)); return; }
        double squash = f == SpriteFrame.Windup ? -4 : f == SpriteFrame.Strike ? 5 : 0;
        double h = ry - squash / 2;
        p.Ellipse(32, 63 - h, rx + squash, h, body);
        p.Ellipse(32 - rx / 3, 63 - h * 1.4, rx / 4, h / 5, shine, false);
        if (eyes)
        {
            p.Ellipse(26, 63 - h, 3, 3, C(250, 250, 230), false); p.Dot(26, 63 - h, C(20, 10, 10));
            p.Ellipse(38, 60 - h, 2.5, 2.5, C(250, 250, 230), false); p.Dot(38, 60 - h, C(20, 10, 10));
        }
    }

    private static void Cube(Paint p, SpriteFrame f)
    {
        uint glass = C(150, 200, 190), edge = C(200, 240, 230);
        if (f == SpriteFrame.Dead) { p.Ellipse(32, 60, 26, 4, glass); return; }
        double grow = f == SpriteFrame.Strike ? 3 : 0;
        p.Rect(6 - grow, 8 - grow, 58 + grow, 63, glass);
        p.Line(6 - grow, 8 - grow, 58 + grow, 8 - grow, 1.5, edge);
        p.Line(6 - grow, 8 - grow, 6 - grow, 63, 1.5, edge);
        p.Ellipse(24, 40, 4, 6, C(200, 200, 190), false); // something it swallowed
        p.Line(36, 46, 44, 38, 2, C(150, 120, 90));
    }

    // ---- Things that float (each trails a wisp down to the floor) ----

    private static void Floater(Paint p, SpriteFrame f, uint body, uint glow, string kind)
    {
        double bob = f == SpriteFrame.Windup ? -4 : f == SpriteFrame.Strike ? 3 : 0;
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 60, 14, 4, Paint.Shade(body, -20));
            p.Ellipse(32, 58, 5, 2, glow, false);
            return;
        }
        // The trail to the floor.
        for (int i = 0; i < 5; i++) p.Ellipse(32 + (i % 2 == 0 ? -2 : 2), 46 + i * 4, 6 - i, 3, Paint.Shade(body, -10 - i * 8), false);
        switch (kind)
        {
            case "flame":
                p.Ellipse(32, 34 + bob, 10, 13, body);
                p.Triangle(24, 30 + bob, 32, 10 + bob, 40, 30 + bob, body);
                p.Ellipse(32, 36 + bob, 5, 7, glow, false);
                break;
            case "imp":
                p.Ellipse(32, 36 + bob, 9, 11, body);
                p.Ellipse(32, 22 + bob, 7, 7, body);
                p.Triangle(26, 18 + bob, 24, 8 + bob, 29, 16 + bob, C(40, 20, 20)); p.Triangle(38, 18 + bob, 40, 8 + bob, 35, 16 + bob, C(40, 20, 20));
                p.Triangle(23, 30 + bob, 6, 20 + bob, 22, 40 + bob, Paint.Shade(body, -40)); p.Triangle(41, 30 + bob, 58, 20 + bob, 42, 40 + bob, Paint.Shade(body, -40));
                p.Dot(29, 21 + bob, glow); p.Dot(35, 21 + bob, glow);
                if (f == SpriteFrame.Windup) p.Ellipse(46, 20 + bob, 4, 4, glow, false);
                break;
            case "ghost":
                p.Ellipse(32, 30 + bob, 13, 18, body);
                p.Triangle(19, 34 + bob, 32, 54 + bob, 45, 34 + bob, body);
                p.Ellipse(27, 24 + bob, 2.5, 3.5, C(20, 20, 30), false); p.Ellipse(37, 24 + bob, 2.5, 3.5, C(20, 20, 30), false);
                p.Dot(27, 24 + bob, glow); p.Dot(37, 24 + bob, glow);
                p.Ellipse(32, 34 + bob, 3, f == SpriteFrame.Idle ? 2 : 5, C(20, 20, 30), false);
                break;
            case "wings":
                p.Triangle(30, 30 + bob, 4, 18 + bob - (f == SpriteFrame.Windup ? 8 : 0), 14, 38 + bob, body);
                p.Triangle(34, 30 + bob, 60, 18 + bob - (f == SpriteFrame.Windup ? 8 : 0), 50, 38 + bob, body);
                p.Ellipse(32, 32 + bob, 6, 8, body);
                p.Dot(30, 29 + bob, glow); p.Dot(34, 29 + bob, glow);
                break;
            case "moth":
                p.Ellipse(24, 30 + bob, 9, 7, body); p.Ellipse(40, 30 + bob, 9, 7, body);
                p.Ellipse(24, 30 + bob, 3, 3, glow, false); p.Ellipse(40, 30 + bob, 3, 3, glow, false);
                p.Line(32, 24 + bob, 32, 38 + bob, 2, C(60, 50, 40));
                break;
            case "brain":
                for (int i = 0; i < 3; i++) { p.Line(26 + i * 6, 40 + bob, 20 + i * 12, 63, 2, Paint.Shade(body, -40)); }
                p.Ellipse(32, 30 + bob, 16, 13, body);
                for (int i = 0; i < 4; i++) p.Line(20 + i * 7, 22 + bob, 22 + i * 7, 38 + bob, 1, Paint.Shade(body, -30));
                if (f == SpriteFrame.Windup) p.Ellipse(32, 30 + bob, 18, 15, glow, false);
                break;
            case "unmaker":
                p.Ellipse(32, 30 + bob, 22, 26, body);
                p.Ellipse(32, 30 + bob, 14, 16, C(4, 2, 8), false);
                for (int i = 0; i < 6; i++) p.Dot(26 + i * 2, 26 + bob + i % 2 * 4, glow);
                p.Ellipse(32, 30 + bob, 4, f == SpriteFrame.Windup ? 10 : 5, glow, false);
                break;
        }
    }

    // ---- Bugs ----

    private static void Bug(Paint p, SpriteView v, SpriteFrame f, uint shell, uint leg, double s, int legs)
    {
        uint eye = C(240, 60, 40);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 56, 16 * s, 7, shell);
            for (int i = 0; i < legs / 2; i++) p.Line(20 + i * 6, 52, 18 + i * 7, 42 - i % 2 * 3, 2, leg);
            return;
        }
        double raise = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? -4 : 0;
        for (int i = 0; i < legs / 2; i++)
        {
            double knee = 36 - i * 3 + (i == 0 ? raise : 0);
            p.Line(26, 50, 12 - i * 2, knee, 2, leg); p.Line(12 - i * 2, knee, 4 + i * 3, 63, 2, leg);
            p.Line(38, 50, 52 + i * 2, knee, 2, leg); p.Line(52 + i * 2, knee, 60 - i * 3, 63, 2, leg);
        }
        p.Ellipse(32, 46, 18 * s, 12 * s, shell);
        p.Line(32, 46 - 12 * s, 32, 46 + 12 * s, 1, Paint.Shade(shell, -30));
        if (v == SpriteView.Back) return;
        p.Ellipse(32, 56, 8, 6, Paint.Shade(shell, -15));
        p.Dot(29, 55, eye); p.Dot(35, 55, eye);
        p.Line(28, 60, 24, 63 + raise / 4, 2, leg); p.Line(36, 60, 40, 63 + raise / 4, 2, leg);
    }

    // ---- Fungi ----

    private static void Fungus(Paint p, SpriteFrame f, uint cap, uint stalk, double s, bool heap = false)
    {
        if (f == SpriteFrame.Dead) { p.Ellipse(32, 58, 22, 6, cap); p.Ellipse(24, 58, 6, 4, stalk); return; }
        double sway = f == SpriteFrame.Windup ? -6 : f == SpriteFrame.Strike ? 6 : 0;
        if (heap)
        {
            p.Ellipse(32, 44, 28, 20, cap);
            for (int i = 0; i < 5; i++) p.Ellipse(14 + i * 9, 30 + i % 2 * 6, 6, 5, stalk);
            p.Line(10, 40, 2 + sway, 26, 5, cap); p.Line(54, 40, 62 + sway, 26, 5, cap);
            p.Rect(18, 56, 26, 63, Paint.Shade(cap, -20)); p.Rect(38, 56, 46, 63, Paint.Shade(cap, -20));
            return;
        }
        double top = 64 - 58 * s;
        p.Line(28, 63, 30, top + 14, 6 * s, stalk); p.Line(36, 63, 34, top + 14, 6 * s, stalk);
        if (s > 0.7) { p.Line(30, top + 24, 18 + sway, top + 34, 3, stalk); p.Line(34, top + 24, 46 + sway, top + 34, 3, stalk); }
        p.Ellipse(32 + sway / 2, top + 10, 20 * s + 4, 10 * s + 2, cap);
        for (int i = 0; i < 4; i++) p.Ellipse(22 + i * 7 + sway / 2, top + 7 + i % 2 * 3, 2.5, 2, stalk, false);
        if (s < 0.7 && f == SpriteFrame.Windup) p.Ellipse(32, top + 10, 24, 14, Paint.Shade(cap, 30), false);
    }

    // ---- Machines ----

    private static void Machine(Paint p, SpriteView v, SpriteFrame f, uint metal, uint eye, double s, bool heavy, bool cracks = false)
    {
        uint dark = Paint.Shade(metal, -30);
        if (f == SpriteFrame.Dead)
        {
            p.Rect(10, 52, 54, 63, metal);
            p.Ellipse(44, 50, 8, 6, dark);
            for (int i = 0; i < 4; i++) p.Rect(12 + i * 11, 48, 16 + i * 11, 52, dark);
            return;
        }
        double w = heavy ? 16 : 11, arm = f == SpriteFrame.Windup ? -14 : f == SpriteFrame.Strike ? 8 : 0;
        double top = 64 - 60 * s;
        p.Rect(32 - w + 3, 46, 32 - 3, 63, dark); p.Rect(32 + 3, 46, 32 + w - 3, 63, dark);
        p.Rect(32 - w, top + 14, 32 + w, 48, metal);
        if (cracks) for (int i = 0; i < 4; i++) p.Line(32 - w + 4 + i * 7, top + 18, 32 - w + 8 + i * 6, top + 40, 1.5, eye);
        p.Rect(32 - w - 8, top + 16, 32 - w, top + 36 + arm, dark);
        p.Rect(32 + w, top + 16, 32 + w + 8, top + 36 + arm, dark);
        p.Rect(26, top + 2, 38, top + 14, metal);
        if (v == SpriteView.Back) return;
        p.Rect(28, top + 6, 36, top + 9, eye, false);
        if (heavy) p.Ellipse(32, top + 30, 5, 5, eye, false);
    }

    private static void Cart(Paint p, SpriteFrame f)
    {
        uint iron = C(80, 76, 72), rust = C(130, 80, 50), eye = C(255, 180, 60);
        if (f == SpriteFrame.Dead) { p.Rect(6, 50, 58, 63, iron); p.Ellipse(14, 60, 6, 4, rust); return; }
        double lunge = f == SpriteFrame.Strike ? 4 : 0;
        p.Rect(6 - lunge, 22, 58 + lunge, 54, iron);
        p.Rect(8 - lunge, 18, 56 + lunge, 24, rust);
        p.Ellipse(16, 56, 7, 7, C(40, 38, 36)); p.Ellipse(48, 56, 7, 7, C(40, 38, 36));
        p.Rect(22, 32, 42, 40, C(20, 18, 16), false);
        p.Rect(24, 34, 30, 37, eye, false); p.Rect(34, 34, 40, 37, eye, false);
        if (f == SpriteFrame.Windup) p.Ellipse(32, 14, 10, 5, C(200, 200, 200), false);
    }

    private static void Engine(Paint p, SpriteFrame f)
    {
        uint brass = C(170, 130, 60), dark = C(90, 66, 34), spark = C(200, 220, 255);
        if (f == SpriteFrame.Dead) { p.Rect(4, 46, 60, 63, dark); p.Ellipse(32, 44, 16, 8, brass); return; }
        p.Rect(8, 30, 56, 63, dark);
        p.Ellipse(32, 22, 18, 18, brass);
        p.Ellipse(32, 26, 10, 10, dark);
        p.Rect(30, 2, 34, 8, brass);
        double swing = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? 10 : 0;
        p.Line(10, 34, 2, 18 + swing, 4, brass); p.Line(54, 34, 62, 18 + swing, 4, brass);
        p.Rect(20, 40, 44, 46, spark, false);
        if (f != SpriteFrame.Idle) for (int i = 0; i < 4; i++) p.Line(14 + i * 12, 50, 18 + i * 12, 58, 1, spark);
    }

    // ---- Serpents and tentacles ----

    private static void Serpent(Paint p, SpriteView v, SpriteFrame f, uint scale, uint trim, double s, bool crown = false)
    {
        uint belly = Paint.Shade(scale, 40);
        if (f == SpriteFrame.Dead) { p.Ellipse(32, 58, 26, 5, scale); p.Ellipse(52, 56, 6, 4, belly); return; }
        double rear = f == SpriteFrame.Windup ? -6 : f == SpriteFrame.Strike ? 3 : 0;
        p.Ellipse(32, 56, 24, 7, scale);
        p.Ellipse(20, 52, 10, 5, Paint.Shade(scale, -15));
        p.Rect(26, 20 + rear, 38, 54, scale);
        if (v != SpriteView.Back) p.Rect(29, 24 + rear, 35, 52, belly, false);
        p.Line(26, 26 + rear, 14, 36 + rear, 3, belly); p.Line(38, 26 + rear, 50, 36 + rear, 3, belly);
        p.Ellipse(32, 14 + rear, 8 * s, 8, scale);
        p.Ellipse(32, 14 + rear, 14 * s, 6, Paint.Shade(scale, -20)); // the hood
        p.Ellipse(32, 14 + rear, 7 * s, 7, scale);
        if (crown) { p.Rect(25, 3 + rear, 39, 6 + rear, trim); p.Triangle(28, 3 + rear, 32, -3 + rear, 36, 3 + rear, trim); }
        if (v == SpriteView.Back) return;
        p.Dot(29, 13 + rear, trim); p.Dot(35, 13 + rear, trim);
        if (f != SpriteFrame.Idle) p.Line(32, 18 + rear, 32, 23 + rear, 1, C(200, 40, 40));
    }

    private static void Tentacles(Paint p, SpriteFrame f)
    {
        uint skin = C(110, 110, 120), dark = C(60, 56, 70), mouth = C(150, 40, 60);
        if (f == SpriteFrame.Dead) { p.Ellipse(32, 58, 24, 6, skin); for (int i = 0; i < 4; i++) p.Line(10 + i * 14, 60, 4 + i * 16, 56, 3, dark); return; }
        double lift = f == SpriteFrame.Windup ? -12 : f == SpriteFrame.Strike ? 6 : 0;
        for (int i = 0; i < 6; i++)
        {
            double a = i / 5.0;
            p.Line(32, 44, 4 + a * 56, 10 + lift + (i % 2) * 10, 4, skin);
            p.Line(4 + a * 56, 10 + lift + (i % 2) * 10, 8 + a * 48, 4 + lift, 2, dark);
        }
        p.Ellipse(32, 48, 16, 15, skin);
        p.Ellipse(32, 48, 8, f == SpriteFrame.Idle ? 4 : 7, mouth, false);
    }

    private static void Mimic(Paint p, SpriteFrame f)
    {
        uint wood = C(120, 84, 50), iron = C(80, 80, 86), tooth = C(240, 236, 220);
        if (f == SpriteFrame.Dead) { p.Rect(10, 46, 54, 63, wood); p.Rect(10, 44, 54, 48, iron); return; }
        double open = f == SpriteFrame.Windup ? 14 : f == SpriteFrame.Strike ? 4 : 0;
        p.Rect(10, 40, 54, 63, wood);
        p.Rect(10, 40, 54, 43, iron);
        p.Rect(8, 24 - open, 56, 40 - open, wood);
        p.Rect(8, 24 - open, 56, 27 - open, iron);
        if (open > 0)
        {
            p.Rect(12, 40 - open, 52, 40, C(120, 20, 30), false);
            for (int i = 0; i < 7; i++) { p.Triangle(12 + i * 6, 40 - open, 15 + i * 6, 46 - open, 18 + i * 6, 40 - open, tooth); p.Triangle(12 + i * 6, 40, 15 + i * 6, 35, 18 + i * 6, 40, tooth); }
            p.Line(32, 40, 32 + open, 46 + open / 2, 3, C(200, 80, 100)); // the tongue
        }
        p.Rect(29, 44, 35, 50, iron);
    }
}
