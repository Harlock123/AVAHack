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

    /// <summary>Marks a hidden pit as found.</summary>
    public void Spot(Pit p) => p.Spotted = true;

    internal void AddLever(Lever l) => _levers[(l.Cell, l.Face)] = l;
}
