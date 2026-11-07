using System.Reflection;

namespace AVAHack.World;

/// <summary>
/// Reads a handmade level from text. Directives come first, then <c>map:</c> and the rows:
/// <code>
/// name: Test Hall
/// start: 2 2 east                 # x y facing
/// lever: 6 4 west -> 7 6          # lever on the west wall of floor cell (6,4), linked to the door at (7,6)
/// pit: 5 2 -> 5 9 hidden 13       # a pit at (5,2) that drops to (5,9); hidden until Perception 13 finds it
/// plate: 8 3 hold -> 9 3          # a pressure plate holding the door at (9,3) open while pressed (or toggle)
/// note: 2 2 Scratched in the wall: "Mind the third flagstone."
/// monster: cellarRat 4 5 north    # a creature from creatures.json, its cell and facing
/// chest: 6 2 chest.cellar [locked 15]   # a chest holding a loot table's roll, maybe locked
/// map:
/// #####
/// #.+.#                           # # wall  . floor  + hand door  P portcullis  O open portcullis
/// #####                           # ^ pit  o plate  > stairs down (the way out)
/// </code>
/// Text after <c>#</c> and a space in a directive is a comment. Every row must be the same width.
/// </summary>
public static class LevelLoader
{
    public static Level Parse(string text) => Parse(text, "");

