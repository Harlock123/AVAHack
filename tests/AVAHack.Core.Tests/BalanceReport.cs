using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>
/// Plays The Cellars with <see cref="Bot"/> over many seeds and reports how it went. Set AVAHACK_BALANCE_RUNS for
/// more runs; read the report with <c>--filter BalanceReport --logger "console;verbosity=detailed"</c>.
/// </summary>
public sealed class BalanceReport(ITestOutputHelper output)
{
    private const double TimeLimit = 40 * 60;

    public sealed record Run(ulong Seed, GameMode Mode, double Seconds, int Kills, int Level, double HpLeft, string Goal, string Killer);

    public static Run Play(ulong seed, bool party = false)
    {
        var db = Content.ContentDb.LoadBuiltIn();
        var g = party
            ? new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), seed, PartyTests.QuickParty(db), db)
            : new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), seed);
        var bot = new Bot(g, (int)seed);
        string killer = "";
        while (g.Mode == GameMode.Playing && g.Time < TimeLimit)
        {
            bot.Think();
            g.Tick();
        }
        if (g.Mode == GameMode.Dead)
            killer = string.Join("+", g.Monsters.Where(m => m.IsAlive && m.Cell.Distance(g.PartyCell) <= 2).Select(m => m.Def.Id).Distinct());
        return new Run(seed, g.Mode, g.Time, g.Kills, g.Hero.Level, g.Hero.Hp, bot.Goal ?? "", killer);
    }

    [Fact]
    public void The_cellars_can_be_won_and_are_not_a_walkover()
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 12;
        var results = Enumerable.Range(1, runs).AsParallel().Select(i => Play((ulong)i * 7919)).OrderBy(r => r.Seed).ToList();
        foreach (var r in results)
            output.WriteLine($"seed {r.Seed,6}: {r.Mode,-7} {r.Seconds / 60,5:F1} min  kills {r.Kills,2}  level {r.Level}  hp {r.HpLeft,4:F0}  goal {r.Goal}  {r.Killer}");
        var won = results.Where(r => r.Mode == GameMode.Won).ToList();
        output.WriteLine($"won {won.Count}/{runs}; died {results.Count(r => r.Mode == GameMode.Dead)}; timed out {results.Count(r => r.Mode == GameMode.Playing)}");
        if (won.Count > 0) output.WriteLine($"winning runs: {won.Average(r => r.Seconds) / 60:F1} min on average, level {won.Average(r => r.Level):F1}, {won.Average(r => r.Kills):F1} kills");

        Assert.True(won.Count > 0, "The bot never got through the cellars.");
        // The bot sometimes paints itself into a corner (an aware creature it can't reach keeps it from resting);
        // a few stuck runs are the bot's fault, not the level's.
        Assert.True(results.Count(r => r.Mode == GameMode.Playing) <= Math.Max(1, runs / 10), "Too many runs got stuck.");
    }

    [Fact]
    public void The_quick_start_party_gets_through_the_cellars()
    {
        int runs = int.TryParse(Environment.GetEnvironmentVariable("AVAHACK_BALANCE_RUNS"), out int n) ? n : 12;
        var results = Enumerable.Range(1, runs).AsParallel().Select(i => Play((ulong)i * 104729, party: true)).OrderBy(r => r.Seed).ToList();
        foreach (var r in results)
            output.WriteLine($"party seed {r.Seed,7}: {r.Mode,-7} {r.Seconds / 60,5:F1} min  kills {r.Kills,2}  level {r.Level}  {r.Killer}");
        var won = results.Count(r => r.Mode == GameMode.Won);
        output.WriteLine($"party won {won}/{runs}");
        // M3's exit test, played by the bot: four heroes should get through far more often than one.
        Assert.True(won >= runs * 2 / 3, $"The party won only {won} of {runs}.");
    }
}
