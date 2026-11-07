using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;

namespace AVAHack.World;

public static partial class Generator
{
    private sealed record Room(int X, int Y, int W, int H, Vault? Vault)
    {
        public Cell Centre => new(X + W / 2, Y + H / 2);
        public bool Contains(Cell c) => c.X >= X && c.Y >= Y && c.X < X + W && c.Y < Y + H;
    }

    private sealed class Builder(ContentDb db, Pcg32 rng, int depth, GenOptions o)
    {
        private readonly CellKind[] _cells = Enumerable.Repeat(CellKind.Wall, Size * Size).ToArray();
        private readonly bool[] _protected = new bool[Size * Size];
        private readonly List<Room> _rooms = new();
        private readonly List<(Cell Outside, Room Room)> _joins = new();
        private readonly List<(Cell Cell, DoorKind Kind, bool Open, string? Key, int Dc)> _doors = new();
        private readonly List<(Cell Cell, Dir Face, List<Cell> Targets)> _levers = new();
        private readonly List<(Cell Cell, PlateMode Mode, List<Cell> Targets)> _plates = new();
        private readonly List<(Cell Cell, string Table, int Dc)> _chests = new();
        private readonly List<(Cell Cell, string Text)> _notes = new();
        private readonly List<Spawn> _spawns = new();
        private readonly List<Trap> _traps = new();
        private readonly List<Pit> _pits = new();
        private readonly List<(Cell Cell, int Dc)> _secrets = new();
        private readonly List<Cell> _illusory = new();
        private readonly List<(Cell Cell, Items.Item Item)> _items = new();
        private readonly List<Cell> _altars = new();
        private readonly HashSet<Cell> _taken = new();
        private Cell _start, _down;
        private Cell? _landing;
        private Dir _facing = Dir.East;
        private readonly string _theme = ThemeFor(db, depth, o.MaxDepth);
        private RegionDef Region => db.Region(_theme);
        private string? _boss;
        private Cell? _waystone;
        private readonly List<Cell> _valves = new();
        private readonly Dictionary<Cell, string> _stations = new();

        /// <summary>Make the simplest level that works: no vaults, locks, secrets or traps.</summary>
        public bool Plain { get; init; }

        private int Idx(Cell c) => c.Y * Size + c.X;
        private CellKind At(Cell c) => c.X <= 0 || c.Y <= 0 || c.X >= Size - 1 || c.Y >= Size - 1 ? CellKind.Wall : _cells[Idx(c)];
        private bool Inside(Cell c) => c.X > 0 && c.Y > 0 && c.X < Size - 1 && c.Y < Size - 1;
        private void Set(Cell c, CellKind k) { if (Inside(c)) _cells[Idx(c)] = k; }
        private int Roll(int n) => n <= 0 ? 0 : rng.Next(n);
        private T Pick<T>(IReadOnlyList<T> list) => list[Roll(list.Count)];

        public bool Build()
        {
            if (!Plain) PlaceVaults();
            PlaceRooms(Plain ? 6 : 8 + Roll(6));
            if (_rooms.Count < 3) return false;
            Connect();
            AddRoomDoors();
            PlaceStairs();
            Specials();
            if (!Plain)
            {
                Barriers();
                Secrets();
                Traps();
                Pits();
            }
            Monsters();
            Loot();
            Flavour();
            return true;
        }

        // ---- Rooms and vaults ----

        private void PlaceVaults()
        {
            var fits = Vault.All.Where(v => v.Themes.Contains(_theme)).OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
            int want = Math.Min(3, 1 + Roll(3));
            for (int n = 0; n < want && fits.Count > 0; n++)
            {
                var v = fits[Roll(fits.Count)];
                fits.Remove(v);
                for (int tries = 0; tries < 60; tries++)
                {
                    int x = 2 + Roll(Size - v.Width - 3), y = 2 + Roll(Size - v.Height - 3);
                    if (!Free(x, y, v.Width, v.Height)) continue;
                    StampVault(v, x, y);
                    break;
                }
            }
        }

        private bool Free(int x, int y, int w, int h)
        {
            if (x < 1 || y < 1 || x + w > Size - 1 || y + h > Size - 1) return false;
            return _rooms.All(r => x + w + 1 < r.X || r.X + r.W + 1 < x || y + h + 1 < r.Y || r.Y + r.H + 1 < y);
        }

