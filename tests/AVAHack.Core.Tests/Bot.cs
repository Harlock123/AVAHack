using AVAHack.Characters;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>
/// A simple player for balance runs. It fights whatever is next to it (turning to face it, blocking when a tell is
/// about to land, swinging when the sword is ready), rests when hurt and nothing is about, and otherwise walks to
/// the next mechanism (levers, then plates) and finally the stairs, avoiding the pits it knows of. With
/// <see cref="Clears"/> it plays each level out first, as a person would (spec section 31).
/// </summary>
internal sealed class Bot(GameEngine g, int seed = 1)
{
    // A person reacts late and misjudges tells: the bot does too.
    private readonly AVAHack.Rules.Pcg32 _r = new((ulong)seed, 99);
    private double _blockAt = -1;
    private double _swingDelay;
    private readonly HashSet<Lever> _pulled = new();
    private readonly HashSet<Plate> _pressed = new();
    private bool _resting;
    private double _restWorst, _restSince;

    public string? Goal { get; private set; }

    /// <summary>
    /// Whether the bot clears each level as a player would before going down: it hunts what lives there, opens the
    /// chests, picks up what lies about, spends its level-ups and uses the Waystone. Off, it heads for the stairs.
    /// </summary>
    public bool Clears { get; init; }

    private readonly HashSet<Cell> _looted = new();
    // A target the bot has chased for two minutes without getting there is one it gives up on, as a player would.
    private readonly HashSet<object> _gaveUp = new();
    private object? _chasing;
    private double _chasingSince;
    private int _killsThen;
    private bool _usedStone;

    /// <summary>The share of tells the bot sees and blocks.</summary>
    public double BlockChance { get; init; } = 0.5;

    private double Next() => _r.Next(1_000_000) / 1_000_000.0;

    /// <summary>Takes on another bot's memory and dice, so a loaded game's bot carries on as the original would.</summary>
    public void CopyMemory(Bot other)
    {
        _pulled.Clear();
        foreach (var l in other._pulled)
            if (g.Level.LeverAt(l.Cell, l.Face) is { } mine) _pulled.Add(mine);
        _pressed.Clear();
        foreach (var p in other._pressed)
            if (g.Level.PlateAt(p.Cell) is { } mine) _pressed.Add(mine);
        _resting = other._resting;
        _level = g.Level; // this level's memory is the other bot's, not a fresh start
        _blockAt = other._blockAt;
        _swingDelay = other._swingDelay;
        var (st, inc) = other._r.GetState();
        _r.SetState(st, inc);
    }

    private Level? _level;

