namespace AVAHack.Render;

/// <summary>
/// Draws the placeholder textures in code: stone blocks, flagstones, a rough ceiling, a planked door, an iron
/// portcullis and a lever. Only integer hashing is used, so the same seed gives the same pixels on every machine.
/// </summary>
public static class TextureFactory
{
    public static TextureSet Cellar(int seed = 1) => Themed(seed, 0x6E6A62, 0x57524A, 0x3F3B36, 0x625D56);

    /// <summary>The textures for a region: cellar (grey stone), works (rust-stained brick) or choir (green, damp stone).</summary>
    public static TextureSet ForTheme(string theme) => theme switch
    {
        "works" => Themed(11, 0x74604E, 0x4E4640, 0x3A322C, 0x6A584A),
        "choir" => Themed(21, 0x566A5E, 0x46524A, 0x2E3A34, 0x5A6A60),
        _ => Cellar(),
    };

    private static TextureSet Themed(int seed, int wall, int floor, int ceiling, int frame) => new()
    {
        Wall = StoneBlocks(seed, wall),
        Floor = Flagstones(seed + 1, floor),
        Ceiling = RoughStone(seed + 2, ceiling),
        DoorFrame = DressedFrame(seed + 3, frame),
        Door = Planks(seed + 4),
        Portcullis = Bars(),
        LeverUp = WithLever(StoneBlocks(seed, wall), down: false),
        LeverDown = WithLever(StoneBlocks(seed, wall), down: true),
        Pit = PitHole(seed + 5),
        Plate = PressurePlate(seed + 6, floor),
        Stairs = StairsDown(seed + 7),
        StairsUp = StairsUpward(seed + 8),
        TrapMark = Marked(Flagstones(seed + 1, floor), 0xA03020),
        Spikes = SpikeBed(seed + 9, floor),
        Glyph = Sigil(Flagstones(seed + 1, floor), 0x60B0FF),
        Rune = Sigil(Flagstones(seed + 1, floor), 0xF0D040),
        Teleporter = Sigil(Flagstones(seed + 1, floor), 0xB060F0),
        Rubble = Broken(seed + 10, floor),
    };

    private static Texture StairsUpward(int seed)
    {
        var t = StairsDown(seed);
        var u = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
            u[x, y] = Shade((int)(t[x, 63 - y] & 0xFFFFFF), 30);
        return u;
    }

    /// <summary>A floor with a warning scratched into it: a spotted trap.</summary>
    private static Texture Marked(Texture floor, int colour)
    {
        var t = new Texture();
        Array.Copy(floor.Pixels, t.Pixels, t.Pixels.Length);
        uint c = Rgb(colour >> 16 & 0xFF, colour >> 8 & 0xFF, colour & 0xFF);
        for (int i = 14; i < 50; i++)
            for (int k = -1; k <= 1; k++) { t[i + k, i] = c; t[63 - i + k, i] = c; }
        return t;
    }

    private static Texture SpikeBed(int seed, int floor)
    {
        var t = Flagstones(seed, floor);
        for (int sy = 6; sy < 64; sy += 12)
        for (int sx = 6; sx < 64; sx += 12)
            for (int r = 0; r < 4; r++)
            for (int k = -r; k <= r; k++)
                t[sx + k, sy + 4 - r] = Rgb(150 + r * 20, 150 + r * 20, 160 + r * 20);
        return t;
    }

    private static Texture Sigil(Texture floor, int colour)
    {
        var t = new Texture();
        Array.Copy(floor.Pixels, t.Pixels, t.Pixels.Length);
        uint c = Rgb(colour >> 16 & 0xFF, colour >> 8 & 0xFF, colour & 0xFF);
        // A circle drawn with integer steps (a fixed table, so it's the same everywhere).
        for (int a = 0; a < 64; a++)
        {
            int dx = CircleX[a], dy = CircleX[(a + 16) % 64];
            t[32 + dx * 22 / 100, 32 + dy * 22 / 100] = c;
            t[32 + dx * 14 / 100, 32 + dy * 14 / 100] = c;
        }
        for (int i = -18; i <= 18; i++) { t[32 + i, 32 + i / 2] = c; t[32 - i / 2, 32 + i] = c; }
        return t;
    }

    /// <summary>cos(2πk/64) × 100, rounded: a circle without floating point.</summary>
    private static readonly int[] CircleX =
    [
        100, 100, 98, 96, 92, 88, 83, 77, 71, 63, 56, 47, 38, 29, 20, 10, 0, -10, -20, -29, -38, -47, -56, -63, -71, -77, -83, -88, -92, -96, -98, -100,
        -100, -100, -98, -96, -92, -88, -83, -77, -71, -63, -56, -47, -38, -29, -20, -10, 0, 10, 20, 29, 38, 47, 56, 63, 71, 77, 83, 88, 92, 96, 98, 100,
    ];

