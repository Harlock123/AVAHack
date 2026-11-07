using System.IO.Compression;

namespace AVAHack.Render;

/// <summary>
/// Reads the PNGs the art import writes (tools/import-sbs-textures.sh): 8-bit RGB or RGBA, not interlaced. Kept in
/// Render so the view needs no image library and decodes the same on every machine.
/// </summary>
public static class PngReader
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Decodes a PNG to 0xAARRGGBB pixels, row by row.</summary>
    public static (uint[] Pixels, int Width, int Height) Read(Stream stream)
    {
        using var br = new BinaryReader(stream);
        if (!br.ReadBytes(8).AsSpan().SequenceEqual(Signature)) throw new InvalidDataException("Not a PNG.");
        int width = 0, height = 0, colourType = 0;
        using var idat = new MemoryStream();
        while (true)
        {
            int length = BigEndian(br.ReadBytes(4));
            string type = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
            byte[] data = br.ReadBytes(length);
            br.ReadBytes(4); // CRC
            if (type == "IHDR")
            {
                width = BigEndian(data.AsSpan(0, 4));
                height = BigEndian(data.AsSpan(4, 4));
                int depth = data[8];
                colourType = data[9];
                if (depth != 8 || colourType is not (2 or 6) || data[12] != 0)
                    throw new InvalidDataException("Only 8-bit RGB or RGBA PNGs without interlacing are read.");
            }
            else if (type == "IDAT") idat.Write(data);
            else if (type == "IEND") break;
        }

        int bpp = colourType == 6 ? 4 : 3, stride = width * bpp;
        var raw = new byte[height * (stride + 1)];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.ReadExactly(raw);

        var pixels = new uint[width * height];
        var prev = new byte[stride];
        var line = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            int at = y * (stride + 1);
            byte filter = raw[at];
            for (int i = 0; i < stride; i++)
            {
                int x = raw[at + 1 + i];
                int a = i >= bpp ? line[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                line[i] = (byte)(filter switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + (a + b) / 2,
                    4 => x + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter}."),
                });
            }
            for (int px = 0; px < width; px++)
            {
                int o = px * bpp;
                uint alpha = bpp == 4 ? line[o + 3] : 255u;
                pixels[y * width + px] = alpha << 24 | (uint)line[o] << 16 | (uint)line[o + 1] << 8 | line[o + 2];
            }
            (prev, line) = (line, prev);
        }
        return (pixels, width, height);
    }

    private static int BigEndian(ReadOnlySpan<byte> b) => b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3];

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
