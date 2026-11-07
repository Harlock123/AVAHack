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

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public Cell Start { get; }
    public Dir StartFacing { get; }

    public IEnumerable<Door> Doors => _doors.Values;
    public IEnumerable<Lever> Levers => _levers.Values;

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

    internal void AddDoor(Door d) => _doors[d.Cell] = d;

    internal void AddLever(Lever l) => _levers[(l.Cell, l.Face)] = l;
}
