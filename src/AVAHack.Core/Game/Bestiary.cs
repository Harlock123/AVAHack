using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.Game;

/// <summary>Creatures made stronger for deeper levels (spec 12.1: tiers rise with depth).</summary>
public static class Bestiary
{
    /// <summary>
    /// A copy of a creature raised to <paramref name="level"/>: more hit dice (+30% a level), +1 HP a level, +1 to
    /// hit and damage every two levels, +1 AC every three. A creature is never scaled down.
    /// </summary>
    public static CreatureDef Scale(CreatureDef d, int level)
    {
        int up = level - d.Level;
        if (up <= 0) return d;
        return ScaleTo(d, level);
    }

    /// <summary>
    /// Like <see cref="Scale"/>, but it also scales down (bosses and their summons fit the depth): below its own
    /// level a creature loses 10% of its hit dice a level (to 35% at least), and its bonuses fall the same way.
    /// </summary>
    public static CreatureDef ScaleTo(CreatureDef d, int level)
    {
        level = Math.Max(1, level);
        int up = level - d.Level;
        if (up == 0) return d;
        var hd = Dice.Parse(d.HitDice);
        int count = Math.Max(1, (int)Math.Round(hd.Count * (up > 0 ? 1 + up * 0.3 : Math.Max(0.35, 1 + up * 0.1))));
        var attacks = d.Attacks.Select(a =>
        {
            var dmg = Dice.Parse(a.Damage);
            return a with { ToHit = Math.Max(1, a.ToHit + up / 2), Damage = (dmg with { Bonus = Math.Max(0, dmg.Bonus + up / 2) }).ToString() };
        }).ToArray();
        return d with
        {
            Level = level,
            HitDice = new Dice(count, hd.Sides, Math.Max(0, hd.Bonus + up)).ToString(),
            Ac = Math.Max(10, d.Ac + up / 3),
            Perception = Math.Max(8, d.Perception + up / 3),
            Attacks = attacks,
        };
    }
}
