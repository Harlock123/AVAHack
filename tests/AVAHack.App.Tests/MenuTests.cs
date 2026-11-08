using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AVAHack.App.Views;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.App.Tests;

/// <summary>The title, making a party, the inventory, the party bar and saving, driven like a player would.</summary>
public sealed class MenuTests
{
    private static readonly ContentDb Db = ContentDb.LoadBuiltIn();

    private static (Window W, GameView V) Open(GameView v)
    {
        var w = new Window { Width = 1280, Height = 800, Content = v };
        w.Show();
        Pump();
        return (w, v);
    }

    private static void Pump()
    {
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Clicks a canvas point (1280×800 canvas in a 1280×800 window: the same coordinates).</summary>
    private static void Click(Window w, double x, double y, MouseButton button = MouseButton.Left)
    {
        Pump();
        w.MouseMove(new Point(x, y));
        w.MouseDown(new Point(x, y), button);
        w.MouseUp(new Point(x, y), button);
        Pump();
    }

    private static void Shot(Window w, string name)
    {
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAHack-{name}.png"), PngBitmapEncoderOptions.Default);
    }

    private static SaveStore TempStore() => new(Path.Combine(Path.GetTempPath(), "avahack-menu-" + Guid.NewGuid().ToString("N")));

    [AvaloniaFact]
    public void Quick_start_from_the_title_begins_with_four_heroes()
    {
        var (w, v) = Open(new GameView(Db, startLoop: false, TempStore()));
        Assert.Equal(Screen.Title, v.Screen);
        Shot(w, "title");
        Click(w, 640, 344); // Quick start
        Assert.Equal(Screen.Options, v.Screen);
        Shot(w, "delve-options");
        Click(w, 1110, 753); // Begin the Delve
        Assert.Equal(Screen.Play, v.Screen);
        Assert.NotNull(v.CurrentDelve);
        Assert.StartsWith("The Upper Hollow", v.Engine.Level.Name);
        Assert.Equal(4, v.Engine.Party.Count);
        Assert.Equal("longsword", v.Engine.Party[Slot.FrontLeft]!.MainHand!.Id);
        Shot(w, "party");
    }

    [AvaloniaFact]
    public void Making_a_party_of_two()
    {
        var (w, v) = Open(new GameView(Db, startLoop: false, TempStore()));
        Click(w, 640, 408); // Make a party
        Assert.Equal(Screen.Create, v.Screen);
        Click(w, 180, 216); // + Add a hero (under the first)
        Click(w, 520, 110); // the name box
        foreach (var _ in "Brannoc") w.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        w.KeyTextInput("Vessa");
        w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Click(w, 803, 260); // next ancestry ▶ (on the calling row? the ancestry row is at y 160)
        Shot(w, "create");
        Click(w, 1120, 753); // Begin the descent
        Assert.Equal(Screen.Options, v.Screen);
        Click(w, 410, 753); // Practise in The Cellars
        Assert.Equal(Screen.Play, v.Screen);
        Assert.Equal(2, v.Engine.Party.Count);
        Assert.Contains(v.Engine.Party.Members, h => h.Name == "Vessa");
        Assert.False(v.Engine.Hero.LoneDelver);
    }

    [AvaloniaFact]
    public void A_heroic_hero_is_rolled_and_made()
    {
        var (w, v) = Open(new GameView(Db, startLoop: false, TempStore()));
        Click(w, 640, 408); // Make a party
        Click(w, 700, 662); // Heroic
        Shot(w, "create-heroic");
        Click(w, 1120, 753); // Begin the descent
        Assert.Equal(Screen.Options, v.Screen);
        Click(w, 410, 753); // Practise in The Cellars
        var hero = v.Engine.Party.Members.Single();
        Assert.True(hero.Heroic);
        Assert.All(Enum.GetValues<AVAHack.Rules.Attribute>(), a => Assert.InRange(hero.BaseAttributes[a], 10, 18));
        Assert.Equal(125, hero.XpToNextLevel);
        // Laid out for the calling: the first hero is a fighter, so Strength is its best score.
        Assert.Equal(Enum.GetValues<AVAHack.Rules.Attribute>().Max(a => hero.BaseAttributes[a]), hero.BaseAttributes[AVAHack.Rules.Attribute.Str]);
    }

    [AvaloniaFact]
    public void The_Waystone_screen_trains_a_hero_and_closes()
    {
        var level = Generator.Generate(Db, 41, 5, new GenOptions { MaxDepth = 12 });
        Assert.NotNull(level.Waystone);
        var g = new GameEngine(level, 41, GameView.DefaultParty(Db), Db);
        var (w, v) = Open(new GameView(g, startLoop: false, store: TempStore()));
        for (int i = 0; i < 4 && g.PartyCell.Step(g.Facing) != level.Waystone; i++)
        {
            w.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);
            v.Advance(1);
        }
        w.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
        v.Advance(0.2);
        Pump();
        Assert.Equal(Overlay.Waystone, v.Overlay);
        g.Party.Gold = 5000;
        v.InvalidateVisual();
        Pump();
        Shot(w, "waystone"); // the merchant's goods, with their icons
        Click(w, 164 + 250, 40 + 78); // the Trainer tab
        int rank = g.Selected.Rank(SkillId.Athletics);
        int index = Array.IndexOf(Enum.GetValues<SkillId>(), SkillId.Athletics);
        Click(w, 164 + index % 3 * 320 + 150, 40 + 112 + 30 + index / 3 * 38 + 16);
        Assert.Equal(rank + 1, g.Selected.Rank(SkillId.Athletics));
        Click(w, 1140 - 110 + 45, 40 + 14 + 16); // Close
        Assert.Equal(Overlay.None, v.Overlay);
    }

