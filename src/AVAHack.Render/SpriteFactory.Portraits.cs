namespace AVAHack.Render;

/// <summary>
/// Hero portraits for the party cards: head and shoulders on a 64-unit grid at 128 px, finished like the
/// creatures. The ancestry sets the face (skin, ears, a dwarf's beard, a saurian's snout, an emberkin's horns),
/// the calling the clothes and headgear, and a hash of the hero's name the hair, its colour, a beard and the eyes,
/// so the same hero always looks the same. A downed hero is drawn grey with closed eyes.
/// </summary>
public sealed partial class SpriteFactory
{
    public SpriteImage Portrait(string ancestry, string calling, string name, bool down = false)
    {
        var key = ($"portrait-{ancestry}-{calling}-{name}-{down}", SpriteView.Front, SpriteFrame.Idle, false);
        if (_cache.TryGetValue(key, out var img)) return img;
        img = new SpriteImage(Size, Size);
        var p = new Paint(img, Scale);
        DrawPortrait(p, ancestry, calling, NameHash(name), down);
        p.Finish();
        if (down)
            for (int i = 0; i < img.Pixels.Length; i++)
            {
                uint c = img.Pixels[i];
                int g = (int)(((c >> 16 & 0xFF) * 3 + (c >> 8 & 0xFF) * 4 + (c & 0xFF)) / 8 * 0.7);
                img.Pixels[i] = 0xFF000000u | (uint)g << 16 | (uint)g << 8 | (uint)(g + 6);
            }
        return _cache[key] = img;
    }

    /// <summary>FNV-1a over the name: a stable seed for the parts a hero's ancestry and calling don't fix.</summary>
    private static uint NameHash(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return h;
    }

