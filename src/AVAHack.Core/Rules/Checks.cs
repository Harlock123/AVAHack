namespace AVAHack.Rules;

public enum Advantage { None, Advantage, Disadvantage }

public static class AdvantageRules
{
    /// <summary>Any advantage and any disadvantage cancel out, however many of each there are.</summary>
    public static Advantage Combine(bool anyAdvantage, bool anyDisadvantage) =>
        anyAdvantage == anyDisadvantage ? Advantage.None : anyAdvantage ? Advantage.Advantage : Advantage.Disadvantage;

    public static Advantage Combine(Advantage a, Advantage b) =>
        Combine(a == Advantage.Advantage || b == Advantage.Advantage, a == Advantage.Disadvantage || b == Advantage.Disadvantage);
}

/// <summary>One d20 roll: the die kept (after advantage), and the total with bonuses.</summary>
public readonly record struct D20Roll(int Natural, int Total)
{
    public bool IsNatural20 => Natural == 20;
    public bool IsNatural1 => Natural == 1;
}

public static class Checks
{
    /// <summary>Rolls a d20, or two and keeps the better or worse.</summary>
    public static int RollD20(Rng rng, Advantage adv)
    {
        int a = rng.Die(20);
        if (adv == Advantage.None) return a;
        int b = rng.Die(20);
        return adv == Advantage.Advantage ? Math.Max(a, b) : Math.Min(a, b);
    }

    /// <summary>A plain check or save: d20 + bonus against a DC. Meeting the DC succeeds.</summary>
    public static (bool Success, D20Roll Roll) Against(Rng rng, int bonus, int dc, Advantage adv = Advantage.None)
    {
        int n = RollD20(rng, adv);
        var roll = new D20Roll(n, n + bonus);
        return (roll.Total >= dc, roll);
    }

    /// <summary>A save's proficiency bonus: 2 + floor(level / 4) (spec 6.2).</summary>
    public static int Proficiency(int level) => 2 + level / 4;

    /// <summary>A spell's save DC: 8 + casting modifier + floor(school skill / 10) (spec 6.3).</summary>
    public static int SpellSaveDc(int castingMod, int schoolSkill) => 8 + castingMod + schoolSkill / 10;

    /// <summary>A spell attack's bonus: casting modifier + floor(school skill / 10).</summary>
    public static int SpellAttackBonus(int castingMod, int schoolSkill) => castingMod + schoolSkill / 10;
}
