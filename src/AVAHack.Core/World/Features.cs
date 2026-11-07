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
    /// <summary>Spikes at the bottom: 4d6 instead of 2d6.</summary>
    public bool Spiked { get; init; }
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

/// <summary>A monster to place when the level starts, raised to <see cref="Level"/> if that is above its own.</summary>
public sealed record Spawn(string Creature, Cell Cell, Dir Facing)
{
    public int? Level { get; init; }
}

/// <summary>What the floor of a cell looks like, for drawing.</summary>
public enum FloorLook { Plain, Pit, Plate, Stairs, StairsUp, Trap, Spikes, Glyph, Rune, Teleporter, Rubble, Water, Shards }

/// <summary>A wall that is really a door, until someone finds it (spec 14.2).</summary>
public sealed class SecretDoor(Cell cell, int dc)
{
    public Cell Cell { get; } = cell;
    public int Dc { get; } = dc;
    public bool Found { get; internal set; }
}

/// <summary>The twenty kinds of trap (spec 11.3), less the pits and plates the level already has.</summary>
public enum TrapKind
{
    DartWall, Tripwire, SpikeFloor, SwingingBlade, FireSpout, FrostGlyph, LightningRune, GasVent, CollapsingCeiling,
    RollingBoulder, Teleporter, Spinner, AlarmPlate, CursedAltar, FloodingRoom, AntiMagic,
}

/// <summary>
/// A trap. Most fire when something steps into <see cref="Cell"/>; spikes and blades work on a cycle; an altar
/// springs when its offering is taken; anti-magic is a zone. Hidden until passive Perception beats
/// <see cref="HideDc"/> (or Detect Traps), and disarmed from the next cell, facing it, against
/// <see cref="DisarmDc"/>.
/// </summary>
public sealed class Trap
{
    public required TrapKind Kind { get; init; }
    public required Cell Cell { get; init; }
    /// <summary>The way darts, flames and a boulder travel.</summary>
    public Dir Facing { get; init; }
    /// <summary>A teleporter's destination, a boulder's starting cell, a flooding room's drain lever cell.</summary>
    public Cell? Target { get; init; }
    public int HideDc { get; init; }
    public int DisarmDc { get; init; }
    /// <summary>How deep it is: its damage grows with depth.</summary>
    public int Depth { get; init; } = 1;
    /// <summary>A variant: tripwires (darts or alarm), gas (poison or sleep).</summary>
    public string Variant { get; init; } = "";
    /// <summary>The cells of a zone or a flooding room.</summary>
    public IReadOnlyList<Cell> Area { get; init; } = [];
    public bool Spotted { get; internal set; }
    public bool Disarmed { get; internal set; }
    /// <summary>One-shot traps that have gone off.</summary>
    public bool Sprung { get; internal set; }
    internal double Timer;
    /// <summary>A blade mid-swing, spikes up, a room flooding.</summary>
    public bool Active { get; internal set; }

    public bool Armed => !Disarmed && !Sprung;
    /// <summary>Spikes and blades keep working; others fire once.</summary>
    public bool Cyclic => Kind is TrapKind.SpikeFloor or TrapKind.SwingingBlade;
    /// <summary>Traps that do real harm, for the generator's rule about warnings on the main path.</summary>
    public bool Deadly => Kind is TrapKind.SwingingBlade or TrapKind.FireSpout or TrapKind.CollapsingCeiling or TrapKind.RollingBoulder or TrapKind.LightningRune;
}
