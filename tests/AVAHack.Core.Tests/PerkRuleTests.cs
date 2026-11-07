using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>What the M4 perks and stealth do in the rules.</summary>
public sealed class PerkRuleTests
{
    private static Character Leveled(Character c, int level = 10, SkillId? except = null)
    {
        Heroes.LevelTo(c, level, except);
        return c;
    }

    [Fact]
    public void Armour_perks_add_ac_in_the_right_armour()
    {
        var b = Leveled(Heroes.Brannoc());
        b.Equip(Heroes.Content.Weapon("longsword"), null, Heroes.Content.ArmourPiece("chainMail"));
        int ac = b.ArmourClass;
        Assert.True(b.BuyPerk("heavyArmour.juggernaut"));
        Assert.Equal(ac + 1, b.ArmourClass);
        b.Equip(Heroes.Content.Weapon("longsword"), null, Heroes.Content.ArmourPiece("leather"));
        int light = b.ArmourClass;
        Assert.True(b.BuyPerk("lightArmour.agileDefender"));
        Assert.Equal(light + 1, b.ArmourClass); // Juggernaut doesn't help in leather; Agile Defender does
    }

    [Fact]
    public void Fists_grow_with_the_unarmed_skill()
    {
        var m = Heroes.Make("human", "monk");
        Assert.Equal(new AVAHack.Rules.Dice(2, 4), GameEngine.FistDice(m)); // Unarmed 20: 1d4 + 1d4
        m.GainSkillXp(SkillId.Unarmed, 100_000, XpSource.Plain);
        Assert.True(GameEngine.FistDice(m).Count >= 3);
    }

    [Fact]
    public void Books_teach_two_ranks_to_a_bookworm()
    {
        var b = Leveled(Heroes.Ama(), 6, SkillId.Blunt);
        b.GainSkillXp(SkillId.Lore, 50_000, XpSource.Plain);
        Assert.True(b.BuyPerk("lore.appraiser"));
        Assert.True(b.BuyPerk("lore.booklore"));
        int blunt = b.Rank(SkillId.Blunt);
        b.ReadBook("book.blunt", SkillId.Blunt);
        Assert.Equal(blunt + 2, b.Rank(SkillId.Blunt));
    }

    [Fact]
    public void Power_strikes_cost_less_stamina()
    {
        var g = new GameEngine(LevelLoader.Parse("start: 1 1 east\nmap:\n#####\n#...#\n#####"));
        var h = g.Hero;
        Heroes.LevelTo(h, 12);
        h.GainSkillXp(SkillId.Athletics, 1_000_000, XpSource.Plain);
        foreach (var p in new[] { "athletics.endurance", "athletics.packMule", "athletics.secondBreath", "athletics.powerStrikes" })
            Assert.True(h.BuyPerk(p), p);
        h.Rest();
        double st = h.Stamina;
        g.HandDown(HandSide.Right);
        for (int i = 0; i < 40; i++) g.Tick();
        g.HandUp(HandSide.Right);
        Assert.Equal(GameEngine.PowerStamina * 0.75, st - h.Stamina, 1);
    }

    [Fact]
    public void Vaulters_jump_a_pit_they_can_see()
    {
        var g = new GameEngine(LevelLoader.Parse("start: 1 1 east\npit: 2 1 -> 1 1\nmap:\n######\n#.^..#\n######"));
        var h = g.Hero;
        Heroes.LevelTo(h, 6);
        h.GainSkillXp(SkillId.Athletics, 1_000_000, XpSource.Plain);
        Assert.True(h.BuyPerk("athletics.endurance"));
        Assert.True(h.BuyPerk("athletics.vault"));
        g.Press(GameAction.StepForward);
        for (int i = 0; i < 60; i++) g.Tick();
        Assert.Equal(new Cell(3, 1), g.PartyCell);
    }

    [Fact]
    public void Sneaking_fills_a_creatures_eye_slowly_and_better_stealth_fills_it_slower()
    {
        static double Fill(int stealthXp)
        {
            var g = new GameEngine(LevelLoader.Parse("start: 1 1 east\nmap:\n#########\n#.......#\n#########"));
            g.Hero.Equip(null, null, null); // no heavy armour
            g.Hero.GainSkillXp(SkillId.Stealth, stealthXp, XpSource.Plain);
            var rat = g.AddMonster("cellarRat", new Cell(5, 1), Dir.West);
            g.ToggleSneak();
            for (int i = 0; i < 30; i++) g.Tick();
            return rat.IsAware ? 1 : rat.Awareness;
        }
        double clumsy = Fill(0), sly = Fill(200_000);
        Assert.InRange(clumsy, 0.01, 0.99); // not noticed at once
        Assert.True(sly < clumsy);
    }

    [Fact]
    public void Out_of_sight_the_eye_closes_again()
    {
        var g = new GameEngine(LevelLoader.Parse("start: 1 1 east\nmap:\n#########\n#.......#\n####.####\n####.####\n#########"));
        g.Hero.Equip(null, null, null);
        var rat = g.AddMonster("cellarRat", new Cell(4, 3), Dir.North);
        g.Stand(new Cell(4, 1), Dir.South);
        g.ToggleSneak();
        for (int i = 0; i < 10; i++) g.Tick();
        double seen = rat.Awareness;
        Assert.True(seen > 0);
        g.Stand(new Cell(1, 1), Dir.East); // round the corner
        for (int i = 0; i < 120; i++) g.Tick();
        Assert.True(rat.Awareness < seen || rat.IsAware);
    }
}
