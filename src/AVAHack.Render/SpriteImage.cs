namespace AVAHack.Render;

/// <summary>A picture of any size in 0xAARRGGBB pixels, row by row; alpha 0 is see-through.</summary>
public sealed class SpriteImage(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public uint[] Pixels { get; } = new uint[width * height];

    public uint this[int x, int y]
    {
        get => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : Pixels[y * Width + x];
        set
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = value;
        }
    }

    public SpriteImage Mirrored()
    {
        var m = new SpriteImage(Width, Height);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            m.Pixels[y * Width + x] = Pixels[y * Width + (Width - 1 - x)];
        return m;
    }
}

/// <summary>One thing to draw standing in the world: a camera-facing picture.</summary>
public readonly record struct SpriteInstance(
    double X, double Y, double Z, double Width, double Height, SpriteImage Image,
    uint Tint = 0, double TintAmount = 0, double Glow = 0, bool SelfLit = false);
