namespace AVAHack.World;

/// <summary>
/// A pit in a floor cell. Stepping in drops the party to <see cref="Landing"/> (the cellar below, on the same map
/// for now). A hidden pit looks like floor until someone's passive Perception beats <see cref="HideDc"/>.
/// </summary>
public sealed class Pit(Cell cell, Cell landing, int hideDc)
{
    public Cell Cell { get; } = cell;
    public Cell Landing { get; } = landing;
    /// <summary>0 for an open pit anyone can see.</summary>
    public int HideDc { get; } = hideDc;
    public bool Spotted { get; internal set; } = hideDc == 0;
}

public enum PlateMode
{
    /// <summary>Each press toggles the linked doors.</summary>
    Toggle,
    /// <summary>The linked doors are open only while something stands on the plate.</summary>
    Hold,
}

/// <summary>A pressure plate. The party or any living creature standing on it presses it.</summary>
public sealed class Plate(Cell cell, PlateMode mode, IReadOnlyList<Cell> targets)
{
    public Cell Cell { get; } = cell;
    public PlateMode Mode { get; } = mode;
    public IReadOnlyList<Cell> Targets { get; } = targets;
    public bool Pressed { get; internal set; }
}

/// <summary>Writing on the floor or a scrap of paper: shown once, when the party first steps on the cell.</summary>
public sealed class Note(Cell cell, string text)
{
    public Cell Cell { get; } = cell;
    public string Text { get; } = text;
    public bool Read { get; internal set; }
}

/// <summary>A monster to place when the level starts.</summary>
public sealed record Spawn(string Creature, Cell Cell, Dir Facing);

/// <summary>What the floor of a cell looks like, for drawing.</summary>
public enum FloorLook { Plain, Pit, Plate, Stairs }
