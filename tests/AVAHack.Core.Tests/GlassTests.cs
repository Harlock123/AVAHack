using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>The Glass Roads (spec 39): broken glass, rifts, and the Lens.</summary>
public sealed class GlassTests
{
    private static readonly ContentDb Db = ContentDb.LoadBuiltIn();

    private const string Hall = """
        start: 2 2 east
        map:
        ############
        #..........#
        #..........#
        #..........#
        ############
        """;

    private static GameEngine Make()
    {
        var level = LevelLoader.Parse(Hall);
        level.Theme = "glass";
        return new GameEngine(level, 22, PartyTests.QuickParty(Db), Db);
    }

    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60; i++) g.Tick();
    }

    [Fact]
    public void The_Glass_Roads_are_levels_21_to_24_with_their_families_glass_rifts_and_the_Lens()
    {
        var levels = Enumerable.Range(1, 8).Select(i => Generator.Generate(Db, (ulong)i * 41, 21 + i % 3, new GenOptions { MaxDepth = 24 })).ToList();
        Assert.All(levels, l => Assert.Equal("glass", l.Theme));
        Assert.All(levels, l => Assert.StartsWith("The Glass Roads", l.Name));
        Assert.InRange(levels.Average(l => l.Stations.Count(s => s.Value == "rift")), 1.5, 2);
        Assert.True(levels.Average(l => l.Shards.Count) >= 10, string.Join(",", levels.Select(l => l.Shards.Count)));
        Assert.True(levels.Average(l => l.MirrorWalls.Count) >= 15);
        var families = levels.SelectMany(l => l.Spawns).Select(s => Db.Creature(s.Creature).Family).ToHashSet();
        Assert.Contains("shardborn", families);
        Assert.Contains("hollowed", families);
        Assert.DoesNotContain("unmade", families);                                     // they come through the rifts
        var table = SpawnTables.For(Db, "glass", 22);
        Assert.Equal(0.5, table.Single(t => t.Id == "theRefractor").Weight);           // the dread are rarer
        Assert.Equal(1.0, table.Single(t => t.Id == "shardling").Weight);
        var boss = Generator.Generate(Db, 99, 24, new GenOptions { MaxDepth = 24 });
        Assert.Equal("theLens", boss.Boss);
        Assert.Contains(boss.Spawns, s => s.Creature == "theLens");
        Assert.True(Generator.HasWaystone(Db, 25, 24));                                 // the Heart's Waystone
    }

    [Fact]
    public void Packs_on_the_Glass_Roads_are_two_or_three()
    {
        Assert.Equal(3, Db.Region("glass").PackMax);
        Assert.Equal(4, Db.Region("rootdark").PackMax);                                // the rest keep 2 to 4
        // A pack of small creatures (shardlings) shares one cell: none holds more than three.
        var levels = Enumerable.Range(1, 40).Select(i => Generator.Generate(Db, (ulong)i * 977, 22, new GenOptions { MaxDepth = 24 }));
        var packs = levels.SelectMany(l => l.Spawns.Where(s => Db.Creature(s.Creature) is { Size: CreatureSize.Small } d && d.Has("pack"))
                                                    .GroupBy(s => (s.Cell, s.Creature)).Select(g => g.Count())).ToList();
        Assert.NotEmpty(packs);
        Assert.InRange(packs.Max(), 2, 3);
    }

    [Fact]
    public void Broken_glass_crunches_once_cutting_the_careless_and_waking_the_hall()
    {
        var g = Make();
        var glass = new Cell(3, 2);
        g.Level.Shards.Add(glass);
        var m = g.AddMonster("hollowPilgrim", new Cell(8, 1), Dir.North);               // facing away, out of earshot of a step
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(glass, g.PartyCell);
        Assert.False(g.ShardsAt(glass));
        Assert.DoesNotContain(glass, g.Level.Shards);                                   // ground to powder
        Assert.True(m.IsAware);
        Assert.Contains(g.Log, l => l.Contains("crunches", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rift_lets_the_Unmade_through_until_it_is_spent_or_sealed()
    {
        var g = Make();
        var rift = new Cell(5, 2);
        g.Level.Stations[rift] = "rift";
        Assert.True(g.RiftOpen(rift));
        // Each one through is cut down at once, so the party lives to see the rift spent.
        for (int i = 0; i < GameEngine.RiftMax; i++)
        {
            Assert.True(g.RiftOpen(rift));
            int before = g.Monsters.Count;
            Run(g, GameEngine.RiftSeconds + 0.1);
            Assert.Equal(before + 1, g.Monsters.Count);
            var came = g.Monsters[^1];
            Assert.Equal("unmade", came.Def.Family);
            Assert.False(came.Def.Has("summoner"));                                     // no Hollow Saints: no endless hounds
            Assert.Equal(g.Level.Depth + GameEngine.RiftLevel, came.Def.Level);         // at the region's strength
            g.DamageMonster(came, came.Hp + 100, DamageType.Force);
        }
        Assert.False(g.RiftOpen(rift));                                                // spent after three
        int spent = g.Monsters.Count;
        Run(g, GameEngine.RiftSeconds + 1);
        Assert.Equal(spent, g.Monsters.Count);

        // Another, sealed by Use: the third try at most.
        var h = Make();
        h.Level.Stations[new Cell(3, 2)] = "rift";
        for (int i = 0; i < 3 && h.RiftOpen(new Cell(3, 2)); i++)
        {
            h.Press(GameAction.Use);
            Run(h, 0.5);
        }
        Assert.False(h.RiftOpen(new Cell(3, 2)));
        Assert.True(h.LevelFlags.ContainsKey($"sealed:{new Cell(3, 2)}"));
    }

    [Fact]
    public void The_Lens_learns_a_blow_and_turns_the_same_kind_aside()
    {
        var g = Make();
        g.Level.Boss = "theLens";
        var lens = g.AddMonster("theLens", new Cell(9, 2), Dir.West);
        var hp = lens.Hp;
        g.DamageMonster(lens, 40, DamageType.Slashing);
        Assert.Equal(hp - 40, lens.Hp);                                                 // the first blow lands in full
        hp = lens.Hp;
        g.DamageMonster(lens, 40, DamageType.Slashing);
        Assert.Equal(10, hp - lens.Hp);                                                 // learned: three quarters turned aside
        hp = lens.Hp;
        g.DamageMonster(lens, 40, DamageType.Bludgeoning);
        Assert.Equal(40, hp - lens.Hp);                                                 // a new kind of blow
    }

    [Fact]
    public void The_Lens_beam_hits_the_party_in_its_line_and_misses_it_out_of_line()
    {
        static double Hurt(bool stepAside)
        {
            var g = Make();
            g.Level.Boss = "theLens";
            var lens = g.AddMonster("theLens", new Cell(9, 2), Dir.West);
            lens.State = MonsterState.Aware;
            lens.SpecialTimer = GameEngine.BeamSeconds;                                  // about to gather light
            g.Tick();
            Assert.True(g.InBeam(g.PartyCell));
            if (stepAside) g.Stand(new Cell(2, 1), Dir.East);
            double before = g.Party.Members.Sum(h => h.Hp);
            Run(g, GameEngine.BeamCharge + 0.2);
            return before - g.Party.Members.Sum(h => h.Hp);
        }
        Assert.True(Hurt(stepAside: false) > 0);
        Assert.Equal(0, Hurt(stepAside: true));
    }

    [Fact]
    public void At_half_health_the_Lens_sheds_shards()
    {
        var g = Make();
        g.Level.Boss = "theLens";
        var lens = g.AddMonster("theLens", new Cell(9, 2), Dir.West);
        lens.State = MonsterState.Aware;
        g.DamageMonster(lens, lens.MaxHp / 2 + 5, DamageType.Force);
        g.Tick();
        Assert.Contains(g.Monsters, m => m.Def.Id == "lensShard");
    }
}