    private static void DrawPortrait(Paint p, string ancestry, string calling, uint seed, bool down)
    {
        int Pick(int n, int shift) => (int)((seed >> shift) % (uint)n);

        // ---- The face's makings, by ancestry ----
        uint[] skins = ancestry switch
        {
            "dwarf" => [C(212, 156, 118), C(190, 130, 96), C(226, 176, 140)],
            "elf" => [C(236, 214, 188), C(214, 190, 160), C(200, 214, 220)],
            "halfling" => [C(226, 176, 136), C(204, 150, 112), C(238, 196, 160)],
            "gnome" => [C(226, 176, 150), C(210, 160, 140), C(236, 196, 170)],
            "orcBlooded" => [C(132, 158, 98), C(110, 140, 86), C(150, 150, 104)],
            "saurian" => [C(92, 140, 108), C(120, 140, 80), C(80, 120, 140)],
            "emberkin" => [C(198, 92, 66), C(176, 70, 60), C(210, 110, 70)],
            _ => [C(214, 168, 132), C(176, 124, 92), C(120, 84, 60), C(236, 196, 166)],
        };
        uint skin = skins[Pick(skins.Length, 0)];
        uint[] hairs = ancestry switch
        {
            "elf" => [C(232, 220, 170), C(200, 200, 214), C(60, 44, 36), C(150, 60, 40)],
            "emberkin" => [C(30, 22, 26), C(70, 20, 20), C(230, 200, 180)],
            "gnome" => [C(230, 120, 60), C(210, 210, 220), C(120, 150, 210), C(80, 50, 30)],
            _ => [C(58, 40, 28), C(120, 72, 38), C(170, 80, 40), C(214, 180, 110), C(30, 26, 26), C(168, 164, 160)],
        };
        uint hair = hairs[Pick(hairs.Length, 4)];
        uint[] eyes = [C(70, 120, 180), C(90, 130, 70), C(110, 70, 40), C(80, 80, 90), C(160, 120, 40)];
        uint iris = ancestry == "emberkin" ? C(255, 190, 60) : ancestry == "saurian" ? C(220, 190, 40) : eyes[Pick(eyes.Length, 8)];
        int style = calling == "monk" ? 2 : Pick(5, 12);    // short, long, shaved, tied back, wild
        bool beard = ancestry == "dwarf" || (ancestry is "human" or "orcBlooded" or "gnome" && Pick(3, 16) == 0);
        bool scales = ancestry == "saurian";

        double cx = 32, hy = 28, rx = 11.5, ry = 13.5;
        switch (ancestry)
        {
            case "dwarf": rx = 13; ry = 12.5; hy = 30; break;
            case "elf": rx = 10.5; ry = 14; break;
            case "halfling": rx = 12; ry = 12.5; hy = 30; break;
            case "gnome": rx = 12.5; ry = 12.5; hy = 30; break;
            case "orcBlooded": rx = 13; ry = 13.5; break;
            case "saurian": rx = 12; ry = 13; break;
        }
        double eyeY = hy - 1, mouthY = hy + ry * 0.55;

        // ---- The background: a lit wall behind them, in the calling's colour ----
        uint bg = calling switch
        {
            "wizard" => C(46, 48, 82), "cleric" => C(78, 72, 56), "paladin" => C(70, 66, 80), "rogue" => C(36, 34, 40),
            "ranger" or "druid" => C(44, 62, 44), "barbarian" => C(76, 52, 40), "monk" => C(80, 60, 40), "spellblade" => C(52, 40, 70),
            _ => C(62, 58, 54),
        };
        p.Rect(0, 0, 64, 64, Paint.Shade(bg, -18), false);
        p.Ellipse(32, 26, 30, 28, bg, false);
        p.Ellipse(32, 24, 20, 19, Paint.Shade(bg, 10), false);

        // ---- Hair that falls behind the shoulders ----
        if (style == 1 && !scales) p.Poly(hair, (cx - rx - 2, hy - 6), (cx + rx + 2, hy - 6), (cx + rx + 5, 52), (cx - rx - 5, 52));
        if (style == 4 && !scales) for (int i = -3; i <= 3; i++) p.Taper(cx + i * 3.6, hy - 6, 3, cx + i * 5, hy + 12 + Math.Abs(i), 1.2, hair);

        // ---- The neck, in the head's shadow, then shoulders and clothes over its foot ----
        p.Rect(cx - 4.6, hy + ry - 6, cx + 4.6, 48, Paint.Shade(skin, -34));
        Outfit(p, calling, skin);

        // ---- Ears and head ----
        Ears(p, ancestry, cx, hy, rx, skin);
        if (scales)
        {
            // A saurian: a broad scaled head, a blunt snout, a crest.
            p.Mat = Material.Gloss;
            p.Ellipse(cx, hy, rx, ry, skin);
            p.Ellipse(cx, hy + 7, rx * 0.75, 6.5, Paint.Shade(skin, 10));                                   // the snout
            p.Mat = Material.Matte;
            for (int i = 0; i < 4; i++) p.Triangle(cx - 6 + i * 4, hy - ry + 1, cx - 4 + i * 4, hy - ry - 6 + i % 2 * 2, cx - 2 + i * 4, hy - ry + 1, Paint.Shade(skin, -26));
            for (int i = 0; i < 9; i++) p.Ellipse(cx - 8 + i * 2 % 16, hy - 8 + i * 5 % 14, 1.4, 1, Paint.Shade(skin, -20), false);
            p.Dot(cx - 2.5, hy + 6, Socket); p.Dot(cx + 2.5, hy + 6, Socket);                                // nostrils
            p.Taper(cx - 7, mouthY + 2, 0.5, cx + 7, mouthY + 2, 0.5, Socket, false);
        }
        else
        {
            // The jaw, then the skull: orcs and dwarves square, elves fine.
            double jaw = ancestry is "orcBlooded" or "dwarf" ? 0.92 : ancestry == "elf" ? 0.62 : 0.78;
            p.Poly(skin, (cx - rx, hy), (cx + rx, hy), (cx + rx * jaw, hy + ry * 0.7), (cx, hy + ry + 0.5), (cx - rx * jaw, hy + ry * 0.7));
            p.Ellipse(cx, hy - 1, rx, ry - 1, skin);
            p.Ellipse(cx - rx * 0.55, hy + 4, 2.6, 1.8, Paint.Shade(skin, 18), false);                    // a little colour in the cheeks
            p.Ellipse(cx + rx * 0.55, hy + 4, 2.6, 1.8, Paint.Shade(skin, 18), false);
        }

        // ---- The face ----
        uint brow = scales ? Paint.Shade(skin, -40) : Paint.Shade(hair, -10);
        for (int side = -1; side <= 1; side += 2)
        {
            double ex = cx + side * rx * 0.42;
            if (down)
            {
                p.Taper(ex - 2.4, eyeY + 0.4, 0.45, ex + 2.4, eyeY + 0.4, 0.45, C(60, 40, 36), false);
                continue;
            }
            if (!scales) p.Ellipse(ex, eyeY, 2.6, 1.6, C(240, 236, 228), false);                               // the white
            p.Ellipse(ex + side * 0.3, eyeY, 1.4, 1.5, iris, false);
            p.Ellipse(ex + side * 0.3, eyeY, scales ? 0.4 : 0.7, scales ? 1.3 : 0.7, Socket, false);
            p.Dot(ex - 0.6, eyeY - 0.8, C(255, 255, 255));
            p.Taper(ex - side * 2.8, eyeY - 3, 0.75, ex + side * 2.4, eyeY - 3.4 + (calling == "barbarian" ? 0.8 : 0), 0.6, brow); // the brow
            if (ancestry == "emberkin") p.Ellipse(ex, eyeY, 2.8, 1.8, Paint.Shade(iris, -40), false);
            if (ancestry == "emberkin") p.Ellipse(ex, eyeY, 1.6, 1.2, iris, false);
        }
        if (!scales)
        {
            double nose = ancestry == "gnome" ? 3.4 : ancestry == "halfling" ? 1.8 : 2;
            p.Taper(cx, eyeY + 1, 0.8, cx + 0.4, hy + 5, nose * 0.8, Paint.Shade(skin, -14));                  // the nose
            p.Ellipse(cx + 0.4, hy + 5.6, nose, nose * 0.7, Paint.Shade(skin, -6));
            p.Taper(cx - 3.4, mouthY, 0.5, cx + 3.4, mouthY, 0.5, down ? C(90, 60, 56) : C(130, 64, 60), false); // the mouth
            p.Taper(cx - 2.4, mouthY + 1, 0.5, cx + 2.4, mouthY + 1, 0.5, Paint.Shade(skin, 12), false);
            if (ancestry == "orcBlooded")
            {
                p.Triangle(cx - 4.4, mouthY + 0.6, cx - 2.8, mouthY + 0.6, cx - 3.8, mouthY - 3.2, Tooth);       // tusks
                p.Triangle(cx + 4.4, mouthY + 0.6, cx + 2.8, mouthY + 0.6, cx + 3.8, mouthY - 3.2, Tooth);
            }
        }
        if (calling == "barbarian" && !scales)
            p.Taper(cx - rx * 0.8, eyeY + 2.5, 0.8, cx - rx * 0.2, eyeY + 2.5, 0.8, C(150, 40, 36), false);    // war paint
        if (beard && !scales)
        {
            p.Mat = Material.Fur;
            double len = ancestry == "dwarf" ? 18 : 6;
            p.Poly(hair, (cx - rx * 0.92, hy + 2), (cx - rx * 0.6, mouthY + 1), (cx + rx * 0.6, mouthY + 1), (cx + rx * 0.92, hy + 2), (cx + rx * 0.7, hy + ry + len * 0.6), (cx, hy + ry + len), (cx - rx * 0.7, hy + ry + len * 0.6));
            p.Mat = Material.Matte;
            p.Poly(Paint.Shade(hair, 10), (cx - 5.5, mouthY - 1.2), (cx + 5.5, mouthY - 1.2), (cx + 4, mouthY + 1.4), (cx, mouthY), (cx - 4, mouthY + 1.4)); // the moustache
            p.Ellipse(cx, mouthY + 2.6, 2.4, 0.9, C(110, 56, 52), false);
            if (ancestry == "dwarf") for (int i = 0; i < 2; i++) { p.Mat = Material.Metal; p.Ellipse(cx - 2.6 + i * 5.2, hy + ry + len * 0.75, 1.4, 1.8, C(200, 166, 70)); p.Mat = Material.Matte; } // braid rings
        }

        // ---- Hair on top, and horns ----
        if (!scales)
        {
            p.Mat = Material.Fur;
            switch (style)
            {
                case 0: // short
                    p.Poly(hair, (cx - rx - 0.5, hy - 1), (cx - rx + 1, hy - ry + 2), (cx - 4, hy - ry - 1.5), (cx + 5, hy - ry - 1), (cx + rx, hy - ry + 3), (cx + rx + 0.5, hy - 1), (cx + rx - 2, hy - 7), (cx - rx + 2, hy - 7));
                    break;
                case 1: // long, parted
                case 3: // tied back
                    p.Ellipse(cx, hy - ry + 4, rx + 0.6, 7, hair);
                    p.Poly(hair, (cx - rx - 1, hy - 4), (cx - rx + 2, hy - ry + 4), (cx - 1, hy - ry + 3), (cx - 3, hy - 6));
                    p.Poly(hair, (cx + rx + 1, hy - 4), (cx + rx - 2, hy - ry + 4), (cx + 1, hy - ry + 3), (cx + 3, hy - 6));
                    if (style == 3) { p.Mat = Material.Matte; p.Rect(cx + rx - 1, hy - 5, cx + rx + 2, hy - 2, C(110, 70, 40)); }
                    break;
                case 2: // shaved: just a shadow of it
                    p.Mat = Material.Matte;
                    p.Ellipse(cx, hy - ry + 4.5, rx - 1.5, 5, Paint.Shade(skin, -16));
                    break;
                default: // wild
                    for (int i = 0; i < 7; i++) p.Ellipse(cx - rx + 1 + i * rx / 3.2, hy - ry + 3 + (i % 2) * 2, 4.4, 4.2, hair);
                    break;
            }
            p.Mat = Material.Matte;
        }
        if (ancestry == "emberkin")
        {
            p.Mat = Material.Bone;
            p.Limb(cx - rx * 0.6, hy - ry + 4, cx - rx - 3, hy - ry - 3, cx - rx + 1, hy - ry - 10, 2.4, 0.6, C(60, 40, 40));
            p.Limb(cx + rx * 0.6, hy - ry + 4, cx + rx + 3, hy - ry - 3, cx + rx - 1, hy - ry - 10, 2.4, 0.6, C(60, 40, 40));
            p.Mat = Material.Matte;
        }

        // ---- Headgear by calling ----
        Headgear(p, calling, cx, hy, rx, ry, hair);
    }

