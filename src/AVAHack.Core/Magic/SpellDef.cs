using AVAHack.Characters;
using AVAHack.Rules;

namespace AVAHack.Magic;

/// <summary>
/// A spell (spec 10). <see cref="Target"/> says where it goes; <see cref="Effect"/> says what it does there. Power
/// scales with the school's skill and the casting attribute.
/// </summary>
public sealed record SpellDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SkillId School { get; init; }
    /// <summary>The school rank a tome needs before it can be learned.</summary>
    public int Rank { get; init; }
    public required double Mana { get; init; }
    /// <summary>Seconds of casting; a hard enough blow breaks it.</summary>
    public double CastTime { get; init; } = 0.6;
    public bool Cantrip { get; init; }
    /// <summary>bolt (flies down the line ahead), ahead (the cell in front), around (the cells beside the party),
    /// blast (3×3 two cells ahead), self, ally (the hero who needs it most), party.</summary>
    public required string Target { get; init; }
    /// <summary>damage, heal, ward, cure, condition, buff, summon, hazard, revive, utility.</summary>
    public required string Effect { get; init; }
    public string? Dice { get; init; }
    public DamageType? Type { get; init; }
    public ConditionId? Condition { get; init; }
    /// <summary>Conditions a cure removes.</summary>
    public ConditionId[] Cures { get; init; } = [];
    /// <summary>The attribute a creature saves with (half damage, or no condition).</summary>
    public Rules.Attribute? Save { get; init; }
    public double Seconds { get; init; }
    /// <summary>A buff's name, a utility's job, or the creature a summon calls.</summary>
    public string? Name2 { get; init; }
    /// <summary>A flat amount: a ward's size, a lock DC opened, a chain's extra targets.</summary>
    public int Amount { get; init; }
    /// <summary>What a bolt looks like: fire, frost, spark, acid, light, dark.</summary>
    public string Look { get; init; } = "spark";
    /// <summary>The element for Adept perks: fire, cold or lightning.</summary>
    public string? Element { get; init; }
    /// <summary>A perk flag the caster must have to learn it (Recall Soul).</summary>
    public string? NeedsPerk { get; init; }
    public required string Text { get; init; }
}
