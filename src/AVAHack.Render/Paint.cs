namespace AVAHack.Render;

/// <summary>What a shape is made of: how <see cref="Paint.Finish"/> lights and textures it.</summary>
internal enum Material : byte { Matte, Fur, Metal, Bone, Gloss, Stone, Cloth }

/// <summary>
/// Shape drawing for the code-drawn sprites. Drawers work in a fixed grid (64 across for a creature) and
/// <see cref="Scale"/> maps it onto the picture, so the same drawing comes out at 128 px. Shapes are laid down
/// flat; <see cref="Finish"/> then shades the picture as pixel art:
/// <list type="bullet">
/// <item>each patch of one colour is treated as a rounded surface and lit from the upper left;</item>
/// <item>its tone is quantised to a five-step ramp, with cool shadows and warm highlights, and ordered-dithered
/// between steps;</item>
/// <item>a shape drawn over another casts a contact shadow on it and gets a darker inner line;</item>
/// <item>fur, metal, bone, slime, stone and cloth add their own grain or shine;</item>
/// <item>the outline is a dark shade of whatever it borders, not flat black.</item>
/// </list>
/// Flat shapes (eyes, mouths, glows) keep their colour. Only integer hashing is used, so every machine draws the
/// same pixels.
/// </summary>
internal sealed class Paint
{
    [Flags]
    private enum Kind : byte { None = 0, Shape = 1, Flat = 2, Baked = 4 }

    private readonly uint[] _base;
    private readonly int[] _seq;
    private readonly Kind[] _kind;
    private readonly Material[] _mat;
    private int _next = 1;

    public Paint(SpriteImage img, double scale = 1)
    {
        Image = img;
        Scale = scale;
        int n = img.Width * img.Height;
        _base = new uint[n];
        _seq = new int[n];
        _kind = new Kind[n];
        _mat = new Material[n];
    }

    public SpriteImage Image { get; }

    /// <summary>Picture pixels per grid unit.</summary>
    public double Scale { get; }

    /// <summary>The material the next shapes are made of.</summary>
    public Material Mat { get; set; }

    public static uint Rgb(int r, int g, int b) => TextureFactory.Rgb(r, g, b);

    public static uint Shade(uint c, int delta)
    {
        int r = (int)(c >> 16 & 0xFF) + delta, g = (int)(c >> 8 & 0xFF) + delta, b = (int)(c & 0xFF) + delta;
        return Rgb(r, g, b);
    }

    private void Put(int x, int y, uint c, bool flat)
    {
        if (x < 0 || y < 0 || x >= Image.Width || y >= Image.Height) return;
        int i = y * Image.Width + x;
        _base[i] = c | 0xFF000000u;
        _seq[i] = _next;
        _kind[i] = flat ? Kind.Flat : Kind.Shape;
        _mat[i] = Mat;
    }

    /// <summary>A filled ellipse. Unshaded ellipses (eyes, mouths, glows) keep their flat colour.</summary>
    public void Ellipse(double cx, double cy, double rx, double ry, uint c, bool shaded = true)
    {
        double s = Scale, X = cx * s, Y = cy * s, RX = Math.Max(0.5, rx * s), RY = Math.Max(0.5, ry * s);
        for (int y = (int)(Y - RY - 1); y <= Y + RY + 1; y++)
        for (int x = (int)(X - RX - 1); x <= X + RX + 1; x++)
        {
            double dx = (x + 0.5 - X) / RX, dy = (y + 0.5 - Y) / RY;
            if (dx * dx + dy * dy <= 1) Put(x, y, c, !shaded);
        }
        _next++;
    }

    public void Rect(double x0, double y0, double x1, double y1, uint c, bool shaded = true)
    {
        double s = Scale;
        for (int y = (int)Math.Floor(y0 * s); y < y1 * s; y++)
        for (int x = (int)Math.Floor(x0 * s); x < x1 * s; x++)
            Put(x, y, c, !shaded);
        _next++;
    }

