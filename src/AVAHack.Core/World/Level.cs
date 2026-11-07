namespace AVAHack.World;

/// <summary>
/// One dungeon level: a grid of cells with doors and wall features. Cell (x, y) covers [x, x+1) × [y, y+1) in
/// world units; y grows southward. Everything outside the grid counts as wall.
/// </summary>
public sealed class Level
{
    private readonly CellKind[] _cells;
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
    public IEnumerable<Cell> Stairs => _stairs;
    public IReadOnlyList<Spawn> Spawns => _spawns;
    public IEnumerable<Chest> Chests => _chests.Values;
    /// <summary>Items lying on the floor, by cell.</summary>
    public IReadOnlyDictionary<Cell, List<Items.Item>> Piles => _piles;
    /// <summary>Where the level came from, for saves: built-in:name, or gen:seed:depth.</summary>
    public string Source { get; internal set; } = "";
    /// <summary>How deep it is (1 for the first): the item level and the monsters' tier.</summary>
    public int Depth { get; internal set; } = 1;

    internal Level(string name, int width, int height, CellKind[] cells, Cell start, Dir startFacing)
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
