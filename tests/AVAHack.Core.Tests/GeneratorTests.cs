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
    /// <paramref name="avoid"/>; returns what was reached.
    /// </summary>
    public static HashSet<Cell> Reach(Level l, Cell? from = null, Cell? avoid = null)
    {
        var open = l.Doors.Where(d => d.Kind == DoorKind.Hand && !d.Locked || d.IsPassable).Select(d => d.Cell).ToHashSet();
        var blocked = l.Chests.Select(c => c.Cell).Concat(l.Pits.Select(p => p.Cell)).Concat(l.Traps.Where(t => t.Kind == TrapKind.CursedAltar).Select(t => t.Cell)).ToHashSet();
        if (avoid is { } a) blocked.Add(a);
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
                    bool secret = l.SecretAt(n) is not null; // findable: counts as a way through
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
    public void A_thousand_seeds_make_levels_that_can_be_finished(int depth)
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_GEN_RUNS"), out int n) ? n : 1000;
        int fallbacks = 0;
        var problems = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(1, runs + 1, i =>
        {
            ulong seed = (ulong)i * 2654435761UL;
            var l = Generator.Generate(Db, seed, depth);
            if (l.Source.EndsWith(":99", StringComparison.Ordinal)) Interlocked.Increment(ref fallbacks);
            var reach = Reach(l);
            bool Near(Cell c) => reach.Contains(c) || Enum.GetValues<Dir>().Any(d => reach.Contains(c.Step(d)));
            void Need(bool ok, string what) { if (!ok) problems.Add($"seed {seed} depth {depth}: {what}"); }
            Need(l.Stairs.Count() == 1 && reach.Contains(l.Stairs.Single()), "the stairs down can't be reached");
            Need(depth == 1 || l.StairsUp.Contains(l.Start), "no stairs up at the start");
            Need(l.Landing is not { } land || reach.Contains(land), "the landing can't be reached");
            // The balance pass: a party that falls in must find the stairs from the landing, and below the first
            // level the way on mustn't run back over the stairs up (stepping on them climbs).
            Need(l.Landing is not { } fell || Reach(l, fell).Contains(l.Stairs.Single()), "the stairs can't be reached from the landing");
            Need(depth == 1 || Enum.GetValues<Dir>().Select(d => l.Start.Step(d)).Where(reach.Contains)
                .Any(c => Reach(l, c, avoid: l.Start).Contains(l.Stairs.Single())), "the way to the stairs runs over the stairs up");
            Need(l.Spawns.All(s => Near(s.Cell)), "a creature can't be reached");
            Need(l.Chests.All(c => Near(c.Cell)), "a chest can't be reached");
            Need(l.Piles.Keys.All(Near), "something on the floor can't be reached");
            Need(l.Traps.Where(t => t.Kind == TrapKind.Teleporter).All(t => t.Target is { } tt && reach.Contains(tt)), "a teleporter goes nowhere reachable");
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
        Assert.Equal("cellar", Generator.ThemeFor(4));
        Assert.Equal("choir", Generator.ThemeFor(12));
        Assert.Equal("cellar", Generator.ThemeFor(13)); // the regions come round again in longer Delves
    }

    [Fact]
    public void Every_vault_reads_cleanly()
    {
        var vaults = Vault.All;
        Assert.True(vaults.Count >= 15);
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
