namespace AVAHack.Render;

public enum SpriteView { Front, Side, Back }

public enum SpriteFrame { Idle, Windup, Strike, Dead }

/// <summary>
/// The monsters' placeholder pictures, drawn in code: each creature from the front, the side (facing right; the
/// left side is the mirror) and the back, standing, winding up an attack, striking and dead. Pictures are
/// 64×64 with the feet on the bottom row. Made once and cached.
/// </summary>
public sealed partial class SpriteFactory
{
    public const int Size = 64;

    private readonly Dictionary<(string, SpriteView, SpriteFrame, bool), SpriteImage> _cache = new();

    public static IReadOnlyList<string> Known => [.. new[] { "rat", "spider", "shambler", "skeleton", "goblin", "slinger" }, .. People.Keys.Order(StringComparer.Ordinal), .. MoreKnown];

    /// <summary>The picture for a sprite id, view and frame. <paramref name="mirror"/> flips a side view to face left.</summary>
    public SpriteImage Get(string sprite, SpriteView view, SpriteFrame frame, bool mirror = false)
    {
        if (frame == SpriteFrame.Dead) { view = SpriteView.Side; }
        var key = (sprite, view, frame, mirror);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = mirror ? Get(sprite, view, frame).Mirrored() : Draw(sprite, view, frame);
        _cache[key] = img;
        return img;
    }

