namespace AVAHack.Render;

/// <summary>A square texture of 0xAARRGGBB pixels, row by row. Alpha 0 means see-through (a portcullis's gaps).</summary>
public sealed class Texture
{
    public const int Size = 64;
    public const int Mask = Size - 1;
    public uint[] Pixels { get; } = new uint[Size * Size];

    public uint this[int x, int y]
    {
        get => Pixels[(y & Mask) * Size + (x & Mask)];
        set => Pixels[(y & Mask) * Size + (x & Mask)] = value;
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
}