    /// <summary>A line with round ends, <paramref name="thick"/> grid units across.</summary>
    public void Line(double x0, double y0, double x1, double y1, double thick, uint c)
    {
        double s = Scale, ax = x0 * s, ay = y0 * s, bx = x1 * s, by = y1 * s;
        double r = Math.Max(0.5, thick * s / 2);
        double vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
        for (int y = (int)(Math.Min(ay, by) - r - 1); y <= Math.Max(ay, by) + r + 1; y++)
        for (int x = (int)(Math.Min(ax, bx) - r - 1); x <= Math.Max(ax, bx) + r + 1; x++)
        {
            double px = x + 0.5 - ax, py = y + 0.5 - ay;
            double t = len2 == 0 ? 0 : Math.Clamp((px * vx + py * vy) / len2, 0, 1);
            double dx = px - vx * t, dy = py - vy * t;
            if (dx * dx + dy * dy <= r * r) Put(x, y, c, false);
        }
        _next++;
    }

    public void Triangle(double ax, double ay, double bx, double by, double cx, double cy, uint c)
    {
        double s = Scale;
        ax *= s; ay *= s; bx *= s; by *= s; cx *= s; cy *= s;
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
            if (!(neg && pos)) Put(x, y, c, false);
        }
        _next++;
    }

    /// <summary>A limb or tail: round at both ends, <paramref name="r0"/> grid units thick at the start tapering to
    /// <paramref name="r1"/> at the end.</summary>
    public void Taper(double x0, double y0, double r0, double x1, double y1, double r1, uint c, bool shaded = true)
    {
        double s = Scale, ax = x0 * s, ay = y0 * s, bx = x1 * s, by = y1 * s, ra = Math.Max(0.5, r0 * s), rb = Math.Max(0.5, r1 * s);
        double vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
        double rm = Math.Max(ra, rb);
        for (int y = (int)(Math.Min(ay, by) - rm - 1); y <= Math.Max(ay, by) + rm + 1; y++)
        for (int x = (int)(Math.Min(ax, bx) - rm - 1); x <= Math.Max(ax, bx) + rm + 1; x++)
        {
            double px = x + 0.5 - ax, py = y + 0.5 - ay;
            double t = len2 == 0 ? 0 : Math.Clamp((px * vx + py * vy) / len2, 0, 1);
            double dx = px - vx * t, dy = py - vy * t, r = ra + (rb - ra) * t;
            if (dx * dx + dy * dy <= r * r) Put(x, y, c, !shaded);
        }
        _next++;
    }

    /// <summary>A two-part limb bent at a joint: upper and lower segments, tapering from <paramref name="r0"/> to
    /// <paramref name="r2"/>. Both segments are one shape, so they shade as one.</summary>
    public void Limb(double x0, double y0, double x1, double y1, double x2, double y2, double r0, double r2, uint c)
    {
        double r1 = (r0 + r2) / 2;
        int seq = _next;
        Taper(x0, y0, r0, x1, y1, r1, c);
        _next = seq;
        Taper(x1, y1, r1, x2, y2, r2, c);
    }

    /// <summary>A filled polygon (grid units), for torsos, cloaks, wings and plates.</summary>
    public void Poly(uint c, params (double X, double Y)[] pts) => Poly(c, true, pts);

    public void Poly(uint c, bool shaded, params (double X, double Y)[] pts)
    {
        double s = Scale;
        double minY = pts.Min(p => p.Y) * s, maxY = pts.Max(p => p.Y) * s;
        var xs = new List<double>();
        for (int y = (int)Math.Floor(minY); y <= maxY; y++)
        {
            double sy = y + 0.5;
            xs.Clear();
            for (int i = 0; i < pts.Length; i++)
            {
                var (ax, ay) = (pts[i].X * s, pts[i].Y * s);
                var (bx, by) = (pts[(i + 1) % pts.Length].X * s, pts[(i + 1) % pts.Length].Y * s);
                if ((ay <= sy && by > sy) || (by <= sy && ay > sy)) xs.Add(ax + (sy - ay) / (by - ay) * (bx - ax));
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
                for (int x = (int)Math.Round(xs[k]); x < Math.Round(xs[k + 1]); x++) Put(x, y, c, !shaded);
        }
        _next++;
    }

    /// <summary>One grid unit of flat colour: an eye, a nostril, a spark.</summary>
    public void Dot(double x, double y, uint c)
    {
        int k = Math.Max(1, (int)Math.Round(Scale));
        int px = (int)Math.Floor(x * Scale), py = (int)Math.Floor(y * Scale);
        for (int dy = 0; dy < k; dy++)
        for (int dx = 0; dx < k; dx++)
            Put(px + dx, py + dy, c, true);
        _next++;
    }

    /// <summary>Copies a finished picture in, already shaded (a swarm's members).</summary>
    public void Bake(int x, int y, uint c)
    {
        if (x < 0 || y < 0 || x >= Image.Width || y >= Image.Height || c >> 24 == 0) return;
        int i = y * Image.Width + x;
        _base[i] = c;
        _seq[i] = _next;
        _kind[i] = Kind.Baked;
    }

    /// <summary>Kept for the drawers: the outline is now part of <see cref="Finish"/>.</summary>
    public void Outline() => Finish();

    // ---- Finishing ----

    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>Shades, textures and outlines what has been drawn, into <see cref="Image"/>.</summary>
    public void Finish()
    {
        int w = Image.Width, h = Image.Height, n = w * h;
        var px = Image.Pixels;
        // A part is a patch of one colour and kind: its own rounded surface.
        bool Same(int i, int j) => _kind[j] != Kind.None && _base[j] == _base[i] && _kind[j] == _kind[i];
        // For the surface's shape, flat marks painted on it (spots, eyes, bubbles) don't count as an edge.
        bool Surface(int i, int j) => Same(i, j) || _kind[j] == Kind.Flat;

        // Distance from each pixel to its part's edge (chamfer), so each part bulges towards the viewer.
        double radius = Math.Max(3, 6.5 * Scale);
        var dist = new float[n];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            if (_kind[i] == Kind.None) continue;
            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                     || !Surface(i, i - 1) || !Surface(i, i + 1) || !Surface(i, i - w) || !Surface(i, i + w);
            dist[i] = edge ? 1 : float.MaxValue;
        }
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            int i = y * w + x;
            if (dist[i] <= 1 || _kind[i] == Kind.None) continue;
            float d = dist[i];
            if (Surface(i, i - 1)) d = Math.Min(d, dist[i - 1] + 1);
            if (Surface(i, i - w)) d = Math.Min(d, dist[i - w] + 1);
            if (Surface(i, i - w - 1)) d = Math.Min(d, dist[i - w - 1] + 1.414f);
            if (Surface(i, i - w + 1)) d = Math.Min(d, dist[i - w + 1] + 1.414f);
            dist[i] = d;
        }
        for (int y = h - 2; y >= 1; y--)
        for (int x = w - 2; x >= 1; x--)
        {
            int i = y * w + x;
            if (dist[i] <= 1 || _kind[i] == Kind.None) continue;
            float d = dist[i];
            if (Surface(i, i + 1)) d = Math.Min(d, dist[i + 1] + 1);
            if (Surface(i, i + w)) d = Math.Min(d, dist[i + w] + 1);
            if (Surface(i, i + w + 1)) d = Math.Min(d, dist[i + w + 1] + 1.414f);
            if (Surface(i, i + w - 1)) d = Math.Min(d, dist[i + w - 1] + 1.414f);
            dist[i] = d;
        }
        var height = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (_kind[i] == Kind.None) continue;
            double t = Math.Min(1, (dist[i] - 0.5) / radius);
            height[i] = (float)Math.Sqrt(Math.Max(0, 1 - (1 - t) * (1 - t))); // a circular profile
        }

        // The light comes from the upper left, a little in front.
        const double lx = -0.48, ly = -0.62, lz = 0.62;
        int shadowReach = Math.Max(1, (int)Math.Round(Scale * 1.5));
        var result = new uint[n];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            var kind = _kind[i];
            if (kind == Kind.None) continue;
            uint c = _base[i];
            if (kind is Kind.Flat or Kind.Baked) { result[i] = c; continue; }

            float H(int j) => Same(i, j) ? height[j] : height[i];
            double gx = (x < w - 1 ? H(i + 1) : height[i]) - (x > 0 ? H(i - 1) : height[i]);
            double gy = (y < h - 1 ? H(i + w) : height[i]) - (y > 0 ? H(i - w) : height[i]);
            double k = radius * 0.55;
            double nx = -gx * k, ny = -gy * k, nz = 1, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double diffuse = Math.Max(0, (nx * lx + ny * ly + nz * lz) / len);
            double light = 0.28 + 0.9 * diffuse;

            // Contact shadow: something drawn later lies over this part close by.
            bool covered = false, overBehind = false;
            for (int dy = -shadowReach; dy <= shadowReach && !covered; dy++)
            for (int dx = -shadowReach; dx <= shadowReach; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                int j = yy * w + xx;
                if (_kind[j] == Kind.Shape && !Same(i, j) && _seq[j] > _seq[i]) { covered = true; break; } // flat marks lie on the surface
            }
            if (covered) light *= 0.72;
            // An inner line where this part lies over another.
            foreach (int j in new[] { i - 1, i + 1, i - w, i + w })
                if (j >= 0 && j < n && _kind[j] == Kind.Shape && !Same(i, j) && _seq[j] < _seq[i]) overBehind = true;
            if (overBehind) light *= 0.78;

            var mat = _mat[i];
            int hash = TextureFactory.Hash(17, x, y);
            switch (mat)
            {
                case Material.Fur: light *= 0.88 + TextureFactory.Hash(5, x, (int)(y / Math.Max(1, Scale * 1.5))) / 255.0 * 0.24; break;
                case Material.Stone: light *= 0.9 + hash / 255.0 * 0.2; break;
                case Material.Cloth: light *= 0.93 + (((x + y / 2) / Math.Max(1, (int)Scale * 2)) % 3 == 0 ? 0.1 : 0); break;
                case Material.Bone: light = 0.45 + light * 0.62; break;
                case Material.Metal: light = Math.Pow(light, 1.6) * 1.18; break;
            }
            // A sharp highlight on shiny things.
            bool spark = (mat is Material.Metal or Material.Gloss) && diffuse > 0.965 && height[i] > 0.35;

            double level = Math.Clamp(light, 0, 1.35) * 4 + (Bayer[(y & 3) * 4 + (x & 3)] / 16.0 - 0.5) * 0.9;
            int step = Math.Clamp((int)Math.Floor(level), 0, 5);
            result[i] = spark ? Mix(c, 0xFFFFFAF0u, 0.75) : Ramp(c, step);
        }

        // The outline: a deep shade of the colour it borders.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            if (_kind[i] != Kind.None) continue;
            uint next = 0;
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                if (j >= 0 && _kind[j] != Kind.None) { next = result[j]; break; }
            if (next != 0) result[i] = Outline(next);
        }
        Array.Copy(result, px, n);
    }

    private static readonly double[] Tones = [0.40, 0.58, 0.76, 0.93, 1.08, 1.22];

    /// <summary>One step of a colour's ramp: darker steps lean cool and violet, lighter ones warm.</summary>
    private static uint Ramp(uint c, int step)
    {
        double f = Tones[step];
        int r = (int)(c >> 16 & 0xFF), g = (int)(c >> 8 & 0xFF), b = (int)(c & 0xFF);
        double rr = r * f, gg = g * f, bb = b * f;
        if (step < 3)
        {
            double t = (3 - step) * 0.09;
            rr = rr * (1 - t) + 34 * t; gg = gg * (1 - t) + 26 * t; bb = bb * (1 - t) + 62 * t;
        }
        else if (step > 3)
        {
            double t = (step - 3) * 0.08;
            rr = rr * (1 - t) + 255 * t; gg = gg * (1 - t) + 236 * t; bb = bb * (1 - t) + 196 * t;
        }
        return Rgb((int)rr, (int)gg, (int)bb);
    }

    private static uint Outline(uint c)
    {
        int r = (int)(c >> 16 & 0xFF), g = (int)(c >> 8 & 0xFF), b = (int)(c & 0xFF);
        return Rgb(r / 5 + 10, g / 5 + 7, b / 5 + 16);
    }

    private static uint Mix(uint a, uint b, double t)
    {
        int Ch(int s) => (int)((a >> s & 0xFF) * (1 - t) + (b >> s & 0xFF) * t);
        return Rgb(Ch(16), Ch(8), Ch(0));
    }
}
