using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>How far a pack's alarm carries, in cells.</summary>
    public const int PackRange = 8;
    /// <summary>How close an ambusher lets the party come before it strikes.</summary>
    public const int AmbushRange = 2;
    /// <summary>How long a frightened creature runs before it turns at bay.</summary>
    public const double FleeSeconds = 8;
    /// <summary>A blow nearer than this alerts anything that hears it.</summary>
    public const int NoiseRange = 4;
    /// <summary>An aware creature that hasn't seen the party for this long gives up the hunt.</summary>
    public const double GiveUpSeconds = 15;
    /// <summary>The fight goes on while an aware creature has seen the party this recently.</summary>
    public const double FightMemory = 3;

    private readonly List<Monster> _monsters = new();
    private int _nextMonsterId = 1;

    public IReadOnlyList<Monster> Monsters => _monsters;

    private void SpawnMonsters()
    {
        foreach (var s in Level.Spawns) AddMonster(s.Creature, s.Cell, s.Facing);
    }

    /// <summary>Places a creature (for levels and tests). Its HP is rolled from its hit dice.</summary>
    public Monster AddMonster(string creature, Cell cell, Dir facing) => AddMonster(Content.Creature(creature), cell, facing);

    /// <summary>Places a creature from a definition (a scaled or champion copy, say).</summary>
    public Monster AddMonster(CreatureDef def, Cell cell, Dir facing)
    {
        var m = new Monster(_nextMonsterId++, def, cell, facing, Dice.Parse(def.HitDice).Roll(Rng.World), Content)
        {
            IdleLeft = 1 + Rng.Ai.Next(300) / 100.0,
        };
        m.Slot = FreeSlot(cell);
        _monsters.Add(m);
        UpdatePlates(); // a creature placed on a plate presses it at once
        return m;
    }

    /// <summary>The living creatures in a cell, nearest slot first.</summary>
    public IEnumerable<Monster> MonstersAt(Cell c) => _monsters.Where(m => m.IsAlive && m.Cell == c).OrderBy(m => m.Slot);

    private int FreeSlot(Cell c)
    {
        var used = _monsters.Where(m => m.IsAlive && m.Cell == c).Select(m => m.Slot).ToHashSet();
        for (int i = 0; i < 4; i++)
            if (!used.Contains(i)) return i;
        return 0;
    }

    /// <summary>Whether creature <paramref name="m"/> may walk into cell <paramref name="c"/> now.</summary>
    private bool CanEnter(Monster m, Cell c, bool forPath)
    {
        if (c == PartyCell || Level.ChestAt(c) is not null) return false;
        var kind = Level.KindAt(c);
        if (kind == CellKind.Wall) return false;
        if (kind == CellKind.Door)
        {
            var d = Level.DoorAt(c)!;
            bool canOpen = m.Def.Has("opener") && d.Kind == DoorKind.Hand;
            if (!d.IsPassable && !(forPath && canOpen)) return false;
        }
        // Creatures know their own cellars: only the mindless blunder into pits.
        if (Level.PitAt(c) is not null && !m.Def.Has("mindless")) return false;
        var others = _monsters.Where(o => o != m && o.IsAlive && o.Cell == c).ToList();
        if (others.Count == 0) return true;
        return m.IsSmall && others.All(o => o.IsSmall) && others.Count < 4;
    }

    private void TickMonsters()
    {
        bool anyAware = false;
        foreach (var m in _monsters.ToList())
        {
            if (m.HurtFlash > 0) m.HurtFlash = Math.Max(0, m.HurtFlash - TickSeconds);
            if (m.StrikeTime > 0) m.StrikeTime = Math.Max(0, m.StrikeTime - TickSeconds);
            if (m.State == MonsterState.Pile)
            {
                TickPile(m);
                continue;
            }
            if (!m.IsAlive) continue;
            if (m.IsAware && !m.Ally)
            {
                if (m.Cell.Distance(PartyCell) <= 1 || (m.Cell.Distance(PartyCell) <= 12 && HasLineOfSight(m.Cell, PartyCell))) m.LastSeen = Time;
                else if (Time - m.LastSeen > GiveUpSeconds && m.Boss is null)
                {
                    m.State = MonsterState.Idle; // lost the scent
                    m.Awareness = 0;
                }
            }
            TickMonster(m);
            if (m.IsAware && !m.Ally && m.Cell.Distance(PartyCell) <= 10 && Time - m.LastSeen < FightMemory) anyAware = true;
        }
        UpdateFight(anyAware);
    }

    private void TickPile(Monster m)
    {
        m.PileLeft -= TickSeconds;
        if (m.PileLeft > 0) return;
        if (m.Cell == PartyCell || _monsters.Any(o => o != m && o.IsAlive && o.Cell == m.Cell))
        {
            m.PileLeft = 0.5; // someone is standing on the bones: wait
            return;
        }
        m.Hp = m.MaxHp;
        m.State = MonsterState.Aware;
        m.Action = MonsterAction.Recovering;
        m.ActionLeft = m.ActionTotal = 1.0;
        Say($"The bones of the {m.Def.Name} clatter back together!");
    }

    private void TickMonster(Monster m)
    {
        if (m.Conditions.Active.Any())
        {
            var tick = m.Conditions.Tick(TickSeconds, Rng.Combat, (a, dc) => Checks.RollD20(Rng.Combat, Advantage.None) + m.SaveBonus(a) >= dc);
            if (tick.Damage > 0) DamageMonster(m, tick.Damage, DamageType.Poison);
            if (!m.IsAlive) return;
        }
        if (!m.Conditions.CanAct)
        {
            if (m.Action == MonsterAction.WindingUp) { m.Action = MonsterAction.None; m.Attack = null; }
            return;
        }
        if (m.Summoned)
        {
            m.SummonLeft -= TickSeconds;
            if (m.SummonLeft <= 0) { m.State = MonsterState.Dead; Say($"The {m.Def.Name} fades away."); return; }
        }
        MonsterTickHook?.Invoke(m);
        if (!m.IsAlive) return;
        if (m.Action != MonsterAction.None)
        {
            m.ActionLeft -= TickSeconds;
            if (m.ActionLeft > 1e-9) return;
            var done = m.Action;
            m.Action = MonsterAction.None;
            m.ActionLeft = m.ActionTotal = 0;
            if (done == MonsterAction.Stepping)
            {
                m.From = m.Cell;
                if (Level.PitAt(m.Cell) is { } pit)
                {
                    m.FellInPit = true;
                    m.State = MonsterState.Dead;
                    Level.Spot(pit);
                    Kills++;
                    Say($"The {m.Def.Name} blunders into the pit and is gone.");
                    Event?.Invoke(GameEvent.MonsterDied);
                    return;
                }
            }
            if (done == MonsterAction.WindingUp)
            {
                ReleaseAttack(m);
                return;
            }
        }

        if (m.Ally)
        {
            AllyFight(m);
            return;
        }
        if (!m.IsAware)
        {
            if (Notices(m)) Alert(m);
            else
            {
                Idle(m);
                return;
            }
        }

        if (m.State == MonsterState.Fleeing)
        {
            m.FleeLeft -= TickSeconds;
            if (m.FleeLeft <= 0) m.State = MonsterState.Aware;
            else if (StepAway(m)) return;
        }
        else if (ShouldFlee(m))
        {
            m.State = MonsterState.Fleeing;
            m.FleeLeft = FleeSeconds;
            Say($"The {m.Def.Name} turns tail!");
            if (StepAway(m)) return;
        }

        Fight(m);
    }

    /// <summary>What an unaware creature does: wanders now and then, or keeps its post.</summary>
    private void Idle(Monster m)
    {
        if (!m.Def.Has("wander")) return;
        m.IdleLeft -= TickSeconds;
        if (m.IdleLeft > 0) return;
        m.IdleLeft = 2 + Rng.Ai.Next(400) / 100.0;
        var dirs = Enum.GetValues<Dir>().Where(d => CanEnter(m, m.Cell.Step(d), false) && Level.PitAt(m.Cell.Step(d)) is null).ToList();
        if (dirs.Count > 0) StepTo(m, dirs[Rng.Ai.Next(dirs.Count)]);
    }

    /// <summary>Champions, bosses and others hook in here each tick.</summary>
    internal Action<Monster>? MonsterTickHook;

    /// <summary>An ally hunts the nearest foe: turns to it and strikes when adjacent, otherwise closes in.</summary>
    private void AllyFight(Monster m)
    {
        var foe = _monsters.Where(o => o.IsAlive && !o.Ally).OrderBy(o => o.Cell.Distance(m.Cell)).FirstOrDefault();
        if (foe is null || foe.Cell.Distance(m.Cell) > 10)
        {
            // Nothing to fight: stay close to the party.
            if (m.Cell.Distance(PartyCell) > 2 && PathStep(m, c => c.Distance(PartyCell) <= 1) is { } back) StepTo(m, back);
            return;
        }
        if (foe.Cell.Distance(m.Cell) == 1 && DirTo(m.Cell, foe.Cell) is { } face)
        {
            if (m.Facing != face) { Turn(m, face); return; }
            if (m.Def.Attacks.FirstOrDefault(a => a.Range == 1) is { } melee) { WindUp(m, melee); return; }
        }
        if (PathStep(m, c => c.Distance(foe.Cell) == 1) is { } dir) StepTo(m, dir);
    }

    /// <summary>Whether a creature sees or senses the party now.</summary>
    private bool Notices(Monster m)
    {
        if (Invisible > 0 && m.Cell.Distance(PartyCell) > 1) return false;
        if (NoticeHook is { } hook) return hook(m);
        if (Sneaking) return SneakNotice(m);
        double d = Math.Sqrt(Sq(m.Cell.X - PartyCell.X) + Sq(m.Cell.Y - PartyCell.Y));
        if (d <= 1.01) return true;
        double range = m.Def.Has("ambush") ? AmbushRange : Math.Max(2, m.Def.Perception - 5);
        return d <= range && HasLineOfSight(m.Cell, PartyCell);
    }

    /// <summary>Makes a creature aware of the party, and its pack with it.</summary>
    private void Alert(Monster m)
    {
        if (!m.IsAlive || m.IsAware) return;
        m.State = MonsterState.Aware;
        m.LastSeen = Time;
        if (!m.Def.Has("pack")) return;
        foreach (var o in _monsters)
            if (o != m && o.IsAlive && !o.IsAware && o.Def.Id == m.Def.Id && o.Cell.Distance(m.Cell) <= PackRange)
            {
                o.State = MonsterState.Aware;
                o.LastSeen = Time;
            }
    }

    /// <summary>Stealth replaces the plain noticing rules when it is on (spec 11.1).</summary>
    internal Func<Monster, bool>? NoticeHook;

    private bool ShouldFlee(Monster m)
    {
        if (!m.Def.Has("flee") || m.Def.FleeAtPercent <= 0) return false;
        double threshold = m.Def.FleeAtPercent + 5 * Hero.Mod(Rules.Attribute.Cha);
        return m.Hp * 100.0 / m.MaxHp < threshold && m.FleeLeft > -30; // a creature that ran once and came back fights on
    }

    /// <summary>The fight itself: turn to face, strike when adjacent, shoot when lined up, otherwise close in.</summary>
    private void Fight(Monster m)
    {
        var toParty = DirTo(m.Cell, PartyCell);
        int dist = m.Cell.Distance(PartyCell);

        if (dist == 1 && toParty is { } face)
        {
            var melee = m.Def.Attacks.FirstOrDefault(a => a.Range == 1);
            // A ranged creature backs off when it can, and only fights hand to hand when cornered.
            if (m.Def.Has("ranged") && StepAway(m)) return;
            if (m.Facing != face)
            {
                Turn(m, face);
                return;
            }
            if (melee is not null)
            {
                WindUp(m, melee);
                return;
            }
        }

        if (m.Def.Has("ranged") && toParty is { } line && dist > 1)
        {
            var shot = m.Def.Attacks.FirstOrDefault(a => a.Range >= dist);
            if (shot is not null && HasClearShot(m.Cell, PartyCell))
            {
                if (m.Facing != line)
                {
                    Turn(m, line);
                    return;
                }
                WindUp(m, shot);
                return;
            }
        }

        if (m.Def.Has("chase")) Approach(m);
    }

    private void Turn(Monster m, Dir to)
    {
        int quarter = (((int)to - (int)m.Facing) & 3) == 2 ? 2 : 1;
        m.Facing = to;
        m.Action = MonsterAction.Turning;
        m.ActionLeft = m.ActionTotal = m.Def.TurnSeconds * quarter;
    }

    private void WindUp(Monster m, CreatureAttackDef attack)
    {
        m.Attack = attack;
        m.Action = MonsterAction.WindingUp;
        m.ActionLeft = m.ActionTotal = attack.Windup;
    }

    private void StepTo(Monster m, Dir d)
    {
        var target = m.Cell.Step(d);
        if (Level.DoorAt(target) is { IsPassable: false } door)
        {
            // An opener lifts the latch and waits for the door to rise.
            door.Opening = true;
            m.Facing = d;
            m.Action = MonsterAction.Opening;
            m.ActionLeft = m.ActionTotal = 0.5;
            return;
        }
        m.Facing = d;
        m.From = m.Cell;
        m.Cell = target;
        m.Slot = FreeSlot(target);
        m.Action = MonsterAction.Stepping;
        m.ActionLeft = m.ActionTotal = m.Def.StepSeconds;
        UpdatePlates();
    }

    /// <summary>Takes one step along the shortest path to a cell from which it can strike (or shoot).</summary>
    private void Approach(Monster m)
    {
        bool ranged = m.Def.Has("ranged");
        int range = ranged ? m.Def.Attacks.Max(a => a.Range) : 1;
        var first = PathStep(m, c =>
        {
            int d = c.Distance(PartyCell);
            if (d == 1) return !ranged;
            return ranged && d <= range && DirTo(c, PartyCell) is not null && HasClearShot(c, PartyCell);
        });
        if (first is { } dir) StepTo(m, dir);
    }

    /// <summary>Steps to the free neighbour farthest from the party, if that is farther than here.</summary>
    private bool StepAway(Monster m)
    {
        int here = m.Cell.Distance(PartyCell);
        Dir? best = null;
        int bestDist = here;
        foreach (var d in Enum.GetValues<Dir>())
        {
            var c = m.Cell.Step(d);
            if (!CanEnter(m, c, false) || Level.PitAt(c) is not null) continue;
            int dist = c.Distance(PartyCell);
            if (dist > bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }
        if (best is not { } b) return false;
        // Backing away keeps its face to the party.
        var keep = m.Facing;
        StepTo(m, b);
        if (m.Action == MonsterAction.Stepping) m.Facing = keep;
        return true;
    }

    /// <summary>Breadth-first search to the nearest cell that satisfies <paramref name="goal"/>; returns the first step.</summary>
    private Dir? PathStep(Monster m, Func<Cell, bool> goal)
    {
        var start = m.Cell;
        if (goal(start)) return null;
        var firstStep = new Dictionary<Cell, Dir> { [start] = Dir.North };
        var queue = new Queue<Cell>();
        queue.Enqueue(start);
        int budget = Level.Width * Level.Height;
        while (queue.Count > 0 && budget-- > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (firstStep.ContainsKey(n)) continue;
                // The first step must be free now; further along, other creatures will have moved on.
                bool open = c == start ? CanEnter(m, n, true) : CanEnterIgnoringCreatures(m, n);
                if (!open) continue;
                var dir = c == start ? d : firstStep[c];
                firstStep[n] = dir;
                if (goal(n)) return dir;
                queue.Enqueue(n);
            }
        }
        return null;
    }

    /// <summary>Further along a path, other creatures will have moved on, so they don't block it.</summary>
    private bool CanEnterIgnoringCreatures(Monster m, Cell c)
    {
        if (c == PartyCell || Level.KindAt(c) == CellKind.Wall) return false;
        if (Level.DoorAt(c) is { } d && !d.IsPassable && !(m.Def.Has("opener") && d.Kind == DoorKind.Hand)) return false;
        return Level.PitAt(c) is null || m.Def.Has("mindless");
    }

    /// <summary>The direction from a to b if they share a row or column, else null.</summary>
    public static Dir? DirTo(Cell a, Cell b)
    {
        if (a == b) return null;
        if (a.X == b.X) return b.Y < a.Y ? Dir.North : Dir.South;
        if (a.Y == b.Y) return b.X < a.X ? Dir.West : Dir.East;
        return null;
    }

    /// <summary>Whether sight passes between two cells: walls and closed wooden doors block it; a portcullis doesn't.</summary>
    public bool HasLineOfSight(Cell a, Cell b) => Walk(a, b, (c) =>
        Level.KindAt(c) == CellKind.Wall || (Level.DoorAt(c) is { } d && d.Kind == DoorKind.Hand && d.Open < 0.5));

    /// <summary>Whether a thrown thing can fly straight from a to b: along a line, through nothing solid or shut.</summary>
    public bool HasClearShot(Cell a, Cell b) => DirTo(a, b) is not null && Walk(a, b, c =>
        Level.KindAt(c) == CellKind.Wall || (Level.DoorAt(c) is { } d && !d.IsPassable) ||
        _monsters.Any(m => m.IsAlive && m.Cell == c));

    /// <summary>Walks the cells between a and b (not a or b) and says whether none of them blocks.</summary>
    private static bool Walk(Cell a, Cell b, Func<Cell, bool> blocks)
    {
        double x0 = a.X + 0.5, y0 = a.Y + 0.5, x1 = b.X + 0.5, y1 = b.Y + 0.5;
        int steps = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 4);
        var last = a;
        for (int i = 1; i < steps; i++)
        {
            double t = (double)i / steps;
            var c = new Cell((int)Math.Floor(x0 + (x1 - x0) * t), (int)Math.Floor(y0 + (y1 - y0) * t));
            if (c == last || c == b) continue;
            last = c;
            if (blocks(c)) return false;
        }
        return true;
    }

    private static double Sq(double v) => v * v;
}
