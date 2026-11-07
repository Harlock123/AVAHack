using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class LevelTests
{
    [Fact]
    public void The_test_hall_loads()
    {
        var l = LevelLoader.LoadBuiltIn("test-hall");
        Assert.Equal("Test Hall", l.Name);
        Assert.Equal((16, 16), (l.Width, l.Height));
        Assert.Equal(new Cell(2, 2), l.Start);
        Assert.Equal(Dir.East, l.StartFacing);
        Assert.Equal(3, l.Doors.Count());
        var lever = Assert.Single(l.Levers);
        Assert.Equal(new Cell(7, 6), Assert.Single(lever.Targets));
    }

    [Fact]
    public void Doors_know_which_way_they_lie()
    {
        var l = LevelLoader.LoadBuiltIn("test-hall");
        Assert.False(l.DoorAt(5, 3)!.AlongX);  // walls north and south: crossed going east-west
        Assert.True(l.DoorAt(10, 10)!.AlongX); // walls west and east: crossed going north-south
        Assert.Equal(DoorKind.Portcullis, l.DoorAt(7, 6)!.Kind);
    }

    [Fact]
    public void Outside_the_grid_is_wall()
    {
        var l = LevelLoader.LoadBuiltIn("test-hall");
        Assert.Equal(CellKind.Wall, l.KindAt(-1, 3));
        Assert.Equal(CellKind.Wall, l.KindAt(3, 99));
    }

    [Fact]
    public void Every_floor_cell_of_the_test_hall_can_be_reached()
    {
        var l = LevelLoader.LoadBuiltIn("test-hall");
        var seen = new HashSet<Cell> { l.Start };
        var todo = new Queue<Cell>(seen);
        while (todo.Count > 0)
        {
            var c = todo.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (l.KindAt(n) != CellKind.Wall && seen.Add(n)) todo.Enqueue(n);
            }
        }
        for (int y = 0; y < l.Height; y++)
        for (int x = 0; x < l.Width; x++)
            if (l.KindAt(x, y) != CellKind.Wall) Assert.Contains(new Cell(x, y), seen);
    }

    [Theory]
    [InlineData("start: 1 1 east\nmap:\n###\n#.#\n##", "same width")]
    [InlineData("map:\n###\n#.#\n###", "no start")]
    [InlineData("start: 0 0 east\nmap:\n###\n#.#\n###", "not a floor")]
    [InlineData("start: 1 1 east\nmap:\n#####\n#.+.#\n#...#\n#####", "opposite sides")]
    [InlineData("start: 1 1 east\nlever: 1 1 east -> 2 1\nmap:\n####\n#..#\n####", "no wall")]
    [InlineData("start: 1 1 east\nlever: 1 1 north -> 2 1\nmap:\n####\n#..#\n####", "no door")]
    [InlineData("start: 1 1 east\nmap:\n###\n#X#\n###", "Unknown map character")]
    [InlineData("start: 1 1 up\nmap:\n###\n#.#\n###", "Not a direction")]
    public void Bad_levels_are_refused_with_a_reason(string text, string reason)
    {
        var e = Assert.Throws<FormatException>(() => LevelLoader.Parse(text));
        Assert.Contains(reason, e.Message);
    }

    [Fact]
    public void Directions_turn_and_face_the_right_way()
    {
        Assert.Equal(Dir.East, Dir.North.Right());
        Assert.Equal(Dir.West, Dir.North.Left());
        Assert.Equal(Dir.South, Dir.North.Opposite());
        Assert.Equal((0, -1), (Dir.North.Dx(), Dir.North.Dy()));
        foreach (var d in Enum.GetValues<Dir>())
        {
            Assert.Equal(d.Dx(), Math.Round(Math.Cos(d.Yaw())), 6);
            Assert.Equal(d.Dy(), Math.Round(Math.Sin(d.Yaw())), 6);
        }
    }
}
