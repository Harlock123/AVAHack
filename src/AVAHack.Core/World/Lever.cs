namespace AVAHack.World;

/// <summary>
/// A lever on a wall. It sits on the <see cref="Face"/> side of floor cell <see cref="Cell"/>, so the party
/// pulls it standing in that cell facing that way. Pulling it toggles every linked door.
/// </summary>
public sealed class Lever
{
    public Cell Cell { get; }
    public Dir Face { get; }
    public IReadOnlyList<Cell> Targets { get; }
    public bool Down { get; internal set; }

    public Lever(Cell cell, Dir face, IReadOnlyList<Cell> targets)
    {
        Cell = cell;
        Face = face;
        Targets = targets;
    }
}
