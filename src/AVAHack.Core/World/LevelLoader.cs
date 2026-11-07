using System.Reflection;

namespace AVAHack.World;

/// <summary>
/// Reads a handmade level from text. Directives come first, then <c>map:</c> and the rows:
/// <code>
/// name: Test Hall
/// start: 2 2 east                 # x y facing
/// lever: 6 4 west -> 7 6          # lever on the west wall of floor cell (6,4), linked to the door at (7,6)
/// map:
/// #####
/// #.+.#                           # # wall  . floor  + hand door  P portcullis  O open portcullis
/// #####
/// </code>
/// Text after <c>#</c> and a space in a directive is a comment. Every row must be the same width.
/// </summary>
public static class LevelLoader
{
    public static Level Parse(string text)
    {
        string name = "Unnamed";
        Cell? start = null;
        Dir facing = Dir.North;
        var leverLines = new List<(int Line, string Text)>();
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
                default:
                    throw Error(i, $"unknown directive '{key}'");
            }
        }

        if (rows.Count == 0) throw new FormatException("The level has no map.");
        int width = rows[0].Length, height = rows.Count;
        if (rows.Any(r => r.Length != width)) throw new FormatException("Every map row must be the same width.");

        var cells = new CellKind[width * height];
        var doors = new List<(Cell Cell, DoorKind Kind, bool Open)>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            char ch = rows[y][x];
            cells[y * width + x] = ch switch
            {
                '#' => CellKind.Wall,
                '.' => CellKind.Floor,
                '+' or 'P' or 'O' => CellKind.Door,
                _ => throw new FormatException($"Unknown map character '{ch}' at {x},{y}."),
            };
            if (ch is '+') doors.Add((new Cell(x, y), DoorKind.Hand, false));
            if (ch is 'P' or 'O') doors.Add((new Cell(x, y), DoorKind.Portcullis, ch == 'O'));
        }

        if (start is not { } s) throw new FormatException("The level has no start.");
        var level = new Level(name, width, height, cells, s, facing);
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
        return level;
    }

    /// <summary>Loads a level shipped inside the Core assembly, e.g. <c>test-hall</c>.</summary>
    public static Level LoadBuiltIn(string name)
    {
        var asm = typeof(LevelLoader).Assembly;
        using var stream = asm.GetManifestResourceStream($"AVAHack.Content.Levels/{name}.txt")
                           ?? asm.GetManifestResourceStream($"AVAHack.Content.Levels\\{name}.txt")
                           ?? throw new FileNotFoundException($"No built-in level '{name}'.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
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
