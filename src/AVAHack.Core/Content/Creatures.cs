using AVAHack.Characters;
using AVAHack.Rules;

namespace AVAHack.Content;

public enum Tier { Lesser, Common, Greater, Dread }

public enum CreatureSize { Small, Medium, Large }

/// <summary>A condition an attack can put on what it hits.</summary>
public sealed record OnHitDef
{
    public required ConditionId Condition { get; init; }
    public required int Dc { get; init; }
    public double? Seconds { get; init; }
    /// <summary>The chance, 0–100, that a hit tries to apply it (the target still saves).</summary>
    public double Chance { get; init; } = 100;
}

public sealed record CreatureAttackDef
{
    public required string Id { get; init; }
    /// <summary>Seconds of visible tell before the blow lands.</summary>
    public required double Windup { get; init; }
    /// <summary>Seconds after the blow before the creature can act again.</summary>
    public required double Recovery { get; init; }
    public required int ToHit { get; init; }
    public required string Damage { get; init; }
    public required DamageType Type { get; init; }
    /// <summary>Cells: 1 is melee (the cell ahead); more is a projectile thrown along a straight line.</summary>
    public int Range { get; init; } = 1;
    /// <summary>Cells a second a projectile flies.</summary>
    public double ProjectileSpeed { get; init; } = 6;
    public OnHitDef? OnHit { get; init; }
    /// <summary>Hits every hero, not one (a sweep, a breath).</summary>
    public bool Area { get; init; }
}

/// <summary>Something a creature does that isn't an attack (e.g. bones that reassemble).</summary>
public sealed record CreatureSpecialDef
{
    /// <summary>reassemble: rises again after <see cref="Seconds"/> unless the killing blow was <see cref="UnlessKilledBy"/>.</summary>
    public required string Kind { get; init; }
    public double Seconds { get; init; }
    public DamageType? UnlessKilledBy { get; init; }
}

/// <summary>A monster (spec 12.1).</summary>
public sealed record CreatureDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Family { get; init; }
    public required Tier Tier { get; init; }
    public required int Level { get; init; }
    public required string HitDice { get; init; }
    public required int Ac { get; init; }
    /// <summary>Save bonuses, keyed str, dex, con, int, wis, cha. Missing ones are 0.</summary>
    public Dictionary<string, int> Saves { get; init; } = [];
    public required int Perception { get; init; }
    public required double StepSeconds { get; init; }
    public required double TurnSeconds { get; init; }
    public CreatureSize Size { get; init; } = CreatureSize.Medium;
    public DamageType[] Resist { get; init; } = [];
    public DamageType[] Vulnerable { get; init; } = [];
    public DamageType[] Immune { get; init; } = [];
    /// <summary>Conditions it can't suffer, e.g. poisoned for the dead.</summary>
    public ConditionId[] ConditionImmune { get; init; } = [];
    public required CreatureAttackDef[] Attacks { get; init; }
    /// <summary>Behaviour blocks (spec 12.4): wander, guard, ambush, chase, melee, ranged, flee, opener, pack, mindless.</summary>
    public required string[] Behaviour { get; init; }
    /// <summary>Flees below this share of its HP, in percent (with the hero's CHA). 0 never flees.</summary>
    public double FleeAtPercent { get; init; }
    public CreatureSpecialDef[] Special { get; init; } = [];
    /// <summary>The loot table rolled when it dies, if any.</summary>
    public string? Loot { get; init; }
    /// <summary>What a summoner calls up every so often.</summary>
    public string? Summons { get; init; }

    /// <summary>How many of its summons a summoner keeps about at once (3 unless set).</summary>
    public int SummonMax { get; init; } = 3;
    /// <summary>A boss's script: hollowKing, bellwright, motherOfCoils, choir, archivist, mycelCrown, crownNode, lens or unmaker.</summary>
    public string? Boss { get; init; }
    public required string Sprite { get; init; }
    public required string Text { get; init; }

    public bool Has(string behaviour) => Behaviour.Contains(behaviour);
}

public static class Behaviours
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        "wander", "guard", "ambush", "patrol", "chase", "melee", "ranged", "flee", "opener", "pack", "mindless",
        "flier", "swimmer", "phaser", "unyielding", "caster", "summoner", "splits", "explodes", "invisible", "raises", "boss",
        "herald", "rooted", "burrower",
    };
}

