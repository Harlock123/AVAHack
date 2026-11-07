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
    /// <summary>In a generated Delve a pit drops to the next level down instead of a cell here.</summary>
    public bool ToNextLevel { get; init; }
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

/// <summary>A chest standing in a cell. It blocks the way; Use, facing it, opens it.</summary>
public sealed class Chest(Cell cell, string table, int lockDc = 0, string? key = null)
{
    public Cell Cell { get; } = cell;
    /// <summary>The loot table it holds.</summary>
    public string Table { get; } = table;
    /// <summary>0 if unlocked; otherwise the Lockpicking DC.</summary>
    public int LockDc { get; internal set; } = lockDc;
    /// <summary>A key that opens it, if any.</summary>
    public string? Key { get; } = key;
    public bool Opened { get; internal set; }
    /// <summary>A mimic waits inside some chests.</summary>
    public bool Mimic { get; init; }
    internal List<Items.Item> Contents { get; } = [];
}

/// <summary>A monster to place when the level starts.</summary>
public sealed record Spawn(string Creature, Cell Cell, Dir Facing);

/// <summary>What the floor of a cell looks like, for drawing.</summary>
public enum FloorLook { Plain, Pit, Plate, Stairs }
