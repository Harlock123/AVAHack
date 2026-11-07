using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class PartyTests
{
    private const string Corridor = """
        start: 3 1 east
        map:
        ##########
        #........#
        ##########
        """;

    internal static Party QuickParty(ContentDb db)
    {
        var p = CharacterBuilder.QuickStartParty(db);
        var party = new Party(p.ToArray());
        Kit.Arm(db, party);
        return party;
    }

    private static GameEngine Arena(string map = Corridor, ulong seed = 1)
    {
        var db = Heroes.Content;
        return new GameEngine(LevelLoader.Parse(map), seed, QuickParty(db), db);
    }

    private static void Run(GameEngine g, double s)
    {
        for (int i = 0; i < s * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    [Fact]
    public void Four_heroes_stand_two_by_two()
    {
        var g = Arena();
        Assert.Equal(4, g.Party.Count);
        Assert.Equal("Brannoc", g.Party[Slot.FrontLeft]!.Name);
        Assert.Equal("Pell", g.Party[Slot.FrontRight]!.Name);
        Assert.False(g.Party.Leader.LoneDelver);
        Assert.True(g.Party.IsFront(g.Party[Slot.FrontRight]!));
        Assert.False(g.Party.IsFront(g.Party[Slot.BackLeft]!));
    }

    [Fact]
    public void Blows_land_on_the_side_they_come_from()
    {
        var g = Arena();
        var p = g.Party;
        Assert.Equal(["Brannoc", "Pell"], p.Exposed(RelativeSide.Ahead).Select(c => c.Name));
        Assert.Equal(["Brannoc", "Sister Ama"], p.Exposed(RelativeSide.Left).Select(c => c.Name));
        Assert.Equal(["Sister Ama", "Ithren"], p.Exposed(RelativeSide.Behind).Select(c => c.Name));
        p[Slot.BackLeft]!.TakeDamage(999);
        p[Slot.BackRight]!.TakeDamage(999);
        Assert.Equal(2, p.Exposed(RelativeSide.Behind).Count); // the back row is down: the front row is exposed
    }

    [Fact]
    public void The_back_row_cannot_reach_with_a_sword_but_can_shoot()
    {
        var g = Arena();
        var ama = g.Party[Slot.BackLeft]!;
        g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        g.HandDown(ama, HandSide.Right);
        g.HandUp(ama, HandSide.Right);
        Assert.Contains("can't reach from the back row", g.LastMessage);
        Assert.True(ama.RightHand.Ready);

        var ithren = g.Party[Slot.BackRight]!;
        var bow = g.Loot.Make("shortbow", 1, new Pcg32(1));
        ithren.Wear(new Item { BaseId = "shortbow" });
        var far = g.AddMonster("shambler", new Cell(7, 1), Dir.West);
        int hits = 0;
        g.Event += e => { if (e == GameEvent.Hit) hits++; };
        for (int i = 0; i < 20 && hits == 0; i++)
        {
            g.HandDown(ithren, HandSide.Right);
            g.HandUp(ithren, HandSide.Right);
            Run(g, 2.5);
        }
        Assert.True(hits > 0);
        Assert.NotNull(bow);
    }

    [Fact]
    public void The_fallen_get_up_after_the_fight()
    {
        var g = Arena();
        var pell = g.Party[Slot.FrontRight]!;
        g.AddMonster("cellarRat", new Cell(4, 1), Dir.West);
        Run(g, 0.2);
        Assert.True(g.InFight);
        pell.TakeDamage(999);
        Assert.True(pell.IsDown);
        for (int i = 0; i < 60 * 30 && g.Monsters.Any(m => m.IsAlive); i++)
        {
            if (g.Hero.RightHand.Ready) { g.HandDown(HandSide.Right); g.HandUp(HandSide.Right); }
            g.Tick();
        }
        Run(g, GameEngine.CalmSeconds + 0.2);
        Assert.False(pell.IsDown);
        Assert.Equal(1, pell.Hp, 0.5);
        Assert.Equal(GameMode.Playing, g.Mode);
    }

    [Fact]
    public void Only_the_whole_party_down_ends_the_game()
    {
        var g = Arena();
        foreach (var c in g.Party.Members.Take(3)) c.TakeDamage(999);
        g.Tick();
        Assert.Equal(GameMode.Playing, g.Mode);
        g.Party[Slot.BackRight]!.TakeDamage(999);
        g.Tick();
        Assert.Equal(GameMode.Dead, g.Mode);
    }

    [Fact]
    public void Swapping_slots_moves_heroes_between_the_rows()
    {
        var g = Arena();
        var ama = g.Party[Slot.BackLeft]!;
        g.Party.Swap(Slot.FrontLeft, Slot.BackLeft);
        Assert.True(g.Party.IsFront(ama));
        Assert.Equal(Slot.FrontLeft, g.Party.SlotOf(ama));
    }

    [Fact]
    public void Too_much_weight_slows_the_party_then_stops_it()
    {
        var g = Arena();
        var pell = g.Party[Slot.FrontRight]!; // STR 8: 120 kg
        for (int i = 0; i < 5; i++) pell.AddToBackpack(new Item { BaseId = "plate" }); // 150 kg
        Assert.True(pell.Encumbrance > 1);
        g.Press(GameAction.StepForward);
        Run(g, GameEngine.StepTicks / 60.0 + 0.02);
        Assert.True(g.IsMoving); // a slow step takes half as long again
        Run(g, 0.3);
        for (int i = 0; i < 2; i++) pell.AddToBackpack(new Item { BaseId = "plate" });
        Assert.True(pell.Encumbrance > 1.5);
        var before = g.PartyCell;
        g.Press(GameAction.StepForward);
        Run(g, 0.5);
        Assert.Equal(before, g.PartyCell);
        Assert.Contains("too much", g.LastMessage);
    }
}

/// <summary>Starting kit for the quick-start party, used by tests and the game alike.</summary>
internal static class Kit
{
    public static void Arm(ContentDb db, Party party)
    {
        foreach (var c in party.Members) StartingKit.Give(db, c);
    }
}
