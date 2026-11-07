using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class TrapTests
{
    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static GameEngine Load(string text) => new(LevelLoader.Parse(text));

    [Fact]
    public void An_unseen_pit_drops_the_party_to_its_landing()
    {
        var g = Load("""
            start: 1 1 east
            pit: 4 1 -> 2 3 hidden 30
            map:
            #######
            #...^.#
            #######
            #.....#
            #######
            """);
        Assert.False(g.Level.PitAt(new Cell(4, 1))!.Spotted);
        Assert.Equal(FloorLook.Plain, g.Level.FloorAt(4, 1));
        double hp = g.Hero.Hp;
        for (int i = 0; i < 3; i++) g.Press(GameAction.StepForward);
        Run(g, 1.5);
        Assert.Equal(new Cell(2, 3), g.PartyCell);
        Assert.True(g.Hero.Hp < hp);
        Assert.Contains("floor gives way", g.Log.Last(m => m.Contains("floor")));
        Assert.Equal(FloorLook.Pit, g.Level.FloorAt(4, 1));
    }

    [Fact]
    public void A_sharp_eye_spots_a_hidden_pit_in_time()
    {
        var g = Load("""
            start: 1 1 east
            pit: 4 1 -> 1 1 hidden 12
            map:
            #######
            #...^.#
            #######
            """);
        Assert.Equal(12, g.Hero.PassivePerception()); // Brannoc: 10 + WIS 1 + Survival 10 / 10
        g.Press(GameAction.StepForward); // (2,1) is within 2 cells of the pit
        Assert.True(g.Level.PitAt(new Cell(4, 1))!.Spotted);
        Assert.Contains("pit", g.LastMessage);
    }

    [Fact]
    public void The_mindless_walk_into_pits()
    {
        var g = Load("""
            start: 1 1 east
            pit: 3 1 -> 1 1
            map:
            ########
            #..^...#
            ########
            """);
        var sham = g.AddMonster("shambler", new Cell(5, 1), Dir.West);
        var gob = g.AddMonster("gutterGoblin", new Cell(6, 1), Dir.West);
        Run(g, 8);
        Assert.True(sham.FellInPit);
        Assert.False(gob.FellInPit);
        Assert.True(gob.IsAlive);
    }

    [Fact]
    public void A_hold_plate_keeps_its_door_open_only_while_pressed()
    {
        var g = Load("""
            start: 1 1 east
            plate: 2 1 hold -> 4 1
            map:
            #######
            #.o.P.#
            #######
            """);
        var gate = g.Level.DoorAt(4, 1)!;
        g.Press(GameAction.StepForward);
        Run(g, 1.2);
        Assert.True(gate.IsPassable);
        g.Press(GameAction.StepBack);
        Run(g, 1.5);
        Assert.True(gate.IsShut);
    }

    [Fact]
    public void A_toggle_plate_flips_its_door_each_press_and_creatures_press_it_too()
    {
        var g = Load("""
            start: 1 1 east
            plate: 3 1 toggle -> 5 1
            map:
            ########
            #..o#P.#
            #....###
            ########
            """);
        var gate = g.Level.DoorAt(5, 1)!;
        g.Stand(new Cell(1, 2), Dir.East);
        g.AddMonster("cellarRat", new Cell(3, 1), Dir.West); // standing on the plate
        g.Tick();
        Assert.True(gate.Opening);

        // The party pressing it twice flips the door twice.
        var h = Load("""
            start: 1 1 east
            plate: 2 1 toggle -> 4 1
            map:
            ######
            #.o.P#
            ######
            """);
        var door = h.Level.DoorAt(4, 1)!;
        h.Press(GameAction.StepForward);
        Assert.True(door.Opening);
        h.Press(GameAction.StepBack);
        Run(h, 1);
        h.Press(GameAction.StepForward);
        Assert.False(door.Opening);
    }

    [Fact]
    public void Notes_are_read_once_and_the_stairs_end_the_level()
    {
        var g = Load("""
            start: 1 1 east
            note: 2 1 Someone has scratched: "Down is the only way out."
            map:
            #####
            #..>#
            #####
            """);
        g.Press(GameAction.StepForward);
        Assert.Contains("only way out", g.LastMessage);
        g.Press(GameAction.StepForward);
        Run(g, 1);
        Assert.Equal(GameMode.Won, g.Mode);
    }

    [Theory]
    [InlineData("start: 1 1 east\nmap:\n####\n#.^#\n####", "has no 'pit:' line")]
    [InlineData("start: 1 1 east\nmap:\n####\n#.o#\n####", "has no 'plate:' line")]
    [InlineData("start: 1 1 east\npit: 2 1 -> 9 9\nmap:\n####\n#.^#\n####", "landing")]
    [InlineData("start: 1 1 east\nplate: 2 1 sometimes -> 1 1\nmap:\n####\n#.o#\n####", "toggle or hold")]
    [InlineData("start: 1 1 east\nmonster: cellarRat 0 0 east\nmap:\n####\n#..#\n####", "must be floor")]
    public void Bad_trap_lines_are_refused(string text, string reason)
    {
        var e = Assert.Throws<FormatException>(() => LevelLoader.Parse(text));
        Assert.Contains(reason, e.Message);
    }
}
