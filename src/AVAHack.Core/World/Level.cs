namespace AVAHack.World;

/// <summary>
/// One dungeon level: a grid of cells with doors and wall features. Cell (x, y) covers [x, x+1) × [y, y+1) in
/// world units; y grows southward. Everything outside the grid counts as wall.
/// </summary>
public sealed class Level
{
    private readonly CellKind[] _cells;
    private readonly Dictionary<Cell, SecretDoor> _secrets = new();
    private readonly HashSet<Cell> _illusory = new();
    private readonly HashSet<Cell> _stairsUp = new();
    private readonly HashSet<Cell> _rubble = new();
    private readonly List<Trap> _traps = new();
    private readonly Dictionary<Cell, Door> _doors = new();
    private readonly Dictionary<(Cell, Dir), Lever> _levers = new();
    private readonly Dictionary<Cell, Pit> _pits = new();
    private readonly Dictionary<Cell, Plate> _plates = new();
    private readonly Dictionary<Cell, Note> _notes = new();
    private readonly HashSet<Cell> _stairs = new();
    private readonly List<Spawn> _spawns = new();
    private readonly Dictionary<Cell, Chest> _chests = new();
    private readonly Dictionary<Cell, List<Items.Item>> _piles = new();

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public Cell Start { get; }
    public Dir StartFacing { get; }

    public IEnumerable<Door> Doors => _doors.Values;
    public IEnumerable<Lever> Levers => _levers.Values;
    public IEnumerable<Pit> Pits => _pits.Values;
    public IEnumerable<Plate> Plates => _plates.Values;
    public IEnumerable<Note> Notes => _notes.Values;
    /// <summary>The plain layout the generator falls back on when 20 tries all fail its check (for tests).</summary>
    public bool Plain { get; internal set; }
    /// <summary>The handmade rooms stitched into it, by name (for tests and reports).</summary>
    public List<string> Vaults { get; } = new();
    public IEnumerable<Cell> Stairs => _stairs;
    public IReadOnlyList<Spawn> Spawns => _spawns;
    public IEnumerable<SecretDoor> Secrets => _secrets.Values;
    public IReadOnlySet<Cell> Illusory => _illusory;
    public IEnumerable<Cell> StairsUp => _stairsUp;
    public IReadOnlyList<Trap> Traps => _traps;
    public IEnumerable<Cell> Rubble => _rubble;
    /// <summary>Where the party lands when it falls from the level above.</summary>
    public Cell? Landing { get; internal set; }
    /// <summary>The look: cellar, works or choir.</summary>
    public string Theme { get; internal set; } = "cellar";
    public IEnumerable<Chest> Chests => _chests.Values;
    /// <summary>Items lying on the floor, by cell.</summary>
    public IReadOnlyDictionary<Cell, List<Items.Item>> Piles => _piles;
    /// <summary>Where the level came from, for saves: built-in:name, or gen:seed:depth.</summary>
    public string Source { get; init; } = "";
    /// <summary>How deep it is (1 for the first): the item level and the monsters' tier.</summary>
    public int Depth { get; internal set; } = 1;
    /// <summary>The boss whose arena holds the stairs down (spec 12.5); the stairs are sealed while it lives.</summary>
    public string? Boss { get; internal set; }
    /// <summary>A Waystone (spec 3): a safe stone with a merchant, a trainer, a shrine and a stash.</summary>
    public Cell? Waystone { get; internal set; }
    /// <summary>The Bellwright Engine's pressure valves: levers that must all be pulled before it can be hurt.</summary>
    public List<Cell> Valves { get; } = new();
    /// <summary>Crafting stations by cell: "anvil" (a forge) or "altar" (an enchanting altar). Each blocks its cell.</summary>
    public Dictionary<Cell, string> Stations { get; } = new();
    /// <summary>Wall cells lined with bookshelves (the Ossuary Stacks): drawn as shelves, and searched with Use.</summary>
    public HashSet<Cell> Shelves { get; } = new();
    /// <summary>Wall cells overgrown with roots (the Rootdark): only the look.</summary>
    public HashSet<Cell> RootWalls { get; } = new();
    /// <summary>Floor cells strewn with broken glass (the Glass Roads).</summary>
    public HashSet<Cell> Shards { get; } = new();
    /// <summary>Walls of polished black glass (the Glass Roads): only the look.</summary>
    public HashSet<Cell> MirrorWalls { get; } = new();
    /// <summary>The Unmade Heart, the Delve's last level.</summary>
    public bool Final { get; internal set; }