    /// <summary>A glowing spell bolt: fire, frost, spark, acid, light, dark or arrow.</summary>
    public SpriteImage Bolt(string look)
    {
        var key = ("bolt-" + look, SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(24, 24);
        var p = new Paint(img);
        (uint outer, uint inner) = look switch
        {
            "fire" => (Paint.Rgb(230, 90, 20), Paint.Rgb(255, 230, 120)),
            "frost" => (Paint.Rgb(80, 160, 240), Paint.Rgb(230, 250, 255)),
            "spark" => (Paint.Rgb(200, 180, 255), Paint.Rgb(255, 255, 255)),
            "acid" => (Paint.Rgb(90, 200, 60), Paint.Rgb(220, 255, 160)),
            "light" => (Paint.Rgb(240, 220, 140), Paint.Rgb(255, 255, 230)),
            "dark" => (Paint.Rgb(90, 40, 130), Paint.Rgb(200, 120, 255)),
            _ => (Paint.Rgb(150, 120, 80), Paint.Rgb(220, 220, 220)),
        };
        if (look == "arrow")
        {
            p.Line(4, 12, 20, 12, 2, Paint.Rgb(140, 110, 70));
            p.Triangle(18, 9, 23, 12, 18, 15, Paint.Rgb(200, 200, 210));
        }
        else
        {
            p.Ellipse(12, 12, 10, 10, outer, false);
            p.Ellipse(12, 12, 5, 5, inner, false);
        }
        return _cache[key] = img;
    }

    public SpriteImage Stone()
    {
        var key = ("stone", SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(16, 16);
        var p = new Paint(img);
        p.Ellipse(8, 8, 5, 4.5, Paint.Rgb(130, 124, 112));
        p.Outline();
        return _cache[key] = img;
    }

    /// <summary>Things a trap or the level puts in a cell: altar, boulder, blade, gas.</summary>
    public SpriteImage Prop(string kind)
    {
        var key = ("prop-" + kind, SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(Size, Size);
        var p = new Paint(img);
        switch (kind)
        {
            case "altar":
                p.Rect(14, 34, 50, 63, Paint.Rgb(90, 84, 80));
                p.Rect(10, 30, 54, 36, Paint.Rgb(110, 104, 98));
                p.Ellipse(32, 27, 6, 4, Paint.Rgb(210, 170, 60));
                p.Ellipse(20, 26, 2, 5, Paint.Rgb(230, 220, 180));
                p.Ellipse(44, 26, 2, 5, Paint.Rgb(230, 220, 180));
                break;
            case "anvil":
                p.Rect(14, 44, 50, 63, Paint.Rgb(70, 50, 40));                // the forge's hearth
                p.Ellipse(32, 46, 12, 4, Paint.Rgb(255, 120, 30), false);
                p.Rect(20, 30, 46, 36, Paint.Rgb(90, 92, 100));                 // the anvil
                p.Triangle(46, 30, 56, 30, 46, 36, Paint.Rgb(90, 92, 100));
                p.Rect(28, 36, 38, 44, Paint.Rgb(70, 72, 80));
                break;
            case "altar-enchant":
                p.Rect(18, 34, 46, 63, Paint.Rgb(110, 100, 130));
                p.Ellipse(32, 32, 16, 5, Paint.Rgb(140, 130, 170));
                p.Ellipse(32, 30, 9, 3, Paint.Rgb(170, 230, 255), false);
                for (int i = 0; i < 3; i++) p.Dot(26 + i * 6, 22 - i % 2 * 4, Paint.Rgb(200, 240, 255));
                break;
            case "waystone":
                p.Rect(22, 14, 42, 63, Paint.Rgb(120, 126, 140));
                p.Triangle(22, 14, 32, 2, 42, 14, Paint.Rgb(120, 126, 140));
                for (int i = 0; i < 4; i++) p.Rect(28, 20 + i * 10, 36, 23 + i * 10, Paint.Rgb(140, 220, 255), false);
                break;
            case "boulder":
                p.Ellipse(32, 36, 27, 27, Paint.Rgb(110, 100, 90));
                p.Ellipse(24, 28, 8, 6, Paint.Rgb(130, 120, 110));
                break;
            case "blade":
                p.Line(32, 0, 32, 30, 2, Paint.Rgb(80, 76, 72));
                p.Ellipse(32, 40, 26, 12, Paint.Rgb(190, 194, 202));
                break;
            case "gasPoison":
            case "gasSleep":
            {
                uint c = kind == "gasPoison" ? Paint.Rgb(90, 160, 60) : Paint.Rgb(200, 170, 220);
                for (int i = 0; i < 9; i++) p.Ellipse(10 + i * 6 % 44, 26 + i * 7 % 30, 10, 8, c, false);
                for (int i = 0; i < img.Pixels.Length; i++) if (img.Pixels[i] >> 24 != 0) img.Pixels[i] = img.Pixels[i] & 0x00FFFFFF | 0xFF000000;
                return _cache[key] = img;
            }
        }
        p.Outline();
        return _cache[key] = img;
    }

    /// <summary>A chest, shut or open.</summary>
    public SpriteImage Chest(bool open)
    {
        var key = (open ? "chest-open" : "chest", SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(Size, Size);
        var p = new Paint(img);
        uint wood = Paint.Rgb(118, 78, 40), band = Paint.Rgb(70, 66, 70), dark = Paint.Rgb(30, 18, 10);
        p.Rect(10, 36, 54, 63, wood);
        if (open)
        {
            p.Rect(12, 38, 52, 44, dark, false);
            p.Rect(10, 18, 54, 30, wood);
            p.Ellipse(32, 40, 10, 3, Paint.Rgb(210, 170, 60));
        }
        else
        {
            p.Ellipse(32, 36, 22, 8, Paint.Rgb(128, 86, 46));
            p.Rect(10, 34, 54, 38, band, false);
            p.Rect(29, 40, 35, 47, Paint.Rgb(190, 160, 70), false);
        }
        p.Rect(14, 36, 17, 63, band, false);
        p.Rect(47, 36, 50, 63, band, false);
        p.Outline();
        return _cache[key] = img;
    }

    /// <summary>A small picture for an item lying on the floor, by kind: weapon, shield, armour, potion, book, gold, other.</summary>
    public SpriteImage ItemIcon(string kind)
    {
        var key = ("item-" + kind, SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(32, 32);
        var p = new Paint(img);
        switch (kind)
        {
            case "weapon":
                p.Line(6, 26, 26, 6, 3, Paint.Rgb(200, 204, 212));
                p.Line(4, 22, 10, 28, 3, Paint.Rgb(140, 110, 60));
                break;
            case "shield":
                p.Ellipse(16, 18, 11, 11, Paint.Rgb(120, 80, 44));
                p.Ellipse(16, 18, 3, 3, Paint.Rgb(170, 170, 176));
                break;
            case "armour":
                p.Rect(8, 10, 24, 28, Paint.Rgb(150, 150, 160));
                p.Rect(4, 10, 28, 15, Paint.Rgb(130, 130, 140));
                break;
            case "potion":
                p.Ellipse(16, 21, 8, 8, Paint.Rgb(190, 40, 40));
                p.Rect(13, 6, 19, 14, Paint.Rgb(170, 190, 200));
                break;
            case "book":
                p.Rect(7, 9, 25, 27, Paint.Rgb(110, 40, 30));
                p.Rect(9, 11, 23, 25, Paint.Rgb(140, 60, 40));
                break;
            case "gold":
                p.Ellipse(12, 22, 6, 3, Paint.Rgb(222, 180, 60));
                p.Ellipse(19, 20, 6, 3, Paint.Rgb(232, 196, 70));
                p.Ellipse(15, 17, 6, 3, Paint.Rgb(242, 206, 80));
                break;
            default:
                p.Ellipse(16, 20, 10, 9, Paint.Rgb(150, 120, 80));
                p.Rect(13, 8, 19, 13, Paint.Rgb(120, 90, 60));
                break;
        }
        p.Outline();
        return _cache[key] = img;
    }

    private static SpriteImage Draw(string sprite, SpriteView view, SpriteFrame frame)
    {
        var img = new SpriteImage(Size, Size);
        var p = new Paint(img);
        switch (sprite)
        {
            case "rat": Rat(p, view, frame); break;
            case "spider": Spider(p, view, frame); break;
            case "shambler": Humanoid(p, view, frame, Shambler); break;
            case "skeleton": Humanoid(p, view, frame, Skeleton); break;
            case "goblin": Humanoid(p, view, frame, Goblin); break;
            case "slinger": Humanoid(p, view, frame, Slinger); break;
            default:
                if (DrawMore(p, sprite, view, frame)) break;
                p.Ellipse(32, 40, 20, 22, Paint.Rgb(200, 0, 200)); // missing art shows as a magenta blob
                break;
        }
        p.Outline();
        return img;
    }

    // ---- Rats ----

    private static void Rat(Paint p, SpriteView v, SpriteFrame f)
    {
        uint fur = Paint.Rgb(104, 88, 76), dark = Paint.Rgb(70, 58, 50), pink = Paint.Rgb(200, 130, 130), eye = Paint.Rgb(230, 40, 30);
        if (f == SpriteFrame.Dead)
        {
            p.Line(14, 58, 4, 50, 2, pink);
            p.Ellipse(30, 56, 18, 7, fur);
            for (int i = 0; i < 4; i++) p.Line(20 + i * 7, 52, 18 + i * 7, 44, 2, pink);
            p.Ellipse(50, 56, 7, 5, fur);
            return;
        }
        double rear = f == SpriteFrame.Windup ? -6 : 0, lunge = f == SpriteFrame.Strike ? 4 : 0;
        switch (v)
        {
            case SpriteView.Front:
                p.Ellipse(32, 50, 17, 12, fur);
                p.Ellipse(20, 34 + rear, 5, 5, pink);
                p.Ellipse(44, 34 + rear, 5, 5, pink);
                p.Ellipse(32, 42 + rear, 10 + lunge, 9 + lunge, fur);
                p.Ellipse(32, 48 + rear, 3, 2, pink, false);
                p.Dot(28, 40 + rear, eye); p.Dot(29, 40 + rear, eye);
                p.Dot(35, 40 + rear, eye); p.Dot(36, 40 + rear, eye);
                if (f != SpriteFrame.Idle) p.Rect(30, 50 + rear, 34, 53 + rear, Paint.Rgb(40, 10, 10), false);
                p.Line(24, 47 + rear, 12, 45 + rear, 1, dark);
                p.Line(40, 47 + rear, 52, 45 + rear, 1, dark);
                break;
            case SpriteView.Side:
                p.Line(12, 52, 2, 40, 2, pink);
                p.Ellipse(28, 50, 19, 11, fur);
                p.Ellipse(48 + lunge, 46 + rear, 10, 7, fur);
                p.Ellipse(44 + lunge, 39 + rear, 4, 4, pink);
                p.Dot(51 + lunge, 44 + rear, eye); p.Dot(52 + lunge, 44 + rear, eye);
                p.Dot(58 + lunge, 47 + rear, pink);
                p.Rect(20, 58, 23, 63, dark); p.Rect(36, 58, 39, 63, dark);
                break;
            default:
                p.Ellipse(32, 50, 17, 12, fur);
                p.Ellipse(20, 38, 5, 5, pink); p.Ellipse(44, 38, 5, 5, pink);
                p.Line(32, 58, 34, 63, 3, pink);
                break;
        }
    }

    // ---- Spiders ----

    private static void Spider(Paint p, SpriteView v, SpriteFrame f)
    {
        uint shell = Paint.Rgb(60, 48, 40), leg = Paint.Rgb(46, 36, 30), mark = Paint.Rgb(150, 60, 40), eye = Paint.Rgb(240, 60, 40);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 56, 14, 7, shell);
            for (int i = 0; i < 4; i++) { p.Line(22 + i * 6, 52, 20 + i * 7, 42 - i % 2 * 3, 2, leg); }
            return;
        }
        double raise = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? -4 : 0;
        if (v == SpriteView.Side)
        {
            for (int i = 0; i < 4; i++)
            {
                double bx = 30 + i * 5;
                p.Line(bx, 48, bx + 6 + i * 2, 36 + (i == 3 ? raise : 0), 2, leg);
                p.Line(bx + 6 + i * 2, 36 + (i == 3 ? raise : 0), bx + 10 + i * 3, 63, 2, leg);
            }
            p.Ellipse(20, 44, 16, 12, shell);
            p.Ellipse(20, 42, 6, 4, mark);
            p.Ellipse(42, 48, 9, 7, shell);
            p.Dot(49, 46, eye); p.Dot(50, 46, eye);
            return;
        }
        // Front and back: legs fanned out either side.
        for (int i = 0; i < 4; i++)
        {
            double knee = 30 - i * 3 + (i == 0 ? raise : 0);
            p.Line(26, 48, 12 - i * 2, knee, 2, leg); p.Line(12 - i * 2, knee, 4 + i * 3, 63, 2, leg);
            p.Line(38, 48, 52 + i * 2, knee, 2, leg); p.Line(52 + i * 2, knee, 60 - i * 3, 63, 2, leg);
        }
        p.Ellipse(32, 40, 15, 12, shell);
        if (v == SpriteView.Back) { p.Ellipse(32, 38, 6, 5, mark); return; }
        p.Ellipse(32, 50, 9, 7, shell);
        foreach (var (x, y) in new[] { (28, 47), (36, 47), (30, 50), (34, 50), (26, 50), (38, 50) }) p.Dot(x, y, eye);
        p.Line(29, 55, 28, 59, 2, Paint.Rgb(20, 14, 10)); p.Line(35, 55, 36, 59, 2, Paint.Rgb(20, 14, 10));
    }

    // ---- Things that walk on two legs ----

    private sealed record Look(uint Skin, uint Clothes, uint Trim, double Height, bool Ears, string Weapon, bool Bones, bool Hood, uint Eye);

    private static readonly Look Shambler = new(Paint.Rgb(118, 134, 98), Paint.Rgb(88, 72, 56), Paint.Rgb(60, 50, 40), 1.0, false, "none", false, false, Paint.Rgb(220, 220, 120));
    private static readonly Look Skeleton = new(Paint.Rgb(214, 206, 184), Paint.Rgb(214, 206, 184), Paint.Rgb(150, 140, 120), 1.0, false, "sword", true, false, Paint.Rgb(255, 70, 40));
    private static readonly Look Goblin = new(Paint.Rgb(98, 142, 62), Paint.Rgb(124, 92, 52), Paint.Rgb(80, 60, 34), 0.8, true, "knife", false, false, Paint.Rgb(250, 220, 60));
    private static readonly Look Slinger = new(Paint.Rgb(98, 142, 62), Paint.Rgb(96, 84, 70), Paint.Rgb(70, 60, 50), 0.8, true, "sling", false, true, Paint.Rgb(250, 220, 60));

    private static void Humanoid(Paint p, SpriteView v, SpriteFrame f, Look k)
    {
        if (f == SpriteFrame.Dead)
        {
            if (k.Bones)
            {
                // A heap of bones with the skull on top.
                for (int i = 0; i < 6; i++) p.Line(14 + i * 6, 60 - i % 3 * 2, 24 + i * 5, 54 + i % 2 * 4, 3, k.Skin);
                p.Ellipse(30, 52, 6, 5, k.Skin);
                p.Dot(28, 52, Paint.Rgb(30, 20, 20)); p.Dot(32, 52, Paint.Rgb(30, 20, 20));
                return;
            }
            p.Ellipse(32, 58, 22, 5, k.Clothes);
            p.Ellipse(52, 56, 6, 5, k.Skin);
            p.Line(18, 56, 8, 60, 3, k.Skin);
            return;
        }

        double s = k.Height;
        double top = 64 - 60 * s;                     // top of the head
        double headY = top + 7 * s, neck = top + 14 * s, hip = top + 36 * s;
        double cx = 32;
        double torsoW = (v == SpriteView.Side ? 6 : 9) * s;
        uint eye = f == SpriteFrame.Windup ? Paint.Rgb(255, 255, 200) : k.Eye;

        // Legs.
        double stride = f == SpriteFrame.Strike ? 5 : 2;
        p.Line(cx - (v == SpriteView.Side ? stride : 4 * s), hip, cx - (v == SpriteView.Side ? stride + 2 : 6 * s), 63, 4 * s, k.Bones ? k.Skin : k.Trim);
        p.Line(cx + (v == SpriteView.Side ? stride : 4 * s), hip, cx + (v == SpriteView.Side ? stride + 2 : 6 * s), 63, 4 * s, k.Bones ? k.Skin : k.Trim);

        // Body.
        if (k.Bones)
        {
            p.Line(cx, neck, cx, hip, 3, k.Skin);
            for (int i = 0; i < 4; i++) p.Line(cx - torsoW, neck + 4 + i * 4, cx + torsoW, neck + 4 + i * 4, 2, k.Skin);
            p.Line(cx - torsoW, hip, cx + torsoW, hip, 3, k.Skin);
        }
        else
        {
            p.Rect(cx - torsoW, neck, cx + torsoW, hip + 2, k.Clothes);
            p.Rect(cx - torsoW, hip - 2, cx + torsoW, hip + 2, k.Trim, false);
        }

        // Arms: down at rest, raised in the tell, thrust forward in the blow.
        double shoulder = neck + 3;
        var (hx, hy) = f switch
        {
            SpriteFrame.Windup => (v == SpriteView.Side ? cx - 4 : cx + 14 * s, top - 2),
            SpriteFrame.Strike => (v == SpriteView.Side ? cx + 22 : cx + 10 * s, neck + 10),
            _ => (v == SpriteView.Side ? cx + 6 : cx + 13 * s, hip + (k.Weapon == "none" ? -6 : 2)),
        };
        uint arm = k.Bones ? k.Skin : k.Skin;
        if (v != SpriteView.Side) p.Line(cx - torsoW - 1, shoulder, cx - 13 * s, hip + (k.Weapon == "none" && f == SpriteFrame.Idle ? -6 : 2), 3 * s, arm);
        if (k.Weapon == "none" && v == SpriteView.Front && f == SpriteFrame.Idle)
        {
            // A shambler's arms reach for you.
            p.Line(cx - torsoW, shoulder, cx - 6, shoulder + 14, 3, arm);
            p.Line(cx + torsoW, shoulder, cx + 6, shoulder + 14, 3, arm);
        }
        else p.Line(cx + torsoW + 1, shoulder, hx, hy, 3 * s, arm);
        Weapon(p, k, hx, hy, f, v);

        // Head.
        if (k.Ears && v != SpriteView.Back)
        {
            p.Triangle(cx - 6 * s, headY - 1, cx - 18 * s, headY - 6, cx - 5 * s, headY + 3, k.Skin);
            p.Triangle(cx + 6 * s, headY - 1, cx + 18 * s, headY - 6, cx + 5 * s, headY + 3, k.Skin);
        }
        p.Ellipse(cx + (v == SpriteView.Side ? 2 : 0), headY, 7 * s, 7.5 * s, k.Hood ? k.Clothes : k.Skin);
        if (k.Hood) p.Ellipse(cx + (v == SpriteView.Side ? 4 : 0), headY + 1.5, 4.5 * s, 4.5 * s, k.Skin);
        if (v == SpriteView.Back) return;
        double ey = headY + (k.Hood ? 1 : 0);
        if (v == SpriteView.Front)
        {
            if (k.Bones)
            {
                p.Ellipse(cx - 3, ey, 2, 2, Paint.Rgb(30, 20, 20), false);
                p.Ellipse(cx + 3, ey, 2, 2, Paint.Rgb(30, 20, 20), false);
                if (f == SpriteFrame.Windup) { p.Dot(cx - 3, ey, eye); p.Dot(cx + 3, ey, eye); }
                p.Rect(cx - 3, ey + 4, cx + 3, ey + 5, Paint.Rgb(60, 50, 40), false);
            }
            else
            {
                p.Dot(cx - 3, ey, eye); p.Dot(cx - 2, ey, eye);
                p.Dot(cx + 2, ey, eye); p.Dot(cx + 3, ey, eye);
                p.Rect(cx - 2, ey + 3, cx + 2, ey + 4, Paint.Rgb(40, 20, 20), false);
            }
        }
        else
        {
            p.Dot(cx + 6, ey, k.Bones ? Paint.Rgb(30, 20, 20) : eye);
            p.Dot(cx + 9 * s, ey + 2, k.Skin);
        }
    }

    private static void Weapon(Paint p, Look k, double hx, double hy, SpriteFrame f, SpriteView v)
    {
        switch (k.Weapon)
        {
            case "sword":
            {
                uint rust = Paint.Rgb(150, 112, 84), hilt = Paint.Rgb(70, 50, 30);
                double tipX = f == SpriteFrame.Strike ? hx + (v == SpriteView.Side ? 14 : 4) : hx + 2, tipY = f == SpriteFrame.Windup ? hy - 18 : f == SpriteFrame.Strike ? hy + 2 : hy - 20;
                p.Line(hx, hy, tipX, tipY, 2.5, rust);
                p.Line(hx - 4, hy, hx + 4, hy, 2, hilt);
                break;
            }
            case "knife":
                p.Line(hx, hy, hx + (f == SpriteFrame.Strike ? 8 : 2), hy - (f == SpriteFrame.Strike ? 2 : 8), 2, Paint.Rgb(170, 170, 176));
                break;
            case "pick":
                p.Line(hx, hy, hx + 2, hy - 16, 2, Paint.Rgb(110, 80, 50));
                p.Line(hx - 6, hy - 16, hx + 10, hy - 14, 2.5, Paint.Rgb(150, 150, 156));
                break;
            case "axe":
                p.Line(hx, hy, hx + 2, hy - 18, 2, Paint.Rgb(110, 80, 50));
                p.Triangle(hx + 2, hy - 20, hx + 12, hy - 22, hx + 10, hy - 10, Paint.Rgb(170, 170, 176));
                break;
            case "club":
                p.Line(hx, hy, hx + 3, hy - 16, 4, Paint.Rgb(110, 80, 50));
                break;
            case "spear":
                p.Line(hx, hy + 8, hx + 2, hy - 24, 2, Paint.Rgb(110, 80, 50));
                p.Triangle(hx - 1, hy - 24, hx + 2, hy - 31, hx + 5, hy - 24, Paint.Rgb(190, 190, 200));
                break;
            case "staff":
                p.Line(hx, hy + 10, hx + 1, hy - 22, 2, Paint.Rgb(90, 70, 50));
                p.Ellipse(hx + 1, hy - 23, 3, 3, f == SpriteFrame.Windup ? Paint.Rgb(255, 255, 200) : Paint.Rgb(150, 200, 255), false);
                break;
            case "whip":
                p.Line(hx, hy, hx + (f == SpriteFrame.Strike ? 18 : 6), hy + (f == SpriteFrame.Strike ? -4 : 14), 1, Paint.Rgb(70, 50, 30));
                break;
            case "bow":
                p.Line(hx, hy - 12, hx + 3, hy, 1.5, Paint.Rgb(120, 90, 60));
                p.Line(hx + 3, hy, hx, hy + 12, 1.5, Paint.Rgb(120, 90, 60));
                p.Line(hx, hy - 12, hx, hy + 12, 1, Paint.Rgb(200, 200, 190));
                break;
            case "lantern":
                p.Line(hx, hy, hx, hy + 4, 1, Paint.Rgb(60, 60, 60));
                p.Rect(hx - 3, hy + 4, hx + 3, hy + 10, Paint.Rgb(120, 255, 160), false);
                break;
            case "sling":
                if (f == SpriteFrame.Windup)
                {
                    p.Line(hx, hy, hx - 8, hy - 6, 1, Paint.Rgb(90, 70, 50));
                    p.Ellipse(hx - 9, hy - 7, 2.5, 2.5, Paint.Rgb(130, 124, 112));
                }
                else p.Line(hx, hy, hx + 1, hy + 10, 1, Paint.Rgb(90, 70, 50));
                break;
        }
    }
}
