namespace AVAHack.World;

public enum DoorKind
{
    /// <summary>A wooden door the party opens and closes by hand.</summary>
    Hand,
    /// <summary>An iron portcullis, worked only by a lever or other mechanism. It can be seen through.</summary>
    Portcullis,
}

/// <summary>
/// A door in the middle of its cell. It slides up into the ceiling: <see cref="Open"/> runs from 0 (shut) to
/// 1 (fully raised). The party can only pass a door that is fully open.
/// </summary>
public sealed class Door
{
    /// <summary>Seconds a door takes to open or close fully.</summary>
    public const double TravelSeconds = 1.0;

    public Cell Cell { get; }
    public DoorKind Kind { get; }

    /// <summary>
    /// True when the door's panel lies along the x axis (walls to its west and east), so it is crossed going
    /// north or south. False when it lies along the y axis and is crossed going east or west.
    /// </summary>
    public bool AlongX { get; }

    public double Open { get; internal set; }
    public bool Opening { get; internal set; }

    public bool IsPassable => Open >= 1.0;
    public bool IsShut => Open <= 0.0;

    public Door(Cell cell, DoorKind kind, bool alongX, bool open = false)
    {
        Cell = cell;
        Kind = kind;
        AlongX = alongX;
        Opening = open;
        Open = open ? 1 : 0;
    }
}
