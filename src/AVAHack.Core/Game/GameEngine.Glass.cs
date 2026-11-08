using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

/// <summary>
/// The Glass Roads' rules (spec 39), switched on by the region's settings in regions.json:
/// <list type="bullet">
/// <item><b>Broken glass</b> (<see cref="Level.Shards"/>) strews some floor cells. The first time the party steps on
/// one, it crunches: anything unaware within <see cref="CrunchReach"/> steps wakes, and each hero makes a DEX save or
/// is cut. Then it's ground to powder.</item>
/// <item><b>Rifts</b> (stations of kind "rift") are tears into the Unmade. While the party is within
/// <see cref="RiftReach"/> cells, an open rift lets a creature of the Unmade through every <see cref="RiftSeconds"/>
/// seconds, <see cref="RiftMax"/> at most, then gutters out. Use seals one: a Warding or Lore check, and the third
/// try always works.</item>
/// <item><b>The Lens</b> learns blows: a blow of the same kind as the last one that hurt it is mostly turned aside, so
/// the party must keep changing weapons and spells. Every few seconds it gathers light and throws a beam down its row
/// or column (step out of the line); at half health it sheds shards.</item>
/// </list>
/// A crushed shard and a sealed or spent rift are kept in the level's flags, so saves keep them.
/// </summary>
public sealed partial class GameEngine
{
    public const int CrunchReach = 4, RiftReach = 6, RiftMax = 3, RiftLevel = 2;
    public const double RiftSeconds = 12, BeamSeconds = 10, BeamCharge = 1.5, LensLearned = 0.75;

    // ---- Broken glass ----

    public bool ShardsAt(Cell c) => Level.Shards.Contains(c) && !LevelFlags.ContainsKey($"crushed:{c}");

    private void StepOnGlass(Cell c)
    {
        if (!ShardsAt(c)) return;
        LevelFlags[$"crushed:{c}"] = "1";
        Level.Shards.Remove(c);
        Cue("glass", c);
        int dc = 13 + Level.Depth / 3, cut = 0;
        foreach (var h in Party.Standing.ToList())
        {
            if (h.Save(Rng.Combat, Attribute.Dex, dc)) continue;
            HurtHero(h, Combat.ApplyAffinity(Dice.Parse("2d6").Roll(Rng.Combat) + Level.Depth / 3, h.Resistances.For(DamageType.Slashing)), DamageType.Slashing);
            cut++;
        }
        int woken = 0;
        foreach (var m in _monsters.ToList())
        {
            if (!m.IsAlive || m.IsAware || m.Ally || m.CalmLeft > 0) continue;
            if (Path(m.Cell) is not var (steps, doors) || steps + doors * HushDoor > CrunchReach) continue;
            Alert(m);
            woken++;
        }
        Say(cut > 0 ? "Broken glass crunches underfoot, and bites through boot leather." : "Broken glass crunches underfoot.");
        if (woken > 0) Say("The sound rings down the glass halls. Something turns toward it.");
        if (Party.AllDown) Die();
    }

    // ---- Rifts ----

    public bool IsRift(Cell c) => Level.Stations.TryGetValue(c, out var k) && k == "rift";

    /// <summary>A rift still open: not sealed, and not yet spent.</summary>
    public bool RiftOpen(Cell c) => IsRift(c) && !LevelFlags.ContainsKey($"sealed:{c}") && RiftCount(c) < RiftMax;

