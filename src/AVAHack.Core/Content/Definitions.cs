using AVAHack.Characters;
using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Content;

// The shapes of the content files in Content/Data. They are read strictly (ContentDb): an unknown field, a
// missing required field or a null where none is allowed fails the load, so these records are the schema.

/// <summary>Use XP for one use of a skill: <c>base + per × amount</c>, never below 0. <c>of</c> names the amount.</summary>
public sealed record UseXpDef
{
    public required double Base { get; init; }
    public double Per { get; init; }
    /// <summary>What the amount is: damage, dc, healed, absorbed, seconds, level, or use (no amount).</summary>
    public required string Of { get; init; }
}

public sealed record SkillDef
{
    public required SkillId Id { get; init; }
    public required string Name { get; init; }
    public required SkillGroup Group { get; init; }
    public required string RaisedBy { get; init; }
    public required UseXpDef UseXp { get; init; }
}

public sealed record AncestryDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Attribute bonuses, keyed str, dex, con, int, wis, cha.</summary>
    public required Dictionary<string, int> Attributes { get; init; }
    /// <summary>Starting skill bonuses, keyed by skill id (e.g. smithing).</summary>
    public Dictionary<string, int> Skills { get; init; } = [];
    /// <summary>Trait ids the rules look for (see <see cref="Traits"/>).</summary>
    public string[] Traits { get; init; } = [];
    public required string Text { get; init; }
}

public sealed record CallingDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int HitDie { get; init; }
    public required Attribute[] Saves { get; init; }
    /// <summary>Six skills that start at +15.</summary>
    public required SkillId[] Major { get; init; }
    /// <summary>Three skills that start at +5.</summary>
    public required SkillId[] Minor { get; init; }
    public required string Signature { get; init; }
    public required string SignatureText { get; init; }
}

public sealed record BackgroundDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SkillId Skill { get; init; }
    public required string Item { get; init; }
    public required string Text { get; init; }
}

/// <summary>One effect of a perk, counted once per rank bought.</summary>
public sealed record PerkEffect
{
    /// <summary>damagePct, recoveryPct, manaCostPct, noisePct, sneakMultiplier, or flag (a named behaviour).</summary>
    public required string Kind { get; init; }
    public double Value { get; init; }
    /// <summary>What it applies to: a skill id, a damage type, a weapon kind, or a flag's name.</summary>
    public string? Scope { get; init; }
}

public sealed record PerkDef
{
    public required string Id { get; init; }
    public required SkillId Skill { get; init; }
    public required string Name { get; init; }
    /// <summary>The skill rank each rank of the perk needs; one entry per rank.</summary>
    public required int[] Ranks { get; init; }
    /// <summary>Perks in the same tree that must be owned first.</summary>
    public string[] Requires { get; init; } = [];
    public PerkEffect[] Effects { get; init; } = [];
    public required string Text { get; init; }

    public int MaxRank => Ranks.Length;
}

public sealed record ConditionDef
{
    public required ConditionId Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The attribute that saves against it every 3 s, or null for no save.</summary>
    public Attribute? Save { get; init; }
    /// <summary>How long it lasts, or null for until cured.</summary>
    public double? Seconds { get; init; }
    public string? DamagePerSecond { get; init; }
    /// <summary>noActions, attackDisadvantage, attackedWithAdvantage, noRegen, cannotApproach.</summary>
    public string[] Effects { get; init; } = [];
    public double SpellFailurePercent { get; init; }
    public double RecoveryFactor { get; init; } = 1;
    public int SavePenalty { get; init; }
    public int StrengthPenalty { get; init; }
    public required string[] Cures { get; init; }
    public required string Text { get; init; }
}

/// <summary>Trait ids that ancestries carry and the rules check for.</summary>
public static class Traits
{
    public const string Versatile = "versatile";
    public const string ExtraPerkPoint = "extraPerkPoint";
    public const string Darkvision = "darkvision";
    public const string PoisonResistance = "poisonResistance";
    public const string KeenSenses = "keenSenses";
    public const string SleepImmunity = "sleepImmunity";
    public const string Lucky = "lucky";
    public const string IllusionWard = "illusionWard";
    public const string Relentless = "relentless";
    public const string ScaledHide = "scaledHide";
    public const string HoldBreath = "holdBreath";
    public const string RawMeat = "rawMeat";
    public const string FireResistance = "fireResistance";
    public const string SparkCantrip = "sparkCantrip";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Versatile, ExtraPerkPoint, Darkvision, PoisonResistance, KeenSenses, SleepImmunity, Lucky, IllusionWard,
        Relentless, ScaledHide, HoldBreath, RawMeat, FireResistance, SparkCantrip,
    };

    public static readonly IReadOnlySet<string> PerkEffectKinds = new HashSet<string>
    {
        "damagePct", "recoveryPct", "manaCostPct", "noisePct", "sneakMultiplier", "flag",
    };

    public static readonly IReadOnlySet<string> ConditionEffects = new HashSet<string>
    {
        "noActions", "attackDisadvantage", "attackedWithAdvantage", "noRegen", "cannotApproach",
    };

    public static readonly IReadOnlySet<string> UseXpAmounts = new HashSet<string>
    {
        "damage", "dc", "healed", "absorbed", "seconds", "level", "use",
    };
}
