using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>The Ossuary Stacks (spec 37): the hush, candles, shelves, heralds and the Archivist.</summary>
public sealed class StacksTests
{
    private static readonly ContentDb Db = ContentDb.LoadBuiltIn();

    // A hall running east, with a side passage round to the south.
    private const string Hall = """
        start: 2 2 east
        map:
        ############
        #..........#
        #..........#
        #..........#
        ############
        """;

    private static GameEngine Make(string theme = "stacks")
    {
        var level = LevelLoader.Parse(Hall);
        level.Theme = theme;
        return new GameEngine(level, 5, PartyTests.QuickParty(Db), Db);
    }

    [Fact]
    public void The_Stacks_are_levels_13_to_16_with_their_families_candles_shelves_and_the_Archivist()
    {
        var levels = Enumerable.Range(1, 8).Select(i => Generator.Generate(Db, (ulong)i * 31, 13 + i % 3, new GenOptions { MaxDepth = 24 })).ToList();
        Assert.All(levels, l => Assert.Equal("stacks", l.Theme));
        Assert.All(levels, l => Assert.StartsWith("The Ossuary Stacks", l.Name));
        Assert.True(levels.Average(l => l.Stations.Count(s => s.Value == "candle")) >= 4);
        Assert.True(levels.Average(l => l.Shelves.Count) >= 15);
        var families = levels.SelectMany(l => l.Spawns).Select(s => Db.Creature(s.Creature).Family).ToHashSet();
        Assert.Contains("inkbound", families);
        Assert.Contains("mourners", families);
        // Level 16 ends the region with the Archivist, his reading candles round him.
        var boss = Generator.Generate(Db, 99, 16, new GenOptions { MaxDepth = 24 });
        Assert.Equal("archivist", boss.Boss);
        var at = boss.Spawns.Single(s => s.Creature == "archivist").Cell;
        Assert.True(boss.Stations.Count(s => s.Value == "candle" && s.Key.Distance(at) <= 8) >= 3);
        Assert.True(Generator.HasWaystone(Db, 17, 24));
    }

    // A shut door between the party (2,1) and a creature five cells east at (7,1): past a blow's noise (4 cells),
    // out of sight, but 5 steps and a door (8) along the passage.
    private const string Corner = """
        start: 2 1 east
        map:
        ##########
        #...+....#
        ##########
        """;

    [Fact]
    public void In_the_hush_a_blow_wakes_the_dead_along_the_passages()
    {
        foreach (var (theme, wakes) in new[] { ("stacks", true), ("cellar", false) })
        {
            var level = LevelLoader.Parse(Corner);
            level.Theme = theme;
            var g = new GameEngine(level, 5, PartyTests.QuickParty(Db), Db);
            var m = g.AddMonster("quillSentinel", new Cell(7, 1), Dir.East);   // round the wall: it can't see the party
            g.HandDown(HandSide.Right);
            g.HandUp(HandSide.Right);
            for (int i = 0; i < 40; i++) g.Tick();
            Assert.Equal(wakes, m.IsAware);
        }
    }

    [Fact]
    public void A_candle_shows_the_party_up_and_Use_snuffs_it()
    {
        var g = Make();
        g.Level.Stations[new Cell(3, 2)] = "candle";
        Assert.True(g.CandleLit(new Cell(3, 2)));
        Assert.True(g.Candlelit);
        g.Press(GameAction.Use);
        for (int i = 0; i < 5; i++) g.Tick();
        Assert.False(g.CandleLit(new Cell(3, 2)));
        Assert.False(g.Candlelit);
        Assert.True(g.LevelFlags.ContainsKey("snuffed:3,2") || g.LevelFlags.Keys.Any(k => k.StartsWith("snuffed:", StringComparison.Ordinal)));
        g.Press(GameAction.Use);                                                       // and lights it again
        for (int i = 0; i < 5; i++) g.Tick();
        Assert.True(g.CandleLit(new Cell(3, 2)));
        // A creature too far off to see the party in the dark sees it by candlelight.
        static bool Seen(bool lit)
        {
            var h = Make();
            h.Level.Stations[new Cell(2, 1)] = "candle";
            if (!lit) h.LevelFlags["snuffed:" + new Cell(2, 1)] = "1";
            var m = h.AddMonster("veiledMourner", new Cell(2 + 12, 2) with { X = 11 }, Dir.West);
            for (int i = 0; i < 30; i++) h.Tick();
            return m.IsAware;
        }
        Assert.True(Seen(lit: true));
    }

    [Fact]
    public void A_shelf_is_searched_once()
    {
        var g = Make();
        g.Stand(new Cell(2, 1), Dir.North);
        var wall = new Cell(2, 0);
        g.Level.Shelves.Add(wall);
        g.Press(GameAction.Use);
        for (int i = 0; i < 5; i++) g.Tick();
        Assert.Contains($"shelf:{wall}", g.LevelFlags.Keys);
        string said = g.Log[^1];
        g.Press(GameAction.Use);
        for (int i = 0; i < 5; i++) g.Tick();
        Assert.Contains("already", g.Log[^1]);
        Assert.NotEqual(said, g.Log[^1]);
    }

    [Fact]
    public void A_herald_wakes_the_hall()
    {
        var g = Make();
        var herald = g.AddMonster("candlewight", new Cell(5, 2), Dir.West);
        var other = g.AddMonster("pageSwarm", new Cell(10, 3), Dir.East);           // eight cells off, facing away
        for (int i = 0; i < 60 && !herald.IsAware; i++) g.Tick();
        Assert.True(herald.IsAware);
        Assert.True(other.IsAware);
    }

    [Fact]
    public void The_Archivist_reads_by_candlelight_and_snuffing_the_candles_bares_him()
    {
        var g = Make();
        var boss = g.AddMonster("archivist", new Cell(8, 2), Dir.West);
        g.Level.Stations[new Cell(8, 1)] = "candle";
        g.Level.Stations[new Cell(8, 3)] = "candle";
        Assert.Equal(0.4, g.ArchivistShield(boss), 6);
        int hp = boss.Hp;
        g.DamageMonster(boss, 50, DamageType.Slashing);
        Assert.Equal(30, hp - boss.Hp);                                                 // two candles turn 40% aside
        g.LevelFlags["snuffed:" + new Cell(8, 1)] = "1";
        g.LevelFlags["snuffed:" + new Cell(8, 3)] = "1";
        hp = boss.Hp;
        g.DamageMonster(boss, 50, DamageType.Slashing);
        Assert.Equal(50, hp - boss.Hp);
        // Awake, it calls pages and files heroes away.
        boss.State = MonsterState.Aware;
        for (int i = 0; i < 60 * 15; i++) g.Tick();
        Assert.Contains(g.Monsters, m => m.Def.Id == "pageSwarm");
    }
}
