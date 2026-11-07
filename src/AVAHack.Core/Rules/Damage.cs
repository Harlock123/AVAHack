namespace AVAHack.Rules;

public enum DamageType { Slashing, Piercing, Bludgeoning, Fire, Cold, Lightning, Acid, Poison, Radiant, Necrotic, Psychic, Force }

public enum Affinity { Normal, Resistant, Vulnerable, Immune }

/// <summary>How a creature takes each damage type.</summary>
public sealed class Resistances
{
    private readonly HashSet<DamageType> _resist = [], _vulnerable = [], _immune = [];

    public Resistances Resist(params DamageType[] t) { _resist.UnionWith(t); return this; }
    public Resistances Vulnerable(params DamageType[] t) { _vulnerable.UnionWith(t); return this; }
    public Resistances Immune(params DamageType[] t) { _immune.UnionWith(t); return this; }

    /// <summary>Immunity wins; resistance and vulnerability together cancel out.</summary>
    public Affinity For(DamageType t)
    {
        if (_immune.Contains(t)) return Affinity.Immune;
        bool r = _resist.Contains(t), v = _vulnerable.Contains(t);
        return r == v ? Affinity.Normal : r ? Affinity.Resistant : Affinity.Vulnerable;
    }

    public static readonly Resistances None = new();
}

/// <summary>An attack roll's result.</summary>
public readonly record struct AttackResult(D20Roll Roll, int TargetAc, bool Hit, bool Critical);

/// <summary>The d20 attack and its damage (spec 6.3).</summary>
public static class Combat
{
    /// <summary>
    /// d20 + <paramref name="attackBonus"/> against <paramref name="targetAc"/>. A natural 20 always hits and is
    /// a critical; a natural 1 always misses. <paramref name="natural"/> may supply the die already rolled (for
    /// rerolls such as a Halfling's luck).
    /// </summary>
    public static AttackResult Attack(Rng rng, int attackBonus, int targetAc, Advantage adv = Advantage.None, int? natural = null)
    {
        int n = natural ?? Checks.RollD20(rng, adv);
        var roll = new D20Roll(n, n + attackBonus);
        bool hit = n == 20 || (n != 1 && roll.Total >= targetAc);
        return new AttackResult(roll, targetAc, hit, n == 20);
    }

    /// <summary>The attack bonus: attribute modifier + floor(weapon skill / 10) + other bonuses.</summary>
    public static int AttackBonus(int attributeMod, int weaponSkill, int otherBonus = 0) => attributeMod + weaponSkill / 10 + otherBonus;

    /// <summary>
    /// Weapon damage: (dice + attribute modifier) × (1 + skill / 200) × the perk multiplier, rounded down and at
    /// least 1, then resistance. A critical rolls the dice twice. Spells pass skill 0 and their own multiplier.
    /// </summary>
    public static int Damage(Rng rng, Dice dice, int attributeMod, int skill, double perkMultiplier, bool critical,
                             Affinity affinity = Affinity.Normal)
    {
        int rolled = dice.RollDice(rng) + (critical ? dice.RollDice(rng) : 0) + dice.Bonus + attributeMod;
        int raw = Math.Max(1, (int)Math.Floor(rolled * SkillMultiplier(skill) * perkMultiplier));
        return ApplyAffinity(raw, affinity);
    }

    /// <summary>1 + skill / 200: a skill of 100 deals half as much again.</summary>
    public static double SkillMultiplier(int skill) => 1 + skill / 200.0;

    /// <summary>Resistance halves (rounded down), vulnerability doubles, immunity zeroes. Applied last.</summary>
    public static int ApplyAffinity(int damage, Affinity a) => a switch
    {
        Affinity.Immune => 0,
        Affinity.Resistant => damage / 2,
        Affinity.Vulnerable => damage * 2,
        _ => damage,
    };
}
