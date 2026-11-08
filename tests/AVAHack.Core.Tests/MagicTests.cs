using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class MagicTests
{
    private const string Hall = """
        start: 2 2 east
        map:
        ##########
        #........#
        #........#
        #........#
        ##########
        """;

    private static ContentDb Db => Heroes.Content;

    private static GameEngine Solo(string calling, string map = Hall, ulong seed = 1)
    {
        var h = ProgressionReport.Hero(Db, calling);
        return new GameEngine(LevelLoader.Parse(map), seed, new Party(h), Db);
    }

    private static void Run(GameEngine g, double s)
    {
        for (int i = 0; i < s * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static void CastAndWait(GameEngine g, Character c, string spell)
    {
        Assert.Null(g.Cast(c, spell));
        Run(g, Db.Spell(spell).CastTime + GameEngine.CastRecovery + 0.1);
    }

    [Fact]
    public void There_are_49_spells_in_five_schools_with_cantrips()
    {
        Assert.Equal(49, Db.Spells.Count);
        Assert.Equal(5, Db.Spells.Select(s => s.School).Distinct().Count());
        Assert.True(Db.Spells.Count(s => s.Cantrip) >= 8);
    }

    [Fact]
    public void Every_callings_opening_spells_work()
    {
        foreach (var calling in Db.Callings.Select(c => c.Id))
        {
            var g = Solo(calling);
            var h = g.Hero;
            foreach (var sp in h.KnownSpells.ToList())
            {
                var s = Db.Spell(sp);
                // Something to cast at, and a reason to cast.
                foreach (var m in g.Monsters.Where(m => m.IsAlive)) m.Hp = 0;
                var target = g.AddMonster("shambler", new Cell(3, 2), Dir.West);
                h.TakeDamage(3);
                h.Rest(); // full Mana
                h.TakeDamage(3);
                h.RightHand.Start(0);
                var before = (Hp: target.Hp, Hero: h.Hp, Ward: h.Ward, Summons: g.Monsters.Count(m => m.Ally), Light: g.MagicLight, Buffs: h.Buffs.Count);
                Assert.True(g.Cast(h, sp) is null, $"{calling} can't cast {sp}: {g.WhyNotCast(h, sp)}");
                Run(g, s.CastTime + 1.2);
                bool did = target.Hp < before.Hp || h.Hp > before.Hero || h.Ward > before.Ward || g.Monsters.Count(m => m.Ally) > before.Summons
                           || g.MagicLight > before.Light || h.Buffs.Count > before.Buffs || target.CalmLeft > 0 || target.Conditions.Active.Any()
                           || g.Detecting > 0 || g.Log.Last().Length > 0;
                Assert.True(did, $"{calling}'s {sp} did nothing visible.");
            }
        }
    }

    [Fact]
    public void A_bolt_flies_down_the_line_and_hurts_what_it_hits()
    {
        var g = Solo("wizard");
        var m = g.AddMonster("shambler", new Cell(6, 2), Dir.West);
        int hits = 0;
        for (int i = 0; i < 12 && m.Hp == m.MaxHp; i++)
        {
            g.Hero.Rest();
            CastAndWait(g, g.Hero, "firedart");
            hits++;
        }
        Assert.True(m.Hp < m.MaxHp);
        Assert.True(g.Hero.Xp(SkillId.Destruction) > 0 || g.Hero.Rank(SkillId.Destruction) > 20);
    }

    [Fact]
    public void Casting_costs_mana_less_with_skill_and_adept_perks()
    {
        var g = Solo("wizard");
        var h = g.Hero;
        var s = Db.Spell("firedart");
        double plain = g.ManaCost(h, s);
        Assert.Equal(5 * (1 - 20 * 0.004), plain, 6); // Destruction 20
        Heroes.LevelTo(h, 4);
        Assert.True(h.BuyPerk("destruction.adeptFlame"));
        Assert.True(g.ManaCost(h, s) < plain * 0.8);
        Assert.True(GameEngine.SpellPower(h, s) > 1.25);
        double mana = h.Mana;
        g.Cast(h, "firedart");
        Assert.Equal(mana - g.ManaCost(h, s), h.Mana, 6);
    }

    [Fact]
    public void Heals_mend_the_most_hurt_and_rally_heals_everyone()
    {
        var party = PartyTests.QuickParty(Db);
        var g = new GameEngine(LevelLoader.Parse(Hall), 2, party, Db);
        var ama = party[Slot.BackLeft]!;
        var pell = party[Slot.FrontRight]!;
        pell.TakeDamage(6);
        CastAndWait(g, ama, "mend");
        Assert.True(pell.Hp > pell.MaxHp - 6);
        Assert.True(ama.Xp(SkillId.Restoration) > 0 || ama.Rank(SkillId.Restoration) > 20);
    }

    [Fact]
    public void A_ward_soaks_blows_and_teaches_its_caster()
    {
        var party = PartyTests.QuickParty(Db);
        var g = new GameEngine(LevelLoader.Parse(Hall), 2, party, Db);
        var ithren = party[Slot.BackRight]!;
        g.Selected = party[Slot.FrontLeft]!;
        CastAndWait(g, ithren, "ward");
        double w0 = g.Selected.Ward;
        Assert.True(w0 >= 20);
        double xp = ithren.Xp(SkillId.Warding);
        int rank = ithren.Rank(SkillId.Warding);
        g.Party.Swap(Slot.FrontRight, Slot.BackRight); // only the warded hero in the front row
        g.AddMonster("boneSentry", new Cell(3, 2), Dir.West);
        for (int i = 0; i < 60 * 60 && g.Selected.Ward >= w0; i++) g.Tick();
        Assert.True(g.Selected.Ward < w0);
        Assert.True(ithren.Xp(SkillId.Warding) > xp || ithren.Rank(SkillId.Warding) > rank);
    }

    [Fact]
    public void Summons_fight_for_the_party_and_fade()
    {
        var g = Solo("druid");
        g.Hero.Rest();
        CastAndWait(g, g.Hero, "summonFamiliar");
        var fam = Assert.Single(g.Monsters, m => m.Ally);
        Assert.True(fam.Summoned);
        var rat = g.AddMonster("cellarRat", new Cell(6, 2), Dir.West);
        for (int i = 0; i < 60 * 20 && rat.IsAlive; i++) g.Tick();
        Assert.False(rat.IsAlive);
        for (int i = 0; i < 60 * 70; i++) g.Tick();
        Assert.False(fam.IsAlive);
    }

    [Fact]
    public void The_party_swaps_places_with_an_ally_and_never_hits_it()
    {
        var g = Solo("druid");
        g.Hero.Rest();
        CastAndWait(g, g.Hero, "summonFamiliar");
        var fam = g.Monsters.Single(m => m.Ally);
        g.Stand(g.PartyCell, GameEngine.DirTo(g.PartyCell, fam.Cell)!.Value);
        int hp = fam.Hp;
        g.HandDown(HandSide.Right);
        g.HandUp(HandSide.Right);
        Assert.Equal(hp, fam.Hp);
        var was = g.PartyCell;
        g.Press(GameAction.StepForward);
        Assert.Equal(was, fam.Cell);
    }

    [Fact]
    public void Fire_on_the_ground_burns_what_stands_in_it()
    {
        var g = Solo("wizard");
        var h = g.Hero;
        h.KnownSpells.Add("cinderWall");
        var m = g.AddMonster("shambler", new Cell(3, 2), Dir.West);
        m.Conditions.Apply(ConditionId.Paralysed, 99, 30); // stand still in it
        CastAndWait(g, h, "cinderWall");
        Assert.NotEmpty(g.Hazards);
        Run(g, 3);
        Assert.True(m.Hp < m.MaxHp);
    }

    [Fact]
    public void A_hard_blow_can_break_a_spell_and_shock_can_fizzle_it()
    {
        var g = Solo("wizard");
        var h = g.Hero;
        var gob = g.AddMonster("boneSentry", new Cell(3, 2), Dir.West);
        bool broke = false;
        for (int round = 0; round < 60 && !broke; round++)
        {
            h.Rest();
            h.RightHand.Start(0);
            if (g.Cast(h, "firedart") is not null) { Run(g, 0.5); continue; }
            for (int t = 0; t < 60 && h.CastingSpell is not null; t++) g.Tick();
            broke = g.Log.Any(l => l.Contains("loses the Firedart"));
            Run(g, 1.2);
        }
        Assert.True(broke);
        var s = Solo("wizard");
        s.Hero.ApplyCondition(ConditionId.Shocked, 99, 600);
        bool fizzled = false;
        for (int i = 0; i < 40 && !fizzled; i++)
        {
            s.Hero.Rest();
            s.Hero.ApplyCondition(ConditionId.Shocked, 99, 600);
            CastAndWait(s, s.Hero, "light");
            fizzled = s.Log.Any(l => l.Contains("fizzles"));
        }
        Assert.True(fizzled);
    }

    [Fact]
    public void Tomes_teach_when_the_school_is_good_enough_scrolls_cast_for_anyone_wands_run_down()
    {
        var g = Solo("fighter");
        var h = g.Hero;
        h.AddToBackpack(new Item { BaseId = "tome", SpellId = "frostlance" });
        int tome = Array.FindIndex(h.Backpack, i => i?.BaseId == "tome");
        Assert.Contains("needs Destruction 15", g.UseItem(h, tome));
        h.GainSkillXp(SkillId.Destruction, 50_000, XpSource.Plain);
        Assert.Contains("learns Frostlance", g.UseItem(h, tome));
        Assert.Contains("frostlance", h.KnownSpells);
        Assert.Contains("frostlance", h.QuickSlots);

        var m = g.AddMonster("shambler", new Cell(3, 2), Dir.West);
        h.AddToBackpack(new Item { BaseId = "scroll", SpellId = "thunderclap" });
        int scroll = Array.FindIndex(h.Backpack, i => i?.BaseId == "scroll");
        double mana = h.Mana;
        Assert.Contains("reads the scroll", g.UseItem(h, scroll));
        Run(g, 2.5);
        Assert.True(m.Hp < m.MaxHp);
        Assert.Equal(mana, h.Mana, 3); // scrolls cost no Mana
        Assert.Null(h.Backpack[scroll]);

        h.AddToBackpack(new Item { BaseId = "wand", SpellId = "firedart", Charges = 2 });
        int wand = Array.FindIndex(h.Backpack, i => i?.BaseId == "wand");
        Assert.Contains("points the wand", g.UseItem(h, wand));
        Run(g, 2.5);
        Assert.Contains("points the wand", g.UseItem(h, wand));
        Run(g, 2.5);
        Assert.Contains("spent", g.UseItem(h, wand));
    }

    [Fact]
    public void Hush_quiets_the_creature_ahead_for_12_seconds()
    {
        var g = Solo("wizard");
        var h = g.Hero;
        h.KnownSpells.Add("hush");
        h.GainSkillXp(SkillId.Warding, 1_000_000, XpSource.Plain);
        var slinger = g.AddMonster("gutterGoblin", new Cell(3, 2), Dir.West);
        CastAndWait(g, h, "hush");
        Assert.InRange(slinger.HushLeft, 9, 12);
        Run(g, 13);
        Assert.Equal(0, slinger.HushLeft);
        Assert.Contains(g.Log, l => l.Contains("finds its voice again"));
    }

    [Fact]
    public void Mind_spells_and_utilities()
    {
        var g = Solo("wizard");
        var h = g.Hero;
        foreach (var sp in new[] { "calm", "fear", "frenzy", "openTheWay", "beacon", "dispel", "mageHand" }) h.KnownSpells.Add(sp);
        h.GainSkillXp(SkillId.Illusion, 1_000_000, XpSource.Plain);
        var gob = g.AddMonster("gutterGoblin", new Cell(3, 2), Dir.West);
        bool calmed = false;
        for (int i = 0; i < 10 && !calmed; i++) { h.Rest(); CastAndWait(g, h, "calm"); calmed = gob.CalmLeft > 0; }
        Assert.True(calmed);
        Assert.False(gob.IsAware);

        CastAndWait(g, h, "openTheWay");
        Assert.NotNull(g.Portal);
        var mark = g.PartyCell;
        g.Stand(new Cell(7, 3), Dir.West);
        h.Rest();
        CastAndWait(g, h, "openTheWay");
        Assert.Equal(mark, g.PartyCell);

        h.Rest();
        CastAndWait(g, h, "beacon");
        Assert.Contains(g.Log, l => l.Contains("whisper"));
    }

    [Fact]
    public void Light_brightens_the_dark()
    {
        var g = new GameEngine(LevelLoader.Parse(Hall), 1, new Party(ProgressionReport.Hero(Db, "wizard")), Db);
        double torch = g.LightStrength;
        Assert.True(g.Lit);
        g.Hero.Rest();
        CastAndWait(g, g.Hero, "light");
        Assert.True(g.LightStrength > torch);
    }

    [Fact]
    public void Magic_state_survives_a_save()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 1, new Party(ProgressionReport.Hero(Db, "wizard")), Db);
        g.Hero.Rest();
        CastAndWait(g, g.Hero, "light");
        g.Hero.Buffs["bulwark"] = 20;
        var back = GameEngine.FromSave(g.ToSave(), Db);
        Assert.True(back.MagicLight > 0);
        Assert.True(back.Hero.Buffs.ContainsKey("bulwark"));
        Assert.Contains("firedart", back.Hero.KnownSpells);
    }
}
