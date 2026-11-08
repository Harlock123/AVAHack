using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

public sealed class CharacterTests
{
    [Fact]
    public void Ancestry_bonuses_are_added_to_the_scores()
    {
        var b = Heroes.Brannoc();
        Assert.Equal([16, 12, 16, 8, 12, 10], Enum.GetValues<Attribute>().Select(b.Score));
        Assert.Equal([3, 1, 3, -1, 1, 0], Enum.GetValues<Attribute>().Select(b.Mod));
        var ama = Heroes.Ama(); // human: +1 to all
        Assert.Equal([13, 11, 15, 9, 16, 13], Enum.GetValues<Attribute>().Select(ama.Score));
    }

    [Fact]
    public void Derived_statistics_at_level_1()
    {
        var b = Heroes.Brannoc();
        Assert.Equal(10 + 3, b.MaxHp);      // d10 maximum + CON
        Assert.Equal(50 + 15, b.MaxStamina);
        Assert.Equal(20 + 5, b.MaxMana);    // better of INT −1 and WIS +1
        Assert.Equal(11, b.ArmourClass);    // 10 + DEX
        Assert.Equal(10 + 1 + 1, b.PassivePerception()); // WIS +1, Survival 10 (a minor skill)
        Assert.Equal(15 * 16, b.CarryCapacityKg);
        Assert.Equal(b.MaxHp, b.Hp);
        Assert.Equal(1.0, b.BuyPriceFactor);
    }

    [Fact]
    public void Armour_class_caps_dex_by_armour()
    {
        var p = Heroes.Pell(); // DEX 17: +3
        Assert.Equal(13, p.ArmourClass);
        p.Armour = new ArmourWorn(Armour: 3, DexCap: 2, Shield: 1);
        Assert.Equal(10 + 3 + 2 + 1, p.ArmourClass);
        p.Armour = new ArmourWorn(Armour: 8, DexCap: 0);
        Assert.Equal(18, p.ArmourClass);
    }

    [Fact]
    public void A_saurian_hide_is_armour_12_plus_dex()
    {
        var s = Heroes.Make("saurian", "monk"); // DEX 14
        Assert.Equal(12 + 2, s.ArmourClass);
        s.Armour = new ArmourWorn(Armour: 1);
        Assert.Equal(10 + 1 + 2, s.ArmourClass); // worn armour replaces the hide
    }

    [Fact]
    public void Keen_senses_help_find_secret_doors()
    {
        var i = Heroes.Ithren(); // WIS 12, Survival 20
        Assert.Equal(10 + 1 + 2, i.PassivePerception());
        Assert.Equal(10 + 1 + 2 + 5, i.PassivePerception(secretDoors: true));
    }

    [Fact]
    public void Starting_skills_add_calling_ancestry_and_background_capped_at_35()
    {
        var b = Heroes.Brannoc();
        Assert.Equal(20, b.Rank(SkillId.Blade));     // 5 + major 15
        Assert.Equal(30, b.Rank(SkillId.Smithing));  // 5 + major 15 + dwarf 10
        Assert.Equal(30, b.Rank(SkillId.Block));     // 5 + major 15 + mercenary 10
        Assert.Equal(10, b.Rank(SkillId.Archery));   // 5 + minor 5
        Assert.Equal(5, b.Rank(SkillId.Lore));
        var gnome = Heroes.Make("gnome", "rogue", "cutpurse");
        Assert.Equal(35, gnome.Rank(SkillId.Lockpicking)); // 5 + 15 + 10 + 10 = 40, capped
    }

    [Fact]
    public void Saves_add_proficiency_from_the_calling()
    {
        var b = Heroes.Brannoc(); // fighter: STR and CON
        Assert.Equal(3 + 2, b.SaveBonus(Attribute.Str));
        Assert.Equal(1, b.SaveBonus(Attribute.Dex));
        Assert.True(b.Save(new ScriptedRng(10), Attribute.Str, 15));
        Assert.False(b.Save(new ScriptedRng(9), Attribute.Str, 15));
    }

    [Fact]
    public void Dwarves_save_against_poison_with_advantage_and_gnomes_against_illusion()
    {
        var b = Heroes.Brannoc(); // CON save +5
        Assert.True(b.Save(new ScriptedRng(2, 12), Attribute.Con, 17, against: "poison"));
        Assert.False(b.Save(new ScriptedRng(2, 12), Attribute.Con, 17)); // without it, only the 2 counts
        var g = Heroes.Make("gnome", "wizard"); // WIS 12 + 2 proficiency = +3
        Assert.True(g.Save(new ScriptedRng(3, 14), Attribute.Wis, 17, against: "illusion"));
    }

