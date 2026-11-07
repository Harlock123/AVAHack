using System.Globalization;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.Game;

/// <summary>The choices made before a Delve (spec 4).</summary>
public sealed record DelveOptions
{
    /// <summary>Any text, or an 8-letter seed code; blank for a random seed.</summary>
    public string Seed { get; init; } = "";
    public int Depth { get; init; } = 12;
    public Difficulty Strength { get; init; } = Difficulty.Standard;
    /// <summary>0.6 sparse, 1 normal, 1.5 swarming.</summary>
    public double Density { get; init; } = 1;
    /// <summary>0.5 few, 1 normal, 1.6 many.</summary>
    public double Traps { get; init; } = 1;
    public bool Food { get; init; } = true;
    public bool Ironman { get; init; }
    public bool PauseInCombat { get; init; } = true;
    public MagicItems Magic { get; init; } = MagicItems.Normal;

    public GenOptions Gen => new() { MaxDepth = Depth, Density = Density, Traps = Traps };

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>The run seed: an 8-letter code is read back exactly; other text is hashed; blank is random.</summary>
    public ulong ResolveSeed()
    {
        var s = Seed.Trim().ToUpperInvariant();
        if (s.Length == 0) return (ulong)Random.Shared.NextInt64() & 0xFF_FFFF_FFFF;
        if (s.Length == 8 && s.All(Alphabet.Contains))
        {
            ulong v = 0;
            foreach (char c in s) v = v * 32 + (ulong)Alphabet.IndexOf(c);
            return v;
        }
        ulong h = 14695981039346656037UL; // FNV-1a
        foreach (char c in Seed) { h ^= c; h *= 1099511628211UL; }
        return h & 0xFF_FFFF_FFFF;
    }

    /// <summary>The 8-letter code that names a seed, to share.</summary>
    public static string Code(ulong seed)
    {
        var chars = new char[8];
        for (int i = 7; i >= 0; i--) { chars[i] = Alphabet[(int)(seed % 32)]; seed /= 32; }
        return new string(chars);
    }

    public Dictionary<string, string> ToDict() => new()
    {
        ["depth"] = Depth.ToString(CultureInfo.InvariantCulture), ["strength"] = Strength.Name,
        ["density"] = Density.ToString(CultureInfo.InvariantCulture), ["traps"] = Traps.ToString(CultureInfo.InvariantCulture),
        ["food"] = Food ? "1" : "0", ["ironman"] = Ironman ? "1" : "0", ["pause"] = PauseInCombat ? "1" : "0", ["magic"] = Magic.ToString(),
    };

    public static DelveOptions FromDict(Dictionary<string, string> d) => new()
    {
        Depth = int.Parse(d["depth"], CultureInfo.InvariantCulture),
        Strength = Difficulty.All.FirstOrDefault(x => x.Name == d["strength"]) ?? Difficulty.Standard,
        Density = double.Parse(d["density"], CultureInfo.InvariantCulture),
        Traps = double.Parse(d["traps"], CultureInfo.InvariantCulture),
        Food = d["food"] == "1", Ironman = d["ironman"] == "1", PauseInCombat = d["pause"] == "1",
        Magic = Enum.Parse<MagicItems>(d["magic"]),
    };
}

/// <summary>
/// A whole Delve: the party going down level by level through generated levels. It owns the engine and swaps
/// levels under it on the stairs and through pits, keeping each visited level as it was left. A save holds the
/// current level and every visited one; levels are rebuilt from the seed.
/// </summary>
public sealed class Delve
{
    private readonly Dictionary<int, LevelStateSave> _visited = new();

    public ContentDb Content { get; }
    public DelveOptions Options { get; }
    public ulong Seed { get; }
    public int Depth { get; private set; } = 1;
    public GameEngine Engine { get; }
    public string SeedCode => DelveOptions.Code(Seed);

    /// <summary>Raised when the party changes level (Ironman saves then; the view changes its textures).</summary>
    public event Action<int>? LevelChanged;

