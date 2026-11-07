using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>The Rootdark (spec 38): spore pods, root tangles, burrowers and the Mycel Crown with its nodes.</summary>
public sealed class RootdarkTests
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
        level.Theme = "rootdark";
        return new GameEngine(level, 18, PartyTests.QuickParty(Db), Db);
    }

    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60; i++) g.Tick();
    }

    [Fact]
    public void The_Rootdark_is_levels_17_to_20_with_its_families_pods_roots_and_the_Crown()
    {
        var levels = Enumerable.Range(1, 8).Select(i => Generator.Generate(Db, (ulong)i * 37, 17 + i % 3, new GenOptions { MaxDepth = 24 })).ToList();
        Assert.All(levels, l => Assert.Equal("rootdark", l.Theme));
        Assert.All(levels, l => Assert.StartsWith("The Rootdark", l.Name));
        Assert.True(levels.Average(l => l.Stations.Count(s => s.Value == "pod")) >= 4);
        Assert.True(levels.Average(l => l.Stations.Count(s => s.Value == "roots")) >= 2);
        Assert.True(levels.Average(l => l.RootWalls.Count) >= 15);
        var families = levels.SelectMany(l => l.Spawns).Select(s => Db.Creature(s.Creature).Family).ToHashSet();
        Assert.Contains("burrowers", families);
        Assert.Contains("sporeThralls", families);
        Assert.DoesNotContain(levels.SelectMany(l => l.Spawns), s => s.Creature == "crownNode"); // only beside the Crown
        // Level 20 ends the region with the Mycel Crown and its three nodes in other rooms.
        var boss = Generator.Generate(Db, 99, 20, new GenOptions { MaxDepth = 24 });
        Assert.Equal("mycelCrown", boss.Boss);
        Assert.Equal(3, boss.Spawns.Count(s => s.Creature == "crownNode"));
        Assert.True(Generator.HasWaystone(Db, 21, 24));
    }

    [Fact]
    public void A_pod_bursts_when_the_party_comes_near_and_fire_sets_the_cloud_off()
    {
        var g = Make();
        g.Level.Stations[new Cell(4, 2)] = "pod";
        Assert.True(g.StationBlocks(new Cell(4, 2)));
        Run(g, 0.2);
        Assert.True(g.PodWhole(new Cell(4, 2)));                                       // two cells off: still whole
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.False(g.PodWhole(new Cell(4, 2)));
        Assert.False(g.StationBlocks(new Cell(4, 2)));
        var cloud = Assert.Single(g.Gas, c => c.Variant == "spores");
        Assert.Contains(g.PartyCell, cloud.Cells);
        // A creature in the cloud, and fire: the cloud goes up, burning both.
        var m = g.AddMonster("tunnelGrub", new Cell(4, 3), Dir.West);
        double hp = m.Hp, party = g.Party.Members.Sum(h => h.Hp);
        g.FireAt(new Cell(4, 2));
        Assert.DoesNotContain(g.Gas, c => c.Variant == "spores");
        Assert.True(m.Hp < hp || !m.IsAlive);
        Assert.True(g.Party.Members.Sum(h => h.Hp) < party);
    }

    [Fact]
    public void A_pod_bursts_for_a_creature_but_not_for_its_own_thralls()
    {
        var g = Make();
        g.Level.Stations[new Cell(9, 1)] = "pod";
        g.Level.Stations[new Cell(9, 3)] = "pod";
        g.AddMonster("thralledDelver", new Cell(8, 1), Dir.East);
        g.AddMonster("tunnelGrub", new Cell(8, 3), Dir.East);
        Run(g, 0.2);
        Assert.True(g.PodWhole(new Cell(9, 1)));
        Assert.False(g.PodWhole(new Cell(9, 3)));
    }

    [Fact]
    public void Roots_block_the_way_until_hacked_through_or_burned()
    {
        var g = Make();
        var roots = new Cell(3, 2);
        g.Level.Stations[roots] = "roots";
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(new Cell(2, 2), g.PartyCell);                                     // can't walk through
        for (int i = 0; i < 3 && g.RootsStand(roots); i++)
        {
            g.Press(GameAction.Use);
            Run(g, 0.5);
        }
        Assert.False(g.RootsStand(roots));                                             // the third try at most
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(roots, g.PartyCell);
        // Fire burns another tangle away at once.
        var other = new Cell(8, 2);
        g.Level.Stations[other] = "roots";
        g.FireAt(other);
        Assert.False(g.RootsStand(other));
    }

    [Fact]
    public void A_burrower_out_of_reach_comes_up_behind_the_party()
    {
        // The long way round a wall: twenty steps.
        var level = LevelLoader.Parse("""
            start: 2 1 east
            map:
            ##############
            #....#.......#
            #....#.......#
            #....#.......#
            #....#.......#
            #....#.......#
            #....#.......#
            #............#
            ##############
            """);
        level.Theme = "rootdark";
        var g = new GameEngine(level, 18, PartyTests.QuickParty(Db), Db);
        var m = g.AddMonster("rootWyrm", new Cell(11, 1), Dir.West);
        m.State = MonsterState.Aware;
        m.LastSeen = 0;
        bool burrowed = false;
        for (int i = 0; i < 60 * 12 && !burrowed; i++)
        {
            g.Tick();
            burrowed = m.Cell.Distance(g.PartyCell) <= 1 && g.Log.Any(l => l.Contains("out of the earth", StringComparison.Ordinal) || l.Contains("tunnels up", StringComparison.Ordinal));
        }
        Assert.True(burrowed, $"{m.Cell} {m.Action} {m.SpecialTimer:F1} at {g.Time:F1}s");
    }

    [Fact]
    public void The_Crown_cannot_be_hurt_while_a_node_lives_and_unburned_nodes_grow_back()
    {
        var g = Make();
        g.Level.Boss = "mycelCrown";
        var crown = g.AddMonster("mycelCrown", new Cell(9, 2), Dir.West);
        var node = g.AddMonster("crownNode", new Cell(9, 1), Dir.West);
        var burned = g.AddMonster("crownNode", new Cell(9, 3), Dir.West);
        Assert.True(g.CrownFed);
        var hp = crown.Hp;
        g.DamageMonster(crown, 40, DamageType.Slashing);
        Assert.Equal(hp, crown.Hp);
        g.DamageMonster(node, node.Hp + 50, DamageType.Slashing);
        g.DamageMonster(burned, burned.Hp + 50, DamageType.Fire);
        Assert.False(node.IsAlive);
        Assert.False(g.CrownFed);
        g.DamageMonster(crown, 40, DamageType.Slashing);
        Assert.Equal(hp - 40, crown.Hp);
        // The cut node knits itself back together; the burned one stays dead.
        Run(g, GameEngine.NodeRegrow + 1);
        Assert.True(node.IsAlive);
        Assert.False(burned.IsAlive);
        Assert.True(g.CrownFed);
    }

    [Fact]
    public void Awake_the_Crown_calls_up_thralls_and_breathes_spores()
    {
        var g = Make();
        g.Level.Boss = "mycelCrown";
        var crown = g.AddMonster("mycelCrown", new Cell(9, 2), Dir.West);
        crown.State = MonsterState.Aware;
        Run(g, 16);
        Assert.Contains(g.Monsters, m => m.Def.Id == "thralledDelver");
    }
}
