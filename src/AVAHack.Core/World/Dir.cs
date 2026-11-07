namespace AVAHack.World;

/// <summary>A compass direction on the grid. North is -y (up the map), East is +x.</summary>
public enum Dir { North, East, South, West }

public static class DirExtensions
{
    public static int Dx(this Dir d) => d switch { Dir.East => 1, Dir.West => -1, _ => 0 };
    public static int Dy(this Dir d) => d switch { Dir.South => 1, Dir.North => -1, _ => 0 };
    public static Dir Right(this Dir d) => (Dir)(((int)d + 1) & 3);
    public static Dir Left(this Dir d) => (Dir)(((int)d + 3) & 3);
    public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);

    /// <summary>The yaw in radians that faces this way: East is 0 and angles grow clockwise on the map (y down).</summary>
    public static double Yaw(this Dir d) => d switch
    {
        Dir.East => 0,
        Dir.South => Math.PI / 2,
        Dir.West => Math.PI,
        _ => -Math.PI / 2,
    };

    public static Dir Parse(string s) => s.Trim().ToLowerInvariant() switch
    {
        "n" or "north" => Dir.North,
        "e" or "east" => Dir.East,
        "s" or "south" => Dir.South,
        "w" or "west" => Dir.West,
        _ => throw new FormatException($"Not a direction: '{s}'"),
    };

    public static char Letter(this Dir d) => "NESW"[(int)d];
}
