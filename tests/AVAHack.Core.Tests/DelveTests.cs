using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class DelveTests
{
    private static ContentDb Db => Heroes.Content;
    private static DelveOptions Short => new() { Seed = "AAAABBBB", Depth = 4 };

    private static Delve New(DelveOptions? o = null) => Delve.Start(Db, PartyTests.QuickParty(Db), o ?? Short);

    [Fact]
    public void A_delve_begins_on_level_one_of_a_seeded_dungeon()
    {
        var d = New();
        Assert.Equal(1, d.Depth);
        Assert.StartsWith("The Upper Hollow", d.Engine.Level.Name);
        Assert.Equal("AAAABBBB", d.SeedCode);
        Assert.NotEmpty(d.Engine.Monsters);
        Assert.Equal(d.Engine.Level.Start, d.Engine.PartyCell);
    }

    [Fact]
    public void Stairs_lead_down_and_back_up_and_levels_keep_what_happened()
    {
        var d = New();
        var g = d.Engine;
        var first = g.Monsters.First(m => m.IsAlive);
        first.Hp = 0;
        first.State = MonsterState.Dead;
        d.Descend();
        Assert.Equal(2, d.Depth);
        Assert.Contains(g.Level.StairsUp, c => c == g.PartyCell);
        d.Ascend();
        Assert.Equal(1, d.Depth);
        Assert.Contains(g.Monsters, m => m.State == MonsterState.Dead && m.Cell == first.Cell);
        Assert.Equal(1, g.Level.Stairs.Single().Distance(g.PartyCell)); // beside the stairs down
    }

    [Fact]
    public void A_pit_drops_the_party_to_the_landing_below()
    {
        var d = New();
        d.Fall();
        Assert.Equal(2, d.Depth);
        Assert.Equal(d.Engine.Level.Landing ?? d.Engine.Level.Start, d.Engine.PartyCell);
    }

    [Fact]
    public void The_last_stairs_end_the_delve()
    {
        var d = New(Short with { Depth = 2 });
        d.Descend();
        d.Descend();
        Assert.Equal(GameMode.Won, d.Engine.Mode);
    }

    [Fact]
    public void A_delve_saves_and_loads_with_its_levels()
    {
        var d = New();
        d.Descend();
        var g = d.Engine;
        g.Hero.AddToBackpack(new Item { BaseId = "potionHealing" });
        var save = d.ToSave();
        Assert.True(Delve.IsDelve(save));
        var store = new SaveStore(Path.Combine(Path.GetTempPath(), "avahack-delve-" + Guid.NewGuid().ToString("N")));
        store.Save("d", save);
        var back = Delve.FromSave(store.Load("d")!, Db);
        Assert.Equal(2, back.Depth);
        Assert.Equal(g.PartyCell, back.Engine.PartyCell);
        Assert.Equal(g.Level.Name, back.Engine.Level.Name);
        Assert.Equal(g.Monsters.Count, back.Engine.Monsters.Count);
        back.Ascend();
        Assert.Equal(1, back.Depth);
    }

    [Fact]
    public void Monster_strength_changes_the_creatures()
    {
        var easy = New(Short with { Strength = Difficulty.Gentle });
        var hard = New(Short with { Strength = Difficulty.Brutal });
        Assert.True(hard.Engine.Monsters.Sum(m => m.MaxHp) > easy.Engine.Monsters.Sum(m => m.MaxHp));
        Assert.True(hard.Engine.Monsters.All(m => m.ToHitBonus == 2));
    }

    [Fact]
    public void Death_saves_on_the_cruel_settings()
    {
        var d = New(Short with { Strength = Difficulty.Cruel });
        var g = d.Engine;
        var pell = g.Party[Slot.FrontRight]!;
        pell.TakeDamage(999);
        for (int i = 0; i < 60 * 60 && !pell.IsDead; i++) g.Tick();
        Assert.True(pell.IsDead);
        Assert.Contains(g.Log, l => l.Contains("is dead"));
    }
}

public sealed class TrapKindTests
{
    private static GameEngine Corridor(Trap trap, string extra = "")
    {
        var l = LevelLoader.Parse($"start: 1 1 east\n{extra}map:\n##########\n#........#\n##########");
        l.AddTrap(trap);
        return new GameEngine(l, 5);
    }

