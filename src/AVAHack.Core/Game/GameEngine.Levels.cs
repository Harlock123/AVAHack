using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.Game;

/// <summary>How hard the Delve is (spec 17): what monster strength does to every creature, and the down rules.</summary>
public sealed record Difficulty(string Name, double Hp, double Damage, int ToHit, bool DeathSaves, double Champions)
{
    public static readonly Difficulty Gentle = new("Gentle", 0.75, 0.75, -2, false, 0.5);
    public static readonly Difficulty Standard = new("Standard", 1, 1, 0, false, 1);
    public static readonly Difficulty Cruel = new("Cruel", 1.25, 1.25, 1, true, 1);
    public static readonly Difficulty Brutal = new("Brutal", 1.5, 1.5, 2, true, 2);
    public static readonly Difficulty[] All = [Gentle, Standard, Cruel, Brutal];
}

public sealed partial class GameEngine
{
    public Difficulty Difficulty { get; set; } = Difficulty.Standard;
    /// <summary>Space pauses only when the Delve allows it (spec 4).</summary>
    public bool PauseAllowed { get; set; } = true;

    /// <summary>
    /// Moves the party to another level: a new one is filled with its creatures and loot, a visited one gets its
    /// saved changes back. The party, the time, the kills and the log carry on.
    /// </summary>
    public void ChangeLevel(Level next, Cell at, Dir facing, LevelStateSave? restore = null)
    {
        Level = next;
        _monsters.Clear();
        _projectiles.Clear();
        _hazards.Clear();
        _sanctuary.Clear();
        _explored.Clear();
        _boulders.Clear();
        _gas.Clear();
        LevelFlags.Clear();
        Portal = null;
        _motion = null;
        _queue.Clear();
        _inFight = false;
        foreach (var c in Party.Members) c.EndFight();
        if (restore is not null) ApplyLevelState(restore);
        else
        {
            SpawnMonsters();
            FillChests();
        }
        PartyCell = at;
        Facing = facing;
        Say($"{next.Name}.");
        EnterCell(at);
        UpdatePlates();
    }

    /// <summary>The Delve is done (or the boss has fallen).</summary>
    public void Win(string message)
    {
        if (Mode != GameMode.Playing) return;
        Mode = GameMode.Won;
        Say(message);
    }

    /// <summary>Puts the party somewhere on arrival without the side effects of a step.</summary>
    internal void PlaceParty(Cell at, Dir facing)
    {
        PartyCell = at;
        Facing = facing;
        _motion = null;
        _queue.Clear();
    }

    /// <summary>Climbing the stairs up is the Delve's to handle.</summary>
    internal Func<Cell, bool>? StairsUpHook;

    // ---- Locked doors ----

    /// <summary>A locked door ahead: a key carried opens it; otherwise the best hands try the lock; arcane ones need Dispel.</summary>
    private bool TryUnlockDoor(Door door)
    {
        if (door.KeyId is { } key)
            foreach (var h in Party.Members)
                for (int i = 0; i < Character.BackpackSize; i++)
                    if (h.Backpack[i] is { KeyId: var k } && k == key)
                    {
                        h.Backpack[i] = null;
                        door.Locked = false;
                        Say($"{h.Name}'s key turns. The door unlocks.");
                        return true;
                    }
        if (door.Arcane && !Party.Standing.Any(h => h.HasPerkFlag("keymaster")))
        {
            Say("The door is sealed by magic. Dispel might open it.");
            return false;
        }
        if (door.LockDc <= 0)
        {
            Say("The door is locked, and has no keyhole you could pick. There must be a key.");
            return false;
        }
        var picker = Party.Standing.MaxBy(h => h.SkillLevel(SkillId.Lockpicking) / 5 + h.Mod(Rules.Attribute.Dex) + (int)h.PerkTotal("lockBonus"));
        if (picker is null) return false;
        int roll = picker.RollD20(Rng.Combat) + picker.Mod(Rules.Attribute.Dex) + picker.SkillLevel(SkillId.Lockpicking) / 5 + (int)picker.PerkTotal("lockBonus");
        if (roll >= door.LockDc)
        {
            door.Locked = false;
            picker.UseSkill(SkillId.Lockpicking, door.LockDc, XpSource.Plain);
            Say($"{picker.Name} picks the lock (DC {door.LockDc}).");
            return true;
        }
        if (!picker.HasPerkFlag("quickPick")) AlertWithin(3);
        Say($"{picker.Name} can't pick this lock (DC {door.LockDc}). The key must be somewhere on this level.");
        return false;
    }

    // ---- Secrets ----

    /// <summary>Passive Perception finds secret doors within a cell or two; elves see them more easily.</summary>
    private void LookForSecrets(Cell c)
    {
        foreach (var s in Level.Secrets)
        {
            if (s.Found || s.Cell.Distance(c) > 2) continue;
            int best = Party.Best(h => h.PassivePerception(secretDoors: true));
            if (best < s.Dc) continue;
            Level.Reveal(s);
            Party.Standing.MaxBy(h => h.PassivePerception(true))?.UseSkill(SkillId.Survival, 0, XpSource.Plain);
            Say("A draught from a crack in the wall... a hidden door!");
        }
    }

    /// <summary>Use on a plain wall searches it, by hand.</summary>
    private bool SearchWall(Cell wall)
    {
        if (Level.SecretAt(wall) is { Found: false } s)
        {
            var best = Party.Standing.MaxBy(h => h.PassivePerception(true));
            if (best is not null && best.RollD20(Rng.Combat) + best.PassivePerception(true) - 10 >= s.Dc - 4)
            {
                Level.Reveal(s);
                Say($"{best.Name} presses a loose stone. A section of wall swings open!");
                return true;
            }
            Say("You run your hands over the stones and find nothing. Or nothing yet.");
            return true;
        }
        return false;
    }

    // ---- Death saves (Cruel and Brutal) ----

    private readonly Dictionary<Character, (double Since, int Fails)> _dying = new();

    /// <summary>On the harder settings a downed hero rolls a DC 10 death save every 3 s; three failures kill (spec 9.5).</summary>
    private void TickDeathSaves()
    {
        if (!Difficulty.DeathSaves) return;
        foreach (var c in Party.Members)
        {
            if (!c.IsDown || c.IsDead)
            {
                _dying.Remove(c);
                continue;
            }
            var (since, fails) = _dying.GetValueOrDefault(c);
            since += TickSeconds;
            if (since >= 3)
            {
                since = 0;
                if (c.RollD20(Rng.Combat) < 10)
                {
                    fails++;
                    Say($"{c.Name} is slipping away ({fails} of 3).");
                    if (fails >= 3)
                    {
                        c.IsDead = true;
                        Say($"{c.Name} is dead.");
                    }
                }
            }
            _dying[c] = (since, fails);
        }
    }

    /// <summary>Anything that heals a dying hero (a bandage, a spell) stabilises them.</summary>
    public bool IsDying(Character c) => _dying.ContainsKey(c);
}