    public static Level Parse(string text, string source)
    {
        string name = "Unnamed";
        Cell? start = null;
        Dir facing = Dir.North;
        var leverLines = new List<(int Line, string Text)>();
        var pitLines = new List<(int Line, string Text)>();
        var plateLines = new List<(int Line, string Text)>();
        var noteLines = new List<(int Line, string Text)>();
        var monsterLines = new List<(int Line, string Text)>();
        var chestLines = new List<(int Line, string Text)>();
        var rows = new List<string>();
        bool inMap = false;

        var lines = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (inMap)
            {
                string row = raw.TrimEnd();
                if (row.Length == 0) continue;
                rows.Add(row);
                continue;
            }
            string trimmed = raw.Trim();
            if (trimmed.StartsWith("note:", StringComparison.OrdinalIgnoreCase))
            {
                noteLines.Add((i, trimmed[5..].Trim())); // notes keep their text whole, # and all
                continue;
            }
            string line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            if (line == "map:") { inMap = true; continue; }
            int colon = line.IndexOf(':');
            if (colon < 0) throw Error(i, $"expected 'key: value', got '{line}'");
            string key = line[..colon].Trim().ToLowerInvariant();
            string value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "name":
                    name = value;
                    break;
                case "start":
                    var p = Split(value);
                    if (p.Length != 3) throw Error(i, "start needs: x y facing");
                    start = new Cell(Int(p[0], i), Int(p[1], i));
                    facing = DirExtensions.Parse(p[2]);
                    break;
                case "lever":
                    leverLines.Add((i, value));
                    break;
                case "pit":
                    pitLines.Add((i, value));
                    break;
                case "plate":
                    plateLines.Add((i, value));
                    break;
                case "monster":
                    monsterLines.Add((i, value));
                    break;
                case "chest":
                    chestLines.Add((i, value));
                    break;
                default:
                    throw Error(i, $"unknown directive '{key}'");
            }
        }

        if (rows.Count == 0) throw new FormatException("The level has no map.");
        int width = rows[0].Length, height = rows.Count;
        if (rows.Any(r => r.Length != width)) throw new FormatException("Every map row must be the same width.");

        var cells = new CellKind[width * height];
        var doors = new List<(Cell Cell, DoorKind Kind, bool Open)>();
        var pitCells = new List<Cell>();
        var plateCells = new List<Cell>();
        var stairs = new List<Cell>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            char ch = rows[y][x];
            cells[y * width + x] = ch switch
            {
                '#' => CellKind.Wall,
                '.' or '^' or 'o' or '>' => CellKind.Floor,
                '+' or 'P' or 'O' => CellKind.Door,
                _ => throw new FormatException($"Unknown map character '{ch}' at {x},{y}."),
            };
            if (ch is '+') doors.Add((new Cell(x, y), DoorKind.Hand, false));
            if (ch is 'P' or 'O') doors.Add((new Cell(x, y), DoorKind.Portcullis, ch == 'O'));
            if (ch is '^') pitCells.Add(new Cell(x, y));
            if (ch is 'o') plateCells.Add(new Cell(x, y));
            if (ch is '>') stairs.Add(new Cell(x, y));
        }

        if (start is not { } s) throw new FormatException("The level has no start.");
        var level = new Level(name, width, height, cells, s, facing) { Source = source };
        if (level.KindAt(s) != CellKind.Floor) throw new FormatException($"The start {s} is not a floor cell.");

        foreach (var (cell, kind, open) in doors)
        {
            bool wallsWestEast = level.IsWall(cell.X - 1, cell.Y) && level.IsWall(cell.X + 1, cell.Y);
            bool wallsNorthSouth = level.IsWall(cell.X, cell.Y - 1) && level.IsWall(cell.X, cell.Y + 1);
            if (wallsWestEast == wallsNorthSouth)
                throw new FormatException($"The door at {cell} needs walls on exactly two opposite sides.");
            level.AddDoor(new Door(cell, kind, alongX: wallsWestEast, open));
        }

        foreach (var (i, value) in leverLines)
        {
            var halves = value.Split("->");
            if (halves.Length != 2) throw Error(i, "lever needs: x y face -> x y [x y ...]");
            var at = Split(halves[0]);
            if (at.Length != 3) throw Error(i, "lever needs: x y face -> x y");
            var cell = new Cell(Int(at[0], i), Int(at[1], i));
            var face = DirExtensions.Parse(at[2]);
            if (level.KindAt(cell) != CellKind.Floor) throw Error(i, $"the lever's cell {cell} is not floor");
            if (!level.IsWall(cell.X + face.Dx(), cell.Y + face.Dy())) throw Error(i, $"no wall on the {face} side of {cell}");
            var t = Split(halves[1]);
            if (t.Length == 0 || t.Length % 2 != 0) throw Error(i, "lever targets are x y pairs");
            var targets = new List<Cell>();
            for (int k = 0; k < t.Length; k += 2)
            {
                var target = new Cell(Int(t[k], i), Int(t[k + 1], i));
                if (level.DoorAt(target) is null) throw Error(i, $"no door at {target}");
                targets.Add(target);
            }
            level.AddLever(new Lever(cell, face, targets));
        }
        foreach (var c in stairs) level.AddStairs(c);

        var pitsDone = new HashSet<Cell>();
        foreach (var (i, value) in pitLines)
        {
            var (at, rest) = Halves(value, i, "pit needs: x y -> x y [hidden dc]");
            var cell = At(at, 2, i);
            if (!pitCells.Contains(cell)) throw Error(i, $"there is no ^ at {cell}");
            var t = Split(rest);
            if (t.Length is not (2 or 4)) throw Error(i, "pit needs: x y -> x y [hidden dc]");
            var landing = new Cell(Int(t[0], i), Int(t[1], i));
            if (level.KindAt(landing) != CellKind.Floor || pitCells.Contains(landing)) throw Error(i, $"the landing {landing} must be floor");
            int dc = 0;
            if (t.Length == 4)
            {
                if (!t[2].Equals("hidden", StringComparison.OrdinalIgnoreCase)) throw Error(i, $"expected 'hidden', got '{t[2]}'");
                dc = Int(t[3], i);
            }
            level.AddPit(new Pit(cell, landing, dc));
            pitsDone.Add(cell);
        }
        foreach (var c in pitCells.Where(c => !pitsDone.Contains(c)))
            throw new FormatException($"The pit at {c} has no 'pit:' line saying where it lands.");

        var platesDone = new HashSet<Cell>();
        foreach (var (i, value) in plateLines)
        {
            var (at, rest) = Halves(value, i, "plate needs: x y toggle|hold -> x y [x y ...]");
            var p = Split(at);
            if (p.Length != 3) throw Error(i, "plate needs: x y toggle|hold -> x y");
            var cell = new Cell(Int(p[0], i), Int(p[1], i));
            if (!plateCells.Contains(cell)) throw Error(i, $"there is no o at {cell}");
            var mode = p[2].ToLowerInvariant() switch
            {
                "toggle" => PlateMode.Toggle,
                "hold" => PlateMode.Hold,
                _ => throw Error(i, $"a plate is toggle or hold, not '{p[2]}'"),
            };
            level.AddPlate(new Plate(cell, mode, Doors(level, rest, i)));
            platesDone.Add(cell);
        }
        foreach (var c in plateCells.Where(c => !platesDone.Contains(c)))
            throw new FormatException($"The plate at {c} has no 'plate:' line saying what it works.");

        foreach (var (i, value) in noteLines)
        {
            var p = value.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) throw Error(i, "note needs: x y text");
            var cell = new Cell(Int(p[0], i), Int(p[1], i));
            if (level.KindAt(cell) == CellKind.Wall) throw Error(i, $"the note's cell {cell} is a wall");
            level.AddNote(new Note(cell, p[2].Trim()));
        }

        foreach (var (i, value) in monsterLines)
        {
            var p = Split(value);
            if (p.Length != 4) throw Error(i, "monster needs: creature x y facing");
            var cell = new Cell(Int(p[1], i), Int(p[2], i));
            if (level.KindAt(cell) != CellKind.Floor || level.PitAt(cell) is not null) throw Error(i, $"the monster's cell {cell} must be floor");
            level.AddSpawn(new Spawn(p[0], cell, DirExtensions.Parse(p[3])));
        }

        foreach (var (i, value) in chestLines)
        {
            var p = Split(value);
            if (p.Length is not (3 or 5)) throw Error(i, "chest needs: x y table [locked dc]");
            var cell = new Cell(Int(p[0], i), Int(p[1], i));
            if (level.KindAt(cell) != CellKind.Floor || level.PitAt(cell) is not null || level.PlateAt(cell) is not null) throw Error(i, $"the chest's cell {cell} must be plain floor");
            int dc = p.Length == 5 && p[3].Equals("locked", StringComparison.OrdinalIgnoreCase) ? Int(p[4], i) : 0;
            level.AddChest(new Chest(cell, p[2], dc));
        }
        return level;
    }

    private static (string At, string After) Halves(string value, int line, string usage)
    {
        var h = value.Split("->");
        if (h.Length != 2) throw Error(line, usage);
        return (h[0], h[1]);
    }

    private static Cell At(string text, int count, int line)
    {
        var p = Split(text);
        if (p.Length != count) throw Error(line, $"expected {count} numbers before ->");
        return new Cell(Int(p[0], line), Int(p[1], line));
    }

    private static List<Cell> Doors(Level level, string text, int line)
    {
        var t = Split(text);
        if (t.Length == 0 || t.Length % 2 != 0) throw Error(line, "targets are x y pairs");
        var list = new List<Cell>();
        for (int k = 0; k < t.Length; k += 2)
        {
            var c = new Cell(Int(t[k], line), Int(t[k + 1], line));
            if (level.DoorAt(c) is null) throw Error(line, $"no door at {c}");
            list.Add(c);
        }
        return list;
    }

    /// <summary>Loads a level shipped inside the Core assembly, e.g. <c>test-hall</c>.</summary>
    public static Level LoadBuiltIn(string name)
    {
        var asm = typeof(LevelLoader).Assembly;
        using var stream = asm.GetManifestResourceStream($"AVAHack.Content.Levels/{name}.txt")
                           ?? asm.GetManifestResourceStream($"AVAHack.Content.Levels\\{name}.txt")
                           ?? throw new FileNotFoundException($"No built-in level '{name}'.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), "built-in:" + name);
    }

    private static string StripComment(string s)
    {
        int i = s.IndexOf(" #", StringComparison.Ordinal);
        return i >= 0 ? s[..i] : s;
    }

    private static string[] Split(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Int(string s, int line) =>
        int.TryParse(s, out int v) ? v : throw Error(line, $"'{s}' is not a whole number");

    private static FormatException Error(int line, string message) => new($"Line {line + 1}: {message}.");
}
