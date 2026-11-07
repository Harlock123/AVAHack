using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

public sealed class ConditionTests
{
    private static ConditionSet New() => new(Heroes.Content);
    private static readonly Func<Attribute, int, bool> AlwaysFail = (_, _) => false;
    private static readonly Func<Attribute, int, bool> AlwaysSave = (_, _) => true;

    [Fact]
    public void Damage_over_time_lands_once_a_second()
    {
        var c = New();
        c.Apply(ConditionId.Bleeding, 12);
        var rng = new ScriptedRng(3, 4);
        Assert.Equal(0, c.Tick(0.5, rng, AlwaysFail).Damage);
        Assert.Equal(3, c.Tick(0.5, rng, AlwaysFail).Damage);
        Assert.Equal(4, c.Tick(1.0, rng, AlwaysFail).Damage);
    }

    [Fact]
    public void A_save_every_three_seconds_can_end_a_condition()
    {
        var c = New();
        c.Apply(ConditionId.Stunned, 14, seconds: 30);
        int saves = 0;
        Assert.Empty(c.Tick(2.9, new ScriptedRng(), (_, _) => { saves++; return true; }).Ended);
        Assert.Equal(0, saves);
        var t = c.Tick(0.1, new ScriptedRng(), (a, dc) => { saves++; Assert.Equal((Attribute.Con, 14), (a, dc)); return true; });
        Assert.Equal([ConditionId.Stunned], t.Ended);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void Conditions_run_out_and_unsaved_ones_wait_for_a_cure()
    {
        var c = New();
        c.Apply(ConditionId.Shocked, 10); // 6 s
        c.Tick(5, new ScriptedRng(), AlwaysFail);
        Assert.True(c.Has(ConditionId.Shocked));
        c.Tick(1, new ScriptedRng(), AlwaysFail);
        Assert.False(c.Has(ConditionId.Shocked));

        c.Apply(ConditionId.Cursed, 0); // no save, no time limit
        c.Tick(10_000, new ScriptedRng(), AlwaysSave);
        Assert.True(c.Has(ConditionId.Cursed));
        Assert.Equal(0, c.CureBy("rest"));
        Assert.Equal(1, c.CureBy("dispel"));
        Assert.False(c.Has(ConditionId.Cursed));
    }

    [Fact]
    public void Reapplying_keeps_the_worst()
    {
        var c = New();
        c.Apply(ConditionId.Bleeding, 10, seconds: 6, damagePerSecond: Dice.Parse("1d4"));
        c.Apply(ConditionId.Bleeding, 15, seconds: 2, damagePerSecond: Dice.Parse("2d4"));
        var b = c.Get(ConditionId.Bleeding)!;
        Assert.Equal((6.0, 15), (b.SecondsLeft, b.SaveDc));
        Assert.Equal(Dice.Parse("2d4"), b.DamagePerSecond);
    }

    [Fact]
    public void What_the_conditions_add_up_to()
    {
        var c = New();
        Assert.True(c.CanAct);
        Assert.Equal((0.0, 1.0, 0), (c.SpellFailurePercent, c.RecoveryFactor, c.SavePenalty));
        c.Apply(ConditionId.Shocked, 10);
        c.Apply(ConditionId.Chilled, 10);
        c.Apply(ConditionId.Cursed, 10);
        c.Apply(ConditionId.Weakened, 10);
        Assert.Equal((25.0, 1.5, 2, 2), (c.SpellFailurePercent, c.RecoveryFactor, c.SavePenalty, c.StrengthPenalty));
        Assert.True(c.CanAct);
        c.Apply(ConditionId.Paralysed, 10);
        Assert.False(c.CanAct);
        Assert.True(c.AttackedWithAdvantage);
        c.Apply(ConditionId.Diseased, 10);
        Assert.True(c.StopsRegeneration);
        c.Apply(ConditionId.Frightened, 10);
        Assert.True(c.AttacksAtDisadvantage);
    }

    [Fact]
    public void A_dwarf_takes_half_damage_from_poison()
    {
        var b = Heroes.Brannoc();
        b.BeginFight(); // no natural healing to muddy the sums
        b.ApplyCondition(ConditionId.Poisoned, 30, damagePerSecond: Dice.Parse("1d6"));
        int dealt = b.Update(1.0, new ScriptedRng(5));
        Assert.Equal(2, dealt);
        Assert.Equal(b.MaxHp - 2, b.Hp, 6);
        var p = Heroes.Pell();
        p.ApplyCondition(ConditionId.Poisoned, 30, damagePerSecond: Dice.Parse("1d6"));
        Assert.Equal(5, p.Update(1.0, new ScriptedRng(5)));
    }

    [Fact]
    public void The_character_saves_with_its_own_bonus_and_curses_make_it_harder()
    {
        var b = Heroes.Brannoc(); // CON save +5
        b.ApplyCondition(ConditionId.Stunned, 15, seconds: 30);
        b.Update(3.0, new ScriptedRng(10)); // 10 + 5 = 15: shaken off
        Assert.False(b.Conditions.Has(ConditionId.Stunned));
        b.ApplyCondition(ConditionId.Cursed, 0);
        b.ApplyCondition(ConditionId.Stunned, 15, seconds: 30);
        b.Update(3.0, new ScriptedRng(10)); // 10 + 5 − 2 = 13: still stunned
        Assert.True(b.Conditions.Has(ConditionId.Stunned));
    }
}
