using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

/// <summary>
/// M4's exit test: each calling fights level-matched creatures in an arena until it reaches level 8, sneaking up,
/// blocking and power-attacking as a player might, and choosing its level-ups and perks. By then it should be a
/// specialist (its best skill 45 or more) with some breadth (its third-best 35 or more). The spec's first target,
/// three skills above 40, didn't fit the level curve; see spec section 26.
/// </summary>
public sealed class ProgressionReport(ITestOutputHelper output)
{
    private const string Arena = """
        start: 2 1 east
        map:
        #########
        #.......#
        #########
        """;

    internal static Character Hero(ContentDb db, string calling)
    {
        var order = calling switch
        {
            "fighter" or "barbarian" or "paladin" => new[] { Attribute.Str, Attribute.Con, Attribute.Dex, Attribute.Wis, Attribute.Cha, Attribute.Int },
            "ranger" or "rogue" or "monk" => [Attribute.Dex, Attribute.Con, Attribute.Wis, Attribute.Str, Attribute.Int, Attribute.Cha],
            "cleric" or "druid" => [Attribute.Wis, Attribute.Con, Attribute.Str, Attribute.Dex, Attribute.Cha, Attribute.Int],
            _ => [Attribute.Int, Attribute.Con, Attribute.Dex, Attribute.Wis, Attribute.Str, Attribute.Cha],
        };
        var a = new AttributeSet();
        int[] arr = [15, 14, 13, 12, 10, 8];
        for (int i = 0; i < 6; i++) a[order[i]] = arr[i];
        var h = CharacterBuilder.Create(db, "Trainee", "human", calling, "mercenary", a);
        StartingKit.Give(db, h);
        return h;
    }

    /// <summary>Spends what a level-up gives: HP, the main attribute, and perks in the best skills.</summary>
    internal static void Spend(Character h)
    {
        while (h.PendingLevelChoices > 0) h.ChooseLevelBonus(h.MaxMana > h.MaxStamina ? LevelChoice.Mana : LevelChoice.Health);
        var main = Enum.GetValues<Attribute>().MaxBy(h.Score);
        while (h.AttributePoints > 0 && (h.RaiseAttribute(main) || h.RaiseAttribute(Attribute.Con))) { }
        while (h.PerkPoints > 0)
        {
            var best = h.Content.Perks.Where(p => h.CanBuyPerk(p.Id)).OrderByDescending(p => h.Rank(p.Skill)).FirstOrDefault();
            if (best is null || !h.BuyPerk(best.Id)) break;
        }
    }

    public sealed record Result(string Calling, int Level, int Fights, int Deaths, int SkillsOver40, string Top, int Best, int Third);

    internal static Result Train(string calling, ulong seed, int targetLevel = 8, int maxFights = 1200)
    {
        var db = ContentDb.LoadBuiltIn();
        var hero = Hero(db, calling);
        var party = new Party(hero);
        hero.LoneDelver = false; // train as a party member would, not with the lone bonus
        var rng = new Pcg32(seed, 7);
        var kinds = db.Creatures.Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        int fights = 0, deaths = 0;
        while (hero.Level < targetLevel && fights < maxFights)
        {
            fights++;
            // Each fight in a fresh arena (a fallen hero ends a game), with the same hero.
            var g = new GameEngine(LevelLoader.Parse(Arena), seed + (ulong)fights, party, db);
            var bot = new Bot(g, (int)seed + fights) { BlockChance = 0.7 };
            var def = Bestiary.Scale(db.Creature(kinds[rng.Next(kinds.Count)]), hero.Level + rng.Next(2));
            // The creature has its back turned at the far end; the hero sneaks up on it.
            var m = g.AddMonster(def, new Cell(6, 1), Dir.East);
            g.ToggleSneak();
            for (int t = 0; t < 60 * 90 && m.IsAlive && g.Mode == GameMode.Playing; t++)
            {
                if (!m.IsAware && m.Cell.Distance(g.PartyCell) > 1 && !g.IsMoving && g.Queued == 0) g.Press(GameAction.StepForward);
                if (m.IsAware && g.Sneaking) g.ToggleSneak();
                bot.Think();
                g.Tick();
            }
            if (hero.IsDown) deaths++;
            for (int t = 0; t < 60 * 4 && g.Mode == GameMode.Playing; t++) g.Tick(); // the fight winds down
            hero.EndFight();
            hero.Revive(1);
            hero.Rest();
            Spend(hero);
        }
        var top = Enum.GetValues<SkillId>().OrderByDescending(hero.Rank).Take(5).Select(s => $"{s} {hero.Rank(s)}");
        var ranks = Enum.GetValues<SkillId>().Select(hero.Rank).OrderDescending().ToList();
        return new Result(calling, hero.Level, fights, deaths, Enum.GetValues<SkillId>().Count(s => hero.Rank(s) > 40), string.Join(", ", top), ranks[0], ranks[2]);
    }

    [Fact]
    public void Every_calling_reaches_level_8_as_a_specialist_with_breadth()
    {
        var callings = ContentDb.LoadBuiltIn().Callings.Select(c => c.Id).ToList();
        var results = callings.AsParallel().Select(c => Train(c, 3)).OrderBy(r => r.Calling).ToList();
        foreach (var r in results)
            output.WriteLine($"{r.Calling,-11} level {r.Level}  fights {r.Fights,3}  deaths {r.Deaths,2}  skills>40 {r.SkillsOver40}  ({r.Top})");
        Assert.All(results, r => Assert.True(r.Level >= 8, $"{r.Calling} only reached level {r.Level}."));
        Assert.All(results, r => Assert.True(r.Best >= 45 && r.Third >= 35, $"{r.Calling}: best {r.Best}, third {r.Third} ({r.Top})."));
        Assert.All(results, r => Assert.InRange(r.Fights, 150, 1200)); // neither a walkover nor a grind
    }
}