    [Fact]
    public void Halfling_luck_rerolls_one_natural_1_per_fight()
    {
        var p = Heroes.Pell();
        p.BeginFight();
        Assert.Equal(15, p.RollD20(new ScriptedRng(1, 15)));
        Assert.True(p.LuckUsed);
        Assert.Equal(1, p.RollD20(new ScriptedRng(1, 15)));
        p.BeginFight();
        Assert.Equal(15, p.RollD20(new ScriptedRng(1, 15)));
        Assert.Equal(1, Heroes.Brannoc().RollD20(new ScriptedRng(1, 15)));
    }

    [Fact]
    public void Attacks_use_the_attribute_and_skill_and_suffer_from_conditions()
    {
        var b = Heroes.Brannoc(); // STR +3, Blade 20 → +5
        Assert.Equal(5, b.AttackBonus(SkillId.Blade, Attribute.Str));
        Assert.True(b.Attack(new ScriptedRng(10), SkillId.Blade, Attribute.Str, 15).Hit);
        b.ApplyCondition(ConditionId.Weakened, 10);
        Assert.Equal(3, b.AttackBonus(SkillId.Blade, Attribute.Str));
        Assert.Equal(1 + 2, b.AttackBonus(SkillId.Blade, Attribute.Dex)); // Weakened only hurts STR
        b.ApplyCondition(ConditionId.Blinded, 10);
        Assert.False(b.Attack(new ScriptedRng(18, 4), SkillId.Blade, Attribute.Str, 15).Hit); // disadvantage keeps the 4
        Assert.True(b.Attack(new ScriptedRng(18, 4), SkillId.Blade, Attribute.Str, 15, Advantage.Advantage).Hit); // flank cancels it
    }

    [Fact]
    public void Spell_power_uses_the_schools_attribute()
    {
        var i = Heroes.Ithren(); // INT 16 (+3), WIS 12 (+1), Destruction 20, Restoration 10
        Assert.Equal(8 + 3 + 2, i.SpellSaveDc(SkillId.Destruction));
        Assert.Equal(1 + 1, i.SpellAttackBonus(SkillId.Restoration));
        Assert.Equal(3, i.CastingMod(SkillId.Illusion)); // the better of INT and CHA
    }

    [Fact]
    public void Recovery_is_faster_with_dex_and_slower_when_chilled()
    {
        var p = Heroes.Pell(); // DEX +3
        Assert.Equal(0.91, p.RecoveryFactor(SkillId.Blade), 6);
        p.ApplyCondition(ConditionId.Chilled, 10);
        Assert.Equal(0.91 * 1.5, p.RecoveryFactor(SkillId.Blade), 6);
    }

    [Fact]
    public void Relentless_keeps_an_orc_standing_once_per_rest()
    {
        var o = Heroes.Make("orcBlooded", "barbarian");
        o.TakeDamage(500);
        Assert.Equal(1, o.Hp);
        Assert.False(o.IsDown);
        o.TakeDamage(500);
        Assert.True(o.IsDown);
        o.Rest();
        Assert.Equal(o.MaxHp, o.Hp);
        o.TakeDamage(500);
        Assert.Equal(1, o.Hp);
    }

    [Fact]
    public void Elves_cannot_be_put_to_sleep_and_damage_wakes_a_sleeper()
    {
        Assert.False(Heroes.Ithren().ApplyCondition(ConditionId.Asleep, 20));
        var b = Heroes.Brannoc();
        Assert.True(b.ApplyCondition(ConditionId.Asleep, 20));
        Assert.False(b.Conditions.CanAct);
        b.TakeDamage(1);
        Assert.True(b.Conditions.CanAct);
    }

