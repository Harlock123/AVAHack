using AVAHack.Content;
using AVAHack.Items;

namespace AVAHack.Render;

/// <summary>
/// Item icons (spec 16): one picture for every item base, drawn on a 32-unit grid at 64 px and finished like the
/// creatures. Weapons lie on the diagonal from the lower left, hilt first; everything else stands upright. A key
/// from <see cref="IconFor"/> names the picture: a base id, or "book:&lt;skill&gt;", "brew:&lt;effect kind&gt;",
/// "fist" and "two" (a hand taken by a two-handed weapon).
/// </summary>
public sealed partial class SpriteFactory
{
    /// <summary>The icon key for an item: its base, a book's skill or a brew's main effect.</summary>
    public static string IconFor(ContentDb db, Item item)
    {
        var b = db.Base(item.BaseId);
        if (item.BaseId == ItemBase.BrewId)
            return "brew:" + (item.Effects.Count > 0 && db.AlchemyEffects.FirstOrDefault(e => e.Id == item.Effects[0].Affix) is { } e ? e.Kind : "heal");
        if (b.Kind == ItemKind.Book && b.Book is { } book) return "book:" + book.Skill.ToString().ToLowerInvariant();
        return item.BaseId;
    }

    /// <summary>The picture for an icon key (64 px square). Unknown keys get a plain sack.</summary>
    public SpriteImage ItemIcon(string icon)
    {
        var key = ("item-" + icon, SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(64, 64);
        var p = new Paint(img, Scale);
        DrawIcon(p, icon);
        p.Finish();
        return _cache[key] = img;
    }

    /// <summary>Every icon key the game can ask for, for the icon sheet test.</summary>
    public static IEnumerable<string> IconKeys(ContentDb db)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal) { "fist", "two" };
        foreach (var b in ItemBase.All(db).Values)
        {
            if (b.Id == ItemBase.BrewId) { foreach (var e in db.AlchemyEffects) keys.Add("brew:" + e.Kind); continue; }
            keys.Add(b.Kind == ItemKind.Book && b.Book is { } bk ? "book:" + bk.Skill.ToString().ToLowerInvariant() : b.Id);
        }
        return keys;
    }

    // ---- Colours ----

    private static readonly uint Steel = 0xFFB4B8C2, Iron = 0xFF7C7E86, Bronze = 0xFFB0884A, Gold = 0xFFD8B048, Wood = 0xFF7A5232,
        DarkWood = 0xFF4E3420, Leather = 0xFF8A5A34, DarkLeather = 0xFF5A3A24, Cloth = 0xFF8C7A62, Glass = 0xFFB8D8E0, Paper = 0xFFE4D6B0,
        Bone = 0xFFE2D8BE;

    /// <summary>A point along a weapon lying on the diagonal: <paramref name="u"/> from the butt towards the tip
    /// (0–40), <paramref name="v"/> across it.</summary>
    private static (double X, double Y) D(double u, double v) => (2.5 + u * 0.7071 + v * 0.7071, 29.5 - u * 0.7071 + v * 0.7071);

    private static void Shaft(Paint p, double u0, double u1, double r0, double r1, uint c)
    {
        var (ax, ay) = D(u0, 0); var (bx, by) = D(u1, 0);
        p.Taper(ax, ay, r0, bx, by, r1, c);
    }

    private static void Blade(Paint p, double u0, double u1, double half, uint c, double curve = 0)
    {
        p.Mat = Material.Metal;
        var pts = new List<(double, double)> { D(u0, -half), D((u0 + u1) / 2, -half - curve), D(u1 - half * 1.6, -half * 0.7 - curve), D(u1, -curve * 0.5), D(u1 - half * 1.6, half * 0.7 - curve), D((u0 + u1) / 2, half - curve), D(u0, half) };
        p.Poly(c, [.. pts]);
        p.Mat = Material.Matte;
        var (fx, fy) = D(u0 + 1, 0); var (tx, ty) = D(u1 - half * 2, -curve * 0.6);
        p.Line(fx, fy, tx, ty, 0.4, Paint.Shade(c, 34)); // the fuller's gleam
    }

    private static void Guard(Paint p, double u, double half, uint c)
    {
        p.Mat = Material.Metal;
        var (ax, ay) = D(u, -half); var (bx, by) = D(u, half);
        p.Taper(ax, ay, 1.1, bx, by, 1.1, c);
        p.Mat = Material.Matte;
    }

    private static void Grip(Paint p, double u0, double u1, uint wrap, uint pommel)
    {
        Shaft(p, u0, u1, 1.1, 1.1, wrap);
        for (double u = u0 + 1; u < u1; u += 1.6) { var (x, y) = D(u, 0); p.Dot(x, y, Paint.Shade(wrap, -30)); }
        p.Mat = Material.Metal;
        var (px, py) = D(u0, 0);
        p.Ellipse(px, py, 1.7, 1.7, pommel);
        p.Mat = Material.Matte;
    }

    private static void Flask(Paint p, uint liquid, bool big = false, bool tall = false)
    {
        double r = big ? 10 : tall ? 6 : 8, cy = tall ? 21 : 21;
        p.Mat = Material.Gloss;
        if (tall) p.Poly(Glass, (10, 29), (22, 29), (20, 13), (12, 13));
        else p.Ellipse(16, cy, r, r, Glass);
        p.Mat = Material.Matte;
        if (tall) p.Poly(liquid, (10.6, 28.4), (21.4, 28.4), (20.2, 17), (11.8, 17));
        else p.Ellipse(16, cy + 1.5, r - 1.2, r - 2.4, liquid);
        p.Ellipse(16 - r * 0.45, cy - r * 0.35, 1.4, 2.2, 0xFFFFFFFF, false);                       // the glint
        p.Mat = Material.Gloss;
        p.Rect(13, tall ? 7 : cy - r - 5, 19, tall ? 13 : cy - r + 1, Glass);                        // the neck
        p.Mat = Material.Matte;
        p.Rect(12.5, tall ? 4 : cy - r - 8, 19.5, tall ? 7.5 : cy - r - 4.5, 0xFF9A6A3A);            // the cork
    }

    private static void Pile(Paint p, uint c, double w = 11, double h = 7, Material m = Material.Stone)
    {
        p.Mat = m;
        p.Ellipse(16, 27 - h / 2, w, h, c);
        p.Ellipse(11, 26, w / 2, h / 2, Paint.Shade(c, -12));
        p.Ellipse(21, 25, w / 2.2, h / 2, Paint.Shade(c, 10));
        p.Mat = Material.Matte;
    }

    private static void Crystal(Paint p, uint c, double s = 1)
    {
        p.Mat = Material.Gloss;
        p.Poly(Paint.Shade(c, -20), (16, 4 * s + 2), (23, 14), (20, 28), (12, 28), (9, 14));
        p.Poly(c, (16, 4 * s + 2), (19, 14), (16, 28), (12, 28), (9, 14));
        p.Mat = Material.Matte;
        p.Taper(13, 10, 0.5, 11, 18, 0.4, 0xFFFFFFFF, false);
    }

    private static void Bowl(Paint p, uint food, uint bits)
    {
        p.Ellipse(16, 19, 12, 4, food);
        for (int i = 0; i < 4; i++) p.Ellipse(9 + i * 4.5, 18.5 + i % 2, 1.8, 1.4, bits);
        p.Mat = Material.Stone;
        p.Poly(0xFF8E6E4E, (4, 19), (28, 19), (24, 28), (8, 28));
        p.Mat = Material.Matte;
        p.Taper(4.5, 19.5, 0.6, 27.5, 19.5, 0.6, 0xFFB08A60);
        if (food != bits) for (int i = 0; i < 3; i++) p.Taper(11 + i * 5, 14, 0.6, 12 + i * 5, 8 - i % 2 * 2, 0.4, 0xFFD8D8D0, false); // steam
    }

    private static void Mushroom(Paint p, uint cap, uint stem, bool spots = true)
    {
        p.Mat = Material.Fur;
        p.Taper(16, 28, 3.4, 16, 15, 2.6, stem);
        p.Mat = Material.Matte;
        p.Ellipse(16, 15, 11, 3, Paint.Shade(stem, -40));
        p.Poly(cap, (4, 15), (7, 8), (12, 5), (20, 5), (25, 8), (28, 15));
        if (spots) foreach (var (x, y) in new[] { (10.0, 10.0), (16.0, 7.5), (22.0, 10.0), (13.0, 12.5), (19.5, 12.5) }) p.Ellipse(x, y, 1.5, 1.1, 0xFFF0E6D0, false);
    }

    private static void Leaf(Paint p, uint c, double ang, double len, double x = 16, double y = 28)
    {
        double tx = x + Math.Sin(ang) * len, ty = y - Math.Cos(ang) * len;
        double mx = (x + tx) / 2, my = (y + ty) / 2, nx = Math.Cos(ang) * len * 0.22, ny = Math.Sin(ang) * len * 0.22;
        p.Poly(c, (x, y), (mx + nx, my + ny), (tx, ty), (mx - nx, my - ny));
        p.Taper(x, y, 0.4, tx, ty, 0.3, Paint.Shade(c, -36), false);
    }

    private static void Flower(Paint p, uint petal, uint heart, double cx = 16, double cy = 10)
    {
        p.Taper(16, 30, 0.8, cx, cy, 0.7, 0xFF4E7A3A);
        Leaf(p, 0xFF5A8A42, -0.9, 9, 16, 26); Leaf(p, 0xFF5A8A42, 0.9, 8, 16, 24);
        for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3; p.Ellipse(cx + Math.Cos(a) * 3.6, cy + Math.Sin(a) * 3.6, 2.8, 2.8, petal); }
        p.Ellipse(cx, cy, 2.2, 2.2, heart);
    }

    // ---- The pictures ----

    private static void DrawIcon(Paint p, string icon)
    {
        string kind = icon.Contains(':') ? icon[..icon.IndexOf(':')] : icon;
        string arg = icon.Contains(':') ? icon[(icon.IndexOf(':') + 1)..] : "";
        switch (kind)
        {
            // Weapons: hilt or haft at the lower left, the business end at the upper right.
            case "dagger": Grip(p, 3, 10, DarkLeather, Steel); Guard(p, 10.5, 4, Steel); Blade(p, 11, 26, 2.2, Steel); break;
            case "shortsword": Grip(p, 3, 10, DarkLeather, Bronze); Guard(p, 10.5, 4.5, Bronze); Blade(p, 11, 32, 2.2, Steel); break;
            case "sabre":
                Grip(p, 3, 10, DarkLeather, Gold);
                p.Mat = Material.Metal; { var (a, b) = D(10, 2.6); var (c, d) = D(4, 3.6); p.Taper(a, b, 0.8, c, d, 0.8, Gold); } p.Mat = Material.Matte; // the knuckle bow
                Guard(p, 10.5, 3.5, Gold); Blade(p, 11, 34, 1.9, Steel, curve: 2.6); break;
            case "longsword": Grip(p, 2, 11, 0xFF3A2A30, Steel); Guard(p, 11.5, 6, Steel); Blade(p, 12, 38, 1.9, Steel); break;
            case "bastardSword": Grip(p, 1, 12, 0xFF2A3A4A, Iron); Guard(p, 12.5, 6.5, Iron); Blade(p, 13, 38, 2.3, Steel); break;
            case "greatsword": Grip(p, 0, 11, DarkLeather, Iron); Guard(p, 11.5, 7.5, Iron); Blade(p, 12, 40, 2.8, Steel); break;
            case "club": Shaft(p, 2, 32, 1.3, 4, Wood); for (int i = 0; i < 3; i++) { var (x, y) = D(20 + i * 4, i % 2 == 0 ? 2 : -2); p.Dot(x, y, DarkWood); } break;
            case "mace":
            {
                Shaft(p, 2, 26, 1.1, 1.1, DarkWood);
                Grip(p, 2, 8, DarkLeather, Iron);
                var (x, y) = D(29, 0);
                p.Mat = Material.Metal;
                for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3; p.Taper(x, y, 2, x + Math.Cos(a) * 6.5, y + Math.Sin(a) * 6.5, 0.8, Iron); }
                p.Ellipse(x, y, 4.2, 4.2, Steel);
                p.Mat = Material.Matte;
                break;
            }
            case "warhammer":
            {
                Shaft(p, 2, 33, 1.1, 1.1, DarkWood);
                Grip(p, 2, 8, DarkLeather, Iron);
                p.Mat = Material.Metal;
                p.Poly(Steel, D(26, -7), D(32, -7), D(32, 5), D(26, 5));
                p.Poly(Iron, D(27, 5), D(31, 5), D(29.5, 10));                                         // the back spike
                p.Mat = Material.Matte;
                break;
            }
            case "flail":
            {
                Shaft(p, 2, 18, 1.2, 1.2, DarkWood);
                Grip(p, 2, 8, DarkLeather, Iron);
                for (int i = 0; i < 4; i++) { var (x, y) = D(19.5 + i * 2.4, i * 1.2); p.Mat = Material.Metal; p.Ellipse(x, y, 1.1, 0.8, Iron); }
                var (bx, by) = D(30, 6);
                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; p.Taper(bx, by, 1.4, bx + Math.Cos(a) * 6, by + Math.Sin(a) * 6, 0.4, Iron); }
                p.Ellipse(bx, by, 4, 4, Steel);
                p.Mat = Material.Matte;
                break;
            }
            case "quarterstaff":
                Shaft(p, 0, 40, 1.4, 1.4, Wood);
                p.Mat = Material.Metal; Shaft(p, 0, 3, 1.6, 1.6, Iron); Shaft(p, 37, 40, 1.6, 1.6, Iron); Shaft(p, 18, 22, 1.6, 1.6, DarkLeather); p.Mat = Material.Matte;
                break;
            case "greataxe":
            {
                Shaft(p, 0, 36, 1.2, 1.2, Wood);
                Grip(p, 0, 7, DarkLeather, Iron);
                p.Mat = Material.Metal;
                p.Poly(Steel, D(24, -1), D(22, -9), D(27, -12), D(33, -11), D(34, -1));
                p.Poly(Steel, D(24, 1), D(22, 9), D(27, 12), D(33, 11), D(34, 1));
                p.Mat = Material.Matte;
                break;
            }
            case "maul":
                Shaft(p, 0, 32, 1.3, 1.3, DarkWood);
                Grip(p, 0, 7, DarkLeather, Iron);
                p.Mat = Material.Metal; p.Poly(Iron, D(26, -6), D(37, -6), D(37, 6), D(26, 6)); p.Mat = Material.Matte;
                { var (x, y) = D(31.5, 0); p.Ellipse(x, y, 2, 2, Steel); }
                break;
            case "halberd":
                Shaft(p, 0, 40, 1.1, 1.1, Wood);
                p.Mat = Material.Metal;
                p.Poly(Steel, D(27, 1), D(26, 9), D(31, 10), D(34, 1));                                // the axe blade
                p.Poly(Iron, D(29, -1), D(31, -6), D(32, -1));                                           // the back hook
                p.Poly(Steel, D(35, -1.6), D(42, 0), D(35, 1.6));                                        // the spike
                p.Mat = Material.Matte;
                break;
            case "spear":
                Shaft(p, 0, 33, 1, 1, Wood);
                p.Mat = Material.Metal; p.Poly(Steel, D(32, 0), D(35, -3), D(42, 0), D(35, 3)); p.Mat = Material.Matte;
                Shaft(p, 31, 33, 1.4, 1.4, DarkLeather);
                break;
            case "shortbow":
            case "longbow":
            {
                double h = kind == "longbow" ? 14 : 11;
                p.Limb(13, 16 - h, 21, 16, 13, 16 + h, 1.2, 1.2, Wood);
                p.Ellipse(21, 16, 1.6, 2.6, DarkLeather);                                                // the grip
                p.Line(13, 16 - h, 13, 16 + h, 0.4, 0xFFE8E0CC);
                p.Line(7, 16, 25, 16, 0.6, 0xFFA08060);                                                 // an arrow
                p.Mat = Material.Metal; p.Triangle(25, 14.5, 29, 16, 25, 17.5, Steel); p.Mat = Material.Matte;
                p.Triangle(5, 14, 8.5, 16, 5, 18, 0xFFD8D0C0);
                break;
            }
            case "crossbow":
                p.Poly(Wood, (14, 30), (18, 30), (18.5, 8), (13.5, 8));                                  // the stock
                p.Mat = Material.Metal;
                p.Limb(3, 15, 16, 9, 29, 15, 1.1, 1.1, Iron);                                           // the steel bow
                p.Mat = Material.Matte;
                p.Line(3, 15, 16, 14, 0.4, 0xFFE8E0CC); p.Line(29, 15, 16, 14, 0.4, 0xFFE8E0CC);
                p.Line(16, 14, 16, 3, 0.7, DarkWood); p.Triangle(14.5, 4, 16, 1, 17.5, 4, Steel);         // the bolt
                p.Rect(14.5, 22, 17.5, 24, Iron);
                break;

            // Armour: chests seen from the front.
            case "padded": Chest(p, Cloth, Material.Cloth); for (int i = 0; i < 4; i++) p.Taper(9, 11 + i * 4.4, 0.35, 23, 11 + i * 4.4, 0.35, Paint.Shade(Cloth, -30), false); break;
            case "leather": Chest(p, Leather, Material.Matte); p.Taper(16, 8, 0.4, 16, 28, 0.4, DarkLeather, false); break;
            case "studded": Chest(p, Leather, Material.Matte); for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) p.Dot(11 + x * 5 + y % 2 * 2.5, 11 + y * 4.4, 0xFFD0C8B8); break;
            case "hide": Chest(p, 0xFF8E7656, Material.Fur); p.Ellipse(16, 7.5, 9, 3, 0xFFB09A7A); break;
            case "scale":
                Chest(p, Bronze, Material.Metal);
                for (int y = 0; y < 5; y++) for (int x = 0; x < 4; x++) p.Ellipse(10 + x * 4 + y % 2 * 2, 10 + y * 4, 2, 2.2, Paint.Shade(Bronze, y % 2 == 0 ? -10 : 8));
                break;
            case "chainMail": Chest(p, Iron, Material.Metal); for (int y = 0; y < 9; y++) for (int x = 0; x < 7; x++) p.Dot(9 + x * 2.2 + y % 2, 9 + y * 2.2, 0xFF50525A); break;
            case "splint": Chest(p, DarkLeather, Material.Matte); p.Mat = Material.Metal; for (int i = 0; i < 4; i++) p.Rect(9, 10 + i * 4.6, 23, 13 + i * 4.6, Iron); p.Mat = Material.Matte; break;
            case "plate":
                Chest(p, Steel, Material.Metal);
                p.Mat = Material.Metal; p.Ellipse(6, 9, 4.5, 3.5, Steel); p.Ellipse(26, 9, 4.5, 3.5, Steel); p.Mat = Material.Matte;  // pauldrons
                p.Taper(16, 8, 0.4, 16, 20, 0.4, 0xFF70747E, false); p.Taper(10, 21, 0.4, 22, 21, 0.4, 0xFF70747E, false);
                break;

            // Shields.
            case "buckler":
                p.Mat = Material.Metal; p.Ellipse(16, 16, 10, 10, Iron); p.Ellipse(16, 16, 4, 4, Steel); p.Mat = Material.Matte;
                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; p.Dot(16 + Math.Cos(a) * 7.5, 16 + Math.Sin(a) * 7.5, 0xFFD0D0D8); }
                break;
            case "roundShield":
                p.Mat = Material.Metal; p.Ellipse(16, 16, 14, 14, Iron); p.Mat = Material.Matte;
                p.Ellipse(16, 16, 12.5, 12.5, Wood);
                for (int i = -2; i <= 2; i++) p.Taper(16 + i * 5, 4.5, 0.3, 16 + i * 5, 27.5, 0.3, DarkWood, false);
                p.Mat = Material.Metal; p.Ellipse(16, 16, 4.5, 4.5, Steel); p.Mat = Material.Matte;
                break;
            case "towerShield":
                p.Mat = Material.Metal; p.Poly(Iron, (5, 2), (27, 2), (27, 24), (16, 31), (5, 24)); p.Mat = Material.Matte;
                p.Poly(0xFF7A2A2A, (7, 4), (25, 4), (25, 23), (16, 29), (7, 23));
                p.Mat = Material.Metal; p.Poly(Gold, (16, 8), (20, 14), (16, 22), (12, 14)); p.Mat = Material.Matte;
                break;

            // Gear.
            case "leatherCap": p.Ellipse(16, 18, 11, 10, Leather); p.Rect(4.5, 18, 27.5, 22, DarkLeather); p.Taper(16, 8, 0.4, 16, 18, 0.4, DarkLeather, false); break;
            case "ironHelm":
                p.Mat = Material.Metal; p.Ellipse(16, 15, 11, 11, Iron); p.Rect(5, 15, 27, 26, Iron); p.Mat = Material.Matte;
                p.Rect(7, 15, 25, 17.5, Socket, false); p.Rect(14.8, 17.5, 17.2, 25, Socket, false);       // eye slit and nose guard gap
                break;
            case "gloves": Glove(p, Leather, Material.Matte); break;
            case "gauntlets": Glove(p, Steel, Material.Metal); break;
            case "boots": Boot(p, Leather, Material.Matte); break;
            case "ironBoots": Boot(p, Iron, Material.Metal); break;
            case "leggings": Legs(p, Cloth, Material.Cloth); break;
            case "greaves": Legs(p, Steel, Material.Metal); break;
            case "cloak":
                p.Mat = Material.Cloth; p.Poly(0xFF3A5A4A, (12, 3), (20, 3), (27, 30), (5, 30)); p.Mat = Material.Matte;
                p.Poly(0xFF2A4236, false, (14, 6), (18, 6), (19, 30), (13, 30));
                p.Mat = Material.Metal; p.Ellipse(16, 4.5, 2.4, 2.4, Gold); p.Mat = Material.Matte;
                break;
            case "belt":
                p.Taper(2, 18, 2.6, 30, 14, 2.6, Leather);
                p.Mat = Material.Metal; p.Rect(12, 12.5, 19, 19.5, Gold); p.Mat = Material.Matte; p.Rect(13.6, 14, 17.4, 18, Leather, false);
                break;
            case "amulet":
                p.Limb(7, 3, 16, 17, 25, 3, 0.5, 0.5, Gold);
                p.Mat = Material.Metal; p.Ellipse(16, 21, 6, 6.5, Gold); p.Mat = Material.Gloss; p.Ellipse(16, 21, 3.6, 4, 0xFF3070C0); p.Mat = Material.Matte;
                break;
            case "ring":
                p.Mat = Material.Metal; p.Ellipse(16, 19, 9, 7.5, Gold); p.Mat = Material.Matte; p.Clear(16, 20, 6, 4.6);
                p.Mat = Material.Gloss; p.Ellipse(16, 11, 3.5, 3.5, 0xFFC02040); p.Mat = Material.Matte;
                break;

            // Consumables.
            case "potionHealing": Flask(p, 0xFFC02424); break;
            case "potionGreaterHealing": Flask(p, 0xFFE02040, big: true); break;
            case "potionStamina": Flask(p, 0xFF40B040); break;
            case "potionMana": Flask(p, 0xFF2C5CD0); break;
            case "antidote": Flask(p, 0xFFB8D040, tall: true); break;
            case "elixirCleansing": Flask(p, 0xFFF0E8C0, tall: true); p.Ellipse(16, 22, 2, 2, 0xFFFFFFFF, false); break;
            case "brew":
            {
                uint c = arg switch
                {
                    "heal" => 0xFFC83030, "stamina" => 0xFF48B048, "mana" => 0xFF3060D0, "cure" => 0xFFD8D050, "buff" => 0xFF9050C0,
                    "coat" => 0xFF2E5A2A, "burn" => 0xFFE07020, "drain" => 0xFF707078, "food" => 0xFF9A7040, "water" => 0xFF60A0D0, _ => 0xFF9050C0,
                };
                // A rough clay-stoppered bottle: home-made.
                p.Mat = Material.Gloss; p.Poly(Glass, (9, 29), (23, 29), (24, 18), (19, 12), (13, 12), (8, 18)); p.Mat = Material.Matte;
                p.Poly(c, (9.6, 28.4), (22.4, 28.4), (23.2, 19), (8.8, 19));
                p.Ellipse(11.5, 21, 1, 2, 0xFFFFFFFF, false);
                p.Rect(13, 6, 19, 12, 0xFFA07850);
                p.Taper(12, 9, 0.4, 20, 9, 0.4, 0xFF5A4030);
                break;
            }
            case "bandage":
                p.Mat = Material.Cloth; p.Ellipse(14, 18, 9, 9, 0xFFE8E2D4); p.Mat = Material.Matte;
                p.Ellipse(14, 18, 3, 3, 0xFFB8B0A0); p.Ellipse(14, 18, 1.3, 1.3, 0xFF6A6050, false);
                p.Poly(0xFFE8E2D4, (21, 20), (30, 24), (29, 28), (19, 25));
                p.Rect(23, 22.4, 25, 26, 0xFFB03030);
                break;
            case "scrollIdentify":
            case "scroll":
                p.Mat = Material.Cloth; p.Rect(7, 6, 25, 26, Paper); p.Mat = Material.Matte;
                p.Taper(6, 6, 2.2, 26, 6, 2.2, Paint.Shade(Paper, -18)); p.Taper(6, 26, 2.2, 26, 26, 2.2, Paint.Shade(Paper, -18));
                for (int i = 0; i < 4; i++) p.Taper(10, 11 + i * 3.4, 0.35, 22 - i % 2 * 4, 11 + i * 3.4, 0.35, 0xFF6A5A48, false);
                p.Ellipse(19, 26, 3, 3, kind == "scroll" ? 0xFF8030A0 : 0xFFB02020);                        // the seal
                break;
            case "torch":
                p.Taper(10, 30, 1.2, 18, 12, 1.6, Wood);
                p.Taper(17, 13, 2.4, 19, 9, 2.4, 0xFF5A4030);
                p.Poly(0xFFE07020, false, (14, 10), (17, 2), (19, 6), (22, 1), (23, 9), (20, 12));
                p.Ellipse(19, 8, 2.2, 3, 0xFFFFE080, false);
                break;
            case "lampOil":
                p.Mat = Material.Stone; p.Ellipse(16, 21, 10, 9, 0xFF8A6A4A); p.Rect(13, 6, 19, 13, 0xFF8A6A4A); p.Mat = Material.Matte;
                p.Limb(19, 9, 26, 12, 24, 20, 1, 1, 0xFF6A4E36);                                            // the handle
                p.Rect(12.5, 3, 19.5, 6.5, 0xFF4A3424);
                p.Ellipse(16, 22, 4, 3, 0xFFE0C060, false);                                                // an oil-drop mark
                break;
            case "ration":
                p.Mat = Material.Cloth; p.Rect(4, 14, 28, 27, 0xFFB09068); p.Mat = Material.Matte;
                p.Taper(4, 20, 0.6, 28, 20, 0.6, 0xFF6A5030);                                              // the string
                p.Ellipse(11, 12, 6, 4, 0xFFC08848);                                                       // a loaf on top
                p.Ellipse(22, 12.5, 5, 3, 0xFF8A4A34);                                                     // dried meat
                break;
            case "waterskin":
                p.Mat = Material.Matte;
                p.Poly(Leather, (16, 6), (24, 14), (26, 24), (20, 30), (10, 30), (6, 24), (8, 14));
                p.Rect(13.5, 2, 18.5, 7, DarkLeather);
                p.Taper(8, 14, 0.5, 4, 6, 0.5, DarkLeather); p.Taper(4, 6, 0.5, 13.5, 3, 0.5, DarkLeather);   // the strap
                p.Ellipse(12, 20, 1.4, 3, 0xFFC8A070, false);
                break;

            // Books and magic.
            case "book":
            {
                uint cover = arg switch
                {
                    "blade" or "blunt" or "twohanded" or "archery" or "unarmed" or "block" or "athletics" or "heavyarmour" or "lightarmour" => 0xFF8A2A22,
                    "destruction" => 0xFFB04020, "restoration" => 0xFFD0B040, "warding" => 0xFF3A6AB0, "conjuration" => 0xFF5A3A8A, "illusion" => 0xFF9A40A0,
                    "enchanting" => 0xFF2A7A8A, _ => 0xFF3A6A34,
                };
                Book(p, cover, Gold);
                uint mark = Gold;
                switch (arg)
                {
                    case "blade" or "twohanded": p.Taper(13, 22, 0.6, 21, 10, 0.4, mark); p.Taper(12, 17, 0.5, 18, 21, 0.5, mark); break;
                    case "archery": p.Limb(14, 9, 19, 16, 14, 23, 0.5, 0.5, mark); p.Line(12, 16, 22, 16, 0.4, mark); break;
                    case "destruction": p.Poly(mark, false, (16, 9), (19, 15), (17, 15), (19, 22), (13, 14), (15.5, 14)); break;
                    case "restoration": p.Rect(15, 9, 17, 22, mark, false); p.Rect(11, 13.5, 21, 15.5, mark, false); break;
                    case "warding": p.Poly(mark, false, (11, 10), (21, 10), (21, 16), (16, 22), (11, 16)); break;
                    default: p.Ellipse(16, 15.5, 4, 4, mark, false); p.Ellipse(16, 15.5, 2.2, 2.2, Paint.Shade(cover, -30), false); break;
                }
                break;
            }
            case "tome":
                Book(p, 0xFF3A2A4A, Iron, thick: true);
                p.Ellipse(16, 15.5, 4.5, 4.5, 0xFF7050C0, false); p.Ellipse(16, 15.5, 2.4, 2.4, 0xFFD0C0FF, false);
                for (int i = 0; i < 4; i++) { double a = i * Math.PI / 2 + 0.78; p.Dot(16 + Math.Cos(a) * 6.5, 15.5 + Math.Sin(a) * 6.5, 0xFFB0A0F0); }
                break;
            case "wand":
                p.Taper(6, 28, 1, 22, 9, 1.2, DarkWood);
                p.Mat = Material.Metal; p.Taper(20, 11, 1.8, 22.5, 8, 1.8, Gold); p.Mat = Material.Gloss;
                p.Ellipse(24.5, 6, 3, 3, 0xFF60C0F0); p.Mat = Material.Matte;
                for (int i = 0; i < 4; i++) p.Dot(24.5 + (i - 1.5) * 3.4, 6 - (i % 2 == 0 ? 4 : -4), 0xFFC0F0FF);
                break;
            case "gold":
                p.Mat = Material.Metal;
                for (int i = 0; i < 6; i++) p.Ellipse(9 + i % 3 * 7, 26 - i / 3 * 4 - i % 2, 5.5, 2.4, Paint.Shade(Gold, i * 4 - 10));
                p.Ellipse(16, 15, 5.5, 2.4, Gold); p.Ellipse(16, 12, 5.5, 2.4, Paint.Shade(Gold, 12));
                p.Mat = Material.Matte;
                p.Ellipse(16, 12, 2.2, 0.9, Paint.Shade(Gold, -50), false);
                break;
            case "key":
                p.Mat = Material.Metal;
                p.Ellipse(9, 10, 5.5, 5.5, Iron); p.Taper(13, 14, 1.3, 27, 28, 1.3, Iron);
                p.Rect(21, 23, 24, 27, Iron); p.Rect(24.5, 26.5, 27.5, 30.5, Iron);
                p.Mat = Material.Matte;
                p.Clear(9, 10, 2.6, 2.6);
                break;
            case "spiritShard": Crystal(p, 0xFF90C8F0); p.Ellipse(16, 18, 3, 5, 0xFFE0F4FF, false); break;
            case "scrapIron":
                p.Mat = Material.Metal;
                p.Poly(Iron, (3, 28), (8, 18), (14, 21), (12, 28)); p.Poly(0xFF6A6460, (11, 28), (16, 14), (23, 17), (21, 28));
                p.Poly(0xFF8A5A3E, (19, 28), (24, 20), (29, 23), (28, 28)); p.Taper(6, 20, 0.6, 18, 10, 0.6, Steel);
                p.Mat = Material.Matte;
                break;
            case "mortar":
                p.Mat = Material.Stone; p.Poly(0xFF9A948A, (5, 16), (27, 16), (23, 28), (9, 28)); p.Ellipse(16, 16, 11, 3.2, 0xFFB0AA9E); p.Mat = Material.Matte;
                p.Ellipse(16, 16, 8.5, 2, 0xFF5A5650, false);
                p.Mat = Material.Stone; p.Taper(14, 15, 1.8, 24, 3, 2.4, 0xFFC0BAAE); p.Mat = Material.Matte;          // the pestle
                break;
            case "campfireKit":
                p.Mat = Material.Metal; p.Ellipse(16, 12, 7, 5, 0xFF4A4A50); p.Mat = Material.Matte; p.Ellipse(16, 9, 6, 1.6, 0xFF2A2A2E, false); // the pot
                p.Taper(4, 28, 1.6, 26, 22, 1.6, Wood); p.Taper(6, 22, 1.6, 28, 28, 1.6, DarkWood);       // kindling
                p.Mat = Material.Stone; p.Ellipse(24, 20, 2.6, 2, 0xFF505058); p.Mat = Material.Matte;       // the flint
                break;
            case "fist":
                p.Ellipse(16, 17, 9, 8, 0xFFD4A47E);
                for (int i = 0; i < 4; i++) p.Ellipse(9.5 + i * 4.4, 11, 2.4, 3, 0xFFC89874);
                p.Ellipse(8, 19, 3, 5, 0xFFC89874);                                                        // the thumb
                p.Rect(10, 25, 22, 31, 0xFF6A5A4A);                                                         // the cuff
                break;
            case "two":
                p.Taper(6, 26, 0.8, 26, 6, 0.8, 0xFF6A645C); p.Taper(6, 6, 0.8, 26, 26, 0.8, 0xFF6A645C);
                break;

            // Ingredients.
            case "ratHaunch": case "wargSteak": case "beetleMeat": case "coilFillet": case "spiderLeg":
                Meat(p, kind);
                break;
            case "puffcapFlesh": for (int i = 0; i < 3; i++) p.Ellipse(9 + i * 7, 22 - i % 2 * 4, 5, 3.6, 0xFFE0C8B0); p.Ellipse(16, 14, 4, 2.4, 0xFFC04030); break;
            case "caveOnion": p.Ellipse(16, 20, 9, 8.5, 0xFFC8B0D0); p.Taper(16, 12, 1.6, 16, 3, 0.6, 0xFF6A9A4A); p.Taper(16, 20, 0.4, 16, 28, 0.4, 0xFF8A7090, false); p.Taper(12, 14, 0.3, 11, 26, 0.3, 0xFFA890B0, false); break;
            case "flour":
                p.Mat = Material.Cloth; p.Poly(0xFFD8CCAE, (8, 30), (24, 30), (26, 14), (21, 8), (11, 8), (6, 14)); p.Mat = Material.Matte;
                p.Taper(10, 9, 0.8, 22, 9, 0.8, 0xFF8A7A5A); p.Ellipse(16, 6, 4, 2, 0xFFF4F0E8);
                break;
            case "salt": Pile(p, 0xFFF0F0F4, 10, 6); p.Ellipse(16, 16, 2.6, 1.4, 0xFFFFFFFF, false); break;
            case "teaLeaves": for (int i = 0; i < 5; i++) Leaf(p, i % 2 == 0 ? 0xFF4A7A3A : 0xFF5E8A40, -1.2 + i * 0.6, 12 - i % 2 * 2, 16, 27); break;

            // Reagents.
            case "gravecap": Mushroom(p, 0xFF5A5A6A, 0xFFB8B0A0); break;
            case "weepcap": Mushroom(p, 0xFF4A6A8A, 0xFFC0C8D0, spots: false); for (int i = 0; i < 3; i++) p.Ellipse(9 + i * 7, 18 + i % 2 * 2, 1, 1.6, 0xFF90C0F0, false); break;
            case "sporePuff": p.Mat = Material.Fur; p.Ellipse(16, 18, 10, 10, 0xFFC8B890); p.Mat = Material.Matte; for (int i = 0; i < 5; i++) p.Ellipse(8 + i * 4, 8 - i % 2 * 3, 1.4, 1.4, 0xFFE0D8B0, false); break;
            case "deepTruffle": Pile(p, 0xFF4A3A30, 9, 8, Material.Stone); p.Dot(13, 21, 0xFF8A7060); p.Dot(19, 23, 0xFF8A7060); break;
            case "ashroot": case "mireRoot":
            {
                uint c = kind == "ashroot" ? 0xFF8A8680 : 0xFF6A5A3A;
                p.Taper(16, 4, 2.6, 16, 18, 2, c);
                for (int i = -2; i <= 2; i++) p.Limb(16, 17, 16 + i * 3, 23, 16 + i * 5, 30, 1.4, 0.4, c);
                Leaf(p, 0xFF5A7A3A, -0.5, 6, 16, 5); Leaf(p, 0xFF5A7A3A, 0.6, 6, 16, 5);
                break;
            }
            case "bellwort": Flower(p, 0xFF7090D0, 0xFFF0E080); break;
            case "knellflower": Flower(p, 0xFF3A3A5A, 0xFFC0C0E0); break;
            case "ghostLily": Flower(p, 0xFFE8F0F4, 0xFFA0E0C0); break;
            case "thornApple": p.Ellipse(16, 19, 9, 9, 0xFF8AA040); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; p.Taper(16 + Math.Cos(a) * 8, 19 + Math.Sin(a) * 8, 0.8, 16 + Math.Cos(a) * 11.5, 19 + Math.Sin(a) * 11.5, 0.2, 0xFF5A6A30); } break;
            case "blindFern": for (int i = 0; i < 5; i++) Leaf(p, 0xFFB0B8A0, -1 + i * 0.5, 14, 16, 30); break;
            case "cinderleaf": Leaf(p, 0xFFC05020, -0.3, 22, 13, 30); p.Ellipse(19, 14, 1.6, 1.6, 0xFFFFC060, false); break;
            case "hollowReed": for (int i = 0; i < 3; i++) { p.Taper(10 + i * 6, 30, 1.4, 12 + i * 5, 3 + i * 2, 1.2, 0xFFA8A060); p.Dot(12 + i * 5, 3 + i * 2, 0xFF3A3020); } break;
            case "copperBark": p.Mat = Material.Stone; p.Poly(0xFFB0683A, (6, 26), (8, 8), (24, 4), (26, 22)); p.Mat = Material.Matte; for (int i = 0; i < 4; i++) p.Taper(9 + i * 4, 9, 0.4, 10 + i * 4, 24, 0.4, 0xFF6A3A20, false); break;
            case "glowmoss": case "tunnelLichen": case "choirBellMoss":
            {
                uint c = kind switch { "glowmoss" => 0xFF60C070, "tunnelLichen" => 0xFF9AA060, _ => 0xFF7090A0 };
                p.Mat = Material.Fur; p.Ellipse(16, 22, 12, 7, c); p.Ellipse(10, 18, 5, 4, Paint.Shade(c, 12)); p.Ellipse(22, 18, 5, 4, Paint.Shade(c, -8)); p.Mat = Material.Matte;
                if (kind == "glowmoss") for (int i = 0; i < 5; i++) p.Dot(7 + i * 4.5, 16 + i % 2 * 5, 0xFFC0FFC0);
                if (kind == "choirBellMoss") for (int i = 0; i < 3; i++) p.Ellipse(10 + i * 6, 13, 2, 2.4, 0xFFC0D0F0);
                break;
            }
            case "drownedKelp": for (int i = 0; i < 3; i++) p.Limb(10 + i * 6, 30, 7 + i * 7, 18, 12 + i * 6, 4, 2, 0.8, 0xFF3A6A4A); break;
            case "cavePearl": p.Mat = Material.Gloss; p.Ellipse(16, 18, 7.5, 7.5, 0xFFE8E4F0); p.Mat = Material.Matte; break;
            case "frostSalt": Crystal(p, 0xFFC0E0F8, 0.6); break;
            case "saltpetre": Pile(p, 0xFFE0DCC8, 11, 6); for (int i = 0; i < 4; i++) p.Dot(10 + i * 4, 21 + i % 2, 0xFFFFFFFF); break;
            case "redClay": p.Mat = Material.Stone; p.Ellipse(16, 21, 11, 8, 0xFFA84A34); p.Mat = Material.Matte; p.Taper(10, 18, 0.5, 20, 16, 0.5, 0xFF7A3020, false); break;
            case "ironBloom": p.Mat = Material.Stone; p.Poly(0xFF5A5450, (5, 27), (8, 13), (18, 7), (27, 15), (26, 27)); p.Mat = Material.Metal; for (int i = 0; i < 4; i++) p.Ellipse(10 + i * 4.5, 18 - i % 2 * 4, 2, 1.6, Steel); p.Mat = Material.Matte; break;
            case "blackAmber": p.Mat = Material.Gloss; p.Poly(0xFF3A2010, (16, 5), (25, 13), (22, 27), (10, 27), (7, 13)); p.Mat = Material.Matte; p.Ellipse(14, 17, 2.4, 1.6, 0xFFD08030, false); break;
            case "brassFilings": Pile(p, Bronze, 11, 5, Material.Metal); break;
            case "boneDust": Pile(p, Bone, 11, 6); break;
            case "unmadeAsh": Pile(p, 0xFF3A3440, 11, 6); for (int i = 0; i < 3; i++) p.Dot(11 + i * 5, 22, 0xFFB090F0); break;
            case "pitchResin": p.Mat = Material.Gloss; p.Ellipse(16, 22, 9, 7, 0xFF2A1E14); p.Taper(16, 16, 3, 18, 6, 1, 0xFF2A1E14); p.Mat = Material.Matte; break;
            case "ratTallow": p.Mat = Material.Gloss; p.Poly(0xFFE8DCB8, (6, 26), (8, 14), (24, 12), (26, 24)); p.Mat = Material.Matte; break;
            case "silkGland": case "grubJelly": case "slickResidue":
            {
                uint c = kind switch { "silkGland" => 0xFFE0C0C8, "grubJelly" => 0xFFC8D060, _ => 0xFF8A9A90 };
                p.Mat = Material.Gloss; p.Ellipse(16, 20, 10, 8.5, c); p.Mat = Material.Matte;
                p.Ellipse(12, 17, 2.6, 1.8, Paint.Shade(c, 40), false);
                if (kind == "silkGland") p.Taper(16, 12, 1, 18, 4, 0.5, c);
                break;
            }
            case "wispEmber": p.Ellipse(16, 18, 8, 8, 0xFFE06020, false); p.Ellipse(16, 18, 5, 5, 0xFFFFB040, false); p.Ellipse(16, 18, 2.4, 2.4, 0xFFFFF4C0, false); break;
            case "impHorn": p.Mat = Material.Bone; p.Limb(8, 28, 12, 14, 24, 4, 3.4, 0.5, 0xFF5A3A30); p.Mat = Material.Matte; break;
            case "coilScale": p.Mat = Material.Gloss; p.Poly(0xFF3A7A6A, (16, 4), (26, 14), (16, 30), (6, 14)); p.Mat = Material.Matte; p.Taper(16, 6, 0.4, 16, 27, 0.4, 0xFF2A5A4A, false); break;
            case "mothWing": p.Poly(0xFFA08A60, (4, 28), (6, 8), (16, 3), (28, 10), (20, 26)); p.Ellipse(15, 13, 4, 4, 0xFF1A1014, false); p.Ellipse(15, 13, 2.4, 2.4, 0xFF60A0F0, false); break;
            case "eyeOfNewtMoth": p.Mat = Material.Gloss; p.Ellipse(16, 17, 9, 8, 0xFFF0ECE0); p.Mat = Material.Matte; p.Ellipse(16, 17, 4.5, 4.5, 0xFFB08020, false); p.Ellipse(16, 17, 2, 2, 0xFF100808, false); for (int i = 0; i < 3; i++) p.Taper(16, 25, 0.6, 12 + i * 4, 30, 0.3, 0xFFC04040); break;
            case "wargFang": p.Mat = Material.Bone; p.Limb(10, 4, 13, 18, 20, 29, 3.4, 0.4, Bone); p.Mat = Material.Matte; break;
            case "spiderSilk": p.Mat = Material.Cloth; p.Ellipse(16, 17, 9, 11, 0xFFE8E8EC); p.Mat = Material.Matte; for (int i = 0; i < 4; i++) p.Taper(8, 10 + i * 4, 0.3, 24, 12 + i * 4, 0.3, 0xFFB0B0B8, false); break;
            case "lanternGall": p.Mat = Material.Gloss; p.Ellipse(16, 19, 9, 9, 0xFF6A8A30); p.Mat = Material.Matte; p.Ellipse(16, 20, 5, 5, 0xFFC0FF80, false); p.Taper(16, 10, 0.8, 18, 3, 0.5, 0xFF4A5A2A); break;

            // Meals.
            case "meal":
                switch (arg)
                {
                    case "ratSkewer": p.Taper(3, 29, 0.6, 29, 3, 0.6, 0xFFA08060); for (int i = 0; i < 3; i++) p.Ellipse(9 + i * 6.5, 23 - i * 6.5, 4, 3.4, 0xFF8A4A2A); break;
                    case "spiderStew": Bowl(p, 0xFF8A5A3A, 0xFF5A3A2A); break;
                    case "mushroomBroth": Bowl(p, 0xFFC0A070, 0xFFE0D0C0); break;
                    case "lanternTea":
                        p.Mat = Material.Stone; p.Poly(0xFFE8E0D0, (7, 12), (25, 12), (22, 27), (10, 27)); p.Mat = Material.Matte;
                        p.Ellipse(16, 12, 9, 2.4, 0xFF9AB040); p.Limb(24, 15, 29, 17, 23, 23, 1, 1, 0xFFE8E0D0);
                        for (int i = 0; i < 2; i++) p.Taper(13 + i * 6, 9, 0.6, 14 + i * 6, 3, 0.4, 0xFFD8D8D0, false);
                        break;
                    case "delversPie":
                        p.Ellipse(16, 22, 13, 6, 0xFFC89048); p.Ellipse(16, 19, 12, 5, 0xFFD8A858);
                        for (int i = 0; i < 3; i++) p.Taper(10 + i * 6, 17, 0.4, 12 + i * 6, 21, 0.4, 0xFF8A5A2A, false);
                        break;
                    default:                                                                                     // a roast or a grill on a plate
                        p.Ellipse(16, 24, 14, 5, 0xFFD8D4CC); p.Ellipse(16, 23.5, 11, 3.6, 0xFFE8E4DC);
                        p.Ellipse(16, 18, 9, 5.5, arg == "coilGrill" ? 0xFF8A9A70 : arg == "beetleBake" ? 0xFF6A5A30 : 0xFF8A4628);
                        for (int i = 0; i < 3; i++) p.Taper(11 + i * 5, 15, 0.4, 12 + i * 5, 21, 0.4, 0xFF4A2A18, false);
                        break;
                }
                break;

            default:
                // A plain sack, for anything without its own picture.
                p.Mat = Material.Cloth; p.Poly(0xFF9A8060, (8, 30), (24, 30), (26, 16), (20, 9), (12, 9), (6, 16)); p.Mat = Material.Matte;
                p.Taper(11, 10, 0.8, 21, 10, 0.8, 0xFF5A4630); p.Ellipse(16, 6.5, 3.5, 2.4, 0xFF9A8060);
                break;
        }
    }

    private static void Chest(Paint p, uint c, Material m)
    {
        p.Mat = m;
        p.Poly(c, (5, 7), (11, 4), (16, 7), (21, 4), (27, 7), (26, 14), (23, 15), (23, 29), (9, 29), (9, 15), (6, 14));
        p.Mat = Material.Matte;
        p.Poly(Paint.Shade(c, -46), false, (12, 4.5), (16, 8), (20, 4.5), (16, 6));                           // the neckline
    }

    private static void Glove(Paint p, uint c, Material m)
    {
        p.Mat = m;
        p.Rect(9, 20, 23, 30, Paint.Shade(c, -14));                                                            // the cuff
        p.Ellipse(16, 16, 7.5, 6.5, c);
        for (int i = 0; i < 4; i++) p.Taper(10.5 + i * 3.6, 12, 1.6, 10 + i * 4, 3 + Math.Abs(i - 1.5), 1.4, c);
        p.Taper(9, 17, 1.8, 4, 12, 1.4, c);                                                                    // the thumb
        p.Mat = Material.Matte;
    }

    private static void Boot(Paint p, uint c, Material m)
    {
        p.Mat = m;
        p.Poly(c, (8, 3), (18, 3), (18, 20), (27, 23), (28, 29), (6, 29), (7, 20));
        p.Mat = Material.Matte;
        p.Rect(6, 27.5, 28, 30, Paint.Shade(c, -50));                                                           // the sole
        p.Rect(7.5, 3, 18.5, 6, Paint.Shade(c, -20));
    }

    private static void Legs(Paint p, uint c, Material m)
    {
        p.Mat = m;
        p.Poly(c, (7, 3), (25, 3), (26, 29), (18.5, 29), (16, 11), (13.5, 29), (6, 29));
        p.Mat = Material.Matte;
        p.Rect(7, 3, 25, 6, Paint.Shade(c, -30));
        if (m == Material.Metal) { p.Ellipse(10, 17, 2.6, 2.2, Paint.Shade(c, 20)); p.Ellipse(22, 17, 2.6, 2.2, Paint.Shade(c, 20)); }
    }

    private static void Book(Paint p, uint cover, uint trim, bool thick = false)
    {
        p.Rect(7, 4, 26, 28, Paint.Shade(cover, -36));                                                         // the back cover
        p.Rect(9, 5.5, 26, 27.5, Paper);                                                                       // the pages' edge
        p.Rect(5, 3, 23, 27, cover);
        p.Mat = thick ? Material.Metal : Material.Matte;
        p.Rect(5, 3, 7.5, 27, Paint.Shade(cover, -24));                                                        // the spine
        if (thick) { p.Rect(21, 11, 26, 14, trim); p.Rect(21, 18, 26, 21, trim); }                             // clasps
        p.Mat = Material.Matte;
        p.Taper(9, 5.5, 0.3, 21, 5.5, 0.3, trim, false); p.Taper(9, 24.5, 0.3, 21, 24.5, 0.3, trim, false);
    }

    private static void Meat(Paint p, string kind)
    {
        switch (kind)
        {
            case "ratHaunch":
                p.Mat = Material.Bone; p.Taper(8, 26, 1.4, 16, 18, 1.4, Bone); p.Ellipse(7, 27, 2.4, 2.4, Bone); p.Mat = Material.Matte;
                p.Ellipse(20, 13, 8.5, 7.5, 0xFFA05838); p.Ellipse(18, 11, 4, 3, 0xFFC07858);
                break;
            case "wargSteak":
                p.Poly(0xFF9A3A34, (4, 18), (8, 9), (20, 6), (28, 12), (26, 24), (12, 27));
                p.Taper(8, 14, 0.8, 24, 18, 0.8, 0xFFE8D0C0, false); p.Ellipse(23, 12, 2.2, 2, Bone, false);
                break;
            case "beetleMeat":
                p.Mat = Material.Gloss; p.Ellipse(16, 18, 11, 9, 0xFF2E4A3A); p.Mat = Material.Matte;
                p.Ellipse(16, 21, 8, 5, 0xFFD0B890);
                break;
            case "coilFillet":
                p.Poly(0xFFE09A80, (3, 20), (10, 12), (24, 11), (29, 16), (24, 22), (10, 24));
                for (int i = 0; i < 4; i++) p.Taper(10 + i * 4, 13, 0.4, 9 + i * 4, 22, 0.4, 0xFFF0C0A8, false);
                break;
            default: // spiderLeg
                p.Limb(4, 28, 14, 10, 28, 6, 2.4, 1, 0xFF4A3A30);
                p.Ellipse(14, 10, 2.6, 2.6, 0xFF5A4A3A);
                p.Ellipse(4, 28, 3, 3, 0xFFD0B090);
                break;
        }
    }
}
