using System.Reflection;

namespace AVAHack.World;

/// <summary>One line of a vault's directives, with the vault-local coordinates already split out.</summary>
public sealed record VaultLine(string Kind, string[] Words, string Text);

/// <summary>A handmade room for the generator (spec 14.3).</summary>
public sealed class Vault
{
    public required string Name { get; init; }
    public required string[] Tags { get; init; }
    public required string[] Themes { get; init; }
    /// <summary>Where corridors join: the edge and the offset along it.</summary>
    public required (Dir Edge, int Offset)[] Connectors { get; init; }
    public required string[] Rows { get; init; }
    public required IReadOnlyList<VaultLine> Lines { get; init; }
    public int Width => Rows[0].Length;
    public int Height => Rows.Length;

    /// <summary>The cell of the vault's own map at a connector (on its edge).</summary>
    public (int X, int Y) ConnectorCell((Dir Edge, int Offset) c) => c.Edge switch
    {
        Dir.North => (c.Offset, 0),
        Dir.South => (c.Offset, Height - 1),
        Dir.West => (0, c.Offset),
        _ => (Width - 1, c.Offset),
    };

    private static List<Vault>? _all;

    /// <summary>The vaults shipped in Content/Vaults/vaults.txt.</summary>
    public static IReadOnlyList<Vault> All => _all ??= Parse(ReadBuiltIn());

    private static string ReadBuiltIn()
    {
        var asm = typeof(Vault).Assembly;
        using var s = asm.GetManifestResourceStream("AVAHack.Content.Vaults/vaults.txt")
                      ?? asm.GetManifestResourceStream("AVAHack.Content.Vaults\\vaults.txt")
                      ?? throw new FileNotFoundException("No vaults.txt.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static List<Vault> Parse(string text)
    {
        var list = new List<Vault>();
        var blocks = text.Replace("\r", "").Split("\n---\n").Skip(1);
        foreach (var block in blocks)
        {
            string name = "?";
            string[] tags = [], themes = [];
            var conns = new List<(Dir, int)>();
            var lines = new List<VaultLine>();
            var rows = new List<string>();
            bool inMap = false;
            foreach (var raw in block.Split('\n'))
            {
                if (inMap)
                {
                    if (raw.Trim().Length > 0) rows.Add(raw.TrimEnd());
                    continue;
                }
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line == "map:") { inMap = true; continue; }
                int colon = line.IndexOf(':');
                if (colon < 0) throw new FormatException($"Vault '{name}': '{line}' is not a directive.");
                string key = line[..colon].Trim(), value = line[(colon + 1)..].Trim();
                string bare = key == "note" ? value : StripComment(value);
                switch (key)
                {
                    case "name": name = value; break;
                    case "tags": tags = Csv(value); break;
                    case "themes": themes = Csv(value); break;
                    case "connectors":
                        foreach (var c in bare.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            conns.Add((DirExtensions.Parse(c[..1]), int.Parse(c[1..], System.Globalization.CultureInfo.InvariantCulture)));
                        break;
                    default:
                        lines.Add(new VaultLine(key, bare.Replace("->", " -> ").Split(' ', StringSplitOptions.RemoveEmptyEntries), value));
                        break;
                }
            }
            if (rows.Count == 0) throw new FormatException($"Vault '{name}' has no map.");
            if (rows.Any(r => r.Length != rows[0].Length)) throw new FormatException($"Vault '{name}': rows differ in width.");
            var v = new Vault { Name = name, Tags = tags, Themes = themes, Connectors = conns.ToArray(), Rows = rows.ToArray(), Lines = lines };
            foreach (var c in v.Connectors)
            {
                var (x, y) = v.ConnectorCell(c);
                if (x < 0 || y < 0 || x >= v.Width || y >= v.Height || v.Rows[y][x] is '#') throw new FormatException($"Vault '{name}': connector {c.Item1.Letter()}{c.Item2} isn't an opening on its edge.");
            }
            if (v.Connectors.Length == 0) throw new FormatException($"Vault '{name}' has no connectors.");
            list.Add(v);
        }
        return list;
    }

    private static string[] Csv(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string StripComment(string s)
    {
        int i = s.IndexOf("  #", StringComparison.Ordinal);
        if (i < 0) i = s.IndexOf(" #", StringComparison.Ordinal);
        return i >= 0 ? s[..i].Trim() : s;
    }
}