        private void StampVault(Vault v, int ox, int oy)
        {
            var room = new Room(ox, oy, v.Width, v.Height, v);
            _rooms.Add(room);
            for (int y = 0; y < v.Height; y++)
            for (int x = 0; x < v.Width; x++)
            {
                var c = new Cell(ox + x, oy + y);
                _protected[Idx(c)] = true;
                char ch = v.Rows[y][x];
                Set(c, ch switch { '#' => CellKind.Wall, '+' or 'P' => CellKind.Door, _ => CellKind.Floor });
                if (ch == '+') _doors.Add((c, DoorKind.Hand, false, null, 0));
                if (ch == 'P') _doors.Add((c, DoorKind.Portcullis, false, null, 0));
                if (ch == '^') { _pits.Add(new Pit(c, c, 0) { ToNextLevel = depth < o.MaxDepth }); _taken.Add(c); }
                if (ch != '#') _taken.Add(c);
            }
            // Where corridors meet it: the cell just outside each connector.
            foreach (var con in v.Connectors)
            {
                var (cx, cy) = v.ConnectorCell(con);
                var inside = new Cell(ox + cx, oy + cy);
                _joins.Add((inside.Step(con.Edge), room));
            }
            Cell L(string[] w, int i) => new(ox + int.Parse(w[i], System.Globalization.CultureInfo.InvariantCulture), oy + int.Parse(w[i + 1], System.Globalization.CultureInfo.InvariantCulture));
            foreach (var line in v.Lines)
            {
                var w = line.Words;
                switch (line.Kind)
                {
                    case "lever": _levers.Add((L(w, 0), DirExtensions.Parse(w[2]), [L(w, 4)])); break;
                    case "plate": _plates.Add((L(w, 0), w[2] == "hold" ? PlateMode.Hold : PlateMode.Toggle, [L(w, 4)])); break;
                    case "chest":
                        int dc = Array.IndexOf(w, "locked") is >= 0 and var li ? int.Parse(w[li + 1], System.Globalization.CultureInfo.InvariantCulture) + depth / 2 : 0;
                        _chests.Add((L(w, 0), w.Contains("rich") || depth >= 5 ? "chest.rich" : "chest.cellar", dc));
                        break;
                    case "monster":
                        var table = SpawnTable(db, _theme, depth).Where(t => !db.Creature(t.Id).Has("pack") || true).ToList();
                        _spawns.Add(new Spawn(Weighted(table), L(w, 0), DirExtensions.Parse("south")) { Level = MonsterLevel() });
                        break;
                    case "trap":
                    {
                        var kind = Enum.Parse<TrapKind>(w[0], true);
                        var at = L(w, 1);
                        Dir face = w.Length > 3 && w[3] != "->" ? DirExtensions.Parse(w[3]) : Dir.North;
                        int arrow = Array.IndexOf(w, "->");
                        Cell? target = arrow >= 0 ? L(w, arrow + 1) : null;
                        var area = kind switch
                        {
                            TrapKind.AntiMagic => Around(at, 1).ToList(),
                            TrapKind.FloodingRoom => RoomCells(room).ToList(),
                            _ => new List<Cell>(),
                        };
                        _traps.Add(MakeTrap(kind, at, face, target, area));
                        break;
                    }
                    case "altar": _altars.Add(L(w, 0)); break;
                    case "secret": _secrets.Add((L(w, 0), 12 + depth / 2)); break;
                    case "illusory": _illusory.Add(L(w, 0)); break;
                    case "note": _notes.Add((L(w, 0), string.Join(' ', line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(2)))); break;
                }
            }
        }

        private IEnumerable<Cell> RoomCells(Room r)
        {
            for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
                if (At(new Cell(x, y)) == CellKind.Floor) yield return new Cell(x, y);
        }

        private static IEnumerable<Cell> Around(Cell c, int r)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                yield return new Cell(c.X + dx, c.Y + dy);
        }

        private void PlaceRooms(int count)
        {
            int placed = _rooms.Count(r => r.Vault is null);
            for (int tries = 0; tries < 400 && placed < count; tries++)
            {
                int w = 2 + Roll(6), h = 2 + Roll(6);
                int x = 1 + Roll(Size - w - 2), y = 1 + Roll(Size - h - 2);
                if (!Free(x, y, w, h)) continue;
                var r = new Room(x, y, w, h, null);
                _rooms.Add(r);
                foreach (var c in RoomCells(r with { }).ToList()) { }
                for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Set(new Cell(xx, yy), CellKind.Floor);
                _joins.Add((r.Centre, r));
                placed++;
            }
        }

        // ---- Corridors ----

        private void Connect()
        {
            var nodes = _joins.ToList();
            // Prim's tree over the join points, then a fifth more edges for loops.
            var inTree = new List<int> { 0 };
            var edges = new List<(int, int)>();
            var left = Enumerable.Range(1, nodes.Count - 1).ToList();
            while (left.Count > 0)
            {
                (int a, int b, int d) best = (-1, -1, int.MaxValue);
                foreach (int i in inTree)
                foreach (int j in left)
                {
                    int d = nodes[i].Outside.Distance(nodes[j].Outside) + (nodes[i].Room == nodes[j].Room ? 1000 : 0);
                    if (d < best.d) best = (i, j, d);
                }
                edges.Add((best.a, best.b));
                inTree.Add(best.b);
                left.Remove(best.b);
            }
            int extra = Math.Max(1, nodes.Count / 5);
            for (int i = 0; i < extra; i++) edges.Add((Roll(nodes.Count), Roll(nodes.Count)));
            foreach (var (a, b) in edges)
                if (a != b && nodes[a].Room != nodes[b].Room) Carve(nodes[a].Outside, nodes[b].Outside, Roll(100) < 15);
            // A vault's other connectors must open onto something too.
            foreach (var (cell, room) in _joins.Where(j => j.Room.Vault is not null))
                if (At(cell) == CellKind.Wall) Carve(cell, NearestFloorOutside(cell, room), false);
        }