public enum ArmourKind { Light, Medium, Heavy }

public sealed record WeaponDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SkillId Skill { get; init; }
    public required string Damage { get; init; }
    public required DamageType Type { get; init; }
    /// <summary>Seconds the hand needs after a swing, before DEX and perks.</summary>
    public required double Recovery { get; init; }
    /// <summary>Finesse weapons may use DEX instead of STR.</summary>
    public bool Finesse { get; init; }
    /// <summary>dagger (sneak attacks), reach (strikes from the back row) or bow (shoots down the line).</summary>
    public string Kind { get; init; } = "";
    public bool TwoHanded { get; init; }
    /// <summary>Cells a bow reaches.</summary>
    public int Range { get; init; } = 1;
    /// <summary>The lowest natural roll that is a critical.</summary>
    public int CritRange { get; init; } = 20;
    public double WeightKg { get; init; }
    public int Value { get; init; } = 10;
    public required string Text { get; init; }
}

public sealed record ShieldDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Ac { get; init; }
    /// <summary>Percent of a blow from the front taken off while blocking, before the Block skill's share.</summary>
    public required double BlockPercent { get; init; }
    /// <summary>A quick shove with the shield's rim.</summary>
    public required string BashDamage { get; init; }
    public required double BashRecovery { get; init; }
    public double WeightKg { get; init; }
    public int Value { get; init; } = 10;
    public required string Text { get; init; }
}

public sealed record ArmourDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ArmourKind Kind { get; init; }
    /// <summary>What it adds to AC.</summary>
    public required int Ac { get; init; }
    /// <summary>The most DEX it lets count, or null for no limit.</summary>
    public int? DexCap { get; init; }
    public double WeightKg { get; init; }
    public int Value { get; init; } = 10;
    public required string Text { get; init; }
}

/// <summary>
/// A region of the Delve (spec 14.1, regions.json): four levels of one look and one set of creature families, with
/// a boss on its last level. Regions follow each other down in file order; the one marked final is the Unmade
/// Heart, below the last level of a long Delve.
/// </summary>
public sealed record RegionDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The creature families that live here.</summary>
    public required string[] Families { get; init; }
    /// <summary>The boss on the region's last level, if it has one yet.</summary>
    public string? Boss { get; init; }
    /// <summary>How much likelier a wandering monster is during a rest here.</summary>
    public double Wanderers { get; init; } = 1;
    /// <summary>A forge in one level in this many (0: none beyond the Waystones').</summary>
    public int ForgeOneIn { get; init; }
    /// <summary>Noise wakes the dead: the party's loud sounds rouse unaware creatures along the passages.</summary>
    public bool Hush { get; init; }
    /// <summary>Lit candles in each level: they show the party to things, and Use snuffs one.</summary>
    public int Candles { get; init; }
    /// <summary>The share of room walls lined with bookshelves, which Use searches for a book.</summary>
    public double Shelves { get; init; }
    /// <summary>Spore pods in each level: they burst near anything, and fire ignites the cloud.</summary>
    public int Pods { get; init; }
    /// <summary>Root tangles choking corridors: Use cuts one, fire burns it.</summary>
    public int Roots { get; init; }
    /// <summary>The share of room walls overgrown with roots (only the look).</summary>
    public double RootWalls { get; init; }
    /// <summary>The share of floor cells strewn with broken glass: it crunches (waking things) and cuts, once.</summary>
    public double Shards { get; init; }
    /// <summary>Rifts in each level: tears that let the Unmade through until Use seals them.</summary>
    public int Rifts { get; init; }
    /// <summary>The most creatures in a pack here (packs are 2 to this; 4 unless set).</summary>
    public int PackMax { get; init; } = 4;
    /// <summary>The highest level its creatures are made at, however deep the region lies (none unless set). The
    /// Unmade Heart's creatures are made for a 12-level Delve; below a 24-level one they'd outgrow everything else.</summary>
    public int? MaxLevel { get; init; }
    /// <summary>The share of room walls of polished black glass (only the look).</summary>
    public double MirrorWalls { get; init; }
    public bool Final { get; init; }
    /// <summary>The final region only: the boss of one more level below it, the last of the Delve (the Knell).</summary>
    public string? Beneath { get; init; }
}