    public void Think()
    {
        if (g.Mode != GameMode.Playing) return;
        if (_level != g.Level)
        {
            // A new level: what the bot knew of the last one is no use here.
            _level = g.Level;
            _pulled.Clear();
            _pressed.Clear();
            _looted.Clear();
            _gaveUp.Clear();
            _pulledAt.Clear();
            _searched.Clear();
            _usedStone = false;
        }
        if (Clears)
            foreach (var c in g.Party.Members.Where(c => !c.IsDead)) ProgressionReport.Spend(c);
        CastSpells();
        foreach (var p in g.Level.Plates.Where(p => p.Pressed)) _pressed.Add(p);

        // Fight anything adjacent.
        var foe = Enum.GetValues<Dir>().Select(d => (Dir: d, M: g.MonstersAt(g.PartyCell.Step(d)).FirstOrDefault(m => !m.Ally)))
                      .Where(x => x.M is not null).OrderBy(x => x.Dir == g.Facing ? 0 : 1).FirstOrDefault();
        // The Engine can't be hurt while its valves are open: leave it and shut them first.
        if (foe.M is { Boss: "bellwright" } && Clears && OpenValve() is not null) foe = default;
        if (foe.M is { Boss: "archivist" } a2 && Clears && g.LitCandles.Any(c => c.Distance(a2.Cell) <= 8)) foe = default; // snuff first
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
                if (_blockAt < 0) _blockAt = Next() < BlockChance ? 0.05 + Next() * 0.45 : 0;
                if (_blockAt > 0 && m.ActionLeft <= _blockAt && !g.LeftHand.Held && g.Hero.Stamina > 10) g.HandDown(HandSide.Left);
            }
            else
            {
                _blockAt = -1;
                if (g.LeftHand.Held && g.LeftHand.HeldFor > GameEngine.BashTap) g.HandUp(HandSide.Left);
            }
            if (g.RightHand.Held)
            {
                // Charging a power attack: let go once it's charged.
                if (g.RightHand.HeldFor >= GameEngine.PowerHold + 0.02) g.HandUp(HandSide.Right);
            }
            else if (g.RightHand.Ready && !g.LeftHand.Held)
            {
                if (_swingDelay <= 0) _swingDelay = 0.15 + Next() * 0.35;
                _swingDelay -= GameEngine.TickSeconds;
                if (_swingDelay <= 0)
                {
                    g.HandDown(HandSide.Right);
                    // A third of the time, when there's breath for it, a power attack.
                    if (!(g.Hero.Stamina > 40 && Next() < 0.33)) g.HandUp(HandSide.Right);
                }
            }
            // The rest of the party joins in: the front row with whatever it holds, the back row only with reach or a bow.
            foreach (var c in g.Party.Standing.Where(c => c != g.Hero))
            {
                var w = c.MainHand;
                bool canReach = g.Party.IsFront(c) || w?.Kind is "reach" or "bow";
                if (canReach && c.RightHand.Ready && Next() < 0.05) { g.HandDown(c, HandSide.Right); g.HandUp(c, HandSide.Right); }
                if (g.Party.IsFront(c) && c.OffWeapon is not null && c.LeftHand.Ready && Next() < 0.05) { g.HandDown(c, HandSide.Left); g.HandUp(c, HandSide.Left); }
            }
            return;
        }
        if (g.LeftHand.Held) g.HandUp(HandSide.Left);

        // Eat and drink when hungry or thirsty (spec 13.6), as a player would.
        foreach (var c in g.Party.Standing)
        {
            if (c.Food < 35 && Array.FindIndex(c.Backpack, i => i is not null && (i.BaseId == "ration" || i.BaseId.StartsWith("meal:", StringComparison.Ordinal))) is >= 0 and var f) g.UseItem(c, f);
            if (c.Water < 35 && Array.FindIndex(c.Backpack, i => i?.BaseId == "waterskin") is >= 0 and var w) g.UseItem(c, w);
        }

        // Rest when hurt and nothing is coming.
        bool danger = g.Monsters.Any(x => x.IsAware && !x.Ally && x.Cell.Distance(g.PartyCell) <= 6);
        double worst = g.Party.Standing.Min(c => c.Hp / c.MaxHp);
        // Before a boss, a player heals up all the way first.
        bool bossAhead = g.LivingBoss is { IsAware: false } && Goal is "stairs" or "boss";
        bool onBlades = g.Level.Traps.Any(tr => tr.Cell == g.PartyCell && tr.Armed && tr.Cyclic);
        if (!danger && !onBlades && (worst < 0.5 || (_resting && worst < 0.9) || (bossAhead && worst < 0.97)))
        {
            // A hurt that won't mend (a disease, hunger) is no reason to wait forever.
            if (!_resting || worst > _restWorst + 0.01) { _restWorst = worst; _restSince = g.Time; }
            _resting = true;
            if (g.Time - _restSince < 60) return;
        }
        else _resting = false;

        if (g.IsMoving || g.Queued > 0) return;
        var (target, face, use) = NextObjective();
        if (target is not { } t) return;
        if (Clears && Goal != "stairs")
        {
            // Two minutes after the last thing done (a kill, a chest, a pickup, a lever), give up on the current target.
            object key = Goal == "hunt" && _prey is not null ? _prey : (Goal!, t);
            int progress = g.Kills + _searched.Count + _looted.Count + _pulled.Count + _pressed.Count + g.Level.Chests.Count(c => c.Opened);
            if (!Equals(_chasing, Goal) || progress != _killsThen) { _chasing = Goal; _chasingSince = g.Time; _killsThen = progress; }
            else if (g.Time - _chasingSince > 120) { _gaveUp.Add(key); _chasingSince = g.Time; return; }
        }
        if (g.PartyCell == t)
        {
            if (face is { } f && f != g.Facing) { g.Press(TurnToward(g.Facing, f)); return; }
            if (use is { } lever) { g.Press(GameAction.Use); _pulled.Add(lever); _pulledAt[lever] = g.Time; }
            if (Goal == "key") MakeRoom();
            if (Goal is "key" or "loot") { g.Press(GameAction.PickUp); _looted.Add(t); }
            if (Goal is "chest" or "snuff") g.Press(GameAction.Use);
            if (Goal == "waystone") UseStone();
            if (Goal == "search" && face is { } wf)
            {
                g.Press(GameAction.Use);
                _searched[(t, wf)] = _searched.GetValueOrDefault((t, wf)) + 1;
            }
            return;
        }
        // Round creatures where there's a way round (Clears); through them, fighting, where there isn't.
        var step = (Clears ? PathStep(t, roundFoes: true) : null) ?? PathStep(t) ?? PathStep(t, braveTraps: true);
        if (step is not { } dir) return;
        var next = g.PartyCell.Step(dir);
        if (dir != g.Facing) { g.Press(TurnToward(g.Facing, dir)); return; }
        // A spotted trap in the only way through: try to disarm it a few times, then go through anyway.
        if (g.Level.Traps.Any(tr => tr.Cell == next && tr.Spotted && tr.Armed && !tr.Cyclic && tr.Kind is not (TrapKind.AntiMagic or TrapKind.FloodingRoom))
            && _disarms < 3)
        {
            _disarms++;
            g.Press(GameAction.Use);
            return;
        }
        _disarms = 0;
        // Spikes and blades on a cycle: a player waits for them to pass, then steps through.
        if (g.Level.Traps.FirstOrDefault(tr => tr.Cell == next && tr.Armed && tr.Cyclic) is { } cyc)
        {
            double cycle = cyc.Kind == TrapKind.SpikeFloor ? GameEngine.SpikeCycle : GameEngine.BladeCycle;
            if (cyc.Timer % cycle > (cyc.Kind == TrapKind.SpikeFloor ? 0.5 : 0.6)) return;
        }
        // A root tangle: hack through it. A whole pod ahead bursts as the party comes up to it; wait for the cloud.
        if (g.RootsStand(next)) { g.Press(GameAction.Use); return; }
        if (g.PodWhole(next)) return;
        if (g.Level.DoorAt(next) is { Kind: DoorKind.Hand, IsPassable: false } door)
        {
            // Open it with the key or pick it (Use does both); keep trying.
            if (!door.Opening || door.Locked) g.Press(GameAction.Use);
            return;
        }
        g.Press(GameAction.StepForward);
    }

    private static readonly string[] Heals = ["rally", "mend", "mendMinor"];
    private static readonly string[] Bolts = ["chainStorm", "frostlance", "firedart", "spark", "frostTouch", "fireball", "thunderclap"];
    private static readonly string[] Summons = ["flameSpirit", "raiseBones", "summonFamiliar"];

    /// <summary>Casters heal the hurt, throw spells at what's ahead, and call allies, as a player would.</summary>
    private void CastSpells()
    {
        bool fight = g.Monsters.Any(m => m.IsAware && !m.Ally && m.IsAlive && m.Cell.Distance(g.PartyCell) <= 6);
        foreach (var c in g.Party.Standing.ToList())
        {
            if (c.CastingSpell is not null || !c.RightHand.Ready || c.RightHand.Held) continue;
            // Casters reach for their spells first; others now and then.
            int magic = new[] { SkillId.Destruction, SkillId.Restoration, SkillId.Warding, SkillId.Conjuration, SkillId.Illusion }.Max(c.Rank);
            int steel = AVAHack.Characters.Progression.WeaponSkills.Max(c.Rank);
            if (Next() > (magic >= steel ? 0.6 : 0.05)) continue;
            if (g.Party.Standing.Any(h => h.Hp < h.MaxHp * 0.7))
                foreach (var h in Heals) if (g.WhyNotCast(c, h) is null) { g.Cast(c, h); goto next; }
            if (!fight) continue;
            if (Summons.FirstOrDefault(s => g.WhyNotCast(c, s) is null) is { } sum && !g.Monsters.Any(m => m.Ally && m.IsAlive)) { g.Cast(c, sum); continue; }
            bool lined = Enumerable.Range(1, 6).Select(i => g.PartyCell.Step(g.Facing) is var first ? Walk(i) : default)
                .Any(cell => g.MonstersAt(cell).Any(m => !m.Ally));
            if (lined && Bolts.FirstOrDefault(s => g.WhyNotCast(c, s) is null) is { } bolt) { g.Cast(c, bolt); continue; }
            if (g.WhyNotCast(c, "ward") is null && g.Hero.Ward <= 0 && Next() < 0.3) { g.Selected = g.Hero; g.Cast(c, "ward"); }
            next: ;
        }
    }

    private AVAHack.World.Cell Walk(int steps)
    {
        var c = g.PartyCell;
        for (int i = 0; i < steps; i++) c = c.Step(g.Facing);
        return c;
    }

    private bool HasKey(string? key) => key is not null && g.Party.Members.Any(h => h.Backpack.Any(i => i?.KeyId == key));

    private (Cell? Cell, Dir? Face, Lever? Use) NextObjective()
    {
        foreach (var (cell, items) in g.Level.Piles)
            if (items.Any(i => i.KeyId is not null) && !Gave("key", cell) && Reachable(cell)) { Goal = "key"; return (cell, null, null); }
        // A woken Engine: its valves come before anything else; a woken Archivist: his candles.
        if (Clears && g.LivingBoss is { IsAware: true } && OpenValve() is { } valve) { Goal = "valve"; return (valve.Cell, valve.Face, valve); }
        if (Clears && g.LivingBoss is { Boss: "archivist", IsAware: true } archivist)
            foreach (var candle in g.LitCandles.Where(c => c.Distance(archivist.Cell) <= 8).OrderBy(c => c.Distance(g.PartyCell)).ToList())
                if (Beside(candle) is { } at && !Gave("snuff", at.Cell)) { Goal = "snuff"; return (at.Cell, at.Face, null); }
        foreach (var lever in g.Level.Levers)
            if (WantsPull(lever) && !Gave("lever", lever.Cell) && Reachable(lever.Cell)) { Goal = "lever"; return (lever.Cell, lever.Face, lever); }
        foreach (var plate in g.Level.Plates)
            if (WantsPress(plate) && !Gave("plate", plate.Cell) && Reachable(plate.Cell)) { Goal = "plate"; return (plate.Cell, null, null); }
        if (Clears)
        {
            if (!_usedStone && g.Level.Waystone is { } w && g.LivingBoss is null)
            {
                Goal = "waystone";
                if (g.AtWaystone) return (g.PartyCell, null, null);
                if (Beside(w) is { } at) return (at.Cell, at.Face, null);
            }
            // The nearest living creature (the boss last: it seals the stairs and wakes when the party nears).
            // Stick with the creature it was after until it falls or the bot gives up on it.
            if (_prey is { IsAlive: true } p && !_gaveUp.Contains(p) && Reachable(p.Cell)) { Goal = "hunt"; return (p.Cell, null, null); }
            var foe = g.Monsters.Where(m => m.IsAlive && !m.Ally && m.Boss is null && !_gaveUp.Contains(m) && Reachable(m.Cell))
                       .MinBy(m => m.Cell.Distance(g.PartyCell));
            if (foe is not null) { Goal = "hunt"; _prey = foe; return (foe.Cell, null, null); }
            foreach (var chest in g.Level.Chests.Where(c => !c.Opened).OrderBy(c => c.Cell.Distance(g.PartyCell)))
                if (Beside(chest.Cell) is { } at && !Gave("chest", at.Cell)) { Goal = "chest"; return (at.Cell, at.Face, null); }
            foreach (var (cell, _) in g.Level.Piles.OrderBy(p => p.Key.Distance(g.PartyCell)))
                if (!_looted.Contains(cell) && !Gave("loot", cell) && Reachable(cell)) { Goal = "loot"; return (cell, null, null); }
            // The Engine's valves must be shut (down) before it can be hurt.
            if (OpenValve() is { } v) { Goal = "valve"; return (v.Cell, v.Face, v); }
            // Last, the boss: it seals the stairs until it falls.
            if (g.LivingBoss is { } boss && Reachable(boss.Cell)) { Goal = "boss"; return (boss.Cell, null, null); }
        }
        // No way to the stairs: search the walls for a hidden door, nearest first, as a stuck player would.
        if (Clears && g.Level.Stairs.Any() && !g.Level.Stairs.Any(Reachable) && WallToSearch() is { } w2)
        {
            Goal = "search";
            return (w2.Cell, w2.Face, null);
        }
        Goal = "stairs";
        return g.Level.Stairs.Any() ? (g.Level.Stairs.First(), null, null) : (null, null, null);
    }

    /// <summary>A reachable cell beside a thing the party can't stand on, and the way to face it.</summary>
    private (Cell Cell, Dir Face)? Beside(Cell thing)
    {
        foreach (var d in Enum.GetValues<Dir>())
        {
            var at = thing.Step(d.Opposite());
            if (Walkable(at) && Reachable(at)) return (at, d);
        }
        return null;
    }

    private static readonly string[] Keep = ["ration", "waterskin", "mortar", "campfireKit", "torch", "scrapIron", "spiritShard"];

    /// <summary>With every pack full, drops the least valuable thing that isn't a key, food, water or a tool.</summary>
    private void MakeRoom()
    {
        if (g.Party.Members.Any(c => !c.IsDead && c.Backpack.Any(i => i is null))) return;
        var worst = g.Party.Members.Where(c => !c.IsDead)
            .SelectMany(c => c.Backpack.Select((it, i) => (c, it, i)))
            .Where(x => x.it is { KeyId: null } it && !Keep.Contains(it.BaseId) && !it.BaseId.StartsWith("meal:", StringComparison.Ordinal))
            .MinBy(x => GameEngine.Worth(g.Content, x.it!));
        if (worst.c is not null) g.DropItem(worst.c, worst.i);
    }

    /// <summary>At the Waystone: raise the fallen, rest, and spend the purse on lessons in each hero's best skills.</summary>
    private void UseStone()
    {
        _usedStone = true;
        foreach (var c in g.Party.Members.Where(c => c.IsDead)) g.ReviveAt(c);
        g.RestAtStone();
        bool bought = true;
        while (bought)
        {
            bought = false;
            foreach (var c in g.Party.Members.Where(c => !c.IsDead))
            {
                var best = Enum.GetValues<SkillId>().OrderByDescending(c.Rank).Take(3)
                    .Where(s => Progression.TrainingCost(c.Rank(s)) <= g.Party.Gold / 2).ToList();
                foreach (var s in best)
                {
                    int before = g.Party.Gold;
                    g.TrainAt(c, s);
                    if (g.Party.Gold < before) { bought = true; break; }
                }
            }
        }
    }

    private IEnumerable<Door> DoorsOf(IReadOnlyList<Cell> targets) => targets.Select(g.Level.DoorAt).OfType<Door>();

    /// <summary>
    /// A lever is worth pulling while a door it works is shut (Clears), or once if it works no door. Without
    /// Clears the bot keeps its old habit: each lever once.
    /// </summary>
    private bool WantsPull(Lever l)
    {
        if (Clears && g.Level.Valves.Contains(l.Cell)) return false; // valves have their own goal
        if (!Clears || !DoorsOf(l.Targets).Any()) return !_pulled.Contains(l);
        if (_pulledAt.TryGetValue(l, out var at) && g.Time - at < 5) return false; // give the door time to move
        return DoorsOf(l.Targets).Any(d => d.IsShut && !d.Opening && !d.Locked);
    }

    /// <summary>A toggle plate is worth stepping on while a door it works is shut; otherwise it is stepped round.</summary>
    private bool WantsPress(Plate p)
    {
        if (!Clears || !DoorsOf(p.Targets).Any()) return !_pressed.Contains(p);
        return DoorsOf(p.Targets).Any(d => d.IsShut && !d.Opening && !d.Locked);
    }

    private readonly Dictionary<Lever, double> _pulledAt = new();
    private readonly Dictionary<(Cell, Dir), int> _searched = new();

    /// <summary>The nearest reachable spot facing a wall not yet searched three times.</summary>
    private (Cell Cell, Dir Face)? WallToSearch()
    {
        var seen = new HashSet<Cell> { g.PartyCell };
        var q = new Queue<Cell>();
        q.Enqueue(g.PartyCell);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (g.Level.KindAt(n) == CellKind.Wall && g.Level.InBounds(n.X, n.Y) && _searched.GetValueOrDefault((c, d)) < 3) return (c, d);
            }
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (seen.Add(n) && Walkable(n)) q.Enqueue(n);
            }
        }
        return null;
    }

    /// <summary>For tracing: the cells the bot thinks it can walk to, as a map.</summary>
    public string ReachMap()
    {
        var seen = new HashSet<Cell> { g.PartyCell };
        var q = new Queue<Cell>();
        q.Enqueue(g.PartyCell);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
                if (c.Step(d) is var n && seen.Add(n) && Walkable(n, true)) q.Enqueue(n);
                else if (!Walkable(c.Step(d), true)) seen.Remove(c.Step(d));
        }
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < g.Level.Height; y++)
        {
            for (int x = 0; x < g.Level.Width; x++) sb.Append(seen.Contains(new Cell(x, y)) ? '*' : g.Level.KindAt(new Cell(x, y)) == CellKind.Wall ? '#' : '.');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private Lever? OpenValve() =>
        g.Level.Levers.FirstOrDefault(l => g.Level.Valves.Contains(l.Cell) && !l.Down && !Gave("valve", l.Cell) && Reachable(l.Cell));

    private bool Gave(string goal, Cell c) => _gaveUp.Contains((goal, c));
    private Monster? _prey;

    private bool Reachable(Cell c) => c == g.PartyCell || PathStep(c) is not null || PathStep(c, braveTraps: true) is not null;

    private int _disarms;

    private bool Walkable(Cell c, bool braveTraps = false)
    {
        var kind = g.Level.KindAt(c);
        if (kind == CellKind.Wall) return false;
        if (g.Level.PitAt(c) is { Spotted: true }) return false;
        if (g.Level.ChestAt(c) is not null || g.Level.TrapAt(c) is { Kind: TrapKind.CursedAltar }) return false;
        if (g.Level.Waystone == c || (g.Level.Stations.TryGetValue(c, out var st) && st is not ("roots" or "pod") && g.StationBlocks(c))) return false;
        if (g.Level.IsStairsUp(c)) return false; // the bot only goes down
        if (!braveTraps && g.Level.Traps.Any(t => t.Cell == c && t.Spotted && t.Armed && t.Kind is not (TrapKind.AntiMagic or TrapKind.FloodingRoom))) return false;
        if (g.Level.PlateAt(c) is { Mode: PlateMode.Toggle } plate && c != g.PartyCell && !WantsPress(plate) && !braveTraps) return false; // round it, unless it's the only way
        if (g.Level.DoorAt(c) is { } d && !d.IsPassable)
        {
            if (d.Locked) return HasKey(d.KeyId) || (Clears && d.LockDc > 0); // a lock can be picked
            if (d.Kind != DoorKind.Hand && !d.Opening) return false;
        }
        return true; // creatures in the way get fought when the bot bumps into them
    }

    private Dir? PathStep(Cell goal, bool braveTraps = false, bool roundFoes = false)
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
                if (first.ContainsKey(n) || !Walkable(n, braveTraps)) continue;
                if (roundFoes && n != goal && g.MonstersAt(n).Any(m => !m.Ally)) continue;
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