    private Delve(ContentDb db, DelveOptions o, ulong seed, GameEngine g, int depth)
    {
        Content = db;
        Options = o;
        Seed = seed;
        Engine = g;
        Depth = depth;
        g.Difficulty = o.Strength;
        g.PauseAllowed = o.PauseInCombat;
        g.Magic = o.Magic;
        g.FoodOn = o.Food;
        g.StairsHook = _ => { Descend(); return true; };
        g.StairsUpHook = _ => { Ascend(); return true; };
        g.FellThrough += pit => { if (pit.ToNextLevel) Fall(); };
    }

    public static Delve Start(ContentDb db, Party party, DelveOptions o)
    {
        ulong seed = o.ResolveSeed();
        var level = Generator.Generate(db, seed, 1, o.Gen);
        var engine = new GameEngine(level, seed, party, db, spawn: false) { };
        engine.Difficulty = o.Strength;
        var delve = new Delve(db, o, seed, engine, 1);
        engine.ChangeLevel(level, level.Start, level.StartFacing);
        engine.Say($"The Delve begins. Seed {delve.SeedCode}, {o.Depth} levels, {o.Strength.Name}.");
        return delve;
    }

    public Level Generate(int depth) => Generator.Generate(Content, Seed, depth, Options.Gen);

    private void Store() => _visited[Depth] = Engine.LevelState();

    /// <summary>The stairs down: the next level, or the end of the Delve from the deepest.</summary>
    public void Descend()
    {
        if (Depth >= Options.Depth)
        {
            if (FinalHook?.Invoke() == true) return;
            Engine.Win("The last stairs. The Delve is done.");
            return;
        }
        Store();
        Depth++;
        var level = Generate(Depth);
        Arrive(level, level.Start, level.StartFacing);
    }

    /// <summary>Through a pit: the level below, at its landing.</summary>
    public void Fall()
    {
        if (Depth >= Options.Depth) return;
        Store();
        Depth++;
        var level = Generate(Depth);
        Arrive(level, level.Landing ?? level.Start, Engine.Facing);
    }

    /// <summary>The stairs up: back to the level above, at its stairs down.</summary>
    public void Ascend()
    {
        if (Depth <= 1)
        {
            Engine.Say("The way back up to Harrowgate is barred. There is only down.");
            return;
        }
        Store();
        Depth--;
        var level = Generate(Depth);
        var down = level.Stairs.First();
        var off = Enum.GetValues<Dir>().FirstOrDefault(d => level.IsPassable(down.Step(d)));
        Arrive(level, down.Step(off), off);
    }

    private void Arrive(Level level, Cell at, Dir facing)
    {
        _visited.TryGetValue(Depth, out var restore);
        Engine.ChangeLevel(level, at, facing, restore);
        if (restore is not null) { Engine.PlaceParty(at, facing); }
        LevelChanged?.Invoke(Depth);
    }

    /// <summary>The final level and its boss take over the last stairs (the bestiary's).</summary>
    public Func<bool>? FinalHook { get; set; }

    public SaveGame ToSave()
    {
        var dict = Options.ToDict();
        dict["seed"] = Seed.ToString(CultureInfo.InvariantCulture);
        dict["at"] = Depth.ToString(CultureInfo.InvariantCulture);
        return Engine.ToSave(_visited.Where(kv => kv.Key != Depth).Select(kv => kv.Value).ToList(), dict, Options.Ironman);
    }

    public static Delve FromSave(SaveGame s, ContentDb? db = null)
    {
        db ??= ContentDb.LoadBuiltIn();
        var o = DelveOptions.FromDict(s.Delve);
        var g = GameEngine.FromSave(s, db);
        var d = new Delve(db, o, ulong.Parse(s.Delve["seed"], CultureInfo.InvariantCulture), g, int.Parse(s.Delve["at"], CultureInfo.InvariantCulture));
        foreach (var v in s.Visited)
            if (v.Source.Split(':') is { Length: >= 3 } p && int.TryParse(p[2], out int depth)) d._visited[depth] = v;
        return d;
    }

    public static bool IsDelve(SaveGame s) => s.Delve.ContainsKey("seed");
}
