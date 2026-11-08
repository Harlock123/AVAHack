using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>
/// Bot-driven Delves on many seeds: M6's four-level run and M7's full twelve levels (spec sections 28, 29 and 31).
/// <list type="bullet">
/// <item><c>AVAHACK_BALANCE_RUNS</c> sets how many seeds;</item>
/// <item><c>AVAHACK_BALANCE_DEBUG=1</c> adds each hero's level, HP and best skills to the report;</item>
/// <item><c>AVAHACK_BALANCE_TRACE=&lt;seed code&gt;</c> with <c>AVAHACK_BALANCE_TRACE_FILE=&lt;path&gt;</c> writes that
/// seed's mechanisms and a map of each level as the party arrives and where it ends.</item>
/// </list>
/// </summary>
public sealed class DelveReport(ITestOutputHelper output)
{
    public sealed record Run(string Seed, GameMode Mode, int Depth, double Minutes, int Kills, int Level, string Note);

    /// <summary>A party as it stood: its heroes' saves and its gold.</summary>
    public sealed record Snapshot(IReadOnlyList<Saves.CharacterSave> Heroes, int Gold);

    /// <summary>
    /// One bot-played Delve. With <paramref name="party"/> it starts from that party at <paramref name="from"/> (the
    /// levels above are generated and passed through, unplayed); with <paramref name="stopAt"/> it stops on arriving
    /// there and returns the party in <paramref name="arrived"/>.
    /// </summary>
    public static Run Play(ulong seed, int depth = 4, double limitMinutes = 60, bool clears = false,
        Snapshot? party = null, int from = 1, int stopAt = 0, Action<Snapshot>? arrived = null)
    {
        var db = ContentDb.LoadBuiltIn();
        string code = DelveOptions.Code(seed);
        var heroes = party is null ? PartyTests.QuickParty(db) : new Party(party.Heroes.Select(h => Character.FromSave(db, h)).ToArray()) { Gold = party.Gold };
        var d = Delve.Start(db, heroes, new DelveOptions { Seed = code, Depth = depth });
        while (d.Depth < from) d.Descend();
        var g = d.Engine;
        var bot = new Bot(g, (int)seed) { Clears = clears };
        var struck = new Dictionary<string, int>();                                        // damage dealt to the heroes on this level, by creature
        g.HeroStruck += (m, _, dmg) => struck[m.Def.Id] = struck.GetValueOrDefault(m.Def.Id) + dmg;
        string? traceFile = Environment.GetEnvironmentVariable("AVAHACK_BALANCE_TRACE") == code ? Environment.GetEnvironmentVariable("AVAHACK_BALANCE_TRACE_FILE") : null;
        double stuckSince = g.Time;
        int lastDepth = d.Depth;
        while (g.Mode == GameMode.Playing && g.Time < limitMinutes * 60)
        {
            bot.Think();
            g.Tick();
            if (d.Depth != lastDepth)
            {
                lastDepth = d.Depth;
                stuckSince = g.Time;
                struck.Clear();
                if (d.Depth == stopAt)
                {
                    arrived?.Invoke(new Snapshot(g.Party.Members.Select(h => h.ToSave()).ToList(), g.Party.Gold));
                    break;
                }
                if (traceFile is not null) File.AppendAllText(traceFile, Trace(g, d.Depth, $"ARRIVE goal {bot.Goal}"));
            }
            if (g.Time - stuckSince > (clears ? 40 : 15) * 60) break; // that long on one level: call it stuck
        }
        if (traceFile is not null) File.AppendAllText(traceFile, Trace(g, d.Depth, $"END goal {bot.Goal}") + bot.ReachMap());
        string note = g.Mode == GameMode.Playing ? $"stuck on {bot.Goal}" : g.Mode == GameMode.Dead ? "fell" : "";
        if (Environment.GetEnvironmentVariable("AVAHACK_BALANCE_DEBUG") is not null)
            note += " | " + string.Join("; ", g.Party.Members.Select(m => $"{m.Name} L{m.Level} {m.MaxHp}hp AC{m.ArmourClass} " +
                string.Join(",", Enum.GetValues<SkillId>().OrderByDescending(m.Rank).Take(3).Select(s => $"{s}{m.Rank(s)}"))))
                + $" | hurt by {string.Join(", ", struck.OrderByDescending(x => x.Value).Take(5).Select(x => $"{x.Key} {x.Value}"))}"
                + $" | boss {g.LivingBoss?.Name} {bot.NodeReport} {bot.PreyReport} | about: {string.Join(", ", g.Monsters.Where(m => m.IsAlive && !m.Ally && m.IsAware && m.Cell.Distance(g.PartyCell) <= 8).Select(m => $"{m.Name}({m.Def.Level},{m.Hp:F0}hp)"))} | {string.Join(" / ", g.Log.TakeLast(int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_LOG"), out int lines) ? lines : 2))}";
        return new Run(code, g.Mode, d.Depth, g.Time / 60, g.Kills, g.Party.Members.Max(m => m.Level), note);
    }

    /// <summary>A level's mechanisms and a map: # wall, + / D L doors (shut, open, gate, locked), ^ v levers, o plates,
    /// O pits, S s secrets (hidden, found), &gt; stairs, M creatures, @ the party.</summary>
    private static string Trace(GameEngine g, int depth, string title)
    {
        var l = g.Level;
        var sb = new System.Text.StringBuilder($"{title} d{depth} start {l.Start} at {g.PartyCell}\n");
        foreach (var dr in l.Doors.Where(x => x.Locked)) sb.Append($"locked {dr.Cell} key {dr.KeyId} dc {dr.LockDc}\n");
        foreach (var (pc, items) in l.Piles) sb.Append($"pile {pc}: {string.Join(",", items.Select(i => i.KeyId ?? i.BaseId))}\n");
        foreach (var v in l.Levers) sb.Append($"lever {v.Cell} down {v.Down} -> {string.Join(",", v.Targets.Select(t => $"{t}:{l.DoorAt(t)?.Open}"))}\n");
        foreach (var p in l.Plates) sb.Append($"plate {p.Cell} {p.Mode} pressed {p.Pressed} -> {string.Join(",", p.Targets.Select(t => $"{t}:{l.DoorAt(t)?.Open}"))}\n");
        for (int y = 0; y < l.Height; y++)
        {
            for (int x = 0; x < l.Width; x++)
            {
                var c = new Cell(x, y);
                char ch = l.KindAt(c) == CellKind.Wall ? '#' : '.';
                if (l.DoorAt(c) is { } dr) ch = dr.IsPassable ? '/' : dr.Locked ? 'L' : dr.Kind == DoorKind.Hand ? '+' : 'D';
                if (l.Levers.FirstOrDefault(v => v.Cell == c) is { } lever) ch = lever.Down ? 'v' : '^';
                if (l.PlateAt(c) is not null) ch = 'o';
                if (l.PitAt(c) is not null) ch = 'O';
                if (l.SecretAt(c) is { } sec) ch = sec.Found ? 's' : 'S';
                if (l.Stairs.Contains(c)) ch = '>';
                if (g.Monsters.Any(m => m.IsAlive && m.Cell == c)) ch = 'M';
                if (c == g.PartyCell) ch = '@';
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    [Fact]
    public void The_party_can_get_through_a_four_level_delve()
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 8;
        var results = Enumerable.Range(1, runs).AsParallel().Select(i => Play((ulong)i * 15485863)).OrderBy(r => r.Seed).ToList();
        foreach (var r in results)
            output.WriteLine($"{r.Seed}: {r.Mode,-7} reached level {r.Depth}  {r.Minutes,5:F1} min  kills {r.Kills,3}  heroes at level {r.Level}  {r.Note}");
        int won = results.Count(r => r.Mode == GameMode.Won);
        output.WriteLine($"through all four levels: {won}/{runs}; average depth reached {results.Average(r => r.Depth):F1}");
        Assert.True(won >= 1, "The bot never got through a four-level Delve.");
        Assert.True(results.Average(r => r.Depth) >= 2.5);
    }

    /// <summary>
    /// M7's exit test, as the balance pass left it (spec section 31): a full 12-level Delve on Standard, with its
    /// bosses and the Unmade Heart, played by a bot that clears each level as a player would. On 16 seeds it wins
    /// about two in three, and the heroes end in their mid-twenties, level for level with the deepest creatures.
    /// </summary>
    [Fact]
    public void A_full_twelve_level_delve()
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 8;
        var results = Enumerable.Range(1, runs).AsParallel().Select(i => Play((ulong)i * 104729, 12, 12 * 60, clears: true)).OrderBy(r => r.Seed).ToList();
        foreach (var r in results)
            output.WriteLine($"{r.Seed}: {r.Mode,-7} reached level {r.Depth,2}  {r.Minutes,6:F1} min  kills {r.Kills,4}  heroes at level {r.Level,2}  {r.Note}");
        int won = results.Count(r => r.Mode == GameMode.Won);
        output.WriteLine($"won {won}/{runs}; average depth {results.Average(r => r.Depth):F1}; " +
                         $"best {results.Max(r => r.Depth)}; average hero level {results.Average(r => r.Level):F1}");
        Assert.True(won * 4 >= runs, $"The bot should win at least a quarter of full Delves; it won {won} of {runs}.");
        Assert.True(results.Average(r => r.Depth) >= 9, "The bot should usually get past the third boss.");
        Assert.DoesNotContain(results, r => r.Mode == GameMode.Playing); // stuck: a level that can't be finished
        var winners = results.Where(r => r.Mode == GameMode.Won).ToList();
        Assert.All(winners, r => Assert.InRange(r.Level, 18, 32)); // the heroes keep up with the deep creatures
    }

    /// <summary>
    /// Opt-in (AVAHACK_BALANCE_DEEP=1; it takes minutes): a full 24-level Delve through all six regions and the
    /// Unmade Heart, played by the clearing bot. A report for the balance work in spec section 37, not a gate.
    /// </summary>
    [Fact]
    public void A_full_twenty_four_level_delve()
    {
        if (Environment.GetEnvironmentVariable("AVAHACK_BALANCE_DEEP") != "1") return;
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 6;
        var results = Enumerable.Range(1, runs).AsParallel().Select(i => Play((ulong)i * 7919, 24, 24 * 60, clears: true)).OrderBy(r => r.Seed).ToList();
        foreach (var r in results)
            output.WriteLine($"{r.Seed}: {r.Mode,-7} reached level {r.Depth,2}  {r.Minutes,6:F1} min  kills {r.Kills,4}  heroes at level {r.Level,2}  {r.Note}");
        output.WriteLine($"won {results.Count(r => r.Mode == GameMode.Won)}/{runs}; average depth {results.Average(r => r.Depth):F1}; " +
                         $"best {results.Max(r => r.Depth)}; average hero level {results.Average(r => r.Level):F1}");
        Assert.DoesNotContain(results, r => r.Mode == GameMode.Playing);
    }

    private static readonly System.Text.Json.JsonSerializerOptions SnapshotJson = new(ContentDb.JsonOptions)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    [Fact]
    public void A_party_snapshot_survives_a_round_trip_through_a_file()
    {
        var db = ContentDb.LoadBuiltIn();
        var party = PartyTests.QuickParty(db);
        var snap = new Snapshot(party.Members.Select(h => h.ToSave()).ToList(), 123);
        var back = System.Text.Json.JsonSerializer.Deserialize<Snapshot>(System.Text.Json.JsonSerializer.Serialize(snap, SnapshotJson), SnapshotJson)!;
        Assert.Equal(123, back.Gold);
        Assert.Equal(party.Members.Select(h => h.Name), back.Heroes.Select(h => Character.FromSave(db, h).Name));
    }

    /// <summary>
    /// Opt-in (AVAHACK_BALANCE_GLASS=1): the Glass Roads on their own. Deep Delves are played to level 21, and each
    /// party that gets there is set down on level 21 of several other seeds and plays on to the Unmade Heart. Many
    /// more Glass Roads runs than the 24-level report gives, for the balance work in spec section 39.
    /// AVAHACK_BALANCE_RUNS sets the deep seeds (12) and AVAHACK_BALANCE_SEEDS the seeds each party plays (4);
    /// AVAHACK_BALANCE_PARTIES=&lt;folder&gt; saves the parties that reach level 21 there, and later runs start from them.
    /// </summary>
    [Fact]
    public void The_Glass_Roads_from_level_21()
    {
        if (Environment.GetEnvironmentVariable("AVAHACK_BALANCE_GLASS") != "1") return;
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 12;
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_SEEDS"), out int k) ? k : 4;
        var parties = new System.Collections.Concurrent.ConcurrentBag<(string From, Snapshot Party)>();
        // AVAHACK_BALANCE_PARTIES=<folder> keeps the parties that got there, so later runs skip the deep Delves.
        var json = SnapshotJson;
        string? folder = Environment.GetEnvironmentVariable("AVAHACK_BALANCE_PARTIES");
        if (folder is not null && Directory.Exists(folder) && Directory.GetFiles(folder, "*.json").Length > 0)
            foreach (var f in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
                parties.Add((Path.GetFileNameWithoutExtension(f), System.Text.Json.JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(f), json)!));
        else
        {
            Enumerable.Range(1, runs).AsParallel().ForAll(i =>
            {
                ulong s = (ulong)i * 7919;
                Play(s, 24, 24 * 60, clears: true, stopAt: 21, arrived: p => parties.Add((DelveOptions.Code(s), p)));
            });
            if (folder is not null)
            {
                Directory.CreateDirectory(folder);
                foreach (var (from, p) in parties)
                    try { File.WriteAllText(Path.Combine(folder, from + ".json"), System.Text.Json.JsonSerializer.Serialize(p, json)); }
                    catch (Exception e) when (e is NotSupportedException or ArgumentException or System.Text.Json.JsonException)
                    { output.WriteLine($"couldn't keep the party from {from}: {e.Message}"); }
            }
        }
        output.WriteLine($"{parties.Count} of {runs} parties reached level 21");
        var results = parties.OrderBy(p => p.From).SelectMany(p => Enumerable.Range(1, seeds).Select(j => (p.From, Seed: (ulong)j * 6151, p.Party)))
            .AsParallel().Select(x => (x.From, Run: Play(x.Seed, 24, 24 * 60, clears: true, party: x.Party, from: 21))).OrderBy(x => x.From).ThenBy(x => x.Run.Seed).ToList();
        foreach (var (from, r) in results)
            output.WriteLine($"{from} on {r.Seed}: {r.Mode,-7} reached level {r.Depth,2}  {r.Minutes,6:F1} min  kills {r.Kills,4}  heroes at level {r.Level,2}  {r.Note}");
        if (results.Count == 0) return;
        output.WriteLine($"through the Glass Roads: {results.Count(x => x.Run.Depth >= 25 || x.Run.Mode == GameMode.Won)}/{results.Count}; " +
                         $"won {results.Count(x => x.Run.Mode == GameMode.Won)}; average depth {results.Average(x => x.Run.Depth):F1}; " +
                         $"deaths by level {string.Join(", ", results.Where(x => x.Run.Mode == GameMode.Dead).GroupBy(x => x.Run.Depth).OrderBy(gr => gr.Key).Select(gr => $"{gr.Key}: {gr.Count()}"))}");
    }
}
