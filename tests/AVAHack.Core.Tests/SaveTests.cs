using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class SaveTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "avahack-saves-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Cellars(ulong seed)
    {
        var db = Heroes.Content;
        return new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), seed, PartyTests.QuickParty(db), db);
    }

    private static void Play(GameEngine g, Bot bot, int ticks)
    {
        for (int i = 0; i < ticks && g.Mode == GameMode.Playing; i++) { bot.Think(); g.Tick(); }
    }

    private static string Fingerprint(GameEngine g) =>
        $"{g.TickCount}|{g.PartyCell}|{g.Facing}|{g.Kills}|{g.Party.Gold}|" +
        string.Join(";", g.Party.Members.Select(c => $"{c.Name}:{c.Hp:F3}:{c.Stamina:F3}:{c.Level}:{string.Join(",", Enum.GetValues<SkillId>().Select(c.Rank))}")) + "|" +
        string.Join(";", g.Monsters.Select(m => $"{m.Def.Id}@{m.Cell}:{m.Hp}:{m.State}"));

    [Fact]
    public void A_save_round_trips_through_a_file()
    {
        var g = Cellars(5);
        Play(g, new Bot(g, 5), 60 * 60);
        g.Hero.AddToBackpack(new Item { BaseId = "longsword", Rarity = Rarity.Fine, Affixes = [new("keen", 2)] });
        var store = new SaveStore(TempDir());
        store.Save("slot1", g.ToSave());
        var back = GameEngine.FromSave(store.Load("slot1")!, Heroes.Content);
        Assert.Equal(Fingerprint(g), Fingerprint(back));
        Assert.Contains(back.Hero.Backpack, i => i?.BaseId == "longsword" && i.Affixes.Single().Value == 2);
        Assert.Equal(g.Explored.Count, back.Explored.Count);
        Assert.Single(store.List());
        Assert.Contains("Brannoc", store.List()[0].Summary);
    }

    [Fact]
    public void A_loaded_game_plays_on_exactly_as_the_original()
    {
        // Two copies of the same run: one keeps going, the other is saved, loaded and then keeps going.
        var a = Cellars(11);
        var b = Cellars(11);
        var botA = new Bot(a, 11);
        var botB = new Bot(b, 11);
        Play(a, botA, 60 * 45);
        Play(b, botB, 60 * 45);
        // Save at a quiet moment: not mid-step (a step in progress isn't saved).
        while (a.IsMoving || a.Queued > 0) { botA.Think(); a.Tick(); botB.Think(); b.Tick(); }
        var loaded = GameEngine.FromSave(b.ToSave(), Heroes.Content);
        var botL = new Bot(loaded, 11);
        botL.CopyMemory(botB);
        // The bots' own random numbers must line up too.
        Play(a, botA, 60 * 30);
        Play(loaded, botL, 60 * 30);
        Assert.Equal(Fingerprint(a), Fingerprint(loaded));
    }

    [Fact]
    public void Formation_conditions_and_level_changes_survive()
    {
        var g = Cellars(2);
        g.Party.Swap(Slot.FrontLeft, Slot.BackRight);
        g.Hero.ApplyCondition(Rules.ConditionId.Poisoned, 12, 30);
        g.Level.DoorAt(19, 2)!.Opening = true;
        g.Stand(new Cell(16, 8), Dir.South);
        g.Level.Spot(g.Level.PitAt(new Cell(15, 9))!);
        var back = GameEngine.FromSave(g.ToSave(), Heroes.Content);
        Assert.Equal("Ithren", back.Party[Slot.FrontLeft]!.Name);
        Assert.True(back.Party[Slot.FrontLeft]!.Conditions.Has(Rules.ConditionId.Poisoned));
        Assert.True(back.Level.DoorAt(19, 2)!.Opening);
        Assert.True(back.Level.PitAt(new Cell(15, 9))!.Spotted);
        Assert.Equal(new Cell(16, 8), back.PartyCell);
    }
}
