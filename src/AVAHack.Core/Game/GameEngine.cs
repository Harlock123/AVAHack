using AVAHack.World;

namespace AVAHack.Game;

/// <summary>
/// The game's clock and rules, with no drawing in it. <see cref="Update"/> runs a fixed 60 Hz tick; the view asks
/// for <see cref="Camera"/> with the fraction of the next tick that has passed, so motion is smooth at any frame
/// rate. Commands come in through <see cref="Press"/> and <see cref="ClickWorld"/>.
/// </summary>
public sealed class GameEngine
{
    public const int TicksPerSecond = 60;
    public const double TickSeconds = 1.0 / TicksPerSecond;

    /// <summary>Ticks a step takes (0.25 s).</summary>
    public const int StepTicks = 15;
    /// <summary>Ticks a 90° turn takes (0.2 s).</summary>
    public const int TurnTicks = 12;
    /// <summary>Ticks the nudge against a wall takes.</summary>
    public const int BumpTicks = 10;
    /// <summary>How many commands may wait while one is being carried out.</summary>
    public const int QueueLimit = 2;

    /// <summary>How far the eye rises and falls over a step, in world units (a cell is 1).</summary>
    public const double BobHeight = 0.025;

    private readonly Queue<GameAction> _queue = new();
    private double _accumulator;
    private Motion? _motion;

    public Level Level { get; }
    public Cell PartyCell { get; private set; }
    public Dir Facing { get; private set; }
    public long TickCount { get; private set; }
    public double Time => TickCount * TickSeconds;
    public bool HeadBob { get; set; } = true;

    /// <summary>True while a step, turn or bump is playing.</summary>
    public bool IsMoving => _motion is not null;
    public int Queued => _queue.Count;

    /// <summary>The latest message for the player ("The lever clanks down.").</summary>
    public string? LastMessage { get; private set; }
    public event Action<string>? Message;
    public event Action<GameEvent>? Event;

    public GameEngine(Level level)
    {
        Level = level;
        PartyCell = level.Start;
        Facing = level.StartFacing;
    }

    /// <summary>Runs the clock forward by <paramref name="seconds"/> of real time, in whole ticks.</summary>
    public void Update(double seconds)
    {
        _accumulator += Math.Clamp(seconds, 0, 0.25);
        while (_accumulator >= TickSeconds)
        {
            _accumulator -= TickSeconds;
            Tick();
        }
    }

    /// <summary>How far through the next tick the clock is, 0 to 1, for smooth drawing.</summary>
    public double Alpha => _accumulator / TickSeconds;

    /// <summary>One 1/60 s step of the simulation.</summary>
    public void Tick()
    {
        TickCount++;
        foreach (var door in Level.Doors) MoveDoor(door);

        if (_motion is { } m)
        {
            m.Elapsed++;
            if (m.Elapsed >= m.Duration) _motion = null;
        }
        if (_motion is null) StartNext();
    }

    /// <summary>Asks for an action. Movement waits its turn in a short queue; extra presses are dropped.</summary>
    public void Press(GameAction action)
    {
        if (action == GameAction.Use)
        {
            UseAhead();
            return;
        }
        if (_queue.Count >= QueueLimit) return;
        _queue.Enqueue(action);
        if (_motion is null) StartNext();
    }

    /// <summary>The player clicked on something in the view. Only things within reach respond.</summary>
    public bool ClickWorld(WorldTarget target)
    {
        switch (target)
        {
            case WallFaceTarget w when new Cell(w.X, w.Y) == PartyCell:
                if (Level.LeverAt(PartyCell, w.Face) is { } lever)
                {
                    Pull(lever);
                    return true;
                }
                return false;
            case DoorTarget d when new Cell(d.X, d.Y).Distance(PartyCell) <= 1:
                return TryWorkDoor(Level.DoorAt(d.X, d.Y)!);
            default:
                return false;
        }
    }

    /// <summary>The eye position for drawing, <paramref name="alpha"/> of the way into the next tick.</summary>
    public CameraPose Camera(double alpha = 0)
    {
        double cx = PartyCell.X + 0.5, cy = PartyCell.Y + 0.5, yaw = Facing.Yaw();
        if (_motion is not { } m) return new CameraPose(cx, cy, yaw);

        double t = Math.Clamp((m.Elapsed + alpha) / m.Duration, 0, 1);
        switch (m.Kind)
        {
            case MotionKind.Step:
            {
                double e = Smooth(t);
                double x = Lerp(m.From.X + 0.5, cx, e), y = Lerp(m.From.Y + 0.5, cy, e);
                double bob = HeadBob ? -Math.Sin(Math.PI * t) * BobHeight : 0;
                return new CameraPose(x, y, yaw, bob);
            }
            case MotionKind.Turn:
                return new CameraPose(cx, cy, Lerp(m.FromYaw, m.ToYaw, Smooth(t)));
            default:
            {
                // A short nudge toward the blocked side and back.
                double push = Math.Sin(Math.PI * t) * 0.06;
                return new CameraPose(cx + m.BumpDir.Dx() * push, cy + m.BumpDir.Dy() * push, yaw);
            }
        }
    }

