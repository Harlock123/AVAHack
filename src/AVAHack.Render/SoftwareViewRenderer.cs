using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Render;

/// <summary>
/// A column-by-column ray caster for the grid. Each screen column casts one ray through the grid (a DDA walk):
/// the first wall it meets is drawn as a textured strip, the floor and ceiling either side are cast row by row,
/// and any doors the ray passed through are drawn over the top, nearest last. Doors stand in the middle of their
/// cell and slide up; a portcullis's gaps are see-through. Light comes from the party's torch and fades with
/// distance. Everything is plain arithmetic on the CPU, so tests can render real frames with no window or GPU.
/// </summary>
public sealed class SoftwareViewRenderer : IViewRenderer
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 400;

    /// <summary>Horizontal field of view in degrees.</summary>
    public const double FieldOfView = 75;

    private const int MaxSteps = 32;
    private const int MaxDoors = 4;
    private const uint FogR = 6, FogG = 6, FogB = 10;

    private TextureSet _tex;

    /// <summary>The textures drawn with; the view swaps them when the party enters another region.</summary>
    public TextureSet Textures { get => _tex; set => _tex = value; }
    private readonly double _planeLength;
    private readonly double _focal;
    private readonly ColumnHit[] _columns;
    private readonly DoorHit[] _doors = new DoorHit[MaxDoors];
    private readonly float[] _depth;
    private FloorLook[] _floor = [];
    private int _floorW;
    private double _camX, _camY, _dirX, _dirY, _planeX, _planeY, _eye;

    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    /// <summary>
    /// How far behind the party's position the eye sits, along the facing, in cells. Grid crawlers look from near
    /// the back of the cell so the wall ahead is about a cell away, not half of one. Less than 0.5 keeps the eye
    /// inside the party's own cell at every yaw, so no ray starts inside a wall.
    /// </summary>
    public double EyeBack { get; set; } = 0.4;

    /// <summary>How bright the torch is at the eye, before flicker: 1 is full light.</summary>
    public double TorchStrength { get; set; } = 1.05;

    public SoftwareViewRenderer(TextureSet textures, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (width < 16 || height < 16) throw new ArgumentOutOfRangeException(nameof(width), "The view must be at least 16×16.");
        _tex = textures;
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
        _depth = new float[width * height];
        _columns = new ColumnHit[width];
        _planeLength = Math.Tan(FieldOfView * Math.PI / 360);
        _focal = width / 2.0 / _planeLength;
    }

    /// <summary>Torch light at distance <paramref name="d"/>, 0..256.</summary>
    private int LightAt(double d, double flicker)
    {
        double l = TorchStrength / (1 + 0.3 * d * d) * flicker + 0.03;
        return (int)(Math.Clamp(l, 0, 1) * 256);
    }

    private static uint Lit(uint c, int light)
    {
        int dark = 256 - light;
        uint r = (((c >> 16) & 0xFF) * (uint)light + FogR * (uint)dark) >> 8;
        uint g = (((c >> 8) & 0xFF) * (uint)light + FogG * (uint)dark) >> 8;
        uint b = ((c & 0xFF) * (uint)light + FogB * (uint)dark) >> 8;
        return 0xFF000000u | r << 16 | g << 8 | b;
    }

    public static double Flicker(double time) =>
        time <= 0 ? 1 : 0.94 + 0.035 * Math.Sin(time * 11.3) + 0.025 * Math.Sin(time * 23.7 + 1.3);

    public void Render(Level level, CameraPose camera, double time, IReadOnlyList<SpriteInstance>? sprites = null)
    {
        CacheFloor(level);
        int w = Width, h = Height;
        double dirX = camera.DirX, dirY = camera.DirY;
        double px = camera.X - dirX * EyeBack, py = camera.Y - dirY * EyeBack;
        double planeX = -dirY * _planeLength, planeY = dirX * _planeLength;
        double eye = 0.5 + camera.Bob;
        double horizon = h / 2.0;
        double flicker = Flicker(time);
        var pixels = Pixels;
        (_camX, _camY, _dirX, _dirY, _planeX, _planeY, _eye) = (px, py, dirX, dirY, planeX, planeY, eye);

        for (int x = 0; x < w; x++)
        {
            double camX = 2 * (x + 0.5) / w - 1;
            double rdx = dirX + planeX * camX, rdy = dirY + planeY * camX;
            CastColumn(level, x, px, py, rdx, rdy, eye, horizon, flicker, pixels);
        }
        if (sprites is { Count: > 0 }) DrawSprites(sprites, flicker);
    }

    private void CacheFloor(Level level)
    {
        if (_floor.Length != level.Width * level.Height) _floor = new FloorLook[level.Width * level.Height];
        _floorW = level.Width;
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
            _floor[y * level.Width + x] = level.FloorAt(x, y);
    }

    private Texture FloorTexture(double wx, double wy)
    {
        int cx = (int)Math.Floor(wx), cy = (int)Math.Floor(wy);
        if (cx < 0 || cy < 0 || cx >= _floorW) return _tex.Floor;
        int i = cy * _floorW + cx;
        if (i >= _floor.Length) return _tex.Floor;
        return _floor[i] switch
        {
            FloorLook.Pit => _tex.Pit,
            FloorLook.Plate => _tex.Plate,
            FloorLook.Stairs => _tex.Stairs,
            FloorLook.StairsUp => _tex.StairsUp ?? _tex.Stairs,
            FloorLook.Trap => _tex.TrapMark ?? _tex.Plate,
            FloorLook.Spikes => _tex.Spikes ?? _tex.Plate,
            FloorLook.Glyph => _tex.Glyph ?? _tex.Plate,
            FloorLook.Rune => _tex.Rune ?? _tex.Plate,
            FloorLook.Teleporter => _tex.Teleporter ?? _tex.Plate,
            FloorLook.Rubble => _tex.Rubble ?? _tex.Floor,
            _ => _tex.Floor,
        };
    }

    public (double X, double Y, double Depth)? Project(double x, double y, double z)
    {
        double sx = x - _camX, sy = y - _camY;
        double invDet = 1.0 / (_planeX * _dirY - _dirX * _planeY);
        double tx = invDet * (_dirY * sx - _dirX * sy);
        double ty = invDet * (-_planeY * sx + _planeX * sy);
        if (ty <= 0.05) return null;
        double screenX = Width / 2.0 * (1 + tx / ty);
        double screenY = Height / 2.0 + (_eye - z) * _focal / ty;
        return (screenX, screenY, ty);
    }

    /// <summary>Camera-facing pictures, farthest first, each pixel tested against the depth of what is already drawn.</summary>
    private void DrawSprites(IReadOnlyList<SpriteInstance> sprites, double flicker)
    {
        var order = new List<(double Depth, SpriteInstance S)>(sprites.Count);
        foreach (var s in sprites)
            if (Project(s.X, s.Y, s.Z) is { } p) order.Add((p.Depth, s));
        order.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        int w = Width, h = Height;
        foreach (var (depth, s) in order)
        {
            var bottom = Project(s.X, s.Y, s.Z)!.Value;
            double scale = _focal / depth;
            double sw = s.Width * scale, sh = s.Height * scale;
            double left = bottom.X - sw / 2, top = bottom.Y - sh;
            int x0 = Math.Max(0, (int)Math.Ceiling(left - 0.5)), x1 = Math.Min(w - 1, (int)Math.Floor(left + sw - 0.5));
            int y0 = Math.Max(0, (int)Math.Ceiling(top - 0.5)), y1 = Math.Min(h - 1, (int)Math.Floor(bottom.Y - 0.5));
            if (x1 < x0 || y1 < y0) continue;
            int light = s.SelfLit ? 256 : LightAt(depth, flicker);
            light = Math.Min(256, (int)(light * (1 + s.Glow)));
            var img = s.Image;
            float d = (float)depth;
            for (int y = y0; y <= y1; y++)
            {
                int iy = (int)((y + 0.5 - top) / sh * img.Height);
                if (iy < 0 || iy >= img.Height) continue;
                int row = y * w;
                for (int x = x0; x <= x1; x++)
                {
                    if (_depth[row + x] <= d) continue;
                    int ix = (int)((x + 0.5 - left) / sw * img.Width);
                    if (ix < 0 || ix >= img.Width) continue;
                    uint c = img.Pixels[iy * img.Width + ix];
                    if (c >> 24 == 0) continue;
                    if (s.TintAmount > 0) c = Mix(c, s.Tint, s.TintAmount);
                    Pixels[row + x] = Lit(c, light);
                    _depth[row + x] = d;
                }
            }
        }
    }

    private static uint Mix(uint a, uint b, double t)
    {
        uint Ch(int shift) => (uint)((a >> shift & 0xFF) * (1 - t) + (b >> shift & 0xFF) * t) & 0xFF;
        return 0xFF000000u | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    private void CastColumn(Level level, int x, double px, double py, double rdx, double rdy,
                            double eye, double horizon, double flicker, uint[] pixels)
    {
        int w = Width, h = Height;
        int mapX = (int)Math.Floor(px), mapY = (int)Math.Floor(py);
        double deltaX = rdx == 0 ? 1e30 : Math.Abs(1 / rdx);
        double deltaY = rdy == 0 ? 1e30 : Math.Abs(1 / rdy);
        int stepX = rdx < 0 ? -1 : 1, stepY = rdy < 0 ? -1 : 1;
        double sideX = rdx < 0 ? (px - mapX) * deltaX : (mapX + 1 - px) * deltaX;
        double sideY = rdy < 0 ? (py - mapY) * deltaY : (mapY + 1 - py) * deltaY;

        int doorCount = 0;
        bool stoppedAtDoor = false;
        if (level.DoorAt(mapX, mapY) is { } here) TryDoor(here, px, py, rdx, rdy, ref doorCount, ref stoppedAtDoor);

        double dist = 1e30;
        int side = 0, prevX = mapX, prevY = mapY;
        bool hitWall = false;
        for (int i = 0; i < MaxSteps && !stoppedAtDoor; i++)
        {
            prevX = mapX;
            prevY = mapY;
            if (sideX < sideY)
            {
                sideX += deltaX;
                mapX += stepX;
                side = 0;
            }
            else
            {
                sideY += deltaY;
                mapY += stepY;
                side = 1;
            }
            var kind = level.KindAt(mapX, mapY);
            if (kind == CellKind.Wall || level.LooksLikeWall(mapX, mapY))
            {
                dist = side == 0 ? sideX - deltaX : sideY - deltaY;
                hitWall = true;
                break;
            }
            if (kind == CellKind.Door) TryDoor(level.DoorAt(mapX, mapY)!, px, py, rdx, rdy, ref doorCount, ref stoppedAtDoor);
        }

        ref var col = ref _columns[x];
        col = default;

        // The far boundary: the wall, or a shut wooden door that ends the ray.
        if (stoppedAtDoor) dist = _doors[doorCount - 1].Dist;
        dist = Math.Max(dist, 0.02);
        double topExact = horizon - (1 - eye) * _focal / dist;
        double bottomExact = horizon + eye * _focal / dist;
        int top = Math.Max(0, (int)Math.Ceiling(topExact - 0.5));
        int bottom = Math.Min(h - 1, (int)Math.Floor(bottomExact - 0.5));

        if (hitWall && !stoppedAtDoor)
        {
            var face = side == 0 ? (stepX > 0 ? Dir.East : Dir.West) : (stepY > 0 ? Dir.South : Dir.North);
            var from = new Cell(prevX, prevY);
            var lever = level.LeverAt(from, face);
            var tex = lever is not null ? (lever.Down ? _tex.LeverDown : _tex.LeverUp)
                    : level.KindAt(from) == CellKind.Door ? _tex.DoorFrame
                    : _tex.Wall;
            double hit = side == 0 ? py + dist * rdy : px + dist * rdx;
            double u = hit - Math.Floor(hit);
            if ((side == 0 && rdx < 0) || (side == 1 && rdy > 0)) u = 1 - u;
            int texX = (int)(u * tex.Side) & tex.SideMask;
            int light = LightAt(dist, flicker);
            if (side == 0) light = light * 13 / 16;
            double span = bottomExact - topExact;
            var tp = tex.Pixels;
            float fd = (float)dist;
            for (int y = top; y <= bottom; y++)
            {
                int texY = (int)((y + 0.5 - topExact) / span * tex.Side) & tex.SideMask;
                pixels[y * w + x] = Lit(tp[texY * tex.Side + texX], light);
                _depth[y * w + x] = fd;
            }
            col.HasWall = true;
            col.WallTop = top;
            col.WallBottom = bottom;
            col.Wall = new WallFaceTarget(from.X, from.Y, face);
        }
        else if (!stoppedAtDoor)
        {
            // Nothing within reach: darkness.
            for (int y = top; y <= bottom; y++)
            {
                pixels[y * w + x] = 0xFF000000u | FogR << 16 | FogG << 8 | FogB;
                _depth[y * w + x] = float.MaxValue;
            }
        }

        // Ceiling above the wall and floor below it, cast row by row.
        var ceilTex = _tex.Ceiling;
        var ceil = ceilTex.Pixels;
        int cs = ceilTex.Side, cm = ceilTex.SideMask;
        for (int y = 0; y < top; y++)
        {
            double rowDist = (1 - eye) * _focal / (horizon - (y + 0.5));
            if (rowDist <= 0) continue;
            double wx = px + rowDist * rdx, wy = py + rowDist * rdy;
            int tx = (int)((wx - Math.Floor(wx)) * cs) & cm;
            int ty = (int)((wy - Math.Floor(wy)) * cs) & cm;
            pixels[y * w + x] = Lit(ceil[ty * cs + tx], LightAt(rowDist, flicker) * 7 / 8);
            _depth[y * w + x] = (float)rowDist;
        }
        for (int y = bottom + 1; y < h; y++)
        {
            double rowDist = eye * _focal / ((y + 0.5) - horizon);
            if (rowDist <= 0) continue;
            double wx = px + rowDist * rdx, wy = py + rowDist * rdy;
            var ft = FloorTexture(wx, wy);
            int tx = (int)((wx - Math.Floor(wx)) * ft.Side) & ft.SideMask;
            int ty = (int)((wy - Math.Floor(wy)) * ft.Side) & ft.SideMask;
            pixels[y * w + x] = Lit(ft.Pixels[ty * ft.Side + tx], LightAt(rowDist, flicker));
            _depth[y * w + x] = (float)rowDist;
        }

        // Doors, farthest first, so nearer ones cover farther ones.
        for (int i = doorCount - 1; i >= 0; i--)
        {
            ref var d = ref _doors[i];
            double dd = Math.Max(d.Dist, 0.02);
            double dTop = horizon - (1 - eye) * _focal / dd;
            double dBottom = horizon + (eye - d.Door.Open) * _focal / dd;
            int y0 = Math.Max(0, (int)Math.Ceiling(dTop - 0.5));
            int y1 = Math.Min(h - 1, (int)Math.Floor(dBottom - 0.5));
            if (y1 < y0) continue;
            var tex = d.Door.Kind == DoorKind.Portcullis ? _tex.Portcullis : _tex.Door;
            var tp = tex.Pixels;
            int texX = (int)(d.U * tex.Side) & tex.SideMask;
            int light = LightAt(dd, flicker);
            if (!d.Door.AlongX) light = light * 13 / 16;
            for (int y = y0; y <= y1; y++)
            {
                // Height on the panel: z runs 0 (floor) to 1 (ceiling); the raised panel shows v from Open to 1.
                double z = eye + (horizon - (y + 0.5)) * dd / _focal;
                double v = 1 + d.Door.Open - z;
                int texY = (int)(v * tex.Side) & tex.SideMask;
                uint c = tp[texY * tex.Side + texX];
                if (c >> 24 == 0) continue;
                pixels[y * w + x] = Lit(c, light);
                _depth[y * w + x] = (float)dd;
            }
            if (i == 0)
            {
                col.HasDoor = true;
                col.DoorTop = y0;
                col.DoorBottom = y1;
                col.Door = new DoorTarget(d.Door.Cell.X, d.Door.Cell.Y);
            }
        }
    }

    /// <summary>Records where the ray crosses this door's panel, if it does, in front of the eye.</summary>
    private void TryDoor(Door door, double px, double py, double rdx, double rdy, ref int count, ref bool stop)
    {
        if (count >= MaxDoors || stop) return;
        double t, along;
        if (door.AlongX)
        {
            if (rdy == 0) return;
            t = (door.Cell.Y + 0.5 - py) / rdy;
            along = px + t * rdx - door.Cell.X;
        }
        else
        {
            if (rdx == 0) return;
            t = (door.Cell.X + 0.5 - px) / rdx;
            along = py + t * rdy - door.Cell.Y;
        }
        if (t <= 0.001 || along < 0 || along >= 1) return;
        if (door.Open >= 1) return; // fully raised: nothing to draw
        // Seen from the far side, flip the texture so it isn't mirrored.
        bool flip = door.AlongX ? rdy > 0 : rdx < 0;
        _doors[count++] = new DoorHit { Door = door, Dist = t, U = flip ? 1 - along : along };
        // A shut wooden door hides everything behind it.
        if (door.Kind == DoorKind.Hand && door.Open <= 0) stop = true;
    }

    public WorldTarget? Pick(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        ref var col = ref _columns[x];
        if (col.HasDoor && y >= col.DoorTop && y <= col.DoorBottom) return col.Door;
        if (col.HasWall && y >= col.WallTop && y <= col.WallBottom) return col.Wall;
        return null;
    }

    private struct DoorHit
    {
        public Door Door;
        public double Dist;
        public double U;
    }

    private struct ColumnHit
    {
        public bool HasWall, HasDoor;
        public int WallTop, WallBottom, DoorTop, DoorBottom;
        public WallFaceTarget? Wall;
        public DoorTarget? Door;
    }
}
