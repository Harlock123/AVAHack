using AVAHack.Characters;
using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

public sealed class ProgressionTests
{
    [Fact]
    public void Skill_xp_curve()
    {
        Assert.Equal(4, Progression.XpToNext(0));
        Assert.InRange(Progression.XpToNext(15), 19.5, 20.0);
        Assert.InRange(Progression.XpToNext(50), 167, 169);
        Assert.InRange(Progression.XpToNext(90), 520, 523);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(10, 325)]
    [InlineData(30, 825)]
    public void Level_xp_curve(int level, int xp) => Assert.Equal(xp, Progression.LevelXp(level));

    [Theory]
    [InlineData(1, 40)]
    [InlineData(2, 45)]
    [InlineData(12, 95)]
    [InlineData(13, 100)]
    [InlineData(40, 100)]
    public void Skill_ceiling(int level, int ceiling) => Assert.Equal(ceiling, Progression.SkillCeiling(level));

    [Fact]
    public void Costs()
    {
        Assert.Equal(2 * 30 * 30, Progression.TrainingCost(30));
        Assert.Equal([200, 400, 800], new[] { 0, 1, 2 }.Select(Progression.RespecCost));
        Assert.Equal([4, 5, 6, 7], new[] { 6, 8, 10, 12 }.Select(Progression.HitDieAverage));
    }

    [Fact]
    public void Use_xp_comes_from_the_skill_file()
    {
        var b = Heroes.Brannoc();
        Assert.Equal(1 + 0.1 * 12, b.UseXp(SkillId.Blade, 12), 9);          // 1 + 0.1 × damage
        Assert.Equal((20 - 5) / 2.0, b.UseXp(SkillId.Lockpicking, 20), 9);  // (DC − 5) / 2
        Assert.Equal(8 / 4.0, b.UseXp(SkillId.Restoration, 8), 9);          // healed / 4
        Assert.Equal(0, b.UseXp(SkillId.Lockpicking, 2));                   // never below 0
    }

    [Fact]
    public void Xp_raises_ranks_and_carries_the_remainder()
    {
        var b = Heroes.Brannoc(); // Blade 20
        double need = Progression.XpToNext(20);
        var p = b.GainSkillXp(SkillId.Blade, need + 1, XpSource.Plain);
        Assert.Equal(21, b.Rank(SkillId.Blade));
        Assert.Equal([(SkillId.Blade, 21)], p.RankUps);
        Assert.Equal(1, b.Xp(SkillId.Blade), 9);
        Assert.Equal(21, b.CharacterXp); // character XP equals the new rank
    }

    [Fact]
    public void Ranks_level_the_character_and_a_level_gives_its_rewards()
    {
        var b = Heroes.Brannoc(); // HP 13
        // Blade 20 → 25 is 21 + 22 + 23 + 24 + 25 = 115 character XP: level 2 with 15 over.
        double xp = Enumerable.Range(20, 5).Sum(Progression.XpToNext) + 0.001;
        var p = b.GainSkillXp(SkillId.Blade, xp, XpSource.Plain);
        Assert.Equal(25, b.Rank(SkillId.Blade));
        Assert.Equal(1, p.LevelsGained);
        Assert.Equal(2, b.Level);
        Assert.Equal(15, b.CharacterXp);
        Assert.Equal(1, b.PerkPoints);
        Assert.Equal(1, b.PendingLevelChoices);
        Assert.Equal(13 + 6 + 3, b.MaxHp);  // average d10 (6) + CON
        Assert.Equal(b.MaxHp, b.Hp);        // the new HP comes full
        Assert.Equal(0, b.AttributePoints);

        Assert.True(b.ChooseLevelBonus(LevelChoice.Health));
        Assert.Equal(22 + 5, b.MaxHp);
        Assert.False(b.ChooseLevelBonus(LevelChoice.Mana));
    }

