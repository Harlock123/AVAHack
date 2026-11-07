using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>
/// A simple player for balance runs. It fights whatever is next to it (turning to face it, blocking when a tell is
/// about to land, swinging when the sword is ready), rests when hurt and nothing is about, and otherwise walks to
/// the next mechanism (levers, then plates) and finally the stairs, avoiding the pits it knows of.
/// </summary>
internal sealed class Bot(GameEngine g, int seed = 1)
{
    // A person reacts late and misjudges tells: the bot does too.
    private readonly Random _r = new(seed);
    private double _blockAt = -1;
    private double _swingDelay;
    private readonly HashSet<Lever> _pulled = new();
    private readonly HashSet<Plate> _pressed = new();
    private bool _resting;

    public string? Goal { get; private set; }

    public void Think()
    {
        if (g.Mode != GameMode.Playing) return;
        foreach (var p in g.Level.Plates.Where(p => p.Pressed)) _pressed.Add(p);

        // Fight anything adjacent.
        var foe = Enum.GetValues<Dir>().Select(d => (Dir: d, M: g.MonstersAt(g.PartyCell.Step(d)).FirstOrDefault()))
                      .Where(x => x.M is not null).OrderBy(x => x.Dir == g.Facing ? 0 : 1).FirstOrDefault();
        if (foe.M is { } m)
        {
            _resting = false;
            if (foe.Dir != g.Facing)
            {
                if (g.LeftHand.Held) g.HandUp(HandSide.Left);
                if (!g.IsMoving && g.Queued == 0) g.Press(TurnToward(g.Facing, foe.Dir));
                return;
            }
            if (m.Action == MonsterAction.WindingUp)
            {
                // Half the time it sees the tell; when it does, it raises the shield somewhere in the last half second.
                if (_blockAt < 0) _blockAt = _r.NextDouble() < 0.5 ? 0.05 + _r.NextDouble() * 0.45 : 0;
                if (_blockAt > 0 && m.ActionLeft <= _blockAt && !g.LeftHand.Held && g.Hero.Stamina > 10) g.HandDown(HandSide.Left);
            }
            else
            {
                _blockAt = -1;
                if (g.LeftHand.Held && g.LeftHand.HeldFor > GameEngine.BashTap) g.HandUp(HandSide.Left);
            }
            if (g.RightHand.Ready && !g.LeftHand.Held)
            {
                if (_swingDelay <= 0) _swingDelay = 0.15 + _r.NextDouble() * 0.35;
                _swingDelay -= GameEngine.TickSeconds;
                if (_swingDelay <= 0)
                {
                    g.HandDown(HandSide.Right);
                    g.HandUp(HandSide.Right);
                }
            }
            return;
        }
        if (g.LeftHand.Held) g.HandUp(HandSide.Left);

        // Rest when hurt and nothing is coming.
        bool danger = g.Monsters.Any(x => x.IsAware && x.Cell.Distance(g.PartyCell) <= 6);
        if (!danger && (g.Hero.Hp < g.Hero.MaxHp * 0.5 || (_resting && g.Hero.Hp < g.Hero.MaxHp * 0.9)))
        {
            _resting = true;
            return;
        }
        _resting = false;

        if (g.IsMoving || g.Queued > 0) return;
        var (target, face, use) = NextObjective();
        if (target is not { } t) return;
        if (g.PartyCell == t)
        {
            if (face is { } f && f != g.Facing) { g.Press(TurnToward(g.Facing, f)); return; }
            if (use is { } lever) { g.Press(GameAction.Use); _pulled.Add(lever); }
            return;
        }
        var step = PathStep(t);
        if (step is not { } dir) return;
        var next = g.PartyCell.Step(dir);
        if (dir != g.Facing) { g.Press(TurnToward(g.Facing, dir)); return; }
        if (g.Level.DoorAt(next) is { Kind: DoorKind.Hand, IsPassable: false } door)
        {
            if (!door.Opening) g.Press(GameAction.Use);
            return;
        }
        g.Press(GameAction.StepForward);
    }

    private (Cell? Cell, Dir? Face, Lever? Use) NextObjective()
    {
        foreach (var lever in g.Level.Levers)
            if (!_pulled.Contains(lever) && Reachable(lever.Cell)) { Goal = "lever"; return (lever.Cell, lever.Face, lever); }
        foreach (var plate in g.Level.Plates)
            if (!_pressed.Contains(plate) && Reachable(plate.Cell)) { Goal = "plate"; return (plate.Cell, null, null); }
        Goal = "stairs";
        return (g.Level.Stairs.First(), null, null);
    }

    private bool Reachable(Cell c) => c == g.PartyCell || PathStep(c) is not null;

    private bool Walkable(Cell c)
    {
        var kind = g.Level.KindAt(c);
        if (kind == CellKind.Wall) return false;
        if (g.Level.PitAt(c) is { Spotted: true }) return false;
        if (g.Level.DoorAt(c) is { } d && !d.IsPassable && !(d.Kind == DoorKind.Hand) && !d.Opening) return false;
        return true; // creatures in the way get fought when the bot bumps into them
    }

    private Dir? PathStep(Cell goal)
    {
        var first = new Dictionary<Cell, Dir> { [g.PartyCell] = Dir.North };
        var q = new Queue<Cell>();
        q.Enqueue(g.PartyCell);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (first.ContainsKey(n) || !Walkable(n)) continue;
                first[n] = c == g.PartyCell ? d : first[c];
                if (n == goal) return first[n];
                q.Enqueue(n);
            }
        }
        return null;
    }

    private static GameAction TurnToward(Dir from, Dir to) =>
        from.Right() == to || from.Opposite() == to ? GameAction.TurnRight : GameAction.TurnLeft;
}
