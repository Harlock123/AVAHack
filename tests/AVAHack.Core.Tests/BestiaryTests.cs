using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>M7: the bestiary, champions and affixes, the bosses and the Waystones (spec 12 and 3).</summary>
public sealed class BestiaryTests
{
    private const string Hall = """
        start: 2 3 east
        map:
        ###########
        #.........#
        #.........#
        #.........#
        #.........#
        #........>#
        ###########
        """;

    private static readonly ContentDb Db = ContentDb.LoadBuiltIn();

    private static GameEngine Arena(ulong seed = 1) => new(LevelLoader.Parse(Hall), seed);

    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static Monster Awake(GameEngine g, string id, Cell at, Dir facing = Dir.West)
    {
        var m = g.AddMonster(id, at, facing);
        m.State = MonsterState.Aware;
        m.LastSeen = g.Time;
        return m;
    }

    private static void Kill(GameEngine g, Monster m, DamageType type = DamageType.Bludgeoning) => g.DamageMonster(m, m.Hp + 1000, type);

    // ---- The bestiary ----

    [Fact]
    public void Every_family_of_spec_12_3_is_there_with_its_members()
    {
        var c = Db.Creatures.Where(x => x.Family != "summoned").ToList();
        Assert.True(c.Count >= 58, $"only {c.Count} creatures");
        foreach (var fam in new[] { "vermin", "restlessDead", "goblinKin", "oozes", "deepFolk", "constructs", "elementals", "sporeKin", "serpentCult", "aberrations", "unmade" })
            Assert.True(c.Count(x => x.Family == fam) >= 3, $"{fam} has too few members");
        foreach (var boss in new[] { "hollowKing", "bellwrightEngine", "motherOfCoils", "choirSinger", "unmaker" })
            Assert.NotNull(Db.Creature(boss).Boss);
        Assert.Contains(c, x => x.Id == "mimic");
    }

    [Fact]
    public void Scaling_down_fits_a_creature_to_a_shallower_depth()
    {
        var king = Db.Creature("hollowKing");
        var low = Bestiary.ScaleTo(king, king.Level - 3);
        Assert.Equal(king.Level - 3, low.Level);
        Assert.True(Dice.Parse(low.HitDice).Count < Dice.Parse(king.HitDice).Count);
        Assert.True(low.Attacks[0].ToHit < king.Attacks[0].ToHit);
        Assert.Same(king, Bestiary.Scale(king, king.Level - 3)); // Scale never goes down
    }

    // ---- Special behaviours ----

    [Fact]
    public void Summoners_call_up_their_kind_and_stop_at_three()
    {
        var g = Arena();
        var mother = Awake(g, "broodMother", new Cell(8, 1));
        mother.CalmLeft = 0;
        g.Party.Members.First().Rest();
        for (int i = 0; i < 60 * 60 && g.Monsters.Count(m => m.SummonedBy == mother.Id) < 3; i++)
        {
            g.Tick();
            foreach (var c in g.Party.Members) c.Rest(); // keep the party up while it watches
        }
        var spawned = g.Monsters.Where(m => m.SummonedBy == mother.Id).ToList();
        Assert.Equal(3, spawned.Count);
        Assert.All(spawned, s => Assert.Equal("tunnelSpider", s.Def.Id));
    }

    [Fact]
    public void Splitters_split_once_into_two_halves()
    {
        var g = Arena();
        var slick = Awake(g, "greySlick", new Cell(6, 1));
        slick.MaxHp = slick.Hp = 20;
        Kill(g, slick, DamageType.Fire);
        var halves = g.Monsters.Where(m => m.IsAlive && m.Def.Id == "greySlick").ToList();
        Assert.Equal(2, halves.Count);
        Assert.All(halves, h => { Assert.Equal(10, h.MaxHp); Assert.True(h.SplitHalf); });
        foreach (var h in halves) Kill(g, h, DamageType.Fire);
        Assert.DoesNotContain(g.Monsters, m => m.IsAlive);
    }