    [AvaloniaFact]
    public void The_craft_screen_brews_a_potion()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(Db), Db);
        var (w, v) = Open(new GameView(g, startLoop: false, store: TempStore()));
        var hero = g.Selected;
        Array.Clear(hero.Backpack);
        var heal = Db.Reagents.Where(r => r.Effects.Contains("heal")).Take(2).ToList();
        hero.AddToBackpack(new Item { BaseId = heal[0].Id });
        hero.AddToBackpack(new Item { BaseId = heal[1].Id });
        hero.AddToBackpack(new Item { BaseId = "mortar" });
        w.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
        Assert.Equal(Overlay.Craft, v.Overlay);
        Click(w, 134 + 380, 30 + 112 + 28 + 15);      // the first reagent
        Click(w, 134 + 380, 30 + 112 + 28 + 36 + 15); // the second
        Shot(w, "craft");
        Click(w, 1170 - 180 + 78, 690 - 80 + 19);     // Brew
        Assert.Contains(hero.Backpack, i => i?.BaseId == ItemBase.BrewId);
        w.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.None);
        Assert.Equal(Overlay.None, v.Overlay);
    }

    [AvaloniaFact]
    public void The_inventory_wears_and_drops_things()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(Db), Db);
        var (w, v) = Open(new GameView(g, startLoop: false, store: TempStore()));
        var hero = g.Hero;
        Array.Clear(hero.Backpack);
        hero.AddToBackpack(new Item { BaseId = "ironHelm", Rarity = Rarity.Fine, Affixes = [new("ofVigour", 4)] });
        int hp = hero.MaxHp;
        w.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.None);
        Assert.Equal(Overlay.Inventory, v.Overlay);
        Click(w, 332, 96); // the first backpack slot
        Assert.Equal("ironHelm", hero.ItemIn(EquipSlot.Head)!.BaseId);
        Assert.Equal(hp + 4, hero.MaxHp);
        w.MouseMove(new Point(60, 96)); // hover the helm on the doll for its tooltip
        Shot(w, "inventory");
        Click(w, 60, 96); // the head slot: take it off again
        Assert.Null(hero.ItemIn(EquipSlot.Head));
        Click(w, 332, 96, MouseButton.Right); // drop it
        Assert.Single(g.Level.PileAt(g.PartyCell));
        // The game doesn't run while the inventory is open.
        long t = g.TickCount;
        v.Advance(0.5);
        Assert.Equal(t, g.TickCount);
    }

    [AvaloniaFact]
    public void Saving_from_the_menu_and_loading_it_back()
    {
        var store = TempStore();
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(Db), Db);
        var (w, v) = Open(new GameView(g, startLoop: false, store: store));
        g.Press(GameAction.StepForward);
        for (int i = 0; i < 30; i++) v.Advance(0.05);
        var at = g.PartyCell;
        w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(Overlay.Menu, v.Overlay);
        Shot(w, "menu");
        Click(w, 640, 248); // save to slot 1
        Assert.True(store.Exists("slot1"));
        g.Press(GameAction.StepForward);
        for (int i = 0; i < 30; i++) v.Advance(0.05);
        Assert.NotEqual(at, g.PartyCell);
        Assert.True(v.LoadSlot("slot1"));
        Assert.Equal(at, v.Engine.PartyCell);
        Assert.NotSame(g, v.Engine);
    }

    [AvaloniaFact]
    public void Each_heros_hand_keys_and_tab_and_swapping()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(Db), Db);
        var (w, v) = Open(new GameView(g, startLoop: false, store: TempStore()));
        var pell = g.Party[Slot.FrontRight]!;
        w.KeyPressQwerty(PhysicalKey.Digit4, RawInputModifiers.None);
        w.KeyReleaseQwerty(PhysicalKey.Digit4, RawInputModifiers.None);
        Assert.False(pell.RightHand.Ready);
        w.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Same(pell, g.Selected);
        w.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control);
        Assert.Equal(Slot.BackLeft, g.Party.SlotOf(pell));
        Click(w, 46, 660); // the first card's portrait selects that hero
        Assert.Same(g.Party[Slot.FrontLeft], g.Selected);
    }
}