    private static void Ears(Paint p, string ancestry, double cx, double hy, double rx, uint skin)
    {
        uint inner = Paint.Shade(skin, -24);
        for (int side = -1; side <= 1; side += 2)
        {
            double ex = cx + side * rx;
            switch (ancestry)
            {
                case "elf": p.Taper(ex - side * 1, hy + 1, 2.4, ex + side * 9, hy - 9, 0.5, skin); break;
                case "gnome": p.Ellipse(ex + side * 1.5, hy, 3.6, 5, skin); p.Ellipse(ex + side * 1.5, hy, 1.8, 3, inner, false); break;
                case "orcBlooded": p.Taper(ex, hy + 1, 2.6, ex + side * 6, hy - 4, 0.6, skin); break;
                case "halfling": p.Taper(ex, hy + 1, 2.6, ex + side * 3.6, hy - 3.6, 1, skin); break;
                case "saurian": break;
                default: p.Ellipse(ex + side * 0.6, hy + 1, 2.4, 3.8, skin); p.Ellipse(ex + side * 0.6, hy + 1, 1, 2.2, inner, false); break;
            }
        }
    }

    private static void Outfit(Paint p, string calling, uint skin)
    {
        (uint main, uint trim, Material mat) = calling switch
        {
            "fighter" => (C(124, 126, 136), C(110, 76, 44), Material.Metal),
            "barbarian" => (skin, C(130, 100, 70), Material.Matte),
            "ranger" => (C(64, 96, 58), C(110, 80, 48), Material.Cloth),
            "rogue" => (C(52, 50, 56), C(90, 70, 50), Material.Cloth),
            "cleric" => (C(220, 214, 196), C(206, 170, 70), Material.Cloth),
            "wizard" => (C(52, 64, 138), C(206, 170, 70), Material.Cloth),
            "paladin" => (C(178, 180, 190), C(206, 170, 70), Material.Metal),
            "druid" => (C(96, 110, 62), C(130, 96, 60), Material.Cloth),
            "monk" => (C(196, 120, 48), C(140, 70, 36), Material.Cloth),
            "spellblade" => (C(70, 54, 70), C(150, 110, 200), Material.Matte),
            _ => (C(120, 110, 96), C(90, 70, 50), Material.Cloth),
        };
        p.Mat = mat;
        p.Poly(main, (0, 64), (2, 54), (12, 46), (24, 43), (40, 43), (52, 46), (62, 54), (64, 64));
        p.Mat = Material.Matte;
        switch (calling)
        {
            case "fighter":
                p.Mat = Material.Metal;
                for (int y = 0; y < 5; y++) for (int x = 0; x < 12; x++) p.Dot(8 + x * 4.2 + y % 2 * 2, 49 + y * 3, Paint.Shade(main, -34)); // the mail's rings
                p.Mat = Material.Matte;
                p.Ellipse(8, 52, 9, 6, C(118, 80, 46)); p.Ellipse(56, 52, 9, 6, C(118, 80, 46));                       // leather pauldrons
                break;
            case "barbarian":
                p.Mat = Material.Fur;
                p.Poly(C(120, 96, 70), (0, 64), (0, 50), (10, 45), (20, 48), (26, 56), (20, 64));
                p.Poly(C(120, 96, 70), (64, 64), (64, 50), (54, 45), (44, 48), (38, 56), (44, 64));
                p.Mat = Material.Matte;
                p.Taper(14, 47, 1, 46, 62, 1, C(90, 60, 36));                                                           // a baldric
                break;
            case "ranger":
            case "rogue":
                p.Mat = Material.Cloth;
                p.Poly(Paint.Shade(main, -14), (8, 64), (10, 48), (22, 42), (32, 47), (42, 42), (54, 48), (56, 64));    // the hood laid back, or the cloak
                p.Mat = Material.Matte;
                p.Ellipse(32, 47.5, 2.4, 2.2, C(190, 160, 80));                                                         // the clasp
                break;
            case "cleric":
                p.Poly(trim, (26, 44), (38, 44), (35, 64), (29, 64));                                                   // the stole
                p.Mat = Material.Metal;
                p.Rect(30.8, 50, 33.2, 60, C(230, 196, 90)); p.Rect(28, 52.5, 36, 54.8, C(230, 196, 90));             // the holy symbol
                p.Mat = Material.Matte;
                break;
            case "wizard":
                for (int i = 0; i < 6; i++) p.Dot(6 + i * 10, 52 + i % 2 * 6, C(230, 220, 140));                        // stars on the robe
                p.Poly(trim, false, (24, 43), (40, 43), (36, 47), (28, 47));
                break;
            case "paladin":
                p.Mat = Material.Metal;
                p.Ellipse(7, 51, 9, 6.5, main); p.Ellipse(57, 51, 9, 6.5, main);                                       // pauldrons
                p.Mat = Material.Cloth;
                p.Poly(C(60, 80, 150), (22, 47), (42, 47), (40, 64), (24, 64));                                         // the tabard
                p.Mat = Material.Matte;
                p.Poly(trim, false, (32, 50), (35, 54), (32, 60), (29, 54));
                break;
            case "druid":
                for (int i = 0; i < 6; i++) Leaf(p, i % 2 == 0 ? C(90, 140, 60) : C(140, 160, 70), -1.2 + i * 0.48, 7, 10 + i * 9, 50);
                break;
            case "monk":
                p.Poly(trim, (20, 44), (26, 43), (44, 64), (36, 64));                                                   // the crossed robe
                break;
            case "spellblade":
                p.Ellipse(9, 52, 8, 5.5, C(60, 46, 60));
                p.Ellipse(9, 52, 3, 3, trim, false); p.Ellipse(9, 52, 1.5, 1.5, C(230, 210, 255), false);               // a rune on the shoulder
                p.Taper(46, 46, 1.2, 56, 30, 1, C(150, 154, 166));                                                       // a hilt over the shoulder
                p.Rect(52, 32, 60, 34, C(110, 80, 50));
                break;
        }
    }

