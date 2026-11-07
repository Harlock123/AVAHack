namespace AVAHack.Render;

/// <summary>Simple shape drawing for the code-drawn placeholder sprites.</summary>
internal sealed class Paint(SpriteImage img)
{
    public SpriteImage Image { get; } = img;

    public static uint Rgb(int r, int g, int b) => TextureFactory.Rgb(r, g, b);

    public static uint Shade(uint c, int delta)
    {
        int r = (int)(c >> 16 & 0xFF) + delta, g = (int)(c >> 8 & 0xFF) + delta, b = (int)(c & 0xFF) + delta;
        return Rgb(r, g, b);
    }

    public void Ellipse(double cx, double cy, double rx, double ry, uint c, bool shaded = true)
    {
        for (int y = (int)(cy - ry - 1); y <= cy + ry + 1; y++)
        for (int x = (int)(cx - rx - 1); x <= cx + rx + 1; x++)
        {
            double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
            double d = dx * dx + dy * dy;
            if (d > 1) continue;
            // Lit from the upper left.
            int light = shaded ? (int)((-dx - dy) * 18 - d * 10) : 0;
            Image[x, y] = Shade(c, light);
        }
    }

    public void Rect(double x0, double y0, double x1, double y1, uint c, bool shaded = true)
    {
        for (int y = (int)Math.Floor(y0); y < y1; y++)
        for (int x = (int)Math.Floor(x0); x < x1; x++)
        {
            int light = shaded ? (int)((x1 - x) / Math.Max(1, x1 - x0) * 16 - 8) : 0;
            Image[x, y] = Shade(c, light);
        }
    }

    public void Line(double x0, double y0, double x1, double y1, double thick, uint c)
    {
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 2) + 1;
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            double x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
            for (int yy = (int)(y - thick / 2); yy <= y + thick / 2; yy++)
            for (int xx = (int)(x - thick / 2); xx <= x + thick / 2; xx++)
                Image[xx, yy] = c;
        }
    }

    public void Triangle(double ax, double ay, double bx, double by, double cx, double cy, uint c)
    {
        int minX = (int)Math.Min(ax, Math.Min(bx, cx)), maxX = (int)Math.Max(ax, Math.Max(bx, cx));
        int minY = (int)Math.Min(ay, Math.Min(by, cy)), maxY = (int)Math.Max(ay, Math.Max(by, cy));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            double px = x + 0.5, py = y + 0.5;
            double d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            double d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            double d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            if (!(neg && pos)) Image[x, y] = c;
        }
    }

    public void Dot(double x, double y, uint c) => Image[(int)x, (int)y] = c;

    /// <summary>A dark one-pixel outline round everything drawn, for the pixel-art look.</summary>
    public void Outline(uint c = 0xFF100C0A)
    {
        var src = (uint[])Image.Pixels.Clone();
        int w = Image.Width, h = Image.Height;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (src[y * w + x] >> 24 != 0) continue;
            bool edge = (x > 0 && src[y * w + x - 1] >> 24 != 0) || (x < w - 1 && src[y * w + x + 1] >> 24 != 0)
                     || (y > 0 && src[(y - 1) * w + x] >> 24 != 0) || (y < h - 1 && src[(y + 1) * w + x] >> 24 != 0);
            if (edge) Image.Pixels[y * w + x] = c;
        }
    }
}
