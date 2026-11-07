using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public enum GameMode { Playing, Dead, Won }

/// <summary>
/// The game's clock and rules, with no drawing in it. <see cref="Update"/> runs a fixed 60 Hz tick; the view asks
/// for <see cref="Camera"/> with the fraction of the next tick that has passed, so motion is smooth at any frame
/// rate. Commands come in through <see cref="Press"/>, <see cref="HandDown"/>/<see cref="HandUp"/> and
/// <see cref="ClickWorld"/>. The parts: movement here; the hero's blows and the monsters' in
/// <c>GameEngine.Combat.cs</c>; monster behaviour in <c>GameEngine.Monsters.cs</c>; pits, plates and notes in
/// <c>GameEngine.Traps.cs</c>.
/// </summary>
public sealed partial class GameEngine
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
    private readonly List<string> _log = new();
    private double _accumulator;
    private Motion? _motion;

    public Level Level { get; }
    public ContentDb Content { get; }
    public Party Party { get; }
    /// <summary>The first hero: the one the single-hero parts of the game (and older tests) talk to.</summary>
    public Character Hero => Party.Leader;
    public LootGenerator Loot { get; }
    public MagicItems Magic { get; init; } = MagicItems.Normal;
    public RngStreams Rng { get; }
    public Cell PartyCell { get; private set; }
    public Dir Facing { get; private set; }
    public long TickCount { get; private set; }
    public double Time => TickCount * TickSeconds;
    public bool HeadBob { get; set; } = true;
    public GameMode Mode { get; private set; } = GameMode.Playing;
    public bool Paused { get; private set; }

    /// <summary>How bright the party's light is, for the view: a lit torch is full; magic and items change it.</summary>
    public double LightStrength => (LightLevel?.Invoke() ?? 1.05) * (Sneaking ? 0.75 : 1);
    internal Func<double>? LightLevel;

    /// <summary>True while a step, turn or bump is playing.</summary>
    public bool IsMoving => _motion is not null;
    public int Queued => _queue.Count;

    /// <summary>The latest message for the player ("The lever clanks down.").</summary>
    public string? LastMessage { get; private set; }
    /// <summary>The latest messages, oldest first.</summary>
    public IReadOnlyList<string> Log => _log;
    public event Action<string>? Message;
    public event Action<GameEvent>? Event;
    public event Action<FloatText>? Float;

    /// <summary>
    /// A new game on <paramref name="level"/>. With no hero given, the quick-start fighter plays alone (as a Lone
    /// Delver) with a longsword, a round shield and chain mail.
    /// </summary>
    public GameEngine(Level level, ulong seed = 1, Character? hero = null, ContentDb? content = null)
        : this(level, seed, new Party(hero ?? DefaultHero(content ?? ContentDb.LoadBuiltIn())), content) { }

    /// <param name="spawn">False when restoring a save: the save brings its own creatures and loot.</param>
    public GameEngine(Level level, ulong seed, Party party, ContentDb? content = null, bool spawn = true)
    {
        Level = level;
        Content = content ?? ContentDb.LoadBuiltIn();
        Rng = new RngStreams(seed);
        Loot = new LootGenerator(Content);
        Party = party;
        PartyCell = level.Start;
        Facing = level.StartFacing;
        InitMagic();
        if (spawn)
        {
            SpawnMonsters();
            FillChests();
        }
        EnterCell(PartyCell);
        LightNextTorch();
    }

    public static Character DefaultHero(ContentDb content)
    {
        var h = CharacterBuilder.QuickStartParty(content)[0];
        h.Equip(content.Weapon("longsword"), content.Shield("roundShield"), content.ArmourPiece("chainMail"));
        h.AddToBackpack(new Items.Item { BaseId = "torch", Count = 2 });
        return h;
    }

    /// <summary>Runs the clock forward by <paramref name="seconds"/> of real time, in whole ticks.</summary>
    public void Update(double seconds)
    {
        if (Paused || Mode != GameMode.Playing) return;
        _accumulator += Math.Clamp(seconds, 0, 0.25);
        while (_accumulator >= TickSeconds && Mode == GameMode.Playing)
        {
            _accumulator -= TickSeconds;
            Tick();
        }
    }

    /// <summary>How far through the next tick the clock is, 0 to 1, for smooth drawing.</summary>
    public double Alpha => Paused || Mode != GameMode.Playing ? 0 : _accumulator / TickSeconds;

    /// <summary>One 1/60 s step of the simulation.</summary>
    public void Tick()
    {
        if (Mode != GameMode.Playing) return;
        TickCount++;
        foreach (var door in Level.Doors) MoveDoor(door);

        if (_motion is { } m)
        {
            m.Elapsed++;
            if (m.Elapsed >= m.Duration)
            {
                _motion = null;
                if (m.Kind == MotionKind.Step) ArriveInCell();
            }
        }
        if (_motion is null && Mode == GameMode.Playing) StartNext();

        TickHands();
        TickMonsters();
        TickProjectiles();
        UpdatePlates();
        TickParty();
        TickCasting();
        TickLight();
    }

    /// <summary>Freezes or resumes the game (Space). Nothing moves while paused.</summary>
    public void TogglePause()
    {
        if (Mode == GameMode.Playing) Paused = !Paused;
    }

    /// <summary>Asks for an action. Movement waits its turn in a short queue; extra presses are dropped.</summary>
    public void Press(GameAction action)
    {
        if (Mode != GameMode.Playing || Paused) return;
        if (action == GameAction.Use)
        {
            UseAhead();
            return;
        }
        if (action == GameAction.Sneak)
        {
            ToggleSneak();
            return;
        }
        if (action == GameAction.Rest)
        {
            RestHook?.Invoke();
            return;
        }
        if (action == GameAction.PickUp)
        {
            if (PickUpAll() == 0) Say("There is nothing here to pick up.");
            return;
        }
        if (_queue.Count >= QueueLimit) return;
        _queue.Enqueue(action);
        if (_motion is null) StartNext();
    }

    /// <summary>The player clicked on something in the view. Only things within reach respond.</summary>
    public bool ClickWorld(WorldTarget target)
    {
        if (Mode != GameMode.Playing || Paused) return false;
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
        Explore(cell);
    }

    /// <summary>Whether the party may step into a cell: open floor or an open door, with no creature in it.</summary>
    public bool CanPartyEnter(Cell c) => Level.IsPassable(c) && Level.ChestAt(c) is null && !Monsters.Any(m => m.IsAlive && m.Cell == c);

    private void StartNext()
    {
        if (!Party.Standing.Any(c => c.Conditions.CanAct))
        {
            _queue.Clear();
            return;
        }
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
                    if (Level.PitAt(target) is { Spotted: true } && Party.Standing.All(c => c.HasPerkFlag("vault")) && CanPartyEnter(target.Step(dir)))
                    {
                        // Over the pit in one bound.
                        _motion = new Motion(MotionKind.Step, StepTicks * 2) { From = PartyCell };
                        PartyCell = target.Step(dir);
                        EnterCell(PartyCell);
                        Say("The party vaults the pit.");
                        Event?.Invoke(GameEvent.Stepped);
                        return;
                    }
                    double load = Party.Members.Max(c => c.Encumbrance);
                    if (load > 1.5 && CanPartyEnter(target))
                    {
                        Say("Someone is carrying far too much to move. Drop something.");
                        _queue.Clear();
                        return;
                    }
                    if (!CanPartyEnter(target) && Level.IsPassable(target) && Level.ChestAt(target) is null
                        && _monsters.Where(m => m.IsAlive && m.Cell == target).ToList() is { Count: > 0 } there && there.All(m => m.Ally))
                    {
                        // Allies step aside: they take the party's place.
                        foreach (var ally in there) { ally.From = target; ally.Cell = PartyCell; }
                    }
                    if (CanPartyEnter(target))
                    {
                        int ticks = load > 1 ? StepTicks * 3 / 2 : StepTicks;
                        if (Sneaking && !Party.Standing.All(c => c.HasPerkFlag("shadowDancer"))) ticks = ticks * 8 / 5;
                        if (load > 1)
                            foreach (var c in Party.Members.Where(c => c.Encumbrance > 1)) c.UseSkill(SkillId.Athletics, 0, XpSource.Plain);
                        _motion = new Motion(MotionKind.Step, ticks) { From = PartyCell };
                        PartyCell = target;
                        EnterCell(target);
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
        if (Level.DoorAt(PartyCell.Step(Facing)) is { } door) { TryWorkDoor(door); return; }
        if (Level.ChestAt(PartyCell.Step(Facing)) is { } chest) OpenChest(chest);
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
            // A door never closes on anyone: it waits, raised, until the doorway is clear.
            if (door.Cell == PartyCell || Monsters.Any(m => m.IsAlive && m.Cell == door.Cell)) return;
            door.Open = Math.Max(0, door.Open - perTick);
        }
    }

    public void Say(string text)
    {
        LastMessage = text;
        _log.Add(text);
        if (_log.Count > 50) _log.RemoveAt(0);
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

public enum GameEvent { Stepped, Turned, Bumped, DoorOpening, DoorClosing, LeverPulled, Hit, Missed, Blocked, HeroHurt, MonsterDied, Fell, Spotted }
