using AVAHack.Content;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>M6's other exit test: the quick-start party plays a four-level Delve, bot-driven, on many seeds.</summary>
public sealed class DelveReport(ITestOutputHelper output)
{
    public sealed record Run(string Seed, GameMode Mode, int Depth, double Minutes, int Kills, int Level, string Note);

    public static Run Play(ulong seed, int depth = 4, double limitMinutes = 60)
    {
        var db = ContentDb.LoadBuiltIn();
        var d = Delve.Start(db, PartyTests.QuickParty(db), new DelveOptions { Seed = DelveOptions.Code(seed), Depth = depth });
        var g = d.Engine;
        var bot = new Bot(g, (int)seed);
        double stuckSince = 0;
        int lastDepth = 1;
        while (g.Mode == GameMode.Playing && g.Time < limitMinutes * 60)
        {
            bot.Think();
            g.Tick();
            if (d.Depth != lastDepth) { lastDepth = d.Depth; stuckSince = g.Time; }
            if (g.Time - stuckSince > 15 * 60) break; // fifteen minutes on one level: call it stuck
        }
        string note = g.Mode == GameMode.Playing ? $"stuck on {bot.Goal}" : g.Mode == GameMode.Dead ? "fell" : "";
        return new Run(DelveOptions.Code(seed), g.Mode, d.Depth, g.Time / 60, g.Kills, g.Party.Members.Max(m => m.Level), note);
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
}
