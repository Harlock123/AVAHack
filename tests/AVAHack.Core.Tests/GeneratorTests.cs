using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>M6's exit test: the generator's promises, over 1,000 seeds in each region.</summary>
public sealed class GeneratorTests(ITestOutputHelper output)
{
    private static ContentDb Db => Heroes.Content;

    private static string Fingerprint(Level l)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < l.Height; y++)
        for (int x = 0; x < l.Width; x++) sb.Append((int)l.KindAt(x, y));
        sb.Append('|').AppendJoin(';', l.Doors.Select(d => $"{d.Cell}{d.Kind}{d.Locked}"));
        sb.Append('|').AppendJoin(';', l.Traps.Select(t => $"{t.Kind}{t.Cell}"));
        sb.Append('|').AppendJoin(';', l.Spawns.Select(s => $"{s.Creature}{s.Cell}{s.Level}"));
        sb.Append('|').AppendJoin(';', l.Chests.Select(c => $"{c.Cell}{c.Table}"));
        sb.Append('|').AppendJoin(';', l.Piles.Select(p => $"{p.Key}:{string.Join(",", p.Value.Select(i => i.BaseId))}"));
        return sb.ToString();
    }

    /// <summary>
    /// Walks the level working its locks in order, from the start or another cell and never through
    /// <paramref name="avoid"/>; returns what was reached. Hidden doors count as found unless <paramref name="secrets"/> is false.
    /// </summary>
    public static HashSet<Cell> Reach(Level l, Cell? from = null, Cell? avoid = null, IEnumerable<Cell>? walls = null, bool secrets = true)
    {
        var open = l.Doors.Where(d => d.Kind == DoorKind.Hand && !d.Locked || d.IsPassable).Select(d => d.Cell).ToHashSet();
        var blocked = l.Chests.Select(c => c.Cell).Concat(l.Pits.Select(p => p.Cell)).Concat(l.Traps.Where(t => t.Kind == TrapKind.CursedAltar).Select(t => t.Cell)).ToHashSet();
        if (avoid is { } a) blocked.Add(a);
        if (walls is not null) blocked.UnionWith(walls);
        HashSet<Cell> reach = [];
        bool grew = true;
        while (grew)
        {
            grew = false;
            var seen = new HashSet<Cell> { from ?? l.Start };
            var q = new Queue<Cell>(seen);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in Enum.GetValues<Dir>())
                {
                    var n = c.Step(d);
                    var k = l.KindAt(n);
                    bool secret = secrets && l.SecretAt(n) is not null; // findable: counts as a way through
                    if ((k == CellKind.Wall && !secret) || blocked.Contains(n) || (k == CellKind.Door && !open.Contains(n)) || !seen.Add(n)) continue;
                    q.Enqueue(n);
                }
            }
            reach = seen;
            foreach (var (cell, items) in l.Piles)
                if (reach.Contains(cell))
                    foreach (var key in items.Where(i => i.KeyId is not null))
                        foreach (var d in l.Doors.Where(d => d.KeyId == key.KeyId)) grew |= open.Add(d.Cell);
            foreach (var v in l.Levers.Where(v => reach.Contains(v.Cell))) foreach (var t in v.Targets) grew |= open.Add(t);
            foreach (var p in l.Plates.Where(p => reach.Contains(p.Cell))) foreach (var t in p.Targets) grew |= open.Add(t);
        }
        return reach;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(13)]      // the Unmade Heart below a 12-level Delve
    [InlineData(14, 24)]  // the Ossuary Stacks, the Rootdark and the Glass Roads of a 24-level one
    [InlineData(18, 24)]
    [InlineData(22, 24)]
    public void A_thousand_seeds_make_levels_that_can_be_finished(int depth, int maxDepth = 12)
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_GEN_RUNS"), out int n) ? n : 1000;
        int fallbacks = 0;
        var problems = new System.Collections.Concurrent.ConcurrentBag<string>();
        var placed = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();
        Parallel.For(1, runs + 1, i =>
        {
            ulong seed = (ulong)i * 2654435761UL;
            var l = Generator.Generate(Db, seed, depth, new GenOptions { MaxDepth = maxDepth });
            if (l.Plain) Interlocked.Increment(ref fallbacks);
            foreach (var v in l.Vaults) placed.TryAdd(v, 0);
            var reach = Reach(l);
            bool Near(Cell c) => reach.Contains(c) || Enum.GetValues<Dir>().Any(d => reach.Contains(c.Step(d)));
            void Need(bool ok, string what) { if (!ok) problems.Add($"seed {seed} depth {depth}: {what}"); }
            Need(l.Stairs.Count() == 1 && reach.Contains(l.Stairs.Single()), "the stairs down can't be reached");
            Need(depth == 1 || l.StairsUp.Contains(l.Start), "no stairs up at the start");
            Need(l.Landing is not { } land || reach.Contains(land), "the landing can't be reached");
            // The balance pass: a party that falls in must find the stairs from the landing, and below the first
            // level the way on mustn't run back over the stairs up (stepping on them climbs). From the landing that
            // holds without a hidden door and without crossing the stairs up either (AAAA62RB's level 12).
            Need(l.Landing is not { } fell || Reach(l, fell, avoid: depth > 1 ? l.Start : null, secrets: false).Contains(l.Stairs.Single()),
                "the stairs can't be reached from the landing without a hidden door or the stairs up");
            Need(depth == 1 || Enum.GetValues<Dir>().Select(d => l.Start.Step(d)).Where(reach.Contains)
                .Any(c => Reach(l, c, avoid: l.Start).Contains(l.Stairs.Single())), "the way to the stairs runs over the stairs up");
            Need(l.Spawns.All(s => Near(s.Cell)), "a creature can't be reached");
            Need(l.Chests.All(c => Near(c.Cell)), "a chest can't be reached");
            Need(l.Piles.Keys.All(Near), "something on the floor can't be reached");
            Need(l.Traps.Where(t => t.Kind == TrapKind.Teleporter).All(t => t.Target is { } tt && reach.Contains(tt)), "a teleporter goes nowhere reachable");
            // Deep down a sigil can't be disarmed: there must be a way round every teleporter.
            Need(Reach(l, walls: l.Traps.Where(t => t.Kind == TrapKind.Teleporter).Select(t => t.Cell)).Contains(l.Stairs.Single()),
                "the only way to the stairs runs over a teleporter");
            Need(l.Traps.Count <= 16, "too many traps");
            Need(l.Secrets.Count() is >= 0 and <= 6, "too many secrets");
            foreach (var d in l.Doors)
            {
                bool we = l.IsWall(d.Cell.X - 1, d.Cell.Y) && l.IsWall(d.Cell.X + 1, d.Cell.Y);
                bool ns = l.IsWall(d.Cell.X, d.Cell.Y - 1) && l.IsWall(d.Cell.X, d.Cell.Y + 1);
                Need(we != ns, $"the door at {d.Cell} doesn't sit between two walls");
            }
            Need(l.Doors.Where(d => d.Locked).All(d => l.Piles.Values.SelectMany(p => p).Any(i => i.KeyId == d.KeyId)), "a locked door has no key");
        });
        output.WriteLine($"depth {depth}: {runs} seeds, {fallbacks} fell back to the plain layout, {problems.Count} problems");
        foreach (var p in problems.Take(10)) output.WriteLine(p);
        Assert.Empty(problems);
        Assert.True(fallbacks <= runs / 100, $"{fallbacks} of {runs} fell back");
        // Every vault made for this region turns up somewhere.
        string theme = Generator.ThemeFor(Db, depth, maxDepth);
        if (runs >= 1000)
            Assert.All(Vault.All.Where(v => v.Themes.Contains(theme)), v => Assert.True(placed.ContainsKey(v.Name), $"{v.Name} never placed at depth {depth}"));
    }

    [Fact]
    public void The_same_seed_gives_the_same_level_and_different_seeds_differ()
    {
        var a = Fingerprint(Generator.Generate(Db, 77, 3));
        var b = Fingerprint(Generator.Generate(Db, 77, 3));
        Assert.Equal(a, b);
        Assert.NotEqual(a, Fingerprint(Generator.Generate(Db, 78, 3)));
        Assert.NotEqual(a, Fingerprint(Generator.Generate(Db, 77, 4)));
    }

    [Fact]
    public void Levels_carry_locks_secrets_traps_and_their_region()
    {
        var levels = Enumerable.Range(1, 60).Select(i => Generator.Generate(Db, (ulong)i, 6)).ToList();
        Assert.All(levels, l => Assert.Equal("works", l.Theme));
        Assert.Contains(levels, l => l.Doors.Any(d => d.Locked));
        Assert.Contains(levels, l => l.Doors.Any(d => d.Kind == DoorKind.Portcullis));
        Assert.True(levels.Average(l => l.Secrets.Count()) >= 1);
        Assert.True(levels.Average(l => l.Traps.Count) >= 4);
        Assert.Contains(levels, l => l.Illusory.Count > 0);
        Assert.True(levels.Select(l => l.Traps.Select(t => t.Kind)).SelectMany(x => x).Distinct().Count() >= 10);
    }

    [Fact]
    public void A_24_level_Delve_runs_through_six_regions_with_a_boss_ending_each_and_the_Heart_below()
    {
        string[] order = ["cellar", "works", "choir", "stacks", "rootdark", "glass"];
        for (int depth = 1; depth <= 24; depth++)
            Assert.Equal(order[(depth - 1) / 4], Generator.ThemeFor(Db, depth, 24));
        Assert.Equal("heart", Generator.ThemeFor(Db, 25, 24));
        Assert.Equal("heart", Generator.ThemeFor(Db, 13, 12));                                  // a 12-level Delve's Heart
        Assert.Equal("glass", Generator.ThemeFor(Db, 30, 40));                                  // past them all, the deepest goes on
        Assert.Equal(("hollowKing", "bellwrightEngine", "motherOfCoils"), (Generator.BossFor(Db, 4, 24), Generator.BossFor(Db, 8, 24), Generator.BossFor(Db, 12, 24)));
        Assert.Equal("unmaker", Generator.BossFor(Db, 25, 24));
        Assert.Equal("unmaker", Generator.BossFor(Db, 13, 12));
        Assert.Equal("theKnell", Generator.BossFor(Db, 14, 12));                                  // and the Knell below it
        Assert.Equal("theKnell", Generator.BossFor(Db, 26, 24));
        Assert.Null(Generator.BossFor(Db, 15, 12));
        Assert.Null(Generator.BossFor(Db, 13, 6));                                               // a short Delve has no Heart
        Assert.Null(Generator.BossFor(Db, 7, 24));
        // Every boss a region has gets its Waystone on the next level; every level of a 24-level Delve builds.
        foreach (var d in Enumerable.Range(1, 25).Where(d => Generator.BossFor(Db, d, 24) is not null && d < 25))
            Assert.True(Generator.HasWaystone(Db, d + 1, 24));
        // And every region below the first opens with one, boss or no boss.
        Assert.Equal([5, 9, 13, 17, 21, 25], Enumerable.Range(1, 25).Where(d => Generator.HasWaystone(Db, d, 24)));
        foreach (var d in Enumerable.Range(13, 13))
        {
            var l = Generator.Generate(Db, 77, d, new GenOptions { MaxDepth = 24 });
            Assert.Equal(Generator.ThemeFor(Db, d, 24), l.Theme);
            Assert.StartsWith(Db.Region(l.Theme).Name, l.Name);
            Assert.NotEmpty(l.Spawns);
            Assert.All(l.Spawns, s => Assert.Contains(Db.Creature(s.Creature).Family, Db.Region(l.Theme).Families.Append("summoned")));
        }
    }

    [Fact]
    public void Every_vault_reads_cleanly()
    {
        var vaults = Vault.All;
        Assert.True(vaults.Count >= 28);
        // Every region has handmade rooms of its own, the Heart too.
        foreach (var r in Db.Regions) Assert.True(vaults.Count(v => v.Themes.Contains(r.Id)) >= 2, $"{r.Id} has too few vaults");
        Assert.All(vaults.SelectMany(v => v.Themes), t => Assert.Contains(Db.Regions, r => r.Id == t));
        foreach (var v in vaults)
        {
            Assert.NotEmpty(v.Themes);
            foreach (var line in v.Lines.Where(l => l.Kind is "lever" or "plate" or "chest" or "monster" or "trap" or "altar" or "secret" or "illusory"))
            {
                int at = line.Kind == "trap" ? 1 : 0;
                int x = int.Parse(line.Words[at]), y = int.Parse(line.Words[at + 1]);
                Assert.InRange(x, 0, v.Width - 1);
                Assert.InRange(y, 0, v.Height - 1);
                if (line.Kind == "lever")
                {
                    var face = DirExtensions.Parse(line.Words[2]);
                    Assert.Equal('#', v.Rows[y + face.Dy()][x + face.Dx()]);
                    int tx = int.Parse(line.Words[4]), ty = int.Parse(line.Words[5]);
                    Assert.Contains(v.Rows[ty][tx], "+P");
                }
            }
        }
    }

    [Fact]
    public void Seed_codes_name_seeds_exactly()
    {
        foreach (ulong s in new ulong[] { 0, 1, 123456789, 0xFF_FFFF_FFFF })
        {
            var code = DelveOptions.Code(s);
            Assert.Equal(8, code.Length);
            Assert.Equal(s, new DelveOptions { Seed = code }.ResolveSeed());
        }
        Assert.Equal(new DelveOptions { Seed = "the cellars" }.ResolveSeed(), new DelveOptions { Seed = "the cellars" }.ResolveSeed());
    }
}
