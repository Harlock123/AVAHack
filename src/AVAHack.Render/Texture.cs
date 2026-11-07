namespace AVAHack.Render;

/// <summary>
/// A square texture of 0xAARRGGBB pixels, row by row. Alpha 0 means see-through (a portcullis's gaps). Code-drawn
/// textures are <see cref="Size"/> (64) on a side; textures from the art pack are 128. The side is a power of two.
/// </summary>
public sealed class Texture
{
    /// <summary>The side of a code-drawn texture.</summary>
    public const int Size = 64;
    public const int Mask = Size - 1;

    public Texture(int side = Size)
    {
        if (side < 1 || (side & (side - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(side), "A texture's side is a power of two.");
        Side = side;
        SideMask = side - 1;
        Pixels = new uint[side * side];
    }

    /// <summary>This texture's side in pixels.</summary>
    public int Side { get; }
    public int SideMask { get; }
    public uint[] Pixels { get; }

    public uint this[int x, int y]
    {
        get => Pixels[(y & SideMask) * Side + (x & SideMask)];
        set => Pixels[(y & SideMask) * Side + (x & SideMask)] = value;
    }

    /// <summary>A copy, to draw on.</summary>
    public Texture Clone()
    {
        var t = new Texture(Side);
        Array.Copy(Pixels, t.Pixels, Pixels.Length);
        return t;
    }

    /// <summary>The average colour, as 0xRRGGBB, for code-drawn pieces that should match it.</summary>
    public int Average()
    {
        long r = 0, g = 0, b = 0;
        foreach (uint p in Pixels) { r += p >> 16 & 0xFF; g += p >> 8 & 0xFF; b += p & 0xFF; }
        int n = Pixels.Length;
        return (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
    }
}

/// <summary>The textures one dungeon theme draws with.</summary>
public sealed class TextureSet
{
    public required Texture Wall { get; init; }
    public required Texture Floor { get; init; }
    public required Texture Ceiling { get; init; }
    public required Texture DoorFrame { get; init; }
    public required Texture Door { get; init; }
    public required Texture Portcullis { get; init; }
    public required Texture LeverUp { get; init; }
    public required Texture LeverDown { get; init; }
    public required Texture Pit { get; init; }
    public required Texture Plate { get; init; }
    public required Texture Stairs { get; init; }
    public Texture? StairsUp { get; init; }
    public Texture? TrapMark { get; init; }
    public Texture? Spikes { get; init; }
    public Texture? Glyph { get; init; }
    public Texture? Rune { get; init; }
    public Texture? Teleporter { get; init; }
    public Texture? Rubble { get; init; }
    /// <summary>A wall lined with bookshelves (the Ossuary Stacks).</summary>
    public Texture? Shelf { get; init; }
}