    private static void Headgear(Paint p, string calling, double cx, double hy, double rx, double ry, uint hair)
    {
        switch (calling)
        {
            case "wizard":
            {
                uint hat = C(52, 64, 138);
                p.Mat = Material.Cloth;
                p.Ellipse(cx, hy - ry + 3, rx + 7, 3.6, hat);                                                            // the brim
                p.Poly(hat, (cx - rx + 1, hy - ry + 3), (cx + rx - 1, hy - ry + 3), (cx + 9, hy - ry - 10), (cx + 14, hy - ry - 16), (cx + 4, hy - ry - 9));
                p.Mat = Material.Matte;
                p.Rect(cx - rx + 1.5, hy - ry + 0.6, cx + rx - 1.5, hy - ry + 2.6, C(206, 170, 70));
                break;
            }
            case "ranger":
                p.Mat = Material.Cloth;
                p.Poly(C(56, 86, 50), (cx - rx - 3, hy + 6), (cx - rx - 2, hy - ry + 2), (cx, hy - ry - 3), (cx + rx + 2, hy - ry + 2), (cx + rx + 3, hy + 6), (cx + rx, hy - 4), (cx, hy - ry + 1.5), (cx - rx, hy - 4));
                p.Mat = Material.Matte;
                break;
            case "rogue":
                p.Mat = Material.Cloth;
                p.Poly(C(44, 42, 48), (cx - rx - 3, hy + 8), (cx - rx - 2, hy - ry + 2), (cx, hy - ry - 3), (cx + rx + 2, hy - ry + 2), (cx + rx + 3, hy + 8), (cx + rx, hy - 4), (cx, hy - ry + 1.5), (cx - rx, hy - 4));
                p.Poly(C(40, 38, 44), (cx - rx + 0.5, hy + 4.5), (cx + rx - 0.5, hy + 4.5), (cx + rx * 0.7, hy + ry + 1), (cx - rx * 0.7, hy + ry + 1)); // the scarf over the mouth
                p.Mat = Material.Matte;
                break;
            case "cleric":
                p.Mat = Material.Cloth;
                p.Poly(C(220, 214, 196), (cx - rx - 2, hy + 10), (cx - rx - 1.5, hy - ry + 3), (cx, hy - ry - 1.5), (cx + rx + 1.5, hy - ry + 3), (cx + rx + 2, hy + 10), (cx + rx - 0.5, hy - 3), (cx, hy - ry + 2.5), (cx - rx + 0.5, hy - 3)); // a coif
                p.Mat = Material.Matte;
                break;
            case "paladin":
                p.Mat = Material.Metal;
                p.Rect(cx - rx + 1, hy - ry + 1, cx + rx - 1, hy - ry + 4, C(206, 170, 70));                                // a circlet
                p.Mat = Material.Matte;
                p.Dot(cx, hy - ry + 2.5, C(120, 200, 255));
                break;
            case "druid":
                p.Mat = Material.Bone;
                p.Limb(cx - 5, hy - ry + 2, cx - 9, hy - ry - 6, cx - 13, hy - ry - 10, 1.2, 0.5, C(170, 140, 100));        // antlers
                p.Limb(cx + 5, hy - ry + 2, cx + 9, hy - ry - 6, cx + 13, hy - ry - 10, 1.2, 0.5, C(170, 140, 100));
                p.Taper(cx - 8, hy - ry - 4, 0.6, cx - 5, hy - ry - 9, 0.4, C(170, 140, 100));
                p.Taper(cx + 8, hy - ry - 4, 0.6, cx + 5, hy - ry - 9, 0.4, C(170, 140, 100));
                p.Mat = Material.Matte;
                for (int i = 0; i < 4; i++) Leaf(p, C(90, 140, 60), -1.2 + i * 0.8, 4, cx - 6 + i * 4, hy - ry + 4);
                break;
            case "spellblade":
                p.Mat = Material.Metal;
                p.Taper(cx - rx + 0.5, hy - ry + 4, 0.8, cx + rx - 0.5, hy - ry + 4, 0.8, C(150, 154, 166));
                p.Mat = Material.Matte;
                p.Ellipse(cx, hy - ry + 4, 1.6, 1.6, C(170, 120, 240), false);
                break;
            case "monk":
                p.Ellipse(cx, hy - ry + 6, 1.4, 1.4, C(230, 180, 80), false);                                                // a mark on the brow
                break;
        }
    }
}
