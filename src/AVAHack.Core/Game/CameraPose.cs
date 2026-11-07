namespace AVAHack.Game;

/// <summary>
/// Where the party's eyes are, in world units (cell (x, y) spans [x, x+1) × [y, y+1)), and which way they look
/// (<see cref="Yaw"/> in radians: 0 is east, π/2 is south). <see cref="Bob"/> raises or lowers the eye a little
/// while walking.
/// </summary>
public readonly record struct CameraPose(double X, double Y, double Yaw, double Bob = 0)
{
    public double DirX => Math.Cos(Yaw);
    public double DirY => Math.Sin(Yaw);
}