    [Fact]
    public void A_puffcap_bursts_on_whoever_is_next_to_it()
    {
        var g = Arena();
        var puff = Awake(g, "puffcap", new Cell(3, 3));
        double before = g.Party.Members.Sum(c => c.Hp);
        Kill(g, puff);
        Assert.True(g.Party.Members.Sum(c => c.Hp) < before);
        Assert.Contains(g.Log, l => l.Contains("bursts", StringComparison.Ordinal));
    }

    [Fact]
    public void A_spore_shepherd_raises_the_dead_as_shamblers()
    {
        var g = Arena();
        var rat = g.AddMonster("cellarRat", new Cell(7, 2), Dir.West);
        Kill(g, rat);
        var shepherd = Awake(g, "sporeShepherd", new Cell(9, 1));
        Run(g, 16);
        Assert.Contains(g.Monsters, m => m.IsAlive && m.Def.Id == "shambler" && m.SummonedBy == shepherd.Id);
        Assert.True(rat.Raised);
    }

    [Fact]
    public void The_Unsaid_raises_the_dead_as_hollow_pilgrims_three_at_a_time()
    {
        var g = Arena();
        var dead = new[] { new Cell(6, 1), new Cell(7, 1), new Cell(6, 4), new Cell(7, 4), new Cell(5, 4) }
            .Select(c => g.AddMonster("cellarRat", c, Dir.West)).ToList();
        foreach (var rat in dead) Kill(g, rat);
        var unsaid = Awake(g, "theUnsaid", new Cell(8, 2));
        for (int i = 0; i < 5; i++)
        {
            unsaid.SpecialTimer = 15;                                                  // a raise is due
            g.Tick();
        }
        var raised = g.Monsters.Where(m => m.IsAlive && m.SummonedBy == unsaid.Id).ToList();
        Assert.Equal(3, raised.Count);                                                 // three about at most
        Assert.All(raised, m => Assert.Equal("hollowPilgrim", m.Def.Id));
        Assert.Contains(g.Log, l => l.Contains("gets up, hollow", StringComparison.Ordinal));
        Assert.DoesNotContain(g.Log, l => l.Contains("Spores", StringComparison.Ordinal));
    }

    [Fact]
    public void A_summoner_keeps_its_own_number_about_the_Refractor_two()
    {
        Assert.Equal(2, Db.Creature("theRefractor").SummonMax);
        Assert.Equal(3, Db.Creature("hollowSaint").SummonMax);
        var g = Arena();
        var refractor = Awake(g, "theRefractor", new Cell(8, 2));
        for (int i = 0; i < 5; i++)
        {
            refractor.SpecialTimer = 12;
            g.Tick();
        }
        Assert.Equal(2, g.Monsters.Count(m => m.IsAlive && m.SummonedBy == refractor.Id && m.Def.Id == "shardling"));
    }

    [Fact]
    public void Invisible_creatures_show_only_close_up()
    {
        var g = Arena();
        var stalker = g.AddMonster("gloomstalker", new Cell(8, 3), Dir.West);
        Assert.True(stalker.HiddenFrom(g.PartyCell));
        Assert.False(stalker.HiddenFrom(new Cell(7, 3)));
        stalker.HurtFlash = 0.2;
        Assert.False(stalker.HiddenFrom(g.PartyCell));
    }

    // ---- Elites and champions ----

    [Fact]
    public void Elites_have_half_again_the_HP_and_two_more_AC()
    {
        var g = Arena();
        var m = g.AddMonster("shambler", new Cell(8, 1), Dir.West);
        int hp = m.MaxHp, ac = m.Ac;
        g.MakeElite(m);
        Assert.Equal(hp * 3 / 2, m.MaxHp);
        Assert.Equal(ac + 2, m.Ac);
    }

