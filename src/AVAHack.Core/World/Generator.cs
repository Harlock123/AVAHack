using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.World;

/// <summary>What the generator is asked for: how deep, and how much of everything (spec 4 and 14).</summary>
public sealed record GenOptions
{
    public int MaxDepth { get; init; } = 12;
    /// <summary>Monster count: 0.6 sparse, 1 normal, 1.5 swarming.</summary>
    public double Density { get; init; } = 1;
    /// <summary>Trap count: 0.5 few, 1 normal, 1.6 many.</summary>
    public double Traps { get; init; } = 1;

    /// <summary>A short form for level sources and seed codes.</summary>
    public string Code => FormattableString.Invariant($"{MaxDepth}:{Density:0.##}:{Traps:0.##}");

    public static GenOptions FromCode(string code)
    {
        var p = code.Split(':');
        return new GenOptions
        {
            MaxDepth = int.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
            Density = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
            Traps = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}

/// <summary>
/// Makes a level from a run seed and a depth (spec 14.2): vaults first, then rooms joined by the cheapest paths
/// into a tree with a few loops; locks along the way from the stairs up to the stairs down, each with its key or
/// mechanism placed before it; secret rooms and an illusory niche; traps, pits, creatures and loot. Each attempt
/// is checked: everything must be reachable working the mechanisms in order. After 20 failed attempts a plain
/// fallback is made. The same seed, depth and options always give the same level.
/// </summary>
public static partial class Generator
{
    public const int Size = 32;

    public static string ThemeFor(int depth) => ((depth - 1) / 4 % 3) switch { 0 => "cellar", 1 => "works", _ => "choir" };

    /// <summary>
    /// The boss guarding a level's stairs down (spec 12.5): the Hollow King on level 4, the Bellwright Engine on 8,
    /// Mother of Coils on 12, and the Unmaker in the Unmade Heart, the level after a 12-level Delve's last.
    /// </summary>
    public static string? BossFor(int depth, int maxDepth) => depth switch
    {
        _ when depth > maxDepth => maxDepth >= 12 && depth == maxDepth + 1 ? "unmaker" : null,
        4 => "hollowKing",
        8 => "bellwrightEngine",
        12 => "motherOfCoils",
        _ => null,
    };

    /// <summary>A Waystone waits at the start of the level after each boss.</summary>
    public static bool HasWaystone(int depth, int maxDepth) => depth > 1 && BossFor(depth - 1, maxDepth) is not null;

    /// <summary>Whether a Delve this deep ends in the Unmade Heart.</summary>
    public static bool HasHeart(int maxDepth) => maxDepth >= 12;

    public static string RegionName(string theme) => theme switch
    {
        "heart" => "The Unmade Heart",
        "cellar" => "The Upper Hollow",
        "works" => "The Deep Works",
        _ => "The Drowned Choir",
    };

    public static Level Generate(ContentDb db, ulong seed, int depth, GenOptions? options = null)
    {
        options ??= new GenOptions();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var rng = new Pcg32(RngStreams.Mix(seed, (ulong)depth * 7919 + (ulong)attempt), 77);
            var b = new Builder(db, rng, depth, options);
            if (b.Build() && b.Check()) return b.Finish(seed, attempt);
        }
        var fb = new Builder(db, new Pcg32(seed + (ulong)depth, 78), depth, options) { Plain = true };
        fb.Build();
        return fb.Finish(seed, 99);
    }

    /// <summary>The creatures a theme sends at a depth: (creature, weight).</summary>
    public static IReadOnlyList<(string Id, double Weight)> SpawnTable(ContentDb db, string theme, int depth) =>
        SpawnTables.For(db, theme, depth);
}