    private static Texture Broken(int seed, int floor)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int rock = Hash(seed, x / 8, y / 8);
            int grain = (Fractal(seed, x, y) - 128) / 4;
            bool gap = (x % 8 == 0 || y % 8 == 0) && Hash(seed + 3, x, y) > 100;
            t[x, y] = gap ? Rgb(20, 18, 16) : Shade(floor, rock / 6 - 10 + grain);
        }
        return t;
    }

    /// <summary>A square hole with a broken rim, black at the bottom.</summary>
    public static Texture PitHole(int seed)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int edge = Math.Min(Math.Min(x, 63 - x), Math.Min(y, 63 - y));
            int jag = Hash(seed, x / 4, y / 4) / 64; // 0..3: a ragged rim
            if (edge < 4 + jag) t[x, y] = Shade(0x4A453F, (Fractal(seed, x, y) - 128) / 5 - 10);
            else t[x, y] = Rgb(Math.Max(0, 14 - edge / 3), Math.Max(0, 12 - edge / 3), Math.Max(0, 12 - edge / 3));
        }
        return t;
    }

    /// <summary>A flagstone raised a little proud of its neighbours, with a dark seam round it.</summary>
    public static Texture PressurePlate(int seed, int baseRgb)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int edge = Math.Min(Math.Min(x, 63 - x), Math.Min(y, 63 - y));
            int grain = (Fractal(seed, x, y) - 128) / 5;
            t[x, y] = edge < 10 ? Shade(baseRgb, -6 + grain)
                    : edge < 12 ? Rgb(20, 18, 16)
                    : edge < 14 ? Shade(baseRgb, 24 + grain)
                    : Shade(baseRgb, 10 + grain);
        }
        return t;
    }

    /// <summary>Steps going down into the dark, lit from the top.</summary>
    public static Texture StairsDown(int seed)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int step = y / 10, sy = y % 10;
            int depth = step * 14;
            int grain = (Fractal(seed, x, y) - 128) / 6;
            bool riser = sy >= 7;
            bool wall = x < 6 || x > 57;
            t[x, y] = wall ? Shade(0x5A554E, grain - 10)
                    : riser ? Shade(0x2C2925, grain - depth / 2)
                    : Shade(0x625C55, grain - depth);
        }
        return t;
    }

    /// <summary>A hash of three integers, spread evenly over 0..255.</summary>
    internal static int Hash(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 0x8DA6B343u ^ (uint)b * 0xD8163841u ^ (uint)c * 0xCB1AB31Fu;
            h ^= h >> 13;
            h *= 0x5BD1E995u;
            h ^= h >> 15;
            return (int)(h & 0xFF);
        }
    }

    /// <summary>Smooth value noise, 0..255, with the given cell size in pixels (a power of two).</summary>
    private static int Noise(int seed, int x, int y, int cell)
    {
        int gx = x / cell, gy = y / cell, fx = x % cell, fy = y % cell;
        int wrap = Texture.Size / cell;
        int a = Hash(seed, gx % wrap, gy % wrap), b = Hash(seed, (gx + 1) % wrap, gy % wrap);
        int c = Hash(seed, gx % wrap, (gy + 1) % wrap), d = Hash(seed, (gx + 1) % wrap, (gy + 1) % wrap);
        int top = a + (b - a) * fx / cell, bottom = c + (d - c) * fx / cell;
        return top + (bottom - top) * fy / cell;
    }

    private static int Fractal(int seed, int x, int y) =>
        (Noise(seed, x, y, 16) * 4 + Noise(seed + 7, x, y, 8) * 2 + Noise(seed + 13, x, y, 4)) / 7;

    internal static uint Rgb(int r, int g, int b) =>
        0xFF000000u | (uint)Math.Clamp(r, 0, 255) << 16 | (uint)Math.Clamp(g, 0, 255) << 8 | (uint)Math.Clamp(b, 0, 255);

    private static uint Shade(int baseRgb, int delta) =>
        Rgb((baseRgb >> 16 & 0xFF) + delta, (baseRgb >> 8 & 0xFF) + delta, (baseRgb & 0xFF) + delta);

    /// <summary>Courses of stone blocks, 16 px high and 32 wide, every other course offset by half a block.</summary>
    public static Texture StoneBlocks(int seed, int baseRgb)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int course = y / 16;
            int sx = (x + (course % 2) * 16) % Texture.Size;
            int block = sx / 32;
            int bx = sx % 32, by = y % 16;
            bool mortar = bx < 2 || by < 2;
            int blockTone = Hash(seed, block, course) / 8 - 16;
            int grain = (Fractal(seed, x, y) - 128) / 5;
            int edge = (bx == 2 || by == 2) ? 14 : (bx == 31 || by == 15) ? -14 : 0;
            int speck = Hash(seed + 99, x, y) > 248 ? -30 : 0;
            t[x, y] = mortar ? Shade(baseRgb, -48 + grain / 2) : Shade(baseRgb, blockTone + grain + edge + speck);
        }
        return t;
    }

    /// <summary>Square flagstones, 32 px, with dark grout and worn centres.</summary>
    public static Texture Flagstones(int seed, int baseRgb)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int fx = x % 32, fy = y % 32;
            bool grout = fx < 2 || fy < 2;
            int tone = Hash(seed, x / 32, y / 32) / 10 - 12;
            int grain = (Fractal(seed, x, y) - 128) / 4;
            int crack = Hash(seed + 5, x / 2, y) > 252 ? -24 : 0;
            t[x, y] = grout ? Shade(baseRgb, -44) : Shade(baseRgb, tone + grain + crack);
        }
        return t;
    }

    public static Texture RoughStone(int seed, int baseRgb)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
            t[x, y] = Shade(baseRgb, (Fractal(seed, x, y) - 128) / 3 + (Hash(seed, x, y) > 250 ? -20 : 0));
        return t;
    }

    /// <summary>Tall dressed stones for the sides of a doorway.</summary>
    public static Texture DressedFrame(int seed, int baseRgb)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int by = y % 32;
            bool joint = by < 2 || x < 2;
            int grain = (Fractal(seed, x, y) - 128) / 6;
            int bevel = x == 2 || by == 2 ? 16 : x == 63 || by == 31 ? -16 : 0;
            t[x, y] = joint ? Shade(baseRgb, -50) : Shade(baseRgb, grain + bevel + Hash(seed, 0, y / 32) / 12);
        }
        return t;
    }

    /// <summary>Vertical oak planks with two iron bands and rivets.</summary>
    public static Texture Planks(int seed)
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int plank = x / 8, px = x % 8;
            int tone = Hash(seed, plank, 0) / 8;
            int grain = (Noise(seed + plank, x * 8 % 64, y, 4) - 128) / 6;
            uint c = px == 0 ? Rgb(40, 24, 12) : Rgb(112 + tone + grain, 70 + tone / 2 + grain, 36 + grain / 2);
            bool band = y is >= 10 and < 15 or >= 48 and < 53;
            if (band)
            {
                bool rivet = (x % 16 == 7) && (y is 12 or 50);
                c = rivet ? Rgb(150, 150, 156) : Rgb(58 + grain / 2, 58 + grain / 2, 64 + grain / 2);
            }
            t[x, y] = c;
        }
        return t;
    }

    /// <summary>An iron grid: four upright bars and three crossbars. The gaps are transparent.</summary>
    public static Texture Bars()
    {
        var t = new Texture();
        for (int y = 0; y < Texture.Size; y++)
        for (int x = 0; x < Texture.Size; x++)
        {
            int bx = (x + 8) % 16, by = (y + 10) % 21;
            bool upright = bx < 4, cross = by < 3;
            if (!upright && !cross) { t[x, y] = 0; continue; }
            int light = upright ? (bx == 1 ? 40 : bx == 3 ? -20 : 0) : (by == 0 ? 30 : -10);
            bool spike = upright && y >= 58;
            t[x, y] = Rgb(70 + light + (spike ? 12 : 0), 68 + light + (spike ? 12 : 0), 66 + light + (spike ? 12 : 0));
        }
        return t;
    }

    /// <summary>A wall texture with an iron plate and a lever handle, up or down.</summary>
    public static Texture WithLever(Texture wall, bool down)
    {
        var t = new Texture();
        Array.Copy(wall.Pixels, t.Pixels, t.Pixels.Length);
        // The mounting plate.
        for (int y = 24; y < 44; y++)
        for (int x = 26; x < 38; x++)
        {
            bool rim = x is 26 or 37 || y is 24 or 43;
            t[x, y] = rim ? Rgb(34, 34, 38) : Rgb(78, 76, 80);
        }
        // The handle: a shaft from the pivot, ending in a knob.
        int pivotX = 31, pivotY = 34;
        int dir = down ? 1 : -1;
        for (int i = 0; i < 18; i++)
        {
            int y = pivotY + dir * i;
            for (int x = pivotX - 1; x <= pivotX + 1; x++)
                t[x, y] = x == pivotX - 1 ? Rgb(150, 140, 120) : Rgb(96, 88, 74);
        }
        int knobY = pivotY + dir * 18;
        for (int y = knobY - 2; y <= knobY + 2; y++)
        for (int x = pivotX - 3; x <= pivotX + 3; x++)
            t[x, y] = Rgb(120, 40, 30);
        t[pivotX - 2, knobY - 1] = Rgb(190, 90, 70);
        return t;
    }
}
