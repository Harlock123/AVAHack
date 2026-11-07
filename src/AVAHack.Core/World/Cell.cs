namespace AVAHack.World;

public enum CellKind { Wall, Floor, Door }

/// <summary>A grid position.</summary>
public readonly record struct Cell(int X, int Y)
{
    public Cell Step(Dir d) => new(X + d.Dx(), Y + d.Dy());
    public int Distance(Cell o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y);
    public override string ToString() => $"{X},{Y}";
}
