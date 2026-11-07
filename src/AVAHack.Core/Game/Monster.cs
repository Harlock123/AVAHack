using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public enum MonsterState { Idle, Aware, Fleeing, Pile, Dead }

public enum MonsterAction { None, Stepping, Turning, WindingUp, Recovering, Staggered, Opening }

/// <summary>
/// A creature in the level. Like the party it holds a grid cell and a facing; it steps, turns, winds up an
/// attack (the tell) and recovers. Small creatures share a cell, up to four, each in its own corner
/// (<see cref="Slot"/>).
/// </summary>
public sealed class Monster
{
    public int Id { get; }
    public CreatureDef Def { get; }
    public Cell Cell { get; internal set; }
    public Dir Facing { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; }
    public MonsterState State { get; internal set; } = MonsterState.Idle;
    public int Slot { get; internal set; }
    public Resistances Resistances { get; }

    public MonsterAction Action { get; internal set; }
    internal double ActionLeft;
    internal double ActionTotal;
    public Cell From { get; internal set; }
    public CreatureAttackDef? Attack { get; internal set; }

    /// <summary>Seconds left of the red flash after a hit.</summary>
    public double HurtFlash { get; internal set; }
    /// <summary>Seconds left of the lunge after a blow lands.</summary>
    public double StrikeTime { get; internal set; }
    /// <summary>Seconds until a pile of bones stands up again.</summary>
    public double PileLeft { get; internal set; }
    public bool FellInPit { get; internal set; }
    internal double FleeLeft;
    internal double IdleLeft;
    internal DamageType? LastHitType;

    public bool IsAlive => State is MonsterState.Idle or MonsterState.Aware or MonsterState.Fleeing;
    public bool IsAware => State is MonsterState.Aware or MonsterState.Fleeing;
    public bool IsSmall => Def.Size == CreatureSize.Small;

    /// <summary>How far through its current action it is, 0 to 1.</summary>
    public double ActionProgress => ActionTotal <= 0 ? 1 : Math.Clamp(1 - ActionLeft / ActionTotal, 0, 1);

    internal Monster(int id, CreatureDef def, Cell cell, Dir facing, int hp)
    {
        Id = id;
        Def = def;
        Cell = From = cell;
        Facing = facing;
        Hp = MaxHp = Math.Max(1, hp);
        Resistances = new Resistances().Resist(def.Resist).Vulnerable(def.Vulnerable).Immune(def.Immune);
    }

    /// <summary>Where to draw it, <paramref name="alpha"/> ticks into the next tick, in world units.</summary>
    public (double X, double Y) Position(double alpha = 0)
    {
        double t = Action == MonsterAction.Stepping
            ? Math.Clamp(1 - (ActionLeft - alpha * GameEngine.TickSeconds) / ActionTotal, 0, 1)
            : 1;
        t = t * t * (3 - 2 * t);
        double x = From.X + (Cell.X - From.X) * t + 0.5, y = From.Y + (Cell.Y - From.Y) * t + 0.5;
        if (IsSmall)
        {
            x += (Slot & 1) == 0 ? -0.2 : 0.2;
            y += (Slot & 2) == 0 ? -0.2 : 0.2;
        }
        if (StrikeTime > 0)
        {
            double lunge = Math.Sin(Math.PI * StrikeTime / 0.2) * 0.18;
            x += Facing.Dx() * lunge;
            y += Facing.Dy() * lunge;
        }
        return (x, y);
    }

    public override string ToString() => $"{Def.Name} #{Id} at {Cell} facing {Facing} ({State}, {Hp}/{MaxHp})";
}

/// <summary>A thrown stone or the like, flying straight along a row or column of the grid.</summary>
public sealed class Projectile
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public Dir Dir { get; }
    public Monster Source { get; }
    public CreatureAttackDef Attack { get; }
    internal double Travelled;
    internal Cell LastCell;

    internal Projectile(Monster source, CreatureAttackDef attack, Dir dir)
    {
        Source = source;
        Attack = attack;
        Dir = dir;
        X = source.Cell.X + 0.5;
        Y = source.Cell.Y + 0.5;
        LastCell = source.Cell;
    }
}

public enum HandSide { Left, Right }

/// <summary>One of the hero's hands: what it's recovering from, and whether it's held down.</summary>
public sealed class Hand
{
    public double Recovery { get; internal set; }
    public double RecoveryTotal { get; internal set; }
    public bool Held { get; internal set; }
    public double HeldFor { get; internal set; }
    public bool Ready => Recovery <= 0;

    /// <summary>How much of the recovery is left, 1 (just used) to 0 (ready), for the cooldown sweep.</summary>
    public double RecoveryShare => RecoveryTotal <= 0 ? 0 : Math.Clamp(Recovery / RecoveryTotal, 0, 1);

    internal void Start(double seconds)
    {
        Recovery = RecoveryTotal = seconds;
    }
}

public enum TextKind { MonsterHurt, HeroHurt, Miss, Blocked, Critical, Info }

/// <summary>A number or word to float up from a place in the world for a moment.</summary>
public readonly record struct FloatText(double X, double Y, double Z, string Text, TextKind Kind);