    [Fact]
    public void Champions_get_a_name_affixes_and_keep_them_through_a_save()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("test-hall"), 7, PartyTests.QuickParty(Db), Db);
        var m = g.AddMonster(Bestiary.Scale(Db.Creature("gutterGoblin"), 6), new Cell(4, 1), Dir.West);
        g.MakeChampion(m, 2, "warded", "stoneskin");
        Assert.True(m.Elite);
        Assert.EndsWith("the Warded", m.Title);
        Assert.Contains("stoneskin", m.Affixes);
        var school = m.Affixes.Single(a => a.StartsWith("ward:", StringComparison.Ordinal))[5..];
        var type = Enum.Parse<DamageType>(school);
        Assert.Equal(Affinity.Immune, m.Resistances.For(type));
        Assert.Contains("warded", g.ChampionAffixes);

        var back = GameEngine.FromSave(g.ToSave(), Db);
        var m2 = back.Monsters.Single(x => x.Title == m.Title);
        Assert.Equal(6, m2.Def.Level); // scaled again on load
        Assert.Equal(m.Ac, m2.Ac);
        Assert.Equal(Affinity.Immune, m2.Resistances.For(type));
        Assert.Equal(Affinity.Resistant, m2.Resistances.For(DamageType.Slashing));
        Assert.Contains("warded", back.ChampionAffixes);
    }

    [Fact]
    public void Shielded_champions_soak_damage_with_a_ward_that_comes_back()
    {
        var g = Arena();
        var m = Awake(g, "shambler", new Cell(8, 1));
        m.MaxHp = m.Hp = 100;
        g.MakeChampion(m, 1, "shielded");
        Assert.True(m.Ward > 0);
        int hp = m.Hp;
        g.DamageMonster(m, 5, DamageType.Slashing);
        Assert.Equal(hp, m.Hp);
        m.Ward = 0;
        Run(g, 10.1);
        Assert.True(m.Ward > 0);
    }

    [Fact]
    public void Mirrored_champions_make_two_reflections_when_they_wake()
    {
        var g = Arena();
        var m = Awake(g, "shambler", new Cell(8, 1));
        g.MakeChampion(m, 1, "mirrored");
        Run(g, 0.1);
        Assert.Equal(2, g.Monsters.Count(x => x.Title?.EndsWith("reflection", StringComparison.Ordinal) == true));
    }

    [Fact]
    public void Generated_levels_bring_elites_and_champions_at_about_the_spec_rates()
    {
        int total = 0, elites = 0, champions = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var level = Generator.Generate(Db, seed, 5);
            var g = new GameEngine(level, seed, PartyTests.QuickParty(Db), Db);
            foreach (var m in g.Monsters.Where(m => m.Boss is null))
            {
                total++;
                if (m.Title is not null) champions++;
                else if (m.Elite) elites++;
            }
        }
        double e = (double)elites / total, c = (double)champions / total;
        Assert.InRange(e, 0.04, 0.13);
        Assert.InRange(c, 0.003, 0.05);
    }

    // ---- Bosses ----

    [Fact]
    public void The_bosses_and_the_waystones_are_where_spec_12_5_puts_them()
    {
        foreach (ulong seed in new ulong[] { 3, 11, 29 })
        {
            var o = new GenOptions { MaxDepth = 12 };
            Assert.Equal("hollowKing", Generator.Generate(Db, seed, 4, o).Boss);
            Assert.NotNull(Generator.Generate(Db, seed, 5, o).Waystone);
            var eight = Generator.Generate(Db, seed, 8, o);
            Assert.Equal("bellwrightEngine", eight.Boss);
            Assert.Equal(3, eight.Valves.Count);
            Assert.Equal(5, Generator.Generate(Db, seed, 6, o).Spawns.Count(s => s.Creature == "choirSinger"));
            Assert.Equal("motherOfCoils", Generator.Generate(Db, seed, 12, o).Boss);
            var heart = Generator.Generate(Db, seed, 13, o);
            Assert.True(heart.Final);
            Assert.Equal("unmaker", heart.Boss);
            Assert.NotNull(heart.Waystone);
            Assert.Equal("The Unmade Heart", heart.Name);
            Assert.Null(Generator.Generate(Db, seed, 3, o).Boss);
            // Every boss stands where the party can reach it.
            foreach (var lvl in new[] { 4, 8, 12, 13 })
            {
                var level = Generator.Generate(Db, seed, lvl, o);
                var spawn = level.Spawns.Single(s => s.Creature == level.Boss);
                Assert.True(spawn.Cell.Distance(level.Stairs.First()) <= 6);
            }
        }
    }

    [Fact]
    public void The_stairs_stay_sealed_while_the_boss_lives()
    {
        var g = Arena();
        g.Level.Boss = "hollowKing";
        var king = g.AddMonster("hollowKing", new Cell(8, 1), Dir.South);
        g.PlaceParty(new Cell(8, 5), Dir.East);
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(GameMode.Playing, g.Mode);
        Assert.Contains(g.Log, l => l.Contains("sealed", StringComparison.Ordinal));
        Assert.True(king.IsAware);
        Kill(g, king);
        g.PlaceParty(new Cell(8, 5), Dir.East);
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(GameMode.Won, g.Mode);
    }

    [Fact]
    public void The_Hollow_King_raises_sentries_and_splits_his_crown_at_half()
    {
        var g = Arena();
        var king = Awake(g, "hollowKing", new Cell(8, 1));
        king.CalmLeft = 0;
        for (int i = 0; i < 21 * 60; i++) { g.Tick(); foreach (var c in g.Party.Members) c.Rest(); }
        Assert.Contains(g.Monsters, m => m.Def.Id == "boneSentry" && m.SummonedBy == king.Id);
        g.DamageMonster(king, king.Hp - king.MaxHp / 2 + 1, DamageType.Slashing);
        g.Tick();
        Assert.Equal(2, g.Monsters.Count(m => m.IsAlive && m.Def.Id == "ossuaryKnight"));
    }

    [Fact]
    public void The_Bellwright_Engine_cannot_be_hurt_until_its_valves_are_shut()
    {
        var g = Arena();
        var valves = new[] { new Cell(1, 1), new Cell(1, 5), new Cell(9, 1) };
        foreach (var v in valves) { g.Level.AddLever(new Lever(v, Dir.North, [])); g.Level.Valves.Add(v); }
        var engine = Awake(g, "bellwrightEngine", new Cell(6, 3));
        int hp = engine.Hp;
        g.DamageMonster(engine, 20, DamageType.Lightning);
        Assert.Equal(hp, engine.Hp);
        foreach (var l in g.Level.Levers) l.Down = true;
        g.DamageMonster(engine, 20, DamageType.Lightning);
        Assert.Equal(hp - 20, engine.Hp);
    }

    [Fact]
    public void Mother_of_Coils_can_only_be_hurt_when_the_water_drops()
    {
        var g = Arena();
        var mother = Awake(g, "motherOfCoils", new Cell(8, 1));
        int hp = mother.Hp;
        g.DamageMonster(mother, 10, DamageType.Slashing);
        Assert.Equal(hp, mother.Hp);
        for (int i = 0; i < 12 * 60 + 5; i++) { g.Tick(); foreach (var c in g.Party.Members) c.Rest(); }
        Assert.Equal(1, mother.Phase % 2);
        g.DamageMonster(mother, 10, DamageType.Slashing);
        Assert.Equal(hp - 10, mother.Hp);
    }

    [Fact]
    public void The_Choir_sings_its_fallen_back_unless_all_fall_together()
    {
        var g = Arena();
        var singers = Enumerable.Range(0, 5).Select(i => Awake(g, "choirSinger", new Cell(5 + i, 1))).ToList();
        Kill(g, singers[0]);
        Run(g, 10.2);
        Assert.True(singers[0].IsAlive);
        foreach (var s in singers) Kill(g, s);
        Run(g, 11);
        Assert.DoesNotContain(singers, s => s.IsAlive);
    }

    [Fact]
    public void The_Unmaker_borrows_the_runs_champion_affixes_and_its_fall_ends_the_Delve()
    {
        var g = Arena();
        g.ChampionAffixes.AddRange(["vampiric", "venomous"]);
        var u = Awake(g, "unmaker", new Cell(8, 1));
        g.DamageMonster(u, u.MaxHp / 3 + 1, DamageType.Force);
        g.Tick();
        Assert.Contains("vampiric", u.Affixes);
        Assert.Contains("venomous", u.Affixes);
        Kill(g, u);
        Assert.Equal(GameMode.Won, g.Mode);
    }

    [Fact]
    public void A_twelve_level_Delve_goes_on_into_the_Unmade_Heart()
    {
        var d = Delve.Start(Db, PartyTests.QuickParty(Db), new DelveOptions { Seed = "HEARTTST", Depth = 12 });
        for (int i = 1; i < 12; i++) d.Descend();
        Assert.Equal(12, d.Depth);
        d.Engine.Level.Boss = null; // as if Mother of Coils were dead
        d.Descend();
        Assert.Equal(13, d.Depth);
        Assert.True(d.Engine.Level.Final);
        Assert.Equal(GameMode.Playing, d.Engine.Mode);
        Assert.Contains(d.Engine.Monsters, m => m.Boss == "unmaker");
        var back = Delve.FromSave(d.ToSave(), Db);
        Assert.Equal(13, back.Depth);
        Assert.Contains(back.Engine.Monsters, m => m.Boss == "unmaker");
    }

    // ---- Waystones ----

    private static GameEngine AtStone()
    {
        var g = new GameEngine(LevelLoader.Parse(Hall), 5, PartyTests.QuickParty(Db), Db);
        g.Level.Waystone = new Cell(3, 3);
        return g;
    }

    [Fact]
    public void The_Waystone_opens_its_screen_when_used_and_blocks_the_way()
    {
        var g = AtStone();
        bool opened = false;
        g.WaystoneTouched += () => opened = true;
        Assert.False(g.CanPartyEnter(new Cell(3, 3)));
        g.Press(GameAction.Use);
        Run(g, 0.2);
        Assert.True(opened);
        Assert.True(g.AtWaystone);
    }

    [Fact]
    public void The_merchant_buys_and_sells_and_keeps_what_is_sold()
    {
        var g = AtStone();
        var hero = g.Party.Members.First();
        var stock = g.Stock();
        Assert.Equal(GameEngine.StockSize, stock.Count);
        Assert.All(stock, s => Assert.Equal(GameEngine.Worth(Db, s.Item) * GameEngine.BuyMarkup, s.Price));
        g.Party.Gold = 0;
        Assert.Contains("costs", g.Buy(stock[0].Index, hero));
        g.Party.Gold = stock[0].Price;
        hero.Backpack[^1] = null;
        g.Buy(stock[0].Index, hero);
        Assert.Equal(0, g.Party.Gold);
        Assert.Equal(GameEngine.StockSize - 1, g.Stock().Count);
        int slot = Array.FindIndex(hero.Backpack, i => i is not null && Db.Base(i.BaseId).Kind != Content.ItemKind.Key);
        var sold = hero.Backpack[slot]!;
        g.Sell(hero, slot);
        Assert.Null(hero.Backpack[slot]);
        Assert.Equal(Math.Max(1, GameEngine.Worth(Db, sold) / 2), g.Party.Gold);
        // The stock is the same each time the level is made.
        Assert.Equal(AtStone().Stock().Select(s => s.Item.BaseId), stock.Select(s => s.Item.BaseId));
    }

    [Fact]
    public void The_trainer_shrine_and_stash_work_and_only_at_the_stone()
    {
        var g = AtStone();
        var hero = g.Party.Members.First();
        g.Party.Gold = 10_000;
        int rank = hero.Rank(SkillId.Athletics);
        g.TrainAt(hero, SkillId.Athletics);
        Assert.Equal(rank + 1, hero.Rank(SkillId.Athletics));
        Assert.Equal(10_000 - Progression.TrainingCost(rank), g.Party.Gold);

        hero.TakeDamage((int)hero.Hp + hero.MaxHp * 2);
        hero.IsDead = true;
        int gold = g.Party.Gold;
        g.ReviveAt(hero);
        Assert.False(hero.IsDead);
        Assert.Equal(gold - GameEngine.ReviveCost(hero), g.Party.Gold);

        int slot = Array.FindIndex(hero.Backpack, i => i is not null);
        var item = hero.Backpack[slot]!;
        g.Deposit(hero, slot);
        Assert.Contains(item, g.Stash);
        Assert.Single(g.ToSave().Stash, i => i.BaseId == item.BaseId);
        g.Withdraw(g.Stash.IndexOf(item), hero);
        Assert.Empty(g.Stash);

        g.PlaceParty(new Cell(8, 1), Dir.East);
        Assert.Contains("no trainer", g.TrainAt(hero, SkillId.Athletics));
    }
}
