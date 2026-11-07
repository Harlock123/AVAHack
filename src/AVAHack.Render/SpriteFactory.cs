namespace AVAHack.Render;

public enum SpriteView { Front, Side, Back }

public enum SpriteFrame { Idle, Windup, Strike, Dead }

/// <summary>
/// The monsters' placeholder pictures, drawn in code: each creature from the front, the side (facing right; the
/// left side is the mirror) and the back, standing, winding up an attack, striking and dead. Drawers work on a
/// 64-unit grid; pictures are <see cref="Size"/> (128) across, with the feet on the bottom row, and are shaded as
/// pixel art by <see cref="Paint.Finish"/>. Made once and cached.
/// </summary>
public sealed partial class SpriteFactory
{
    /// <summary>The drawing grid a creature is drawn on.</summary>
    public const int Grid = 64;
    /// <summary>Picture pixels per grid unit.</summary>
    public const double Scale = 2;
    /// <summary>A creature's picture, in pixels.</summary>
    public const int Size = (int)(Grid * Scale);

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
        img = new SpriteImage(48, 48);
        var p = new Paint(img, Scale);
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
        img = new SpriteImage(32, 32);
        var p = new Paint(img, Scale);
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
        var p = new Paint(img, Scale);
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
            case "candle":
            case "candle-out":
            {
                // A tall iron candlestand with three candles: lit, or pinched out and smoking.
                p.Mat = Material.Metal;
                p.Taper(32, 63, 1.4, 32, 26, 1.2, C(60, 58, 62));
                p.Ellipse(32, 62, 9, 2, C(60, 58, 62));
                p.Taper(20, 26, 1, 44, 26, 1, C(70, 68, 72));
                p.Mat = Material.Matte;
                foreach (int x in new[] { 21, 32, 43 })
                {
                    p.Rect(x - 1.6, x == 32 ? 12 : 16, x + 1.6, 26, C(226, 216, 186));
                    if (kind == "candle")
                    {
                        p.Ellipse(x, (x == 32 ? 12 : 16) - 3, 2, 3.2, C(255, 180, 60), false);
                        p.Ellipse(x, (x == 32 ? 12 : 16) - 2.4, 0.9, 1.6, C(255, 250, 220), false);
                    }
                    else p.Taper(x, (x == 32 ? 12 : 16) - 1, 0.5, x + 2, (x == 32 ? 12 : 16) - 9, 0.4, C(120, 116, 112), false);
                }
                break;
            }
            case "pod":
            case "pod-burst":
            {
                // A spore pod on a knot of root: a swollen, veined sac, or its torn husk.
                p.Mat = Material.Matte;
                p.Ellipse(32, 60, 14, 4, C(70, 52, 36));
                p.Limb(22, 62, 26, 54, 30, 46, 2.4, 1.6, C(88, 66, 42));
                p.Limb(42, 62, 38, 54, 34, 46, 2.4, 1.6, C(88, 66, 42));
                if (kind == "pod")
                {
                    p.Mat = Material.Gloss;
                    p.Ellipse(32, 34, 15, 17, C(176, 150, 70));
                    p.Ellipse(32, 30, 9, 9, C(206, 186, 96));
                    p.Mat = Material.Matte;
                    for (int i = 0; i < 5; i++) p.Line(24 + i * 4, 20 + i % 2 * 3, 26 + i * 3, 46, 0.7, C(130, 100, 50));
                    foreach (var (x, y) in new[] { (26, 26), (38, 30), (31, 42), (35, 21) }) p.Ellipse(x, y, 1.4, 1.4, C(240, 230, 140), false);
                }
                else
                {
                    p.Poly(C(120, 96, 52), (20, 48), (24, 34), (28, 42), (32, 30), (36, 42), (41, 33), (44, 48));
                    p.Ellipse(32, 47, 12, 4, C(70, 54, 30));
                }
                break;
            }
            case "roots":
            case "roots-cut":
            {
                // A tangle of roots grown down across the passage, floor to ceiling; or the hacked stumps.
                p.Mat = Material.Matte;
                uint bark = C(92, 70, 48), dark = C(64, 48, 34);
                if (kind == "roots")
                {
                    for (int i = 0; i < 9; i++)
                    {
                        double x0 = 4 + i * 7, x2 = 60 - i * 6.5;
                        p.Limb(x0, 0, 32 + (i % 3 - 1) * 10, 30 + i % 4 * 3, x2, 63, 2.6 - i % 3 * 0.5, 1.6, i % 2 == 0 ? bark : dark);
                    }
                    for (int i = 0; i < 6; i++) p.Taper(10 + i * 9, 6 + i * 3 % 14, 1.2, 14 + i * 8, 40 + i * 5 % 20, 0.4, C(118, 94, 64));
                }
                else
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double x = 8 + i * 10;
                        p.Taper(x, 0, 2.4, x + 2, 8 + i % 3 * 4, 1.6, i % 2 == 0 ? bark : dark);
                        p.Taper(x + 3, 63, 2.4, x + 2, 56 - i % 2 * 4, 1.4, dark);
                    }
                    p.Ellipse(32, 61, 16, 2, C(150, 120, 80), false);   // chips and sawdust
                }
                break;
            }
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
            case "gasSpores":
            {
                uint c = kind == "gasPoison" ? Paint.Rgb(90, 160, 60) : kind == "gasSpores" ? Paint.Rgb(214, 190, 80) : Paint.Rgb(200, 170, 220);
                for (int i = 0; i < 9; i++) p.Ellipse(10 + i * 6 % 44, 26 + i * 7 % 30, 10, 8, c, false);
                p.Finish();
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
        var p = new Paint(img, Scale);
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

    private static SpriteImage Draw(string sprite, SpriteView view, SpriteFrame frame)
    {
        var img = new SpriteImage(Size, Size);
        var p = new Paint(img, Scale);
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

    // ---- Things that walk on two legs ----

    private sealed record Look(uint Skin, uint Clothes, uint Trim, double Height, bool Ears, string Weapon, bool Bones, bool Hood, uint Eye);

    private static readonly Look Shambler = new(Paint.Rgb(118, 134, 98), Paint.Rgb(88, 72, 56), Paint.Rgb(60, 50, 40), 1.0, false, "none", false, false, Paint.Rgb(220, 220, 120));
    private static readonly Look Skeleton = new(Paint.Rgb(214, 206, 184), Paint.Rgb(214, 206, 184), Paint.Rgb(150, 140, 120), 1.0, false, "sword", true, false, Paint.Rgb(255, 70, 40));
    private static readonly Look Goblin = new(Paint.Rgb(98, 142, 62), Paint.Rgb(124, 92, 52), Paint.Rgb(80, 60, 34), 0.8, true, "knife", false, false, Paint.Rgb(250, 220, 60));
    private static readonly Look Slinger = new(Paint.Rgb(98, 142, 62), Paint.Rgb(96, 84, 70), Paint.Rgb(70, 60, 50), 0.8, true, "sling", false, true, Paint.Rgb(250, 220, 60));

    private static void Humanoid(Paint p, SpriteView v, SpriteFrame f, Look k)
    {
        uint skinDark = Paint.Shade(k.Skin, -34), cloth = k.Clothes, trim = k.Trim, boot = Paint.Shade(k.Trim, -30);
        uint socket = C(22, 14, 16), mouth = C(46, 16, 18);
        if (f == SpriteFrame.Dead)
        {
            if (k.Bones)
            {
                // A heap of bones with the skull on top.
                p.Mat = Material.Bone;
                for (int i = 0; i < 7; i++) p.Taper(10 + i * 6, 61 - i % 3 * 2, 1.6, 20 + i * 5, 56 + i % 2 * 4, 1.2, k.Skin);
                p.Ellipse(24, 58, 6, 3, k.Skin);
                p.Ellipse(36, 53, 6, 5.5, k.Skin);
                p.Rect(33, 56, 39, 59, k.Skin);
                p.Ellipse(34, 53, 1.6, 1.6, socket, false); p.Ellipse(38.5, 53, 1.4, 1.6, socket, false);
                p.Mat = Material.Matte;
                return;
            }
            p.Mat = Material.Cloth;
            p.Ellipse(30, 58, 18, 4.5, cloth);
            p.Mat = Material.Matte;
            p.Taper(14, 59, 2.4, 4, 61, 2, k.Trim);
            p.Taper(16, 56, 2.2, 8, 54, 1.8, k.Skin);
            p.Taper(40, 58, 2.2, 46, 62, 1.8, k.Skin);
            p.Ellipse(50, 57, 5.5, 4.5, k.Hood ? cloth : k.Skin);
            if (k.Ears) p.Triangle(48, 54, 56, 50, 52, 57, k.Skin);
            p.Dot(51, 56, socket);
            return;
        }

        double s = k.Height, cx = 32;
        double top = 63 - 58 * s;
        double headY = top + 6.5 * s, neck = top + 13 * s, shoulderY = top + 16 * s, waist = top + 31 * s, hip = top + 33 * s;
        bool side = v == SpriteView.Side, back = v == SpriteView.Back, front = v == SpriteView.Front;
        bool shamble = k.Weapon == "none" && !k.Bones && k.Hood == false && k.Skin == Shambler.Skin;
        if (shamble) { headY += 2; neck += 1; } // it slumps
        double hw = (side ? 6.5 : 10) * s;    // half the shoulders
        double ww = (side ? 5.5 : 7) * s;     // half the waist
        double headX = cx + (side ? 2.5 * s : 0);

        // Where the weapon hand goes: low at rest, up and back in the tell, out in the blow.
        var (ex, ey, hx, hy) = f switch
        {
            SpriteFrame.Windup => side ? (cx - 3 * s, top + 10 * s, cx - 6 * s, top - 2 * s) : (cx + 15 * s, top + 11 * s, cx + 12 * s, top - 1 * s),
            SpriteFrame.Strike => side ? (cx + 10 * s, top + 20 * s, cx + 19 * s, top + 22 * s) : (cx + 13 * s, top + 22 * s, cx + 7 * s, top + 28 * s),
            _ => side ? (cx + 2 * s, top + 26 * s, cx + 5 * s, top + 35 * s) : (cx + 12.5 * s, top + 26 * s, cx + 13 * s, top + 35 * s),
        };

        // ---- Legs and feet ----
        double stride = side ? (f == SpriteFrame.Strike ? 6 : 3) * s : 0;
        void Leg(double sign, double fwd)
        {
            double lx = cx + (side ? fwd : sign * 4 * s);
            uint legC = k.Bones ? k.Skin : trim;
            if (k.Bones)
            {
                p.Mat = Material.Bone;
                p.Limb(lx, hip, lx + fwd * 0.3 + (side ? 0 : sign * 0.5 * s), hip + 12 * s, lx + fwd * 0.6 + (side ? 0 : sign * 1.2 * s), 60.5, 1.6 * s, 1.2 * s, legC);
                p.Ellipse(lx + fwd * 0.3 + (side ? 0 : sign * 0.5 * s), hip + 12 * s, 2 * s, 1.8 * s, legC);
                p.Ellipse(lx + fwd * 0.6 + (side ? 1.5 : sign * 1.5) * s, 61.5, 3 * s, 1.6, legC);
                p.Mat = Material.Matte;
                return;
            }
            p.Limb(lx, hip, lx + fwd * 0.3 + (side ? 0 : sign * 0.6 * s), hip + 12 * s, lx + fwd * 0.6 + (side ? 0 : sign * 1.2 * s), 59, 3.2 * s, 2.4 * s, legC);
            p.Ellipse(lx + fwd * 0.6 + (side ? 2 : sign * 1.5) * s, 61, 3.6 * s, 2.2, boot);
        }
        if (side) { Leg(-1, -stride); Leg(1, stride); }
        else { Leg(-1, 0); Leg(1, 0); }

        // ---- The far arm (behind the body) ----
        void Arm(double sx, double sy, double elx, double ely, double hdx, double hdy)
        {
            if (k.Bones)
            {
                p.Mat = Material.Bone;
                p.Limb(sx, sy, elx, ely, hdx, hdy, 1.5 * s, 1.1 * s, k.Skin);
                p.Ellipse(elx, ely, 1.7 * s, 1.7 * s, k.Skin);
                p.Ellipse(hdx, hdy, 2 * s, 1.8 * s, k.Skin);
                p.Mat = Material.Matte;
                return;
            }
            p.Limb(sx, sy, elx, ely, hdx, hdy, 2.8 * s, 2.1 * s, k.Skin);
            p.Mat = Material.Cloth;
            p.Taper(sx, sy, 3.2 * s, (sx + elx) / 2, (sy + ely) / 2, 2.8 * s, cloth); // the sleeve
            p.Mat = Material.Matte;
            p.Ellipse(hdx, hdy, 2.4 * s, 2.4 * s, k.Skin);
        }
        if (side) Arm(cx - 1 * s, shoulderY + 1, cx - 3 * s, top + 26 * s, cx - 2 * s, top + 34 * s);

        // ---- The body ----
        if (k.Bones)
        {
            p.Mat = Material.Bone;
            p.Taper(cx, neck, 1.6 * s, cx, hip, 1.4 * s, k.Skin);                              // the spine
            for (int i = 0; i < 4; i++)
            {
                double ry = neck + 4 * s + i * 3.6 * s, span = hw - 1 - i * 0.8 * s;
                if (side) p.Taper(cx, ry, 1.2 * s, cx + 4 * s, ry + 2.5 * s, 0.9 * s, k.Skin);
                else
                {
                    p.Taper(cx, ry, 1.2 * s, cx - span, ry + 2.5 * s, 0.9 * s, k.Skin);
                    p.Taper(cx, ry, 1.2 * s, cx + span, ry + 2.5 * s, 0.9 * s, k.Skin);
                }
            }
            p.Ellipse(cx, hip, (side ? 4 : 7) * s, 2.6 * s, k.Skin);                           // the pelvis
            p.Taper(cx - hw, shoulderY, 1.3 * s, cx + hw, shoulderY, 1.3 * s, k.Skin);         // the collarbones
            if (k.Clothes != k.Skin)
            {
                // A king's mantle over the bones.
                p.Mat = Material.Cloth;
                p.Poly(k.Clothes, (cx - hw - 1, shoulderY - 1), (cx + hw + 1, shoulderY - 1), (cx + hw + 4 * s, 58), (cx - hw - 4 * s, 58));
                p.Mat = Material.Matte;
                if (front) p.Poly(C(20, 14, 18), false, (cx - 4 * s, shoulderY), (cx + 4 * s, shoulderY), (cx + 6 * s, 58), (cx - 6 * s, 58));
                if (front) for (int i = 0; i < 3; i++) p.Taper(cx - 3 * s, shoulderY + 4 * s + i * 3.6 * s, 1 * s, cx + 3 * s, shoulderY + 4 * s + i * 3.6 * s, 1 * s, k.Skin);
            }
            p.Mat = Material.Matte;
        }
        else
        {
            p.Ellipse(cx, neck, 2.6 * s, 2.4 * s, k.Skin);                                     // the neck
            p.Mat = Material.Cloth;
            double hem = k.Hood ? 57 : top + 41 * s;                                             // robes run to the floor
            p.Poly(cloth, (cx - hw, shoulderY), (cx + hw, shoulderY), (cx + ww + 1.5 * s, waist), (cx + ww + (k.Hood ? 5 : 2.5) * s, hem), (cx - ww - (k.Hood ? 5 : 2.5) * s, hem), (cx - ww - 1.5 * s, waist));
            p.Ellipse(cx - hw + 1.5 * s, shoulderY + 1.5 * s, 3.2 * s, 2.8 * s, cloth);
            p.Ellipse(cx + hw - 1.5 * s, shoulderY + 1.5 * s, 3.2 * s, 2.8 * s, cloth);
            p.Mat = Material.Matte;
            if (!back)
            {
                // A collar or neckline, a belt and its buckle.
                if (front) p.Triangle(cx - 3 * s, shoulderY - 0.5, cx + 3 * s, shoulderY - 0.5, cx, shoulderY + 4 * s, Paint.Shade(k.Skin, -12));
                p.Rect(cx - ww - 1.5 * s, waist - 1.2 * s, cx + ww + 1.5 * s, waist + 1.4 * s, trim);
                if (front) p.Rect(cx - 1.4 * s, waist - 1.2 * s, cx + 1.4 * s, waist + 1.4 * s, C(190, 160, 80));
                if (front && k.Hood) p.Taper(cx, waist + 2 * s, 0.8 * s, cx, hem - 1, 1.2 * s, Paint.Shade(cloth, -26)); // the robe's fold
            }
            if (shamble && front)
            {
                // Rents in the rags.
                p.Poly(Paint.Shade(k.Skin, -10), (cx - 6 * s, top + 22 * s), (cx - 2 * s, top + 25 * s), (cx - 5 * s, top + 29 * s));
                p.Poly(Paint.Shade(k.Skin, -10), (cx + 3 * s, top + 33 * s), (cx + 7 * s, top + 34 * s), (cx + 5 * s, top + 39 * s));
            }
        }

        // ---- The near arm or arms ----
        if (front || back)
        {
            if (shamble && front && f == SpriteFrame.Idle)
            {
                // A shambler's arms reach for you.
                Arm(cx - hw + 1, shoulderY + 1, cx - 9 * s, top + 22 * s, cx - 5 * s, top + 24 * s);
                Arm(cx + hw - 1, shoulderY + 1, cx + 9 * s, top + 22 * s, cx + 5 * s, top + 24 * s);
            }
            else
            {
                Arm(cx - hw + 1, shoulderY + 1, cx - 12.5 * s, top + 26 * s, cx - 13 * s, top + 35 * s);
                Arm(cx + hw - 1, shoulderY + 1, ex, ey, hx, hy);
            }
        }
        else Arm(cx + 1 * s, shoulderY + 1, ex, ey, hx, hy);
        if (!(shamble && f == SpriteFrame.Idle && front)) Weapon(p, k, hx, hy, f, v);

        // ---- The head ----
        if (k.Ears && !back)
        {
            if (side) p.Taper(headX - 2 * s, headY - 1 * s, 2.4 * s, headX - 12 * s, headY - 6 * s, 0.6 * s, k.Skin);
            else
            {
                p.Taper(cx - 5 * s, headY, 2.6 * s, cx - 15 * s, headY - 5.5 * s, 0.6 * s, k.Skin);
                p.Taper(cx + 5 * s, headY, 2.6 * s, cx + 15 * s, headY - 5.5 * s, 0.6 * s, k.Skin);
            }
        }
        if (k.Hood)
        {
            p.Mat = Material.Cloth;
            p.Poly(cloth, (headX - 7.5 * s, headY + 2 * s), (headX + 7.5 * s, headY + 2 * s), (headX + 9 * s, shoulderY + 3 * s), (headX - 9 * s, shoulderY + 3 * s)); // the cowl
            p.Ellipse(headX, headY - 0.5 * s, 7.6 * s, 8.2 * s, cloth);
            p.Mat = Material.Matte;
            if (back) return;
            // The face in the hood's shadow.
            double fx = headX + (side ? 3 * s : 0);
            p.Ellipse(fx, headY + 1 * s, (side ? 3.6 : 4.8) * s, 5 * s, Paint.Shade(k.Skin, -48), false);
            uint glow = f == SpriteFrame.Windup ? C(255, 255, 220) : k.Eye;
            if (side) p.Ellipse(fx + 1.5 * s, headY + 0.5 * s, 1.1 * s, 0.9 * s, glow, false);
            else { p.Ellipse(fx - 2 * s, headY + 0.5 * s, 1.1 * s, 0.9 * s, glow, false); p.Ellipse(fx + 2 * s, headY + 0.5 * s, 1.1 * s, 0.9 * s, glow, false); }
            return;
        }

        if (k.Bones)
        {
            p.Mat = Material.Bone;
            p.Ellipse(headX, headY - 0.5 * s, 6 * s, 6.2 * s, k.Skin);
            p.Rect(headX - 3.5 * s + (side ? 2 * s : 0), headY + 3 * s, headX + 3.5 * s + (side ? 1.5 * s : 0), headY + 7 * s, k.Skin); // the jaw
            p.Mat = Material.Matte;
            if (back) return;
            uint glow = f == SpriteFrame.Windup ? C(255, 255, 210) : k.Eye;
            if (side)
            {
                p.Ellipse(headX + 3 * s, headY, 1.6 * s, 1.8 * s, socket, false);
                p.Dot(headX + 3 * s, headY, glow);
                p.Ellipse(headX + 5.5 * s, headY + 2.5 * s, 0.7 * s, 1 * s, socket, false);
            }
            else
            {
                p.Ellipse(headX - 2.6 * s, headY - 0.2 * s, 2 * s, 2.2 * s, socket, false);
                p.Ellipse(headX + 2.6 * s, headY - 0.2 * s, 2 * s, 2.2 * s, socket, false);
                p.Dot(headX - 2.6 * s, headY, glow); p.Dot(headX + 2.6 * s, headY, glow);
                p.Triangle(headX - 0.9 * s, headY + 3.2 * s, headX + 0.9 * s, headY + 3.2 * s, headX, headY + 1.6 * s, socket);
            }
            for (int i = 0; i < 4; i++) p.Rect(headX - 3 * s + i * 1.6 * s + (side ? 2 * s : 0), headY + 4.6 * s, headX - 2.2 * s + i * 1.6 * s + (side ? 2 * s : 0), headY + 6 * s, socket, false); // teeth gaps
            return;
        }

        p.Ellipse(headX, headY, 6.2 * s, 6.8 * s, k.Skin);
        if (back) { p.Ellipse(headX, headY - 1.5 * s, 6 * s, 5.5 * s, skinDark); return; } // the back of the head, in its hair
        uint eyeC = f == SpriteFrame.Windup ? C(255, 255, 220) : k.Eye;
        if (side)
        {
            p.Ellipse(headX + 5 * s, headY + 1.5 * s, 2 * s, 1.6 * s, k.Skin);                  // the nose
            p.Ellipse(headX + 2.6 * s, headY - 0.6 * s, 1.3 * s, 1 * s, socket, false);
            p.Dot(headX + 2.8 * s, headY - 0.6 * s, eyeC);
            p.Taper(headX + 1.5 * s, headY - 2.4 * s, 0.7 * s, headX + 4 * s, headY - 2 * s, 0.6 * s, skinDark); // the brow
            p.Taper(headX + 2.5 * s, headY + 3.6 * s, 0.5 * s, headX + 5 * s, headY + 3.4 * s, 0.5 * s, mouth, false);
            p.Ellipse(headX - 3 * s, headY - 2.5 * s, 4.5 * s, 4 * s, skinDark);              // the hair
            return;
        }
        // The face, from the front.
        p.Ellipse(headX - 2.5 * s, headY - 0.6 * s, 1.6 * s, 1.2 * s, socket, false);
        p.Ellipse(headX + 2.5 * s, headY - 0.6 * s, 1.6 * s, 1.2 * s, socket, false);
        p.Dot(headX - 2.5 * s, headY - 0.6 * s, eyeC); p.Dot(headX + 2.5 * s, headY - 0.6 * s, eyeC);
        p.Taper(headX - 4.2 * s, headY - 2.6 * s, 0.7 * s, headX - 1.2 * s, headY - 2 * s, 0.6 * s, skinDark); // brows, set in a scowl
        p.Taper(headX + 4.2 * s, headY - 2.6 * s, 0.7 * s, headX + 1.2 * s, headY - 2 * s, 0.6 * s, skinDark);
        p.Taper(headX, headY - 0.6 * s, 0.6 * s, headX, headY + 2 * s, 1.1 * s, Paint.Shade(k.Skin, -14)); // the nose
        if (f == SpriteFrame.Idle) p.Taper(headX - 2 * s, headY + 3.8 * s, 0.5 * s, headX + 2 * s, headY + 3.8 * s, 0.5 * s, mouth, false);
        else p.Ellipse(headX, headY + 3.9 * s, 2 * s, 1.4 * s, mouth, false);
        if (k.Ears) for (int i = -1; i <= 1; i += 2) p.Triangle(headX + i * 1.4 * s, headY + 3.2 * s, headX + i * 0.4 * s, headY + 3.2 * s, headX + i * 0.9 * s, headY + 4.6 * s, C(236, 230, 210)); // goblin fangs
    }

    private static void Weapon(Paint p, Look k, double hx, double hy, SpriteFrame f, SpriteView v)
    {
        uint steel = C(176, 180, 190), wood = C(112, 80, 48), dark = C(60, 44, 30);
        bool raised = f == SpriteFrame.Windup, swing = f == SpriteFrame.Strike;
        // Most weapons point up from the hand; in the blow they lie out towards the target.
        double ang = raised ? -0.35 : swing ? (v == SpriteView.Side ? 1.35 : 0.55) : -0.08;
        (double, double) Along(double len) => (hx + Math.Sin(ang) * len, hy - Math.Cos(ang) * len);
        switch (k.Weapon)
        {
            case "sword":
            {
                uint blade = k.Bones || k.Skin == C(200, 196, 180) ? C(150, 118, 92) : steel; // the dead carry rusted iron
                var (tx, ty) = Along(22);
                var (gx, gy) = Along(2.5);
                p.Mat = Material.Metal;
                p.Taper(gx, gy, 1.6, tx, ty, 0.5, blade);
                p.Mat = Material.Matte;
                var (px2, py2) = (Math.Cos(ang) * 4.5, Math.Sin(ang) * 4.5);
                p.Taper(gx - px2, gy - py2, 1, gx + px2, gy + py2, 1, C(150, 120, 60));         // the guard
                var (bx, by) = Along(-3);
                p.Taper(hx, hy, 1, bx, by, 1, dark);                                         // the grip
                p.Ellipse(bx, by, 1.4, 1.4, C(150, 120, 60));
                break;
            }
            case "knife":
            {
                var (tx, ty) = Along(9);
                p.Mat = Material.Metal;
                p.Taper(hx, hy, 1.3, tx, ty, 0.4, steel);
                p.Mat = Material.Matte;
                break;
            }
            case "pick":
            {
                var (tx, ty) = Along(17);
                p.Taper(hx, hy + 3, 1.1, tx, ty, 1, wood);
                p.Mat = Material.Metal;
                double cx = Math.Cos(ang), sy = Math.Sin(ang);
                p.Taper(tx - cx * 8, ty - sy * 8 + 2, 0.5, tx, ty, 1.6, C(140, 140, 148));
                p.Taper(tx, ty, 1.6, tx + cx * 9, ty + sy * 9 + 2, 0.5, C(140, 140, 148));
                p.Mat = Material.Matte;
                break;
            }
            case "axe":
            {
                var (tx, ty) = Along(19);
                p.Taper(hx, hy + 3, 1.1, tx, ty, 1, wood);
                p.Mat = Material.Metal;
                double cx = Math.Cos(ang), sy = Math.Sin(ang);
                p.Poly(steel, (tx - sy * 1, ty + 1), (tx + cx * 10, ty + sy * 10 - 5), (tx + cx * 11 + sy * 6, ty + sy * 11 + 4), (tx + sy * 5, ty + 6));
                p.Mat = Material.Matte;
                break;
            }
            case "club":
            {
                var (tx, ty) = Along(17);
                p.Taper(hx, hy + 2, 1.4, tx, ty, 3.4, wood);
                for (int i = 0; i < 3; i++) { var (nx, ny) = Along(9 + i * 3.5); p.Dot(nx + (i % 2 == 0 ? 2 : -2), ny, C(200, 200, 200)); }
                break;
            }
            case "spear":
            {
                var (bx, by) = Along(-9);
                var (tx, ty) = Along(26);
                p.Taper(bx, by, 0.9, tx, ty, 0.9, wood);
                var (hx2, hy2) = Along(32);
                p.Mat = Material.Metal;
                p.Taper(tx, ty, 2, hx2, hy2, 0.3, steel);
                p.Mat = Material.Matte;
                break;
            }
            case "staff":
            {
                var (bx, by) = Along(-11);
                var (tx, ty) = Along(22);
                p.Taper(bx, by, 1, tx, ty, 1.2, C(92, 70, 48));
                uint orb = raised ? C(255, 255, 210) : C(150, 210, 255);
                p.Ellipse(tx, ty - 2, 3.2, 3.2, Paint.Shade(orb, -60));
                p.Ellipse(tx, ty - 2, 2, 2, orb, false);
                break;
            }
            case "whip":
                if (swing) p.Taper(hx, hy, 0.9, hx + 20, hy - 5, 0.4, C(80, 56, 34));
                else { p.Taper(hx, hy, 0.9, hx + 4, hy + 10, 0.6, C(80, 56, 34)); p.Taper(hx + 4, hy + 10, 0.6, hx - 2, hy + 16, 0.4, C(80, 56, 34)); }
                break;
            case "bow":
                p.Taper(hx, hy - 13, 0.7, hx + 4, hy, 1.1, C(130, 96, 60));
                p.Taper(hx + 4, hy, 1.1, hx, hy + 13, 0.7, C(130, 96, 60));
                p.Line(hx, hy - 13, raised ? hx - 6 : hx, hy, 0.5, C(220, 214, 200));
                p.Line(raised ? hx - 6 : hx, hy, hx, hy + 13, 0.5, C(220, 214, 200));
                if (raised) p.Line(hx - 6, hy, hx + 10, hy, 0.7, C(170, 150, 120));
                break;
            case "book":
            {
                // A heavy tome held open, its pages lit.
                double open = raised ? 6 : 4;
                p.Rect(hx - open, hy - 4, hx + open, hy + 4, C(70, 40, 30));
                p.Rect(hx - open + 1, hy - 3, hx + open - 1, hy + 3, C(230, 220, 190));
                p.Taper(hx, hy - 3, 0.3, hx, hy + 3, 0.3, C(120, 90, 60), false);
                if (raised) for (int i = 0; i < 3; i++) p.Dot(hx - 3 + i * 3, hy - 7 - i % 2 * 2, k.Eye);
                break;
            }
            case "candle":
                p.Line(hx, hy, hx, hy + 3, 0.6, C(60, 50, 40));
                p.Rect(hx - 1.6, hy - 8, hx + 1.6, hy, C(230, 220, 190));
                p.Ellipse(hx, hy - 10.5, 1.6, 2.6, raised ? C(255, 250, 220) : C(255, 190, 80), false);
                break;
            case "hook":
            {
                var (tx, ty) = Along(22);
                p.Taper(hx, hy + 6, 0.9, tx, ty, 0.9, C(100, 80, 56));
                p.Mat = Material.Metal;
                p.Limb(tx, ty, tx + 6, ty - 2, tx + 6, ty + 5, 1.1, 0.5, C(170, 172, 180));
                p.Mat = Material.Matte;
                break;
            }
            case "lantern":
                p.Line(hx, hy, hx, hy + 4, 0.6, C(60, 60, 60));
                p.Mat = Material.Metal;
                p.Rect(hx - 3.5, hy + 4, hx + 3.5, hy + 12, C(70, 70, 76));
                p.Mat = Material.Matte;
                p.Rect(hx - 2.5, hy + 5.5, hx + 2.5, hy + 10.5, raised ? C(220, 255, 220) : C(120, 255, 160), false);
                break;
            case "sling":
                if (raised)
                {
                    p.Line(hx, hy, hx - 8, hy - 6, 0.6, C(90, 70, 50));
                    p.Mat = Material.Stone;
                    p.Ellipse(hx - 9, hy - 7, 2.5, 2.5, C(130, 124, 112));
                    p.Mat = Material.Matte;
                }
                else p.Line(hx, hy, hx + 1, hy + 10, 0.6, C(90, 70, 50));
                break;
        }
    }
}
