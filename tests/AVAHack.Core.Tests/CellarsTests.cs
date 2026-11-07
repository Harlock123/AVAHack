using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>The M2 level, The Cellars: it loads, and it can be finished.</summary>
public sealed class CellarsTests
{
    private static Level Load() => LevelLoader.LoadBuiltIn("the-cellars");

    [Fact]
    public void It_loads_with_its_creatures_and_traps()
    {
        var l = Load();
        Assert.Equal("The Cellars", l.Name);
        Assert.Equal(24, l.Spawns.Count);
        Assert.Equal(6, l.Spawns.Select(s => s.Creature).Distinct().Count());
        Assert.Equal(2, l.Pits.Count());
        Assert.Single(l.Plates);
        Assert.Single(l.Stairs);
        var g = new GameEngine(l);
        Assert.Equal(24, g.Monsters.Count);
        Assert.Contains(g.Log, m => m.Contains("booms shut")); // the note on the start cell
        Assert.True(g.Lit); // a torch is lit straight away
    }

    /// <summary>
    /// Walks the map as a player could: portcullises stay shut until their lever or plate has been reached, and
    /// the walk ends at the stairs. Pits count as floor (they lead to their landing).
    /// </summary>
    [Fact]
    public void The_stairs_can_be_reached_working_the_mechanisms_in_order()
    {
        var l = Load();
        var open = new HashSet<Cell>(l.Doors.Where(d => d.Kind == DoorKind.Hand || d.IsPassable).Select(d => d.Cell));
        var reached = new HashSet<Cell>();
        bool grew = true;
        while (grew)
        {
            grew = false;
            var seen = Flood(l, l.Start, open);
            if (seen.Count > reached.Count) { reached = seen; grew = true; }
            foreach (var lever in l.Levers.Where(v => reached.Contains(v.Cell)))
                foreach (var t in lever.Targets) grew |= open.Add(t);
            foreach (var plate in l.Plates.Where(p => reached.Contains(p.Cell)))
                foreach (var t in plate.Targets) grew |= open.Add(t);
        }
        Assert.Contains(l.Stairs.Single(), reached);
        Assert.All(l.Spawns, s => Assert.Contains(s.Cell, reached));
        // Every floor cell is somewhere a player can go.
        for (int y = 0; y < l.Height; y++)
        for (int x = 0; x < l.Width; x++)
            if (l.KindAt(x, y) == CellKind.Floor) Assert.Contains(new Cell(x, y), reached);
    }

    [Fact]
    public void The_hidden_pit_can_be_walked_round_once_spotted()
    {
        var l = Load();
        var pit = l.PitAt(new Cell(15, 9))!;
        Assert.False(pit.Spotted);
        Assert.Equal(CellKind.Floor, l.KindAt(16, 9)); // the corridor is two wide there
        var g = new GameEngine(l);
        Assert.True(g.Hero.PassivePerception() >= pit.HideDc); // the fighter can spot it in time
    }

    private static HashSet<Cell> Flood(Level l, Cell start, HashSet<Cell> openDoors)
    {
        var seen = new HashSet<Cell> { start };
        var todo = new Queue<Cell>(seen);
        while (todo.Count > 0)
        {
            var c = todo.Dequeue();
            var next = new List<Cell>();
            if (l.PitAt(c) is { } pit) next.Add(pit.Landing);
            foreach (var d in Enum.GetValues<Dir>()) next.Add(c.Step(d));
            foreach (var n in next)
            {
                var kind = l.KindAt(n);
                if (kind == CellKind.Wall || (kind == CellKind.Door && !openDoors.Contains(n))) continue;
                if (seen.Add(n)) todo.Enqueue(n);
            }
        }
        return seen;
    }
}