    [Fact]
    public void Regeneration_out_of_combat_and_in_it()
    {
        var b = Heroes.Brannoc(); // HP 13, Stamina 65, Mana 25, WIS +1
        b.TakeDamage(10);
        b.SpendStamina(60);
        b.SpendMana(20);
        b.Regenerate(10);
        Assert.Equal(3 + 13 * 0.01 * 10, b.Hp, 6);
        Assert.Equal(b.MaxStamina, b.Stamina);                    // 10% a second: full in 10 s
        Assert.Equal(5 + 25 * 0.035 * 10, b.Mana, 6);

        var c = Heroes.Brannoc();
        c.TakeDamage(10);
        c.SpendStamina(60);
        c.BeginFight();
        c.Regenerate(4);
        Assert.Equal(3, c.Hp);                                     // no HP in a fight
        Assert.Equal(5 + 65 * 0.10 * 0.25 * 4, c.Stamina, 6);      // quartered
    }

    [Fact]
    public void The_downed_and_the_diseased_do_not_heal_naturally()
    {
        var b = Heroes.Brannoc();
        b.TakeDamage(99);
        b.Regenerate(30);
        Assert.Equal(0, b.Hp);
        var d = Heroes.Brannoc();
        d.TakeDamage(5);
        d.ApplyCondition(ConditionId.Diseased, 0);
        d.Regenerate(30);
        d.Rest();
        Assert.Equal(d.MaxHp - 5, d.Hp);
        Assert.True(d.Conditions.Has(ConditionId.Diseased)); // rest doesn't cure disease
    }

    [Fact]
    public void Creation_checks_its_inputs()
    {
        var db = Heroes.Content;
        Assert.Throws<ArgumentException>(() => CharacterBuilder.Create(db, "X", "elf", "wizard", "miner", new AttributeSet(16, 8, 8, 8, 8, 8)));
        Assert.Throws<ArgumentException>(() => CharacterBuilder.Create(db, " ", "elf", "wizard", "miner", new AttributeSet(8, 8, 8, 8, 8, 8)));
        Assert.Throws<KeyNotFoundException>(() => CharacterBuilder.Create(db, "X", "dragon", "wizard", "miner", new AttributeSet(8, 8, 8, 8, 8, 8)));
        var rolled = CharacterBuilder.Create(db, "X", "elf", "wizard", "miner", new AttributeSet(18, 3, 17, 16, 9, 4), rolled: true);
        Assert.Equal(18, rolled.Score(Attribute.Str));
        Assert.Throws<ArgumentException>(() => CharacterBuilder.Create(db, "X", "elf", "wizard", "miner", new AttributeSet(19, 3, 3, 3, 3, 3), rolled: true));
    }

    [Fact]
    public void A_heroic_hero_has_scores_of_ten_to_eighteen_and_levels_slower()
    {
        var db = Heroes.Content;
        var hero = CharacterBuilder.Create(db, "X", "human", "fighter", "miner", new AttributeSet(18, 16, 15, 12, 11, 10), heroic: true);
        Assert.True(hero.Heroic);
        Assert.Throws<ArgumentException>(() => CharacterBuilder.Create(db, "X", "human", "fighter", "miner", new AttributeSet(18, 16, 15, 12, 11, 9), heroic: true));
        var plain = CharacterBuilder.Create(db, "Y", "human", "fighter", "miner", new AttributeSet(15, 14, 13, 12, 10, 8));
        Assert.False(plain.Heroic);
        // 15% more XP for each level: 115 for level 2, not 100.
        Assert.Equal(100, plain.XpToNextLevel);
        Assert.Equal(115, hero.XpToNextLevel);
        Assert.Equal(1, plain.GainCharacterXp(100));
        Assert.Equal(0, hero.GainCharacterXp(100));
        Assert.Equal(1, hero.GainCharacterXp(15));
        Assert.Equal(2, hero.Level);
        Assert.Equal((Progression.LevelXp(10) * 115 + 99) / 100, Progression.LevelXp(10, heroic: true)); // 1.15 ×, rounded up
        // A save keeps it.
        Assert.True(Character.FromSave(db, hero.ToSave()).Heroic);
        Assert.False(Character.FromSave(db, plain.ToSave()).Heroic);
    }

    [Fact]
    public void The_quick_start_party_is_a_legal_mixed_party()
    {
        var party = CharacterBuilder.QuickStartParty(Heroes.Content);
        Assert.Equal(["fighter", "rogue", "cleric", "wizard"], party.Select(c => c.Calling.Id));
        Assert.Equal(4, party.Select(c => c.Ancestry.Id).Distinct().Count());
        Assert.All(party, c => Assert.True(AttributeGen.IsValidPointBuy(c.BaseAttributes)));
        Assert.Equal(1, party[2].PerkPoints); // the human
        Assert.Equal(0, party[0].PerkPoints);
    }
}