    private int RiftCount(Cell c) =>
        LevelFlags.TryGetValue($"rift:{c}", out var n) ? int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) : 0;

    private readonly Dictionary<Cell, double> _riftTimers = new();

    private Level? _shardsPruned;

    private void TickRifts()
    {
        // Glass crushed before a save stays crushed after it: the level comes back from its seed, the flags from the save.
        if (_shardsPruned != Level)
        {
            _shardsPruned = Level;
            Level.Shards.RemoveWhere(c => LevelFlags.ContainsKey($"crushed:{c}"));
        }
        if (Region.Rifts == 0 && !Level.Stations.ContainsValue("rift")) return;
        foreach (var (cell, kind) in Level.Stations.ToList())
        {
            if (kind != "rift" || !RiftOpen(cell) || cell.Distance(PartyCell) > RiftReach) continue;
            double t = _riftTimers.GetValueOrDefault(cell) + TickSeconds;
            if (t < RiftSeconds) { _riftTimers[cell] = t; continue; }
            _riftTimers[cell] = 0;
            var pick = Content.Creatures.Where(c => c.Family == "unmade" && c.Boss is null && !c.Has("boss") && !c.Has("summoner")).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
            if (pick.Count == 0) return;
            var def = Bestiary.ScaleTo(pick[Rng.Ai.Next(pick.Count)], Level.Depth + RiftLevel);
            var probe = new Monster(0, def, cell, Dir.North, 1, Content);
            var spot = Enum.GetValues<Dir>().Select(d => cell.Step(d)).FirstOrDefault(c => c != PartyCell && Level.IsPassable(c) && !StationBlocks(c) && CanEnter(probe, c, false));
            if (spot == default) continue;
            var s = AddMonster(def, spot, Dir.North);
            ApplyDifficulty(s);
            s.State = MonsterState.Aware;
            s.LastSeen = Time;
            int n = RiftCount(cell) + 1;
            LevelFlags[$"rift:{cell}"] = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Cue("rift", cell);
            Say(n >= RiftMax ? $"A {def.Name} claws out of the rift, and the tear gutters out behind it." : $"The rift shudders, and a {def.Name} steps out of nothing.");
        }
    }

    private bool SealRift(Cell ahead)
    {
        if (!IsRift(ahead)) return false;
        if (!RiftOpen(ahead)) { Say("The tear is closed: only a seam of dark glass shows where it was."); return true; }
        var sealer = Party.Standing.MaxBy(h => Math.Max(h.SkillLevel(SkillId.Warding), h.SkillLevel(SkillId.Lore)) / 5 + h.Mod(Attribute.Wis));
        if (sealer is null) return true;
        int roll = sealer.RollD20(Rng.Combat) + Math.Max(sealer.SkillLevel(SkillId.Warding), sealer.SkillLevel(SkillId.Lore)) / 5 + sealer.Mod(Attribute.Wis);
        int tries = LevelFlags.TryGetValue($"sealing:{ahead}", out var t) ? int.Parse(t, System.Globalization.CultureInfo.InvariantCulture) + 1 : 1;
        sealer.UseSkill(SkillId.Warding, Level.Depth, XpSource.Plain);
        if (roll >= 16 || tries >= 3)
        {
            LevelFlags[$"sealed:{ahead}"] = "1";
            LevelFlags.Remove($"sealing:{ahead}");
            Cue("seal", ahead);
            Say($"{sealer.Name} draws the edges of the rift together, and it seals with a sound like a bell.");
        }
        else
        {
            LevelFlags[$"sealing:{ahead}"] = tries.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Say($"{sealer.Name} pushes at the edges of the rift. It pulls against them; keep at it.");
        }
        return true;
    }

    // ---- The Lens ----

    /// <summary>A blow of the kind that last hurt the Lens is mostly turned aside: it has learned it.</summary>
    private int LensShield(Monster m, int dmg, DamageType type)
    {
        if (m.Hp >= m.MaxHp || type != m.LastHitType) return dmg;
        if (!LevelFlags.TryGetValue("bossSaid", out var said) || Time - double.Parse(said, System.Globalization.CultureInfo.InvariantCulture) > 6)
        {
            LevelFlags["bossSaid"] = Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            Say($"The Lens has learned that blow ({type.ToString().ToLowerInvariant()}), and turns it aside. Strike it some other way!");
        }
        return Math.Max(1, (int)Math.Round(dmg * (1 - LensLearned)));
    }

    /// <summary>When the Lens's beam fires, and down which line: a row (true) or a column, and its index.</summary>
    private double _beamAt = -1;
    private (bool Row, int Line) _beamLine;

    /// <summary>Whether a cell is in the line the Lens is charging its beam down (for the view and the bot).</summary>
    public bool InBeam(Cell c) => _beamAt >= 0 && (_beamLine.Row ? c.Y == _beamLine.Line : c.X == _beamLine.Line);

    private void LensTick(Monster m, double share)
    {
        if (_beamAt >= 0 && Time >= _beamAt)
        {
            _beamAt = -1;
            Cue("beam", m.Cell);
            bool inLine = _beamLine.Row ? PartyCell.Y == _beamLine.Line : PartyCell.X == _beamLine.Line;
            if (inLine && PartyCell.Distance(m.Cell) <= 10 && HasLineOfSight(m.Cell, PartyCell))
            {
                Say("The beam of the Lens strikes you full on!");
                foreach (var h in Party.Standing.ToList())
                {
                    int dmg = Dice.Parse("6d8").Roll(Rng.Combat) + Level.Depth;
                    if (h.Save(Rng.Combat, Attribute.Dex, 16 + Level.Depth / 4)) dmg /= 2;
                    HurtHero(h, Combat.ApplyAffinity(dmg, h.Resistances.For(DamageType.Radiant)), DamageType.Radiant);
                }
                if (Party.AllDown) Die();
            }
            else Say("The beam of the Lens scores the glass where you stood.");
        }
        if (m.SpecialTimer >= BeamSeconds && _beamAt < 0)
        {
            m.SpecialTimer = 0;
            // Down the line the party is nearer to lining up with.
            bool row = Math.Abs(PartyCell.Y - m.Cell.Y) <= Math.Abs(PartyCell.X - m.Cell.X);
            _beamLine = (row, row ? m.Cell.Y : m.Cell.X);
            _beamAt = Time + BeamCharge;
            Cue("beam.charge", m.Cell);
            Say(row ? "The Lens turns edge-on, and light gathers along the hall: get out of its line!" : "The Lens tilts, and light gathers down the hall: get out of its line!");
        }
        if (m.Phase == 0 && share <= 0.5)
        {
            m.Phase = 1;
            Say("The Lens cracks across, and shards break away from it, still turning to the light.");
            for (int i = 0; i < 3; i++) Summon(m, "lensShard", 3);
        }
    }
}
