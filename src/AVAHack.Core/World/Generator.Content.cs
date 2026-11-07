using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;

namespace AVAHack.World;

/// <summary>Which creatures live in which region (spec 3 and 12.3).</summary>
public static class SpawnTables
{
    /// <summary>The creatures a region sends at a depth, by its families in regions.json.</summary>
    public static IReadOnlyList<(string Id, double Weight)> For(ContentDb db, string theme, int depth)
    {
        var fams = (db.Regions.FirstOrDefault(r => r.Id == theme) ?? db.Regions[0]).Families;
        bool Fits(CreatureDef c) => c.Tier switch
        {
            Tier.Lesser => depth <= 8,
            Tier.Common => depth >= 3,
            Tier.Greater => depth >= 7,
            _ => depth >= 11,
        };
        var list = db.Creatures.Where(c => fams.Contains(c.Family) && Fits(c) && !c.Has("boss") && !c.Has("rooted") && c.Family != "summoned" && c.Id != "mimic")
                               .OrderBy(c => c.Id, StringComparer.Ordinal)
                               .Select(c => (c.Id, c.Tier == Tier.Lesser && depth > 4 ? 0.5 : 1.0)).ToList();
        if (list.Count < 3)
            list = db.Creatures.Where(c => c.Family is not ("summoned" or "unmade") && c.Id != "mimic" && !c.Has("boss"))
                               .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (c.Id, 1.0)).ToList();
        return list;
    }
}

public static partial class Generator
{
    private static readonly string[] FlavourNotes =
    [
        "Scratched into the wall: the date of a winter forty years gone, and a name.",
        "A child's chalk drawing of a bell, and an arrow pointing down.",
        "Someone has written THE KNELL MOVES IT ALL over and over, smaller each time.",
        "A broken lantern, and boot prints going one way only.",
        "The stones here are warm, though nothing burns.",
        "A delver's tally: nine marks, then nothing.",
        "Water drips somewhere, slow and patient.",
        "A smell of old candles and older dust.",
    ];

    private static readonly (TrapKind Kind, int MinDepth)[] TrapDepths =
    [
        (TrapKind.Tripwire, 1), (TrapKind.DartWall, 1), (TrapKind.SpikeFloor, 2), (TrapKind.Spinner, 2), (TrapKind.AlarmPlate, 2),
        (TrapKind.FrostGlyph, 3), (TrapKind.Teleporter, 3), (TrapKind.GasVent, 4), (TrapKind.SwingingBlade, 4),
        (TrapKind.FireSpout, 5), (TrapKind.LightningRune, 5), (TrapKind.CollapsingCeiling, 6), (TrapKind.RollingBoulder, 6),
        (TrapKind.AntiMagic, 7),
    ];

}
