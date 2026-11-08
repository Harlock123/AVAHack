using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

public sealed class RulesTests
{
    [Fact]
    public void Pcg32_matches_the_reference_sequence()
    {
        // pcg32_srandom_r(&rng, 42, 54) from the PCG reference demo.
        var r = new Pcg32(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
        foreach (var e in expected) Assert.Equal(e, r.NextUInt());
    }

    [Fact]
    public void Dice_stay_in_range_and_are_fair()
    {
        var r = new Pcg32(7);
        var counts = new int[7];
        for (int i = 0; i < 60_000; i++) counts[r.Die(6)]++;
        Assert.Equal(0, counts[0]);
        for (int f = 1; f <= 6; f++) Assert.InRange(counts[f], 9_400, 10_600);
    }

    [Fact]
    public void Streams_are_independent_and_repeatable()
    {
        var a = new RngStreams(99);
        var b = new RngStreams(99);
        Assert.Equal(a.Loot.NextUInt(), b.Loot.NextUInt());
        Assert.NotEqual(new RngStreams(99).World.NextUInt(), new RngStreams(99).Loot.NextUInt());
        // Drawing from one stream doesn't move another.
        var c = new RngStreams(5);
        for (int i = 0; i < 100; i++) c.Loot.NextUInt();
        Assert.Equal(new RngStreams(5).World.NextUInt(), c.World.NextUInt());
    }

    [Theory]
    [InlineData("2d6+3", 2, 6, 3)]
    [InlineData("1d8", 1, 8, 0)]
    [InlineData("d4-1", 1, 4, -1)]
    [InlineData("3D10", 3, 10, 0)]
    [InlineData("5", 0, 0, 5)]
    public void Dice_parse(string text, int count, int sides, int bonus)
    {
        var d = Dice.Parse(text);
        Assert.Equal(new Dice(count, sides, bonus), d);
        Assert.Equal(d, Dice.Parse(d.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2d")]
    [InlineData("0d6")]
    [InlineData("2d1")]
    [InlineData("d6+")]
    public void Bad_dice_are_refused(string text) => Assert.Throws<FormatException>(() => Dice.Parse(text));

    [Fact]
    public void Dice_min_max_and_average()
    {
        var d = Dice.Parse("2d6+3");
        Assert.Equal((5, 15, 10.0), (d.Min, d.Max, d.Average));
        Assert.Equal(3 + 4 + 3, d.Roll(new ScriptedRng(3, 4)));
    }

    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(19, 4)]
    [InlineData(20, 5)]
    [InlineData(24, 7)]
    public void Modifiers_follow_the_floor_rule(int score, int mod) => Assert.Equal(mod, AttributeSet.Modifier(score));

    [Fact]
    public void Point_buy_costs_and_limits()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5, 7, 9], Enumerable.Range(8, 8).Select(AttributeGen.PointCost));
        Assert.True(AttributeGen.IsValidPointBuy(new AttributeSet(15, 15, 15, 8, 8, 8)));   // 27
        Assert.False(AttributeGen.IsValidPointBuy(new AttributeSet(15, 15, 15, 9, 8, 8)));  // 28
        Assert.False(AttributeGen.IsValidPointBuy(new AttributeSet(16, 8, 8, 8, 8, 8)));    // above 15
        Assert.False(AttributeGen.IsValidPointBuy(new AttributeSet(7, 8, 8, 8, 8, 8)));     // below 8
    }

    [Fact]
    public void Rolling_drops_the_lowest_of_four()
    {
        var rng = new ScriptedRng(6, 1, 5, 4,  3, 3, 3, 3,  6, 6, 6, 6,  1, 1, 1, 2,  2, 5, 5, 5,  4, 4, 4, 1);
        var (s, rolls) = AttributeGen.Roll4d6DropLowest(rng);
        Assert.Equal([15, 9, 18, 4, 15, 12], Enum.GetValues<Attribute>().Select(a => s[a]));
        Assert.Equal([6, 1, 5, 4], rolls[0]);
    }

    [Fact]
    public void A_heroic_roll_rolls_ones_again_and_keeps_no_score_under_ten()
    {
        // STR: a 1 rolled again (as 6), then 6 5 4 → 15. DEX: 2 2 2 2 → 6, under 10, so rolled again: 3 3 3 4 → 10.
        var rng = new ScriptedRng(1, 6, 5, 4, 2,  2, 2, 2, 2,  3, 3, 3, 4,  6, 6, 6, 6,  5, 5, 5, 5,  4, 4, 4, 4,  3, 4, 4, 4);
        var (s, rolls) = AttributeGen.RollHeroic(rng);
        Assert.Equal([15, 10, 18, 15, 12, 12], Enum.GetValues<Attribute>().Select(a => s[a]));
        Assert.Equal([6, 5, 4, 2], rolls[0]);
        Assert.Equal([3, 3, 3, 4], rolls[1]);
        // Over many rolls: 10–18, and about 14 on average (4d6 drop the lowest averages about 12.2).
        var real = new Pcg32(77);
        var scores = Enumerable.Range(0, 2000).SelectMany(_ => { var r = AttributeGen.RollHeroic(real).Scores; return Enum.GetValues<Attribute>().Select(a => r[a]); }).ToList();
        Assert.InRange(scores.Min(), 10, 10);
        Assert.Equal(18, scores.Max());
        Assert.InRange(scores.Average(), 13.7, 14.4);
    }

