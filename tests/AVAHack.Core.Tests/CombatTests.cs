using AVAHack.Characters;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class CombatTests
{
    /// <summary>A corridor running east from the start, with room to back off.</summary>
    private const string Corridor = """
        start: 3 1 east
        map:
        ##########
        #........#
        ##########
        """;

    private static GameEngine Arena(string map = Corridor, ulong seed = 1) => new(LevelLoader.Parse(map), seed);

    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static bool RunUntil(GameEngine g, Func<bool> done, double seconds = 30)
    {
        for (int i = 0; i < seconds * 60; i++)
        {
            if (done()) return true;
            if (g.Mode != GameMode.Playing) return done();
            g.Tick();
        }
        return done();
    }

    private static void Swing(GameEngine g, HandSide side = HandSide.Right, double hold = 0)
    {
        RunUntil(g, () => g.HandOf(side).Ready);
        g.HandDown(side);
        Run(g, hold);
        g.HandUp(side);
    }

    [Fact]
    public void The_default_hero_is_a_lone_fighter_in_chain_and_shield()
    {
        var g = Arena();
        Assert.Equal("Brannoc", g.Hero.Name);
        Assert.True(g.Hero.LoneDelver);
        Assert.Equal(19, g.Hero.MaxHp);                // (10 + 3) × 1.5
        Assert.Equal(10 + 6 + 2, g.Hero.ArmourClass);  // chain mail 6 (no DEX) and a round shield
        Assert.Equal("longsword", g.Hero.MainHand!.Id);
        Assert.Equal(SkillId.HeavyArmour, g.Hero.ArmourSkill);
    }

    [Fact]
    public void A_swing_needs_its_recovery_before_the_next()
    {
        var g = Arena();
        g.HandDown(HandSide.Right);
        g.HandUp(HandSide.Right);
        Assert.False(g.RightHand.Ready);
        double expected = 2.0 * g.Hero.RecoveryFactor(SkillId.Blade);
        Assert.Equal(expected, g.RightHand.Recovery, 6);
        Run(g, expected - 0.05);
        Assert.False(g.RightHand.Ready);
        Run(g, 0.1);
        Assert.True(g.RightHand.Ready);
    }

    [Fact]
    public void A_power_attack_costs_stamina_and_recovers_slower()
    {
        var g = Arena();
        double stamina = g.Hero.Stamina;
        Swing(g, hold: GameEngine.PowerHold + 0.05);
        Assert.True(g.Hero.Stamina < stamina - 10);
        Assert.Equal(2.0 * g.Hero.RecoveryFactor(SkillId.Blade) * 1.3, g.RightHand.RecoveryTotal, 6);
    }

    [Fact]
    public void Tapping_the_shield_bashes_and_holding_it_does_not()
    {
        var g = Arena();
        Swing(g, HandSide.Left, hold: 0.1);
        Assert.False(g.LeftHand.Ready);
        Run(g, 3);
        g.HandDown(HandSide.Left);
        Run(g, 0.5);
        Assert.True(g.Blocking);
        g.HandUp(HandSide.Left);
        Assert.True(g.LeftHand.Ready);
        Assert.False(g.Blocking);
    }

    [Fact]
    public void Swords_kill_rats_and_skill_grows_with_use()
    {
        var g = Arena();
        var rat = g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        int blade = g.Hero.Rank(SkillId.Blade);
        double xp = g.Hero.Xp(SkillId.Blade);
        Assert.True(RunUntil(g, () => { if (g.RightHand.Ready) Swing(g); return !rat.IsAlive; }, 60));
        Assert.Equal(MonsterState.Dead, rat.State);
        Assert.Equal(1, g.Kills);
        Assert.True(g.Hero.Rank(SkillId.Blade) > blade || g.Hero.Xp(SkillId.Blade) > xp);
    }

    [Fact]
    public void A_kill_gives_every_standing_hero_character_xp()
    {
        var g = new GameEngine(LevelLoader.Parse(Corridor), 1, PartyTests.QuickParty(Heroes.Content), Heroes.Content);
        var rat = g.AddMonster(Bestiary.Scale(Heroes.Content.Creature("cellarRat"), 4), new Cell(4, 1), Dir.West);
        var before = g.Party.Members.ToDictionary(c => c, c => c.CharacterXp);
        g.DamageMonster(rat, 10_000, AVAHack.Rules.DamageType.Slashing);
        Assert.Equal(1, g.Kills);
        Assert.All(g.Party.Members, c => Assert.Equal(before[c] + Progression.KillXp(4, c.Level), c.CharacterXp));
    }

    [Fact]
    public void A_creature_called_up_by_another_gives_no_character_xp()
    {
        var g = Arena();
        var rat = g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        rat.SummonedBy = 99;
        int before = g.Hero.CharacterXp;
        g.DamageMonster(rat, 10_000, AVAHack.Rules.DamageType.Slashing);
        Assert.Equal(before, g.Hero.CharacterXp);
    }

    [Fact]
    public void A_creature_closes_in_and_attacks()
    {
        var g = Arena();
        var gob = g.AddMonster("gutterGoblin", new Cell(8, 1), Dir.West);
        Assert.True(RunUntil(g, () => gob.Cell == new Cell(4, 1), 10));
        Assert.True(gob.IsAware);
        Assert.True(g.InFight);
        Assert.True(RunUntil(g, () => g.Hero.Hp < g.Hero.MaxHp, 120));
        Assert.True(g.Hero.Xp(SkillId.HeavyArmour) > 0 || g.Hero.Rank(SkillId.HeavyArmour) > 15);
    }

    [Fact]
    public void Stepping_back_during_the_tell_makes_it_miss()
    {
        var g = Arena();
        var floats = new List<FloatText>();
        g.Float += floats.Add;
        var sham = g.AddMonster("shambler", new Cell(4, 1), Dir.West);
        Assert.True(RunUntil(g, () => sham.Action == MonsterAction.WindingUp, 10));
        double hp = g.Hero.Hp;
        g.Press(GameAction.StepBack);
        Assert.True(RunUntil(g, () => sham.Action == MonsterAction.Recovering, 5));
        Assert.Equal(hp, g.Hero.Hp);
        Assert.Contains(floats, f => f.Text == "whiff");
    }

    [Fact]
    public void A_perfect_block_stops_the_blow_and_staggers_the_attacker()
    {
        var g = Arena(seed: 3);
        var floats = new List<FloatText>();
        g.Float += floats.Add;
        var sham = g.AddMonster("shambler", new Cell(4, 1), Dir.West);
        double hp = g.Hero.Hp;
        for (int round = 0; round < 25 && !floats.Any(f => f.Text == "perfect block"); round++)
        {
            Assert.True(RunUntil(g, () => sham.Action == MonsterAction.WindingUp && sham.ActionLeft < 0.12, 10));
            g.HandDown(HandSide.Left);
            RunUntil(g, () => sham.Action != MonsterAction.WindingUp, 2);
            g.HandUp(HandSide.Left);
        }
        Assert.Equal(hp, g.Hero.Hp);
        Assert.Contains(floats, f => f.Text == "perfect block");
    }

    [Fact]
    public void A_late_block_takes_part_of_the_blow_for_stamina()
    {
        var g = Arena(seed: 5);
        var floats = new List<FloatText>();
        g.Float += floats.Add;
        g.AddMonster("boneSentry", new Cell(4, 1), Dir.West);
        g.HandDown(HandSide.Left);
        Assert.True(RunUntil(g, () => floats.Any(f => f.Text.StartsWith("blocked")), 60));
        Assert.True(g.Hero.Stamina < g.Hero.MaxStamina);
    }

    [Fact]
    public void Bone_sentries_rise_again_unless_crushed()
    {
        var g = Arena(seed: 7);
        var s = g.AddMonster("boneSentry", new Cell(4, 1), Dir.West);
        Assert.True(RunUntil(g, () => { if (g.RightHand.Ready) Swing(g); return s.State == MonsterState.Pile; }, 60));
        Assert.Equal(0, g.Kills);
        Assert.True(RunUntil(g, () => s.IsAlive, 10)); // eight seconds later it stands up
        Assert.Equal(s.MaxHp, s.Hp);
        Assert.True(RunUntil(g, () => { if (g.RightHand.Ready) Swing(g); return s.State == MonsterState.Pile; }, 60));
        Assert.True(RunUntil(g, () => { if (g.LeftHand.Ready) Swing(g, HandSide.Left); return s.State == MonsterState.Dead; }, 10));
        Assert.Equal(1, g.Kills);
    }

    [Fact]
    public void A_slinger_keeps_its_distance_and_throws_stones()
    {
        var g = Arena(seed: 2);
        var sl = g.AddMonster("goblinSlinger", new Cell(7, 1), Dir.West);
        Assert.True(RunUntil(g, () => g.Projectiles.Count > 0, 10));
        Assert.True(sl.Cell.Distance(g.PartyCell) > 1);
        Assert.True(RunUntil(g, () => g.Hero.Hp < g.Hero.MaxHp, 120));
        // Walk up to it: it backs away down the corridor.
        g.Press(GameAction.StepForward);
        g.Press(GameAction.StepForward);
        Run(g, 1.0);
        Assert.True(sl.Cell.X >= 7);
    }

    [Fact]
    public void The_party_and_creatures_never_share_a_cell()
    {
        var g = Arena();
        g.AddMonster("shambler", new Cell(4, 1), Dir.West);
        g.Press(GameAction.StepForward);
        Assert.Equal(new Cell(3, 1), g.PartyCell);
        Assert.False(g.CanPartyEnter(new Cell(4, 1)));
    }

    [Fact]
    public void Rats_pack_four_to_a_cell_and_alert_each_other()
    {
        const string room = """
            start: 1 1 east
            map:
            ##########
            #........#
            #........#
            ##########
            """;
        var g = Arena(room);
        var rats = Enumerable.Range(0, 4).Select(_ => g.AddMonster("cellarRat", new Cell(8, 2), Dir.West)).ToList();
        Assert.Equal([0, 1, 2, 3], rats.Select(r => r.Slot).Order());
        Run(g, 0.1);
        Assert.All(rats, r => Assert.False(r.IsAware)); // 7 cells off: too far for a rat to see
        var scout = g.AddMonster("cellarRat", new Cell(5, 1), Dir.West);
        Run(g, 0.1);
        Assert.True(scout.IsAware);
        Assert.All(rats, r => Assert.True(r.IsAware)); // the scout saw the party; the pack heard it
    }

    [Fact]
    public void Goblins_open_doors_shamblers_do_not()
    {
        const string doors = """
            start: 1 1 east
            map:
            #######
            #...+.#
            #######
            """;
        var g = Arena(doors);
        var gob = g.AddMonster("gutterGoblin", new Cell(5, 1), Dir.West);
        Run(g, 2);
        Assert.False(gob.IsAware); // the shut door hides the party
        Swing(g);                  // but a blow is loud
        Assert.True(gob.IsAware);
        Assert.True(RunUntil(g, () => g.Level.DoorAt(4, 1)!.Opening, 10));
        Assert.True(RunUntil(g, () => gob.Cell.X <= 3, 10));

        var h = Arena(doors);
        var sham = h.AddMonster("shambler", new Cell(5, 1), Dir.West);
        Swing(h);
        Run(h, 10);
        Assert.False(h.Level.DoorAt(4, 1)!.Opening);
        Assert.Equal(new Cell(5, 1), sham.Cell);
    }

    [Fact]
    public void Pause_freezes_everything()
    {
        var g = Arena();
        var gob = g.AddMonster("gutterGoblin", new Cell(8, 1), Dir.West);
        g.TogglePause();
        g.Update(0.25);
        g.Update(0.25);
        Assert.Equal(0, g.TickCount);
        g.Press(GameAction.StepBack);
        Assert.False(g.IsMoving);
        g.TogglePause();
        g.Update(0.2);
        Assert.True(g.TickCount > 0);
    }

    [Fact]
    public void A_fallen_hero_ends_the_game()
    {
        var g = Arena();
        for (int i = 0; i < 3; i++) g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        g.AddMonster("boneSentry", new Cell(2, 1), Dir.East);
        Assert.True(RunUntil(g, () => g.Mode == GameMode.Dead, 600));
        Assert.True(g.Hero.IsDown);
        Assert.Contains("has fallen", g.LastMessage);
        g.Update(1);
        Assert.Equal(GameMode.Dead, g.Mode);
    }

    [Fact]
    public void The_fight_ends_when_the_danger_is_gone()
    {
        var g = Arena();
        var rat = g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        Run(g, 0.1);
        Assert.True(g.InFight);
        Assert.True(RunUntil(g, () => { if (g.RightHand.Ready) Swing(g); return !rat.IsAlive; }, 60));
        Run(g, GameEngine.CalmSeconds + 0.1);
        Assert.False(g.InFight);
    }

    [Fact]
    public void The_same_seed_gives_the_same_fight()
    {
        static string Play()
        {
            var g = Arena(seed: 11);
            g.AddMonster("gutterGoblin", new Cell(6, 1), Dir.West);
            g.AddMonster("goblinSlinger", new Cell(8, 1), Dir.West);
            var log = new List<string>();
            g.Float += f => log.Add($"{g.TickCount}:{f.Text}");
            for (int i = 0; i < 60 * 40 && g.Mode == GameMode.Playing; i++)
            {
                if (g.RightHand.Ready && i % 7 == 0) { g.HandDown(HandSide.Right); g.HandUp(HandSide.Right); }
                g.Tick();
            }
            return string.Join(',', log) + $"|{g.Hero.Hp}|{g.Kills}";
        }
        Assert.Equal(Play(), Play());
    }
}

public sealed class FightMemoryTests
{
    [Fact]
    public void A_creature_that_cant_see_the_party_gives_up_and_the_fight_ends()
    {
        var h = new GameEngine(LevelLoader.Parse("""
            start: 1 1 east
            map:
            #########
            #...+...#
            #########
            """));
        var r2 = h.AddMonster("shambler", new Cell(5, 1), Dir.West); // shamblers can't open doors
        h.HandDown(HandSide.Right);
        h.HandUp(HandSide.Right); // heard through the door
        Assert.True(r2.IsAware);
        for (int i = 0; i < 60 * (int)(GameEngine.GiveUpSeconds + 2); i++) h.Tick();
        Assert.False(r2.IsAware);
        Assert.False(h.InFight);
    }
}
