namespace AVAHack.Characters;

/// <summary>The progression formulas (spec 7). Pure functions, so they can be tested and tabulated.</summary>
public static class Progression
{
    public const int MaxSkill = 100;
    public const int StartingSkill = 5;
    public const int StartingCap = 35;
    public const int MajorBonus = 15;
    public const int MinorBonus = 5;
    public const int BackgroundBonus = 10;
    public const int MaxRanksPerFight = 3;
    public const int LessonsPerLevel = 5;
    public const int TrivialTargetGap = 5;

    /// <summary>Use XP for the next rank: 4 + 0.08 × rank^1.95 (rank 15 needs about 20, rank 50 about 168, rank 90 about 521).</summary>
    public static double XpToNext(int rank) => 4 + 0.08 * Math.Pow(rank, 1.95);

    /// <summary>Character XP a skill's new rank gives: a third of the rank, rounded up (spec 7.3, as tuned in M4).</summary>
    public static int CharacterXpFor(int newRank) => (newRank + 2) / 3;

    /// <summary>Character XP for the next level: (level + 3) × 25.</summary>
    public static int LevelXp(int level) => (level + 3) * 25;

    /// <summary>The highest a skill may be at this level: 35 + 5 × level, at most 100.</summary>
    public static int SkillCeiling(int level) => Math.Min(MaxSkill, 35 + 5 * level);

    /// <summary>What a trainer charges for the next rank: rank² × 2 gold.</summary>
    public static int TrainingCost(int rank) => rank * rank * 2;

    /// <summary>What a perk reset costs, after <paramref name="previousResets"/> resets: 200, 400, 800 ... gold.</summary>
    public static int RespecCost(int previousResets) => 200 << Math.Min(previousResets, 20);

    /// <summary>The average of a hit die, rounded up: d6 → 4, d8 → 5, d10 → 6, d12 → 7.</summary>
    public static int HitDieAverage(int hitDie) => hitDie / 2 + 1;

    /// <summary>Levels at which the best weapon skill recovers 10% faster (the "extra attack").</summary>
    public static readonly IReadOnlyList<int> RecoveryLevels = [5, 11, 17];

    public static bool IsAttributeLevel(int level) => level % 4 == 0;

    public static readonly IReadOnlyList<SkillId> WeaponSkills =
        [SkillId.Blade, SkillId.Blunt, SkillId.TwoHanded, SkillId.Archery, SkillId.Unarmed];
}

/// <summary>The extra a level-up offers (spec 7.4).</summary>
public enum LevelChoice { Health, Stamina, Mana }

/// <summary>Where a use of a skill happened, for the anti-grind rules (spec 7.2).</summary>
public readonly record struct XpSource
{
    /// <summary>The level of the creature hit, fooled or evaded, if any.</summary>
    public int? TargetLevel { get; init; }
    /// <summary>Hitting a door, a crate or a friend teaches nothing.</summary>
    public bool TargetIsObjectOrAlly { get; init; }
    /// <summary>A spell teaches only when it does something: damage, healing missing HP, a ward absorbing.</summary>
    public bool SpellHadEffect { get; init; }

    public static XpSource Plain => new() { SpellHadEffect = true };
    public static XpSource Against(int level) => new() { TargetLevel = level, SpellHadEffect = true };
}

/// <summary>What a gain of skill XP led to.</summary>
public sealed record Progress(IReadOnlyList<(SkillId Skill, int NewRank)> RankUps, int LevelsGained)
{
    public static readonly Progress None = new([], 0);
}