        private Cell NearestFloorOutside(Cell from, Room room)
        {
            var best = from;
            int bd = int.MaxValue;
            for (int y = 1; y < Size - 1; y++)
            for (int x = 1; x < Size - 1; x++)
            {
                var c = new Cell(x, y);
                if (At(c) != CellKind.Floor || room.Contains(c) || _protected[Idx(c)]) continue;
                int d = c.Distance(from);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>Cuts the cheapest path from a to b: reusing floor is cheap, cutting rock dearer; vaults are not cut.</summary>
        private void Carve(Cell a, Cell b, bool wide)
        {
            var dist = new Dictionary<Cell, int> { [a] = 0 };
            var from = new Dictionary<Cell, Cell>();
            var open = new PriorityQueue<Cell, int>();
            open.Enqueue(a, 0);
            while (open.TryDequeue(out var c, out _))
            {
                if (c == b) break;
                foreach (var d in Enum.GetValues<Dir>())
                {
                    var n = c.Step(d);
                    if (!Inside(n) || (_protected[Idx(n)] && n != b)) continue;
                    int step = At(n) == CellKind.Floor ? 1 : 3;
                    int nd = dist[c] + step;
                    if (dist.TryGetValue(n, out int old) && old <= nd) continue;
                    dist[n] = nd;
                    from[n] = c;
                    open.Enqueue(n, nd + n.Distance(b));
                }
            }
            if (!dist.ContainsKey(b)) return;
            var at = b;
            while (true)
            {
                if (!_protected[Idx(at)]) Set(at, CellKind.Floor);
                if (wide && from.TryGetValue(at, out var prev) && prev.X != at.X)
                {
                    var side = new Cell(at.X, at.Y + 1);
                    if (Inside(side) && !_protected[Idx(side)] && side.Y < Size - 2) Set(side, CellKind.Floor);
                }
                if (at == a) break;
                at = from[at];
            }
        }

        private void AddRoomDoors()
        {
            foreach (var r in _rooms.Where(r => r.Vault is null && r.W * r.H >= 6))
            {
                for (int y = r.Y - 1; y <= r.Y + r.H; y++)
                for (int x = r.X - 1; x <= r.X + r.W; x++)
                {
                    var c = new Cell(x, y);
                    bool edge = x == r.X - 1 || x == r.X + r.W || y == r.Y - 1 || y == r.Y + r.H;
                    if (!edge || !Inside(c) || At(c) != CellKind.Floor || _protected[Idx(c)] || Roll(100) >= 30) continue;
                    bool ns = At(c.Step(Dir.West)) == CellKind.Wall && At(c.Step(Dir.East)) == CellKind.Wall && At(c.Step(Dir.North)) == CellKind.Floor && At(c.Step(Dir.South)) == CellKind.Floor;
                    bool ew = At(c.Step(Dir.North)) == CellKind.Wall && At(c.Step(Dir.South)) == CellKind.Wall && At(c.Step(Dir.West)) == CellKind.Floor && At(c.Step(Dir.East)) == CellKind.Floor;
                    if (!(ns || ew) || _doors.Any(d => d.Cell.Distance(c) <= 1)) continue;
                    Set(c, CellKind.Door);
                    _doors.Add((c, DoorKind.Hand, false, null, 0));
                    _taken.Add(c);
                }
            }
        }

        // ---- Stairs ----

        private void PlaceStairs()
        {
            var plainRooms = _rooms.Where(r => r.Vault is null).ToList();
            var first = plainRooms[0];
            _start = first.Centre;
            _taken.Add(_start);
            var dist = Flood(_start, new HashSet<Cell>());
            var far = dist.Where(kv => plainRooms.Any(r => r.Contains(kv.Key)) && !_taken.Contains(kv.Key) && At(kv.Key) == CellKind.Floor)
                          .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X).FirstOrDefault();
            _down = far.Key == default ? NearestFloorOutside(_start, first) : far.Key;
            _taken.Add(_down);
            var landingRoom = plainRooms.Where(r => !r.Contains(_start) && !r.Contains(_down)).ToList();
            if (landingRoom.Count > 0)
            {
                var lr = Pick(landingRoom);
                _landing = lr.Centre;
                _taken.Add(lr.Centre);
            }
            // Face the way out of the start room.
            _facing = Enum.GetValues<Dir>().FirstOrDefault(d => At(_start.Step(d)) == CellKind.Floor);
        }

        /// <summary>Distances over walkable cells (floor and doors) from a start, with some cells blocked.</summary>
        private Dictionary<Cell, int> Flood(Cell from, HashSet<Cell> blocked, HashSet<Cell>? extra = null)
        {
            var d = new Dictionary<Cell, int> { [from] = 0 };
            var q = new Queue<Cell>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var dir in Enum.GetValues<Dir>())
                {
                    var n = c.Step(dir);
                    if (d.ContainsKey(n) || blocked.Contains(n) || (At(n) == CellKind.Wall && extra?.Contains(n) != true)) continue;
                    d[n] = d[c] + 1;
                    q.Enqueue(n);
                }
            }
            return d;
        }

        private List<Cell> PathTo(Cell from, Cell to)
        {
            var prev = new Dictionary<Cell, Cell>();
            var q = new Queue<Cell>();
            q.Enqueue(from);
            prev[from] = from;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) break;
                foreach (var dir in Enum.GetValues<Dir>())
                {
                    var n = c.Step(dir);
                    if (prev.ContainsKey(n) || At(n) == CellKind.Wall) continue;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            var path = new List<Cell>();
            if (!prev.ContainsKey(to)) return path;
            for (var c = to; c != from; c = prev[c]) path.Add(c);
            path.Add(from);
            path.Reverse();
            return path;
        }
    
        // ---- Locks along the way ----

        private int _keys;

        private void Barriers()
        {
            var path = PathTo(_start, _down);
            if (path.Count < 8) return;
            int want = Math.Min(3, depth == 1 ? 1 : 1 + Roll(2) + (depth >= 6 ? 1 : 0));
            var candidates = new List<int>();
            for (int i = 3; i < path.Count - 3; i++)
            {
                var c = path[i];
                if (At(c) != CellKind.Floor || _protected[Idx(c)] || _taken.Contains(c) || _rooms.Any(r => r.Contains(c))) continue;
                bool ns = At(c.Step(Dir.West)) == CellKind.Wall && At(c.Step(Dir.East)) == CellKind.Wall;
                bool ew = At(c.Step(Dir.North)) == CellKind.Wall && At(c.Step(Dir.South)) == CellKind.Wall;
                if (ns == ew) continue;
                if (Flood(_start, [c]).ContainsKey(_down)) continue; // not a chokepoint
                candidates.Add(i);
            }
            if (candidates.Count == 0) return;
            var chosen = new List<Cell>();
            for (int k = 0; k < want; k++)
            {
                int i = candidates[Math.Min(candidates.Count - 1, (k + 1) * candidates.Count / (want + 1))];
                var c = path[i];
                if (!chosen.Contains(c) && chosen.All(x => x.Distance(c) > 3)) chosen.Add(c);
            }
            for (int k = 0; k < chosen.Count; k++)
            {
                var barrier = chosen[k];
                var region = Flood(_start, chosen.Skip(k).ToHashSet()).Keys.Where(c => At(c) == CellKind.Floor && !_taken.Contains(c) && !_protected[Idx(c)]).ToList();
                var roomCells = region.Where(c => _rooms.Any(r => r.Vault is null && r.Contains(c)) && c != _start).ToList();
                if (roomCells.Count == 0) roomCells = region.Where(c => c != _start).ToList();
                if (roomCells.Count == 0) continue;
                // The mechanism goes as far from the barrier as the region allows: a detour, not a step aside.
                var far = roomCells.OrderByDescending(c => c.Distance(barrier)).Take(Math.Max(1, roomCells.Count / 3)).ToList();
                Set(barrier, CellKind.Door);
                _taken.Add(barrier);
                switch (Roll(3))
                {
                    case 0:
                    {
                        string key = $"key-{depth}-{++_keys}";
                        _doors.Add((barrier, DoorKind.Hand, false, key, 14 + depth));
                        var at = Pick(far);
                        _items.Add((at, new Item { BaseId = "key", KeyId = key }));
                        _taken.Add(at);
                        break;
                    }
                    case 1:
                    {
                        _doors.Add((barrier, DoorKind.Portcullis, false, null, 0));
                        var walls = far.SelectMany(c => Enum.GetValues<Dir>().Where(d => At(c.Step(d)) == CellKind.Wall).Select(d => (c, d))).ToList();
                        if (walls.Count == 0) goto default;
                        var (cell, face) = Pick(walls);
                        _levers.Add((cell, face, [barrier]));
                        _taken.Add(cell);
                        break;
                    }
                    default:
                    {
                        if (_doors.Count == 0 || _doors[^1].Cell != barrier) _doors.Add((barrier, DoorKind.Portcullis, false, null, 0));
                        var at = Pick(far);
                        _plates.Add((at, PlateMode.Toggle, [barrier]));
                        _taken.Add(at);
                        break;
                    }
                }
            }
        }

        // ---- Secrets ----

        private void Secrets()
        {
            int want = 2 + Roll(3);
            for (int n = 0; n < want + 1; n++)
            {
                bool illusion = n == want;
                for (int tries = 0; tries < 120; tries++)
                {
                    var f = new Cell(2 + Roll(Size - 4), 2 + Roll(Size - 4));
                    if (At(f) != CellKind.Floor || _protected[Idx(f)]) continue;
                    var d = Pick(Enum.GetValues<Dir>());
                    var w = f.Step(d);
                    if (At(w) != CellKind.Wall || _protected[Idx(w)] || !Inside(w)) continue;
                    var side = d is Dir.North or Dir.South ? (Dir.West, Dir.East) : (Dir.North, Dir.South);
                    if (At(w.Step(side.Item1)) != CellKind.Wall || At(w.Step(side.Item2)) != CellKind.Wall) continue;
                    int sw = illusion ? 1 : 2 + Roll(2), sh = illusion ? 1 : 2 + Roll(2);
                    var cells = new List<Cell>();
                    var inner = w.Step(d);
                    // The little room behind, laid out away from the wall.
                    for (int a = 0; a < sh; a++)
                    for (int b = 0; b < sw; b++)
                    {
                        var step = inner;
                        for (int i = 0; i < a; i++) step = step.Step(d);
                        int off = b - sw / 2;
                        var c = d is Dir.North or Dir.South ? new Cell(step.X + off, step.Y) : new Cell(step.X, step.Y + off);
                        cells.Add(c);
                    }
                    bool ok = cells.All(c => Inside(c) && At(c) == CellKind.Wall && !_protected[Idx(c)]
                        && Enum.GetValues<Dir>().All(dd => cells.Contains(c.Step(dd)) || c.Step(dd) == w || (At(c.Step(dd)) == CellKind.Wall && !_protected[Idx(c.Step(dd))])));
                    ok &= cells.SelectMany(c => Around(c, 1)).Where(c => !cells.Contains(c) && c != w).All(c => At(c) == CellKind.Wall);
                    if (!ok) continue;
                    foreach (var c in cells) { Set(c, CellKind.Floor); _protected[Idx(c)] = true; _taken.Add(c); }
                    _protected[Idx(w)] = true;
                    _taken.Add(w);
                    if (illusion)
                    {
                        Set(w, CellKind.Floor);
                        _illusory.Add(w);
                        foreach (var it in new LootGenerator(db).Roll("chest.cellar", depth, rng)) _items.Add((cells[0], it));
                    }
                    else
                    {
                        _secrets.Add((w, 12 + depth / 2 + Roll(4)));
                        _chests.Add((cells[^1], "chest.rich", 0));
                    }
                    break;
                }
            }
        }

        // ---- Traps ----

        private Trap MakeTrap(TrapKind kind, Cell at, Dir face, Cell? target, IReadOnlyList<Cell> area) => new()
        {
            Kind = kind, Cell = at, Facing = face, Target = target, Area = area, Depth = depth,
            HideDc = kind switch { TrapKind.Spinner => 15 + depth, TrapKind.SwingingBlade or TrapKind.SpikeFloor or TrapKind.AntiMagic => 0, _ => 10 + depth + Roll(4) },
            DisarmDc = 12 + depth + Roll(4),
            Variant = kind switch { TrapKind.Tripwire => Roll(2) == 0 ? "darts" : "alarm", TrapKind.GasVent => Roll(3) == 0 ? "sleep" : "poison", _ => "" },
        };

        private void Traps()
        {
            int budget = Math.Min(14, (int)Math.Round((2 + depth * 0.8) * o.Traps));
            var kinds = TrapDepths.Where(t => t.MinDepth <= depth).Select(t => t.Kind).ToList();
            var path = PathTo(_start, _down).ToHashSet();
            int deadlyOnPath = 0;
            var floor = new List<Cell>();
            for (int y = 1; y < Size - 1; y++)
            for (int x = 1; x < Size - 1; x++)
            {
                var c = new Cell(x, y);
                if (At(c) == CellKind.Floor && !_taken.Contains(c) && !_protected[Idx(c)] && c.Distance(_start) > 3 && c.Distance(_down) > 1
                    && (_landing is not { } l || c.Distance(l) > 1)) floor.Add(c);
            }
            for (int n = 0; n < budget && floor.Count > 0; n++)
            {
                var kind = Pick(kinds);
                for (int tries = 0; tries < 40; tries++)
                {
                    var c = Pick(floor);
                    if (_taken.Contains(c)) continue;
                    bool inRoom = _rooms.Any(r => r.Contains(c));
                    if (kind is TrapKind.CollapsingCeiling or TrapKind.AntiMagic && !inRoom) continue;
                    if (kind is TrapKind.Tripwire or TrapKind.DartWall or TrapKind.SwingingBlade or TrapKind.SpikeFloor && inRoom && Roll(3) > 0) continue;
                    Dir face = Dir.North;
                    Cell? target = null;
                    if (kind is TrapKind.FireSpout or TrapKind.RollingBoulder)
                    {
                        var line = Enum.GetValues<Dir>().Select(d => (d, Line(c, d))).Where(x => x.Item2 >= 4).ToList();
                        if (line.Count == 0) continue;
                        (face, int len) = Pick(line);
                        if (kind == TrapKind.RollingBoulder)
                        {
                            // The boulder waits up the slope behind the plate, and rolls down past it.
                            var back = c;
                            for (int i = 0; i < Math.Min(len, 6) && At(back.Step(face.Opposite())) == CellKind.Floor; i++) back = back.Step(face.Opposite());
                            if (back == c) continue;
                            target = back;
                        }
                    }
                    if (kind == TrapKind.Teleporter)
                    {
                        var dests = _rooms.Where(r => r.Vault is null && !r.Contains(c)).Select(r => r.Centre).Where(x => !_taken.Contains(x) && At(x) == CellKind.Floor).ToList();
                        if (dests.Count == 0) continue;
                        target = Pick(dests);
                    }
                    var trap = MakeTrap(kind, c, face, target, kind == TrapKind.AntiMagic ? Around(c, 1).Where(x => At(x) == CellKind.Floor).ToList() : []);
                    if (trap.Deadly && path.Contains(c))
                    {
                        // Never more than two deadly traps on the way without a warning in front of them.
                        if (++deadlyOnPath > 2)
                        {
                            var before = Enum.GetValues<Dir>().Select(d => c.Step(d)).FirstOrDefault(x => path.Contains(x) && At(x) == CellKind.Floor && !_taken.Contains(x));
                            if (before == default) continue;
                            _notes.Add((before, Hint(kind)));
                            _taken.Add(before);
                        }
                    }
                    _traps.Add(trap);
                    _taken.Add(c);
                    break;
                }
            }
            foreach (var a in _altars) _traps.Add(MakeTrap(TrapKind.CursedAltar, a, Dir.North, null, []));
        }

        private static string Hint(TrapKind k) => k switch
        {
            TrapKind.FireSpout => "The walls ahead are scorched black.",
            TrapKind.SwingingBlade => "Long, regular cuts score the walls.",
            TrapKind.RollingBoulder => "The floor is worn smooth in a groove.",
            TrapKind.CollapsingCeiling => "Cracks run across the ceiling here.",
            _ => "Bones, and a dark stain on the floor ahead.",
        };

        private int Line(Cell c, Dir d)
        {
            int n = 0;
            for (var x = c.Step(d); At(x) == CellKind.Floor && n < 10; x = x.Step(d)) n++;
            return n;
        }

        private void Pits()
        {
            if (depth >= o.MaxDepth) return;
            var path = PathTo(_start, _down).ToHashSet();
            int want = 1 + Roll(2);
            for (int n = 0, tries = 0; n < want && tries < 80; tries++)
            {
                var r = Pick(_rooms);
                if (r.Vault is not null || r.W * r.H < 6) continue;
                var c = new Cell(r.X + Roll(r.W), r.Y + Roll(r.H));
                if (_taken.Contains(c) || path.Contains(c) || c.Distance(_start) < 3) continue;
                _pits.Add(new Pit(c, c, Roll(2) == 0 ? 0 : 11 + depth) { ToNextLevel = true, Spiked = depth >= 6 && Roll(3) == 0 });
                _taken.Add(c);
                n++;
            }
        }

        // ---- Creatures and loot ----

        // ---- Bosses, the Choir and Waystones (spec 12.5 and 3) ----

        private void Specials()
        {
            if (HasWaystone(db, depth, o.MaxDepth))
            {
                // Next to the start (so the party can face it), never in the way it faces on arrival.
                var spot = Enum.GetValues<Dir>().Where(d => d != _facing).Select(d => _start.Step(d))
                               .FirstOrDefault(c => At(c) == CellKind.Floor && !_taken.Contains(c) && _rooms.Any(r => r.Contains(c) && r.Contains(_start)));
                if (spot != default) { _waystone = spot; _taken.Add(spot); }
            }
            _boss = BossFor(db, depth, o.MaxDepth);
            if (_boss is not null)
            {
                var room = _rooms.FirstOrDefault(r => r.Contains(_down));
                var near = (room is null ? Around(_down, 2) : RoomCells(room))
                           .Where(c => At(c) == CellKind.Floor && !_taken.Contains(c) && c != _down)
                           .OrderBy(c => Enum.GetValues<Dir>().Count(d => At(c.Step(d)) == CellKind.Floor) >= 3 ? 0 : 1)
                           .ThenBy(c => Math.Abs(c.Distance(_down) - 2)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
                if (near.Count == 0) { _boss = null; return; }
                var at = near[0];
                _taken.Add(at);
                var def = db.Creature(_boss);
                int lvl = depth + 1 + Roll(2); // the boss fits the depth (spec 12.5), not the depth × 1.6 of its level's creatures
                _spawns.Add(new Spawn(_boss, at, Enum.GetValues<Dir>().OrderBy(d => at.Step(d).Distance(_down)).First()) { Level = lvl });
                if (_boss == "bellwrightEngine" && room is not null)
                {
                    // Three pressure valves on the arena's walls.
                    var walls = RoomCells(room).Where(c => !_taken.Contains(c) && c != _down)
                                .SelectMany(c => Enum.GetValues<Dir>().Where(d => At(c.Step(d)) == CellKind.Wall).Select(d => (c, d)))
                                .GroupBy(x => x.c).Select(g => g.First()).ToList();
                    for (int i = 0; i < 3 && walls.Count > 0; i++)
                    {
                        var (cell, face) = walls[Roll(walls.Count)];
                        walls.RemoveAll(w => w.c == cell);
                        _levers.Add((cell, face, []));
                        _valves.Add(cell);
                        _taken.Add(cell);
                    }
                }
                if (_boss == "mycelCrown")
                {
                    // Its three nodes, spread through the level's other rooms.
                    var rooms = _rooms.Where(r => r.Vault is null && r != room && !r.Contains(_start)).OrderBy(r => r.X * 7 + r.Y * 13 % 11).ToList();
                    int placed = 0;
                    foreach (var r in rooms)
                    {
                        var spot = RoomCells(r).Where(c => At(c) == CellKind.Floor && !_taken.Contains(c)).OrderBy(c => Math.Abs(c.X - (r.X + r.W / 2)) + Math.Abs(c.Y - (r.Y + r.H / 2))).FirstOrDefault();
                        if (spot == default) continue;
                        _spawns.Add(new Spawn("crownNode", spot, Dir.South) { Level = lvl });
                        _taken.Add(spot);
                        if (++placed == 3) break;
                    }
                }
                if (_boss == "archivist")
                    foreach (var c in near.Skip(1).Where(c => c.Distance(at) >= 2).Take(4)) { _stations[c] = "candle"; _taken.Add(c); } // his reading candles
                if (_boss == "motherOfCoils")
                    foreach (var c in near.Skip(1).Take(2)) { _spawns.Add(new Spawn("coilGuard", c, Dir.South) { Level = MonsterLevel() }); _taken.Add(c); }
            }
            // Crafting stations (spec 13.5): a forge in half the Deep Works' levels, an enchanting altar in a quarter from level 3.
            if (!Plain)
            {
                if (Region.ForgeOneIn > 0 && Roll(Region.ForgeOneIn) == 0) PlaceStation("anvil");
                if (depth >= 3 && Roll(4) == 0) PlaceStation("altar");
                for (int i = 0; i < Region.Candles; i++) PlaceStation("candle");
                for (int i = 0; i < Region.Pods; i++) PlaceStation("pod");
                for (int i = 0; i < Region.Rifts; i++) PlaceStation("rift");
                if (Region.Roots > 0) PlaceRoots(Region.Roots);
            }
            // The Hollow Choir: five singers in a hall on level 6 (an optional fight; the stairs don't need it).
            if (depth == 6 && o.MaxDepth >= 6 && !Plain)
            {
                var hall = _rooms.Where(r => r.Vault is null && !r.Contains(_start) && !r.Contains(_down) && (_landing is not { } l || !r.Contains(l)))
                                 .OrderByDescending(r => r.W * r.H).FirstOrDefault();
                if (hall is not null)
                {
                    var cells = RoomCells(hall).Where(c => At(c) == CellKind.Floor && !_taken.Contains(c)).ToList();
                    for (int i = 0; i < 5 && cells.Count > 0; i++)
                    {
                        var c = cells[Roll(cells.Count)];
                        cells.Remove(c);
                        _taken.Add(c);
                        _spawns.Add(new Spawn("choirSinger", c, Dir.South) { Level = MonsterLevel() });
                    }
                    _notes.Add((hall.Centre, "Five voices, singing one note that never ends. The air shapes itself to it."));
                }
            }
        }

        private void PlaceStation(string kind)
        {
            var spots = _rooms.Where(r => r.Vault is null && !r.Contains(_start)).SelectMany(RoomCells)
                              .Where(c => At(c) == CellKind.Floor && !_taken.Contains(c) && c.Distance(_down) > 1
                                          && Enum.GetValues<Dir>().All(d => At(c.Step(d)) != CellKind.Door)
                                          && Enum.GetValues<Dir>().Count(d => At(c.Step(d)) == CellKind.Floor) >= 3)
                              .ToList();
            if (spots.Count == 0) return;
            var at = spots[Roll(spots.Count)];
            _stations[at] = kind;
            _taken.Add(at);
        }

        /// <summary>Bookshelves along room walls (the Ossuary Stacks): never a door, a secret or the level's edge.</summary>
        private void PlaceShelves(Level level)
        {
            var secrets = _secrets.Select(s => s.Cell).ToHashSet();
            foreach (var room in _rooms.Where(r => r.Vault is null))
                foreach (var c in RoomCells(room))
                    foreach (var d in Enum.GetValues<Dir>())
                    {
                        var w = c.Step(d);
                        if (At(w) != CellKind.Wall || secrets.Contains(w) || w.X <= 0 || w.Y <= 0 || w.X >= Size - 1 || w.Y >= Size - 1 || level.Shelves.Contains(w)) continue;
                        if (Roll(100) < Region.Shelves * 100) level.Shelves.Add(w);
                    }
        }

        /// <summary>Root tangles in corridors (the Rootdark), away from the stairs. They can be cut or burned, so
        /// the check counts them as open.</summary>
        private void PlaceRoots(int count)
        {
            var corridor = Enumerable.Range(0, Size * Size).Select(i => new Cell(i % Size, i / Size))
                .Where(c => At(c) == CellKind.Floor && !_taken.Contains(c) && !_rooms.Any(r => r.Contains(c))
                            && c.Distance(_start) > 3 && c.Distance(_down) > 3
                            && Enum.GetValues<Dir>().Count(d => At(c.Step(d)) is CellKind.Floor or CellKind.Door) == 2)
                .ToList();
            for (int i = 0; i < count && corridor.Count > 0; i++)
            {
                var at = corridor[Roll(corridor.Count)];
                corridor.RemoveAll(c => c.Distance(at) <= 4);
                _stations[at] = "roots";
                _taken.Add(at);
            }
        }

        /// <summary>Roots grown over some of the rooms' walls (the Rootdark): only the look.</summary>
        private void PlaceRootWalls(Level level)
        {
            foreach (var room in _rooms.Where(r => r.Vault is null))
                foreach (var c in RoomCells(room))
                    foreach (var d in Enum.GetValues<Dir>())
                    {
                        var w = c.Step(d);
                        if (At(w) != CellKind.Wall || level.RootWalls.Contains(w) || w.X <= 0 || w.Y <= 0 || w.X >= Size - 1 || w.Y >= Size - 1) continue;
                        if (Roll(100) < Region.RootWalls * 100) level.RootWalls.Add(w);
                    }
        }

        /// <summary>Walls of polished black glass round some rooms (the Glass Roads): only the look.</summary>
        private void PlaceMirrorWalls(Level level)
        {
            foreach (var room in _rooms.Where(r => r.Vault is null))
                foreach (var c in RoomCells(room))
                    foreach (var d in Enum.GetValues<Dir>())
                    {
                        var w = c.Step(d);
                        if (At(w) != CellKind.Wall || level.MirrorWalls.Contains(w) || w.X <= 0 || w.Y <= 0 || w.X >= Size - 1 || w.Y >= Size - 1) continue;
                        if (Roll(100) < Region.MirrorWalls * 100) level.MirrorWalls.Add(w);
                    }
        }

        /// <summary>Broken glass on some floor cells (the Glass Roads), never on the start, the stairs or a station.</summary>
        private void PlaceShards(Level level)
        {
            for (int y = 1; y < Size - 1; y++)
                for (int x = 1; x < Size - 1; x++)
                {
                    var c = new Cell(x, y);
                    if (At(c) != CellKind.Floor || _taken.Contains(c) || _stations.ContainsKey(c) || c.Distance(_start) <= 2 || c == _down) continue;
                    if (Roll(1000) < Region.Shards * 1000) level.Shards.Add(c);
                }
        }

        private int MonsterLevel() => Math.Max(1, (int)(depth * 1.6)) + Roll(2);

        private string Weighted(IReadOnlyList<(string Id, double Weight)> table)
        {
            double total = table.Sum(t => t.Weight), r = rng.Next(1_000_000) / 1_000_000.0 * total;
            foreach (var t in table) if ((r -= t.Weight) < 0) return t.Id;
            return table[^1].Id;
        }

        private void Monsters()
        {
            var table = SpawnTable(db, _theme, depth);
            int want = Math.Min(30, (int)Math.Round((4 + depth) * o.Density));
            var spots = new List<Cell>();
            foreach (var r in _rooms)
                foreach (var c in RoomCells(r))
                    if (!_taken.Contains(c) && c.Distance(_start) >= 5) spots.Add(c);
            int placed = 0;
            for (int tries = 0; placed < want && tries < 200 && spots.Count > 0; tries++)
            {
                var id = Weighted(table);
                var def = db.Creature(id);
                var at = Pick(spots);
                int group = def.Has("pack") ? 2 + Roll(3) : 1;
                for (int g = 0; g < group && placed < want; g++)
                {
                    var cell = def.Size == CreatureSize.Small ? at : spots.OrderBy(s => s.Distance(at)).Skip(g).FirstOrDefault();
                    if (cell == default) break;
                    _spawns.Add(new Spawn(id, cell, Pick(Enum.GetValues<Dir>())) { Level = MonsterLevel() });
                    if (def.Size != CreatureSize.Small) spots.Remove(cell);
                    placed++;
                }
            }
        }

        private void Loot()
        {
            var path = PathTo(_start, _down).ToHashSet();
            int want = 2 + Roll(2);
            var gen = new LootGenerator(db);
            for (int n = 0, tries = 0; n < want && tries < 100; tries++)
            {
                var r = Pick(_rooms);
                if (r.Vault is not null) continue;
                var c = new Cell(r.X + Roll(r.W), r.Y + Roll(r.H));
                int open = Enum.GetValues<Dir>().Count(d => At(c.Step(d)) == CellKind.Floor);
                if (_taken.Contains(c) || path.Contains(c) || open < 3 || _doors.Any(d => d.Cell.Distance(c) <= 1)) continue;
                _chests.Add((c, depth >= 5 && Roll(10) < 4 ? "chest.rich" : "chest.cellar", Roll(4) == 0 ? 12 + depth : 0));
                _taken.Add(c);
                n++;
            }
            // A dead delver or two, and what they carried.
            for (int n = 0; n < 1 + Roll(2); n++)
            {
                var r = Pick(_rooms);
                var c = new Cell(r.X + Roll(r.W), r.Y + Roll(r.H));
                if (_taken.Contains(c) || At(c) != CellKind.Floor) continue;
                foreach (var it in gen.Roll("chest.cellar", depth, rng)) _items.Add((c, it));
                _notes.Add((c, "A delver lies here, long dead. Their pack is still on their back."));
                _taken.Add(c);
            }
        }

        private void Flavour()
        {
            for (int n = 0; n < 1 + Roll(2); n++)
            {
                var r = Pick(_rooms);
                var c = r.Centre;
                if (_taken.Contains(c) || At(c) != CellKind.Floor) continue;
                _notes.Add((c, FlavourNotes[Roll(FlavourNotes.Length)]));
                _taken.Add(c);
            }
        }

        // ---- Checking and finishing ----

        /// <summary>
        /// Walks the level as a player could: locked doors open once their key is reached, gates once their lever
        /// or plate is. Pits, chests and altars count as blocked. The stairs, the landing, every creature, every
        /// chest's side and every teleporter's destination must be reached.
        /// </summary>
        public bool Check()
        {
            foreach (var d in _doors)
            {
                bool we = At(d.Cell.Step(Dir.West)) == CellKind.Wall && At(d.Cell.Step(Dir.East)) == CellKind.Wall;
                bool ns = At(d.Cell.Step(Dir.North)) == CellKind.Wall && At(d.Cell.Step(Dir.South)) == CellKind.Wall;
                if (we == ns) return false;
            }
            var reach = Walk(_start);
            if (!reach.Contains(_down)) return false;
            if (_landing is { } land && !reach.Contains(land)) return false;
            // A party that falls in lands here, with every barrier still shut: it must find the stairs from there too.
            if (_landing is { } fell && !Walk(fell).Contains(_down)) return false;
            // Below the first level the start holds the stairs up, and stepping back on them climbs: the way on
            // must not run back through the start.
            if (depth > 1 && !Enum.GetValues<Dir>().Select(d => _start.Step(d)).Where(reach.Contains)
                    .Any(n => Walk(n, avoid: _start).Contains(_down))) return false;
            if (_traps.Any(t => t.Kind == TrapKind.Teleporter && t.Target is { } tt && !reach.Contains(tt))) return false;
            // A teleporter must not drop the party in a pocket it can only leave through a hidden door: from where
            // it lands, it must find its way back to the start without one.
            if (_traps.Any(t => t.Kind == TrapKind.Teleporter && t.Target is { } to && !Walk(to, secrets: false).Contains(_start))) return false;
            // Secret doors count as found: what lies behind them must be reachable too.
            bool Reachable(Cell c) => reach.Contains(c) || Enum.GetValues<Dir>().Any(d => reach.Contains(c.Step(d)));
            if (_spawns.Any(s => !Reachable(s.Cell))) return false;
            if (_chests.Any(c => !Reachable(c.Cell))) return false;
            if (_items.Any(i => !Reachable(i.Cell))) return false;
            return true;
        }

        /// <summary>Everything a party starting at <paramref name="from"/> can reach, opening barriers as it goes.</summary>
        private HashSet<Cell> Walk(Cell from, Cell? avoid = null, bool secrets = true)
        {
            var openDoors = _doors.Where(d => d.Kind == DoorKind.Hand && d.Key is null || d.Open).Select(d => d.Cell).ToHashSet();
            var blocked = _chests.Select(c => c.Cell).Concat(_pits.Select(p => p.Cell)).Concat(_altars).ToHashSet();
            if (_waystone is { } ws) blocked.Add(ws);
            blocked.UnionWith(_stations.Where(s => s.Value is not ("roots" or "pod")).Select(s => s.Key)); // tangles are cut, pods burst
            if (avoid is { } a) blocked.Add(a);
            HashSet<Cell> reach = [];
            bool grew = true;
            while (grew)
            {
                grew = false;
                var shut = _doors.Select(d => d.Cell).Where(c => !openDoors.Contains(c)).ToHashSet();
                shut.UnionWith(blocked);
                reach = Flood(from, shut, secrets ? _secrets.Select(x => x.Cell).ToHashSet() : []).Keys.ToHashSet();
                foreach (var (cell, item) in _items)
                    if (item.KeyId is { } k && reach.Contains(cell))
                        foreach (var d in _doors.Where(d => d.Key == k)) grew |= openDoors.Add(d.Cell);
                foreach (var l in _levers) if (reach.Contains(l.Cell)) foreach (var t in l.Targets) grew |= openDoors.Add(t);
                foreach (var p in _plates) if (reach.Contains(p.Cell)) foreach (var t in p.Targets) grew |= openDoors.Add(t);
            }
            return reach;
        }

        public Level Finish(ulong seed, int attempt)
        {
            var level = new Level(Region.Final ? Region.Name : $"{Region.Name}, level {depth}", Size, Size, (CellKind[])_cells.Clone(), _start, _facing)
            {
                Source = $"gen:{seed}:{depth}:{o.Code}",
            };
            level.Theme = _theme;
            level.Depth = depth;
            level.Landing = _landing;
            level.Boss = _boss;
            level.Waystone = _waystone;
            level.Valves.AddRange(_valves);
            foreach (var (c, k) in _stations) level.Stations[c] = k;
            if (Region.Shelves > 0 && !Plain) PlaceShelves(level);
            if (Region.RootWalls > 0 && !Plain) PlaceRootWalls(level);
            if (Region.MirrorWalls > 0 && !Plain) PlaceMirrorWalls(level);
            if (Region.Shards > 0 && !Plain) PlaceShards(level);
            level.Final = Region.Final;
            level.AddStairs(_down);
            if (depth > 1) level.AddStairsUp(_start);
            foreach (var d in _doors.GroupBy(d => d.Cell).Select(g => g.Last()))
            {
                bool alongX = At(d.Cell.Step(Dir.West)) == CellKind.Wall && At(d.Cell.Step(Dir.East)) == CellKind.Wall;
                level.SetKind(d.Cell, CellKind.Door);
                level.AddDoor(new Door(d.Cell, d.Kind, alongX, d.Open) { Locked = d.Key is not null, KeyId = d.Key, LockDc = d.Dc });
            }
            foreach (var l in _levers) level.AddLever(new Lever(l.Cell, l.Face, l.Targets));
            foreach (var p in _plates) level.AddPlate(new Plate(p.Cell, p.Mode, p.Targets));
            foreach (var p in _pits) { level.AddPit(p); }
            foreach (var n in _notes.GroupBy(n => n.Cell).Select(g => g.First())) level.AddNote(new Note(n.Cell, n.Text));
            foreach (var c in _chests) level.AddChest(new Chest(c.Cell, c.Table, c.Dc));
            foreach (var s in _spawns) level.AddSpawn(s);
            foreach (var s in _secrets) level.AddSecret(new SecretDoor(s.Cell, s.Dc));
            foreach (var c in _illusory) level.AddIllusory(c);
            foreach (var t in _traps) level.AddTrap(t);
            foreach (var (cell, item) in _items) level.Drop(cell, item);
            return level;
        }
    }
}