    [Fact]
    public void Advantage_keeps_the_better_die_and_disadvantage_the_worse()
    {
        Assert.Equal(17, Checks.RollD20(new ScriptedRng(5, 17), Advantage.Advantage));
        Assert.Equal(5, Checks.RollD20(new ScriptedRng(5, 17), Advantage.Disadvantage));
        Assert.Equal(5, Checks.RollD20(new ScriptedRng(5, 17), Advantage.None));
        Assert.Equal(Advantage.None, AdvantageRules.Combine(true, true));
        Assert.Equal(Advantage.Advantage, AdvantageRules.Combine(true, false));
        Assert.Equal(Advantage.None, AdvantageRules.Combine(Advantage.Advantage, Advantage.Disadvantage));
    }

    [Fact]
    public void Checks_succeed_on_meeting_the_dc()
    {
        Assert.True(Checks.Against(new ScriptedRng(10), 5, 15).Success);
        Assert.False(Checks.Against(new ScriptedRng(9), 5, 15).Success);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(8, 4)]
    [InlineData(20, 7)]
    public void Proficiency_grows_every_four_levels(int level, int bonus) => Assert.Equal(bonus, Checks.Proficiency(level));

    [Fact]
    public void Spell_dc_and_attack()
    {
        Assert.Equal(8 + 3 + 4, Checks.SpellSaveDc(3, 45));
        Assert.Equal(3 + 4, Checks.SpellAttackBonus(3, 49));
    }

    [Fact]
    public void Natural_20_always_hits_and_crits_and_natural_1_always_misses()
    {
        var crit = Combat.Attack(new ScriptedRng(20), attackBonus: -5, targetAc: 99);
        Assert.True(crit.Hit && crit.Critical);
        var fumble = Combat.Attack(new ScriptedRng(1), attackBonus: 50, targetAc: 5);
        Assert.False(fumble.Hit);
        Assert.True(Combat.Attack(new ScriptedRng(12), 3, 15).Hit);  // 15 meets 15
        Assert.False(Combat.Attack(new ScriptedRng(11), 3, 15).Hit);
        Assert.False(Combat.Attack(new ScriptedRng(19), 3, 15).Critical);
    }

    [Fact]
    public void Attack_bonus_is_attribute_plus_a_tenth_of_skill()
    {
        Assert.Equal(3 + 4 + 1, Combat.AttackBonus(3, 47, 1));
        Assert.Equal(-1, Combat.AttackBonus(-1, 9));
    }

    [Fact]
    public void Damage_scales_with_skill_and_perks_then_resistance()
    {
        var d8 = Dice.Parse("1d8");
        // (5 + 3) × 1.25 = 10
        Assert.Equal(10, Combat.Damage(new ScriptedRng(5), d8, 3, 50, 1, false));
        // with a 1.2 perk multiplier: 12
        Assert.Equal(12, Combat.Damage(new ScriptedRng(5), d8, 3, 50, 1.2, false));
        // critical: (5 + 7 + 3) × 1.25 = 18.75 → 18
        Assert.Equal(18, Combat.Damage(new ScriptedRng(5, 7), d8, 3, 50, 1, true));
        // resistance last: 18 → 9; vulnerable → 36; immune → 0
        Assert.Equal(9, Combat.Damage(new ScriptedRng(5, 7), d8, 3, 50, 1, true, Affinity.Resistant));
        Assert.Equal(36, Combat.Damage(new ScriptedRng(5, 7), d8, 3, 50, 1, true, Affinity.Vulnerable));
        Assert.Equal(0, Combat.Damage(new ScriptedRng(5, 7), d8, 3, 50, 1, true, Affinity.Immune));
        // A hit always does at least 1 before resistance.
        Assert.Equal(1, Combat.Damage(new ScriptedRng(1), Dice.Parse("1d4"), -3, 0, 1, false));
        Assert.Equal(1.5, Combat.SkillMultiplier(100));
    }

    [Fact]
    public void Resistance_and_vulnerability_cancel_and_immunity_wins()
    {
        var r = new Resistances().Resist(DamageType.Fire, DamageType.Cold).Vulnerable(DamageType.Cold, DamageType.Radiant).Immune(DamageType.Fire);
        Assert.Equal(Affinity.Immune, r.For(DamageType.Fire));
        Assert.Equal(Affinity.Normal, r.For(DamageType.Cold));
        Assert.Equal(Affinity.Vulnerable, r.For(DamageType.Radiant));
        Assert.Equal(Affinity.Normal, r.For(DamageType.Acid));
    }
}
