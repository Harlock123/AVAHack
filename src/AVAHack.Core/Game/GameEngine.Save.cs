using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>Turns a level source back into the level (built-in levels here; a Delve adds its generator).</summary>
    public static Func<string, Level>? LevelResolver { get; set; }

    public static Level ResolveLevel(string source)
    {
        if (source.StartsWith("built-in:", StringComparison.Ordinal)) return LevelLoader.LoadBuiltIn(source["built-in:".Length..]);
        return LevelResolver?.Invoke(source) ?? throw new InvalidDataException($"Can't rebuild the level '{source}'.");
    }

    /// <summary>Everything needed to carry on exactly here.</summary>
    public SaveGame ToSave(IReadOnlyList<LevelStateSave>? visited = null, Dictionary<string, string>? delve = null, bool ironman = false)
    {
        var members = Party.Members.ToList();
        var t = TimeSpan.FromSeconds(Time);
        return new SaveGame
        {
            Seed = Rng.Seed,
            RngStates = Rng.All.SelectMany(r => { var (st, inc) = r.GetState(); return new[] { st, inc }; }).ToArray(),
            Party = members.Select(c => c.ToSave()).ToList(),
            Slots = members.Select(c => (int)Party.SlotOf(c)).ToArray(),
            Gold = Party.Gold, TickCount = TickCount, Kills = Kills, Log = Log.TakeLast(12).ToList(),
            LightLeft = LightLeft, Invisible = Invisible, Selected = members.IndexOf(Selected),
            Accumulator = _accumulator, InFight = _inFight, Calm = _calm,
            Current = LevelState(),
            Visited = visited?.ToList() ?? [],
            Delve = delve ?? [],
            Ironman = ironman,
            Summary = $"{string.Join(", ", members.Select(c => c.Name))} · {Level.Name} · {(int)t.TotalMinutes}:{t.Seconds:00}",
        };
    }

    /// <summary>What has changed in the current level.</summary>
    public LevelStateSave LevelState() => new()
    {
        Source = Level.Source,
        Doors = Level.Doors.Select(d => new DoorSave(d.Cell.X, d.Cell.Y, d.Open, d.Opening)).ToList(),
        Levers = Level.Levers.Select(l => new LeverSave(l.Cell.X, l.Cell.Y, l.Face, l.Down)).ToList(),
        PlatesPressed = Level.Plates.Where(p => p.Pressed).Select(p => new CellSave(p.Cell.X, p.Cell.Y)).ToList(),
        PitsSpotted = Level.Pits.Where(p => p.Spotted).Select(p => new CellSave(p.Cell.X, p.Cell.Y)).ToList(),
        NotesRead = Level.Notes.Where(n => n.Read).Select(n => new CellSave(n.Cell.X, n.Cell.Y)).ToList(),
        Chests = Level.Chests.Select(c => new ChestSave(c.Cell.X, c.Cell.Y, c.Opened, c.LockDc, c.Contents.ToList())).ToList(),
        Piles = Level.Piles.Where(p => p.Value.Count > 0).Select(p => new PileSave(p.Key.X, p.Key.Y, p.Value.ToList())).ToList(),
        Explored = _explored.Select(c => new CellSave(c.X, c.Y)).ToList(),
        Monsters = _monsters.Select(m => new MonsterSave
        {
            Def = m.Def.Id, X = m.Cell.X, Y = m.Cell.Y, Facing = m.Facing, Hp = m.Hp, MaxHp = m.MaxHp, State = m.State,
            Slot = m.Slot, PileLeft = m.PileLeft, Fell = m.FellInPit, Ally = m.Ally, Summoned = m.Summoned,
            SummonLeft = m.SummonLeft, Affixes = m.Affixes.ToList(), Elite = m.Elite, Title = m.Title, AcBonus = m.AcBonus,
            ToHitBonus = m.ToHitBonus, DamageFactor = m.DamageFactor, Ward = m.Ward, Boss = m.Boss, Phase = m.Phase,
            Action = m.Action, ActionLeft = m.ActionLeft, ActionTotal = m.ActionTotal, Attack = m.Attack?.Id,
            FromX = m.From.X, FromY = m.From.Y, StrikeTime = m.StrikeTime, IdleLeft = m.IdleLeft, FleeLeft = m.FleeLeft,
            AffixTimer = m.AffixTimer, LastHitType = m.LastHitType, Conditions = Character.SaveConditions(m.Conditions),
            LastSeen = m.LastSeen, Awareness = m.Awareness,
        }).ToList(),
        Flags = new Dictionary<string, string>(LevelFlags),
        PartyX = PartyCell.X, PartyY = PartyCell.Y, Facing = Facing,
    };

    /// <summary>The generator's traps and secrets record their changes here, so a save keeps them.</summary>
    public Dictionary<string, string> LevelFlags { get; } = new();

    /// <summary>Rebuilds a game from a save.</summary>
    public static GameEngine FromSave(SaveGame s, ContentDb? content = null)
    {
        if (s.Version > SaveGame.CurrentVersion) throw new InvalidDataException($"This save is from a newer version ({s.Version}).");
        content ??= ContentDb.LoadBuiltIn();
        var heroes = s.Party.Select(h => Character.FromSave(content, h)).ToList();
        var party = PartyInSlots(heroes, s.Slots);
        party.Gold = s.Gold;
        var level = ResolveLevel(s.Current.Source);
        var g = new GameEngine(level, s.Seed, party, content, spawn: false);
        g.ApplyLevelState(s.Current);
        var all = g.Rng.All;
        for (int i = 0; i < all.Length && i * 2 + 1 < s.RngStates.Length; i++) all[i].SetState(s.RngStates[i * 2], s.RngStates[i * 2 + 1]);
        g.TickCount = s.TickCount;
        g.Kills = s.Kills;
        g._log.Clear();
        g._log.AddRange(s.Log);
        g.LightLeft = s.LightLeft;
        g.Invisible = s.Invisible;
        g._accumulator = s.Accumulator;
        g._inFight = s.InFight;
        g._calm = s.Calm;
        if (s.Selected >= 0 && s.Selected < heroes.Count) g.Selected = heroes[s.Selected];
        return g;
    }

    private static Party PartyInSlots(List<Character> heroes, int[] slots)
    {
        var party = new Party(heroes.ToArray());
        // The Party constructor fills slots in order; put each hero back where they stood.
        for (int i = 0; i < heroes.Count; i++)
        {
            var want = (Slot)slots[i];
            var now = party.SlotOf(heroes[i]);
            if (now != want) party.Swap(now, want);
        }
        return party;
    }

    /// <summary>Puts a level's saved changes back.</summary>
    internal void ApplyLevelState(LevelStateSave st)
    {
        foreach (var d in st.Doors)
            if (Level.DoorAt(d.X, d.Y) is { } door) { door.Open = d.Open; door.Opening = d.Opening; }
        foreach (var l in st.Levers)
            if (Level.LeverAt(new Cell(l.X, l.Y), l.Face) is { } lever) lever.Down = l.Down;
        foreach (var p in Level.Plates) p.Pressed = st.PlatesPressed.Any(c => c.X == p.Cell.X && c.Y == p.Cell.Y);
        foreach (var c in st.PitsSpotted) if (Level.PitAt(new Cell(c.X, c.Y)) is { } pit) Level.Spot(pit);
        foreach (var c in st.NotesRead) if (Level.NoteAt(new Cell(c.X, c.Y)) is { } note) note.Read = true;
        foreach (var cs in st.Chests)
            if (Level.ChestAt(new Cell(cs.X, cs.Y)) is { } chest)
            {
                chest.Opened = cs.Opened;
                chest.LockDc = cs.LockDc;
                chest.Contents.Clear();
                chest.Contents.AddRange(cs.Contents);
            }
        foreach (var pile in st.Piles) foreach (var it in pile.Items) Level.Drop(new Cell(pile.X, pile.Y), it);
        foreach (var c in st.Explored) _explored.Add(new Cell(c.X, c.Y));
        _monsters.Clear();
        foreach (var ms in st.Monsters)
        {
            var m = new Monster(_nextMonsterId++, Content.Creature(ms.Def), new Cell(ms.X, ms.Y), ms.Facing, ms.MaxHp, Content)
            {
                Hp = ms.Hp, State = ms.State, Slot = ms.Slot, PileLeft = ms.PileLeft, FellInPit = ms.Fell, Ally = ms.Ally,
                Summoned = ms.Summoned, SummonLeft = ms.SummonLeft, Elite = ms.Elite, Title = ms.Title, AcBonus = ms.AcBonus,
                ToHitBonus = ms.ToHitBonus, DamageFactor = ms.DamageFactor, Ward = ms.Ward, Boss = ms.Boss, Phase = ms.Phase,
            };
            foreach (var a in ms.Affixes) m.Affixes.Add(a);
            m.Action = ms.Action;
            m.ActionLeft = ms.ActionLeft;
            m.ActionTotal = ms.ActionTotal;
            m.Attack = ms.Attack is { } atk ? m.Def.Attacks.FirstOrDefault(a => a.Id == atk) : null;
            m.From = new Cell(ms.FromX, ms.FromY);
            m.StrikeTime = ms.StrikeTime;
            m.IdleLeft = ms.IdleLeft;
            m.FleeLeft = ms.FleeLeft;
            m.AffixTimer = ms.AffixTimer;
            m.LastHitType = ms.LastHitType;
            m.LastSeen = ms.LastSeen;
            m.Awareness = ms.Awareness;
            Character.LoadConditions(m.Conditions, ms.Conditions);
            _monsters.Add(m);
        }
        foreach (var (k, v) in st.Flags) LevelFlags[k] = v;
        PartyCell = new Cell(st.PartyX, st.PartyY);
        Facing = st.Facing;
        _motion = null;
        _queue.Clear();
        _projectiles.Clear();
        LevelRestored?.Invoke(st);
    }

    /// <summary>Generated levels re-apply their trap and secret flags when restored.</summary>
    internal event Action<LevelStateSave>? LevelRestored;
}