    /// <summary>For tests and debugging: puts the party somewhere at once, stopping any motion.</summary>
    public void Stand(Cell cell, Dir facing)
    {
        PartyCell = cell;
        Facing = facing;
        _motion = null;
        _queue.Clear();
    }

    private void StartNext()
    {
        while (_queue.Count > 0)
        {
            var a = _queue.Dequeue();
            switch (a)
            {
                case GameAction.TurnLeft:
                case GameAction.TurnRight:
                {
                    bool right = a == GameAction.TurnRight;
                    var to = right ? Facing.Right() : Facing.Left();
                    double fromYaw = Facing.Yaw();
                    _motion = new Motion(MotionKind.Turn, TurnTicks)
                    {
                        FromYaw = fromYaw,
                        ToYaw = fromYaw + (right ? Math.PI / 2 : -Math.PI / 2),
                    };
                    Facing = to;
                    Event?.Invoke(GameEvent.Turned);
                    return;
                }
                default:
                {
                    var dir = a switch
                    {
                        GameAction.StepForward => Facing,
                        GameAction.StepBack => Facing.Opposite(),
                        GameAction.StrafeLeft => Facing.Left(),
                        _ => Facing.Right(),
                    };
                    var target = PartyCell.Step(dir);
                    if (Level.IsPassable(target))
                    {
                        _motion = new Motion(MotionKind.Step, StepTicks) { From = PartyCell };
                        PartyCell = target;
                        Event?.Invoke(GameEvent.Stepped);
                    }
                    else
                    {
                        _motion = new Motion(MotionKind.Bump, BumpTicks) { BumpDir = dir };
                        _queue.Clear();
                        Event?.Invoke(GameEvent.Bumped);
                    }
                    return;
                }
            }
        }
    }

    private void UseAhead()
    {
        if (Level.LeverAt(PartyCell, Facing) is { } lever)
        {
            Pull(lever);
            return;
        }
        if (Level.DoorAt(PartyCell.Step(Facing)) is { } door) TryWorkDoor(door);
    }

    private void Pull(Lever lever)
    {
        lever.Down = !lever.Down;
        foreach (var c in lever.Targets)
        {
            var d = Level.DoorAt(c)!;
            d.Opening = !d.Opening;
        }
        Say(lever.Down ? "The lever clanks down. Somewhere, chains rattle." : "The lever grinds back up.");
        Event?.Invoke(GameEvent.LeverPulled);
    }

    private bool TryWorkDoor(Door door)
    {
        if (door.Kind == DoorKind.Portcullis)
        {
            Say("The portcullis won't budge. There must be a mechanism.");
            return false;
        }
        if (door.Opening && door.Cell == PartyCell)
        {
            Say("You are standing in the doorway.");
            return false;
        }
        door.Opening = !door.Opening;
        Event?.Invoke(door.Opening ? GameEvent.DoorOpening : GameEvent.DoorClosing);
        return true;
    }

    private void MoveDoor(Door door)
    {
        const double perTick = TickSeconds / Door.TravelSeconds;
        if (door.Opening)
        {
            door.Open = Math.Min(1, door.Open + perTick);
        }
        else if (door.Open > 0)
        {
            // A door never closes on the party: it waits, raised, until the doorway is clear.
            if (door.Cell == PartyCell) return;
            door.Open = Math.Max(0, door.Open - perTick);
        }
    }

    private void Say(string text)
    {
        LastMessage = text;
        Message?.Invoke(text);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Ease in and out.</summary>
    private static double Smooth(double t) => t * t * (3 - 2 * t);

    private enum MotionKind { Step, Turn, Bump }

    private sealed class Motion(MotionKind kind, int duration)
    {
        public MotionKind Kind { get; } = kind;
        public int Duration { get; } = duration;
        public int Elapsed { get; set; }
        public Cell From { get; init; }
        public double FromYaw { get; init; }
        public double ToYaw { get; init; }
        public Dir BumpDir { get; init; }
    }
}

public enum GameEvent { Stepped, Turned, Bumped, DoorOpening, DoorClosing, LeverPulled }