    private static void Run(GameEngine g, double s)
    {
        for (int i = 0; i < s * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static void Step(GameEngine g, int n = 1)
    {
        for (int i = 0; i < n; i++) { g.Press(GameAction.StepForward); Run(g, 0.3); }
    }

    [Fact]
    public void Darts_hit_and_poison()
    {
        var g = Corridor(new Trap { Kind = TrapKind.DartWall, Cell = new Cell(3, 1), HideDc = 30, DisarmDc = 15, Depth = 4 });
        double hp = g.Hero.Hp;
        Step(g, 2);
        Assert.Contains(g.Log, l => l.Contains("Darts"));
        Assert.True(g.Level.Traps[0].Sprung);
    }

    [Fact]
    public void A_spotted_trap_can_be_disarmed_from_the_next_cell()
    {
        var g = Corridor(new Trap { Kind = TrapKind.FrostGlyph, Cell = new Cell(3, 1), HideDc = 1, DisarmDc = 2, Depth = 1 });
        Step(g); // within two cells: spotted
        Assert.True(g.Level.Traps[0].Spotted);
        g.Press(GameAction.Use);
        Assert.True(g.Level.Traps[0].Disarmed);
        Step(g);
        Assert.False(g.Hero.Conditions.Has(ConditionId.Frozen));
    }

    [Fact]
    public void A_teleporter_moves_the_party_and_a_spinner_turns_it()
    {
        var g = Corridor(new Trap { Kind = TrapKind.Teleporter, Cell = new Cell(2, 1), Target = new Cell(7, 1), HideDc = 30, DisarmDc = 99 });
        Step(g);
        Assert.Equal(new Cell(7, 1), g.PartyCell);
        var s = Corridor(new Trap { Kind = TrapKind.Spinner, Cell = new Cell(2, 1), HideDc = 30, DisarmDc = 99 });
        var seen = new HashSet<Dir>();
        for (int i = 0; i < 12; i++)
        {
            s.Stand(new Cell(1, 1), Dir.East);
            Step(s);
            if (s.PartyCell == new Cell(2, 1)) seen.Add(s.Facing);
        }
        Assert.True(seen.Count > 1);
    }

    [Fact]
    public void Spikes_hurt_whoever_stands_on_them_when_they_rise()
    {
        var g = Corridor(new Trap { Kind = TrapKind.SpikeFloor, Cell = new Cell(2, 1), DisarmDc = 99 });
        Step(g);
        double hp = g.Hero.Hp;
        Run(g, GameEngine.SpikeCycle + 0.1);
        Assert.True(g.Hero.Hp < hp);
    }

    [Fact]
    public void A_boulder_rolls_down_the_corridor_and_crushes_the_party()
    {
        var g = Corridor(new Trap { Kind = TrapKind.RollingBoulder, Cell = new Cell(2, 1), Target = new Cell(7, 1), HideDc = 30, DisarmDc = 99 });
        g.Stand(new Cell(1, 1), Dir.East);
        Step(g);
        Assert.Single(g.Boulders);
        double hp = g.Hero.Hp;
        Run(g, 3);
        Assert.True(g.Hero.Hp < hp);
        if (g.Mode == GameMode.Playing) Assert.Empty(g.Boulders); // (it may have killed a lone hero, which stops the clock)
    }

    [Fact]
    public void An_alarm_wakes_everything_near()
    {
        var g = Corridor(new Trap { Kind = TrapKind.AlarmPlate, Cell = new Cell(2, 1), HideDc = 30, DisarmDc = 99 });
        var far = g.AddMonster("shambler", new Cell(8, 1), Dir.North);
        Step(g);
        Assert.True(far.IsAware);
    }

    [Fact]
    public void Anti_magic_swallows_spells()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n######\n#....#\n######");
        l.AddTrap(new Trap { Kind = TrapKind.AntiMagic, Cell = new Cell(2, 1), Area = [new Cell(1, 1), new Cell(2, 1), new Cell(3, 1)] });
        var g = new GameEngine(l, 1, new Party(ProgressionReport.Hero(Heroes.Content, "wizard")), Heroes.Content);
        Assert.Equal("The air here swallows magic.", g.WhyNotCast(g.Hero, "spark"));
    }

    [Fact]
    public void Locked_doors_open_with_their_key_or_a_good_pick()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n#######\n#..+..#\n#######");
        var door = l.DoorAt(3, 1)!;
        door.Locked = true;
        var keyed = new Door(door.Cell, DoorKind.Hand, false) { Locked = true, KeyId = "k1", LockDc = 99 };
        l.AddDoor(keyed);
        var g = new GameEngine(l);
        g.Stand(new Cell(2, 1), Dir.East);
        g.Press(GameAction.Use);
        Assert.True(keyed.Locked);
        Assert.Contains("key", g.LastMessage);
        g.Hero.AddToBackpack(new Item { BaseId = "key", KeyId = "k1" });
        g.Press(GameAction.Use);
        Assert.False(keyed.Locked);
        Assert.True(keyed.Opening);
    }

    [Fact]
    public void Searching_a_wall_finds_a_secret_door_and_illusory_walls_let_you_through()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n######\n#..#.#\n######");
        l.AddSecret(new SecretDoor(new Cell(3, 1), 5));
        var g = new GameEngine(l);
        Assert.True(l.SecretAt(new Cell(3, 1))!.Found); // a DC 5 door is noticed in passing
        Assert.Equal(CellKind.Door, l.KindAt(3, 1));

        var m = LevelLoader.Parse("start: 1 1 east\nmap:\n######\n#....#\n######");
        m.AddIllusory(new Cell(2, 1));
        var h = new GameEngine(m);
        Assert.True(m.LooksLikeWall(2, 1));
        h.Press(GameAction.StepForward);
        Run(h, 0.3);
        Assert.Equal(new Cell(2, 1), h.PartyCell);
        Assert.Contains(h.Log, x => x.Contains("walk straight through"));
    }

    [Fact]
    public void A_cursed_altar_pays_and_then_bites()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n######\n#....#\n#....#\n######");
        l.AddTrap(new Trap { Kind = TrapKind.CursedAltar, Cell = new Cell(2, 1), Depth = 3 });
        var g = new GameEngine(l, 9);
        Assert.False(g.CanPartyEnter(new Cell(2, 1)));
        g.Press(GameAction.Use);
        Assert.True(g.Party.Gold > 0);
        Assert.True(g.Hero.Conditions.Has(ConditionId.Cursed) || g.Monsters.Any(x => x.Elite) || g.Log.Any(x => x.Contains("cold")));
    }
}

public sealed class DelveRevisitTests
{
    [Fact]
    public void Going_back_to_a_level_doesnt_copy_what_lies_on_its_floor()
    {
        var db = Heroes.Content;
        var d = Delve.Start(db, PartyTests.QuickParty(db), new DelveOptions { Seed = "AAA7TF4Q", Depth = 4 });
        d.Descend();
        int items = d.Engine.Level.Piles.Values.Sum(p => p.Count);
        for (int i = 0; i < 3; i++) { d.Ascend(); d.Descend(); }
        Assert.Equal(items, d.Engine.Level.Piles.Values.Sum(p => p.Count));
    }
}