public sealed class SheetTests
{
    [AvaloniaFact]
    public void The_sheet_takes_a_level_up_and_a_perk()
    {
        var db = ContentDb.LoadBuiltIn();
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(db), db);
        var v = new GameView(g, startLoop: false, store: new SaveStore(Path.Combine(Path.GetTempPath(), "avahack-sheet-" + Guid.NewGuid().ToString("N"))));
        var w = new Window { Width = 1280, Height = 800, Content = v };
        w.Show();
        var h = g.Hero;
        while (h.Level < 2) h.GainSkillXp(SkillId.Blade, 50, XpSource.Plain);
        Assert.Equal(1, h.PendingLevelChoices);
        w.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
        Assert.Equal(Overlay.Character, v.Overlay);
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        w.CaptureRenderedFrame();
        int hp = h.MaxHp;
        // The "+5 HP" button sits under the attributes.
        w.MouseDown(new Point(66, 300), MouseButton.Left);
        w.MouseUp(new Point(66, 300), MouseButton.Left);
        Assert.Equal(hp + 5, h.MaxHp);
        // Blade is the first skill, already chosen; its first perk's Take button.
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        w.CaptureRenderedFrame();
        w.MouseDown(new Point(904, 121), MouseButton.Left);
        w.MouseUp(new Point(904, 121), MouseButton.Left);
        Assert.Equal(1, h.PerkRank("blade.edgeDiscipline"));
        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is { Length: > 0 } dir)
            w.CaptureRenderedFrame()!.Save(Path.Combine(dir, "AVAHack-sheet.png"), PngBitmapEncoderOptions.Default);
    }
}

public sealed class SpellbookTests
{
    [AvaloniaFact]
    public void Quick_slots_cast_and_the_spellbook_lists_the_spells()
    {
        var db = ContentDb.LoadBuiltIn();
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(db), db);
        var v = new GameView(g, startLoop: false, store: new SaveStore(Path.Combine(Path.GetTempPath(), "avahack-book-" + Guid.NewGuid().ToString("N"))));
        var w = new Window { Width = 1280, Height = 800, Content = v };
        w.Show();
        var ithren = g.Party[Slot.BackRight]!;
        g.Selected = ithren;
        Assert.Equal("spark", ithren.QuickSlots[0]);
        double mana = ithren.Mana;
        w.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None);
        Assert.Equal("spark", ithren.CastingSpell);
        Assert.True(ithren.Mana < mana);
        w.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.None);
        Assert.Equal(Overlay.Spellbook, v.Overlay);
        for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        var frame = w.CaptureRenderedFrame();
        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is { Length: > 0 } dir) frame?.Save(Path.Combine(dir, "AVAHack-spellbook.png"), PngBitmapEncoderOptions.Default);
    }
}


public sealed class DelveWindowTests
{
    [AvaloniaFact]
    public void A_delve_in_each_region_and_a_delve_save_loads_back()
    {
        var db = ContentDb.LoadBuiltIn();
        var store = new SaveStore(Path.Combine(Path.GetTempPath(), "avahack-dw-" + Guid.NewGuid().ToString("N")));
        var v = new GameView(db, startLoop: false, store);
        var w = new Window { Width = 1280, Height = 800, Content = v };
        w.Show();
        v.StartDelve(GameView.DefaultParty(db), new DelveOptions { Seed = "GOODWORK", Depth = 12 });
        var d = v.CurrentDelve!;
        foreach (int depth in new[] { 1, 5, 9 })
        {
            while (d.Depth < depth) d.Descend();
            v.Advance(0.1);
            for (int i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
            var frame = w.CaptureRenderedFrame();
            if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is { Length: > 0 } dir) frame?.Save(Path.Combine(dir, $"AVAHack-delve-{d.Engine.Level.Theme}.png"), PngBitmapEncoderOptions.Default);
        }
        Assert.Equal("choir", d.Engine.Level.Theme);
        store.Save("slot1", d.ToSave());
        Assert.True(v.LoadSlot("slot1"));
        Assert.Equal(9, v.CurrentDelve!.Depth);
    }
}