    [Fact]
    public void Stamina_and_mana_choices()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 3);
        b.ChooseLevelBonus(LevelChoice.Stamina);
        b.ChooseLevelBonus(LevelChoice.Mana);
        Assert.Equal(65 + 10, b.MaxStamina);
        Assert.Equal(25 + 10, b.MaxMana);
    }

    [Fact]
    public void Every_fourth_level_gives_two_attribute_points()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 4);
        Assert.Equal(2, b.AttributePoints);
        int hp = b.MaxHp;
        Assert.True(b.RaiseAttribute(Attribute.Con));  // 16 → 17: no new modifier yet
        Assert.True(b.RaiseAttribute(Attribute.Con));  // 18: +4, so +1 HP for each of 4 levels
        Assert.Equal(hp + 4, b.MaxHp);
        Assert.False(b.RaiseAttribute(Attribute.Str)); // no points left
        Heroes.LevelTo(b, 8);
        Assert.True(b.RaiseAttribute(Attribute.Str));
        Assert.True(b.RaiseAttribute(Attribute.Str));
        Assert.Equal(18, b.Score(Attribute.Str));
    }

    [Fact]
    public void At_most_two_points_into_one_attribute_per_level_and_never_past_20()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 8);
        Assert.Equal(4, b.AttributePoints);
        Assert.True(b.RaiseAttribute(Attribute.Con));
        Assert.True(b.RaiseAttribute(Attribute.Con));
        Assert.False(b.RaiseAttribute(Attribute.Con));
        Assert.True(b.RaiseAttribute(Attribute.Dex));
        Assert.Equal(1, b.AttributePoints);
    }

    [Fact]
    public void Levels_5_11_and_17_speed_up_the_best_weapon()
    {
        var b = Heroes.Brannoc();
        Heroes.LevelTo(b, 5);
        Assert.Equal(1, b.RecoverySpeedUps);
        var best = b.BestWeaponSkill;
        var other = Progression.WeaponSkills.First(s => s != best);
        Assert.Equal(b.RecoveryFactor(other) * 0.9, b.RecoveryFactor(best), 9);
    }

    [Fact]
    public void Skills_stop_at_the_ceiling()
    {
        var b = Heroes.Brannoc();
        b.GainSkillXp(SkillId.Lore, 1_000_000, XpSource.Plain);
        Assert.True(b.Rank(SkillId.Lore) <= b.SkillCeiling);
        Assert.Equal(b.SkillCeiling, b.Rank(SkillId.Lore));
        Assert.Equal(0, b.Xp(SkillId.Lore));
        Assert.True(b.Level > 1);
    }

    [Fact]
    public void A_skill_rises_at_most_three_ranks_in_one_fight()
    {
        var b = Heroes.Brannoc();
        b.BeginFight();
        var p = b.GainSkillXp(SkillId.Blade, 10_000, XpSource.Plain);
        Assert.Equal(3, p.RankUps.Count);
        Assert.Equal(23, b.Rank(SkillId.Blade));
        b.GainSkillXp(SkillId.Blade, 10_000, XpSource.Plain);
        Assert.Equal(23, b.Rank(SkillId.Blade));
        b.GainSkillXp(SkillId.Blunt, 10_000, XpSource.Plain); // each skill has its own three
        Assert.Equal(23, b.Rank(SkillId.Blunt));
        b.EndFight();
        b.GainSkillXp(SkillId.Blade, Progression.XpToNext(23), XpSource.Plain);
        Assert.Equal(24, b.Rank(SkillId.Blade));
    }

    [Fact]
    public void Anti_grind_rules()
    {
        var b = Heroes.Brannoc();
        Assert.Equal(0, b.EffectiveXp(SkillId.Blade, 10, new XpSource { TargetIsObjectOrAlly = true }));
        Assert.Equal(0, b.EffectiveXp(SkillId.Destruction, 10, new XpSource { SpellHadEffect = false }));
        Assert.Equal(10, b.EffectiveXp(SkillId.Destruction, 10, XpSource.Plain));
        Assert.Equal(10, b.EffectiveXp(SkillId.Lockpicking, 10, new XpSource())); // not combat or magic: no limits
        Heroes.LevelTo(b, 8);
        Assert.Equal(1, b.EffectiveXp(SkillId.Blade, 10, XpSource.Against(2)), 9);   // more than 5 below: a tenth
        Assert.Equal(10, b.EffectiveXp(SkillId.Blade, 10, XpSource.Against(3)), 9);
    }

    [Fact]
    public void Humans_and_the_delve_learn_faster()
    {
        Assert.Equal(11, Heroes.Ama().EffectiveXp(SkillId.Blade, 10, XpSource.Plain), 9);
        var b = Heroes.Brannoc();
        b.ExtraSkillXpPercent = 100; // Lone Delver
        Assert.Equal(20, b.EffectiveXp(SkillId.Blade, 10, XpSource.Plain), 9);
    }

    [Fact]
    public void Books_give_a_rank_once_per_title()
    {
        var b = Heroes.Brannoc();
        Assert.NotNull(b.ReadBook("The Hollow Blade", SkillId.Blade));
        Assert.Equal(21, b.Rank(SkillId.Blade));
        Assert.Equal(21, b.CharacterXp);
        Assert.Null(b.ReadBook("The Hollow Blade", SkillId.Blade));
        Assert.Equal(21, b.Rank(SkillId.Blade));
        Assert.True(b.HasRead("The Hollow Blade"));
    }

    [Fact]
    public void Trainers_cost_rank_squared_times_two_and_teach_five_lessons_a_level()
    {
        var b = Heroes.Brannoc(); // Lore 5
        int gold = 10_000;
        Assert.NotNull(b.Train(SkillId.Lore, ref gold));
        Assert.Equal(10_000 - 50, gold);
        for (int i = 0; i < 4; i++) Assert.NotNull(b.Train(SkillId.Lore, ref gold));
        Assert.Null(b.Train(SkillId.Lore, ref gold)); // five this level
        int poor = 10;
        var c = Heroes.Brannoc();
        Assert.Null(c.Train(SkillId.Blade, ref poor)); // 800 gold for rank 20
        Assert.Equal(10, poor);
    }
}
