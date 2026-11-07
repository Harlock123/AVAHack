using SkiaSharp;

namespace AVAHack.Render.Tests;

/// <summary>Reads and writes 0xAARRGGBB framebuffers as PNG files.</summary>
internal static class Png
{
    public static void Save(string path, uint[] pixels, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var span = bmp.GetPixelSpan();
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()).CopyTo(
            System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span), span.Length));
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    public static (uint[] Pixels, int Width, int Height) Load(string path)
    {
        using var decoded = SKBitmap.Decode(path) ?? throw new InvalidDataException($"Not a PNG: {path}");
        using var bmp = decoded.Copy(SKColorType.Bgra8888);
        var bytes = bmp.GetPixelSpan();
        var pixels = new uint[bmp.Width * bmp.Height];
        bytes.CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()));
        return (pixels, bmp.Width, bmp.Height);
    }
}