    public Level(string name, int width, int height, CellKind[] cells, Cell start, Dir startFacing)
    {
        Name = name;
        Width = width;
        Height = height;
        _cells = cells;
        Start = start;
        StartFacing = startFacing;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public CellKind KindAt(int x, int y) => InBounds(x, y) ? _cells[y * Width + x] : CellKind.Wall;

    /// <summary>Whether a cell is drawn as wall: real walls, and illusory ones.</summary>
    public bool LooksLikeWall(int x, int y) => KindAt(x, y) == CellKind.Wall || _illusory.Contains(new Cell(x, y));

    public SecretDoor? SecretAt(Cell c) => _secrets.GetValueOrDefault(c);
    public Trap? TrapAt(Cell c) => _traps.FirstOrDefault(t => t.Cell == c && t.Kind != TrapKind.AntiMagic);
    public bool IsStairsUp(Cell c) => _stairsUp.Contains(c);

    /// <summary>A found secret door becomes an open wooden door.</summary>
    public void Reveal(SecretDoor s)
    {
        if (s.Found) return;
        s.Found = true;
        _cells[s.Cell.Y * Width + s.Cell.X] = CellKind.Door;
        bool alongX = IsWall(s.Cell.X - 1, s.Cell.Y) && IsWall(s.Cell.X + 1, s.Cell.Y);
        AddDoor(new Door(s.Cell, DoorKind.Hand, alongX, open: true));
    }

    public void MakeRubble(Cell c) => _rubble.Add(c);

    internal void SetKind(Cell c, CellKind k) => _cells[c.Y * Width + c.X] = k;
    internal void AddSecret(SecretDoor s) => _secrets[s.Cell] = s;
    internal void AddIllusory(Cell c) => _illusory.Add(c);
    internal void AddStairsUp(Cell c) => _stairsUp.Add(c);
    internal void AddTrap(Trap t) => _traps.Add(t);
    public CellKind KindAt(Cell c) => KindAt(c.X, c.Y);

    public bool IsWall(int x, int y) => KindAt(x, y) == CellKind.Wall;

    public Door? DoorAt(int x, int y) => _doors.GetValueOrDefault(new Cell(x, y));
    public Door? DoorAt(Cell c) => _doors.GetValueOrDefault(c);

    public Lever? LeverAt(Cell c, Dir face) => _levers.GetValueOrDefault((c, face));

    /// <summary>Whether the party may stand in this cell now.</summary>
    public bool IsPassable(Cell c) => KindAt(c) switch
    {
        CellKind.Floor => true,
        CellKind.Door => DoorAt(c)!.IsPassable,
        _ => false,
    };

    public Pit? PitAt(Cell c) => _pits.GetValueOrDefault(c);
    public Chest? ChestAt(Cell c) => _chests.GetValueOrDefault(c);

    /// <summary>The items on the floor of a cell (an empty list if none).</summary>
    public List<Items.Item> PileAt(Cell c) => _piles.TryGetValue(c, out var p) ? p : [];

    public void Drop(Cell c, Items.Item item)
    {
        if (!_piles.TryGetValue(c, out var p)) _piles[c] = p = new List<Items.Item>();
        p.Add(item);
    }

    public void ClearPile(Cell c) => _piles.Remove(c);

    /// <summary>Clears every floor pile (before a save's piles are put back).</summary>
    public void ClearPiles() => _piles.Clear();
    public Plate? PlateAt(Cell c) => _plates.GetValueOrDefault(c);
    public Note? NoteAt(Cell c) => _notes.GetValueOrDefault(c);
    public bool IsStairs(Cell c) => _stairs.Contains(c);

    /// <summary>What the floor of (x, y) shows. A hidden pit shows as plain floor until spotted.</summary>
    public FloorLook FloorAt(int x, int y)
    {
        var c = new Cell(x, y);
        if (_pits.TryGetValue(c, out var p)) return p.Spotted ? FloorLook.Pit : FloorLook.Plain;
        if (_plates.ContainsKey(c)) return FloorLook.Plate;
        if (_stairs.Contains(c)) return FloorLook.Stairs;
        if (_stairsUp.Contains(c)) return FloorLook.StairsUp;
        if (_rubble.Contains(c)) return FloorLook.Rubble;
        if (Shards.Contains(c)) return FloorLook.Shards;
        foreach (var t in _traps)
        {
            if (t.Cell != c || !(t.Spotted || t.Kind is TrapKind.SwingingBlade or TrapKind.SpikeFloor)) continue;
            if (t.Disarmed) return FloorLook.Plain;
            return t.Kind switch
            {
                TrapKind.SpikeFloor => FloorLook.Spikes,
                TrapKind.FrostGlyph => FloorLook.Glyph,
                TrapKind.LightningRune => FloorLook.Rune,
                TrapKind.Teleporter => FloorLook.Teleporter,
                TrapKind.FloodingRoom or TrapKind.AntiMagic or TrapKind.SwingingBlade or TrapKind.Spinner => FloorLook.Plain,
                _ => FloorLook.Trap,
            };
        }
        return FloorLook.Plain;
    }

    internal void AddDoor(Door d) => _doors[d.Cell] = d;
    internal void AddPit(Pit p) => _pits[p.Cell] = p;
    internal void AddPlate(Plate p) => _plates[p.Cell] = p;
    internal void AddNote(Note n) => _notes[n.Cell] = n;
    internal void AddStairs(Cell c) => _stairs.Add(c);
    internal void AddSpawn(Spawn s) => _spawns.Add(s);
    internal void AddChest(Chest c) => _chests[c.Cell] = c;
    internal void RemoveChest(Cell c) => _chests.Remove(c);

    /// <summary>Marks a hidden pit as found.</summary>
    public void Spot(Pit p) => p.Spotted = true;

    internal void AddLever(Lever l) => _levers[(l.Cell, l.Face)] = l;
}
