using AVAHack.Characters;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

/// <summary>
/// The Rootdark's rules (spec 38), switched on by the region's settings in regions.json:
/// <list type="bullet">
/// <item><b>Spore pods</b> (stations of kind "pod") burst when anything comes within a cell: a 3×3 cloud of
/// choking spores for 12 s. Fire (a bolt, a burning blow, a fire trap) ignites a cloud: it goes up in a blast that
/// burns everything in it, friend and foe, and clears it.</item>
/// <item><b>Root tangles</b> (stations of kind "roots") choke corridors. Use hacks at one (a Survival check; it
/// gives way at once to a good cutter, after a few tries to anyone); fire burns it away.</item>
/// <item><b>Burrowers</b> tunnel through the earth: once every few seconds an aware one comes out of the wall
/// behind the party, or beside it.</item>
/// <item><b>The Mycel Crown</b> can't be hurt while any Node of the Crown lives. A node cut down grows back in
/// <see cref="NodeRegrow"/> seconds, once, unless fire killed it; the Crown calls up thralls and breathes spores.</item>
/// </list>
/// A burst pod and a cut tangle are kept in the level's flags, so saves keep them.
/// </summary>
public sealed partial class GameEngine
{
    public const int CrownReach = 6;
    public const double SporeSeconds = 12, NodeRegrow = 90, BurrowSeconds = 10, BurrowGap = 8, CrownCall = 20;

    /// <summary>A station that still stands in the way: a spore pod that hasn't burst, a tangle not yet cut.</summary>
    public bool StationBlocks(Cell c) =>
        Level.Stations.TryGetValue(c, out var k) && !(k is "pod" && LevelFlags.ContainsKey($"burst:{c}")) && !(k is "roots" && LevelFlags.ContainsKey($"cut:{c}"));

    public bool PodWhole(Cell c) => Level.Stations.TryGetValue(c, out var k) && k == "pod" && !LevelFlags.ContainsKey($"burst:{c}");
    public bool RootsStand(Cell c) => Level.Stations.TryGetValue(c, out var k) && k == "roots" && !LevelFlags.ContainsKey($"cut:{c}");

    // ---- Spore pods ----

    /// <summary>Each tick: a pod bursts when the party or a creature (other than its own thralls) comes within a cell.</summary>
    private void TickPods()
    {
        if (Region.Pods == 0 && !Level.Stations.ContainsValue("pod")) return;
        foreach (var (cell, kind) in Level.Stations.ToList())
        {
            if (kind != "pod" || LevelFlags.ContainsKey($"burst:{cell}")) continue;
            bool near = cell.Distance(PartyCell) <= 1 || _monsters.Any(m => m.IsAlive && !m.Ally && m.Def.Family is not ("sporeThralls" or "sporeKin") && m.Cell.Distance(cell) <= 1);
            if (near) BurstPod(cell);
        }
    }

    internal void BurstPod(Cell cell)
    {
        LevelFlags[$"burst:{cell}"] = "1";
        Cue("spore", cell);
        Say("A spore pod bursts in a choking yellow cloud!");
        _gas.Add(new GasCloud
        {
            Cells = [.. Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new Cell(cell.X + dx, cell.Y + dy)))],
            Variant = "spores", SecondsLeft = SporeSeconds, Dc = 12 + Level.Depth / 3, Since = 2,
        });
    }

    /// <summary>Fire reaching a cell: a spore cloud there goes up, and a tangle there burns away.</summary>
    internal void FireAt(Cell cell)
    {
        if (RootsStand(cell))
        {
            LevelFlags[$"cut:{cell}"] = "1";
            Say("The roots catch, and burn away to ash.");
            Cue("torch", cell);
        }
        if (_gas.FirstOrDefault(g => g.Variant == "spores" && g.Cells.Contains(cell)) is not { } cloud) return;
        _gas.Remove(cloud);
        Say("The spores catch fire! The cloud goes up in a roaring flash.");
        Cue("ignite", cell);
        int d = Level.Depth;
        foreach (var m in _monsters.Where(m => m.IsAlive && cloud.Cells.Contains(m.Cell)).ToList())
            DamageMonster(m, Combat.ApplyAffinity(Dice.Parse("4d6").Roll(Rng.Combat) + d, m.Resistances.For(DamageType.Fire)), DamageType.Fire);
        if (cloud.Cells.Contains(PartyCell))
        {
            foreach (var c in Party.Standing.ToList())
            {
                int dmg = Dice.Parse("4d6").Roll(Rng.Combat) + d;
                if (c.Save(Rng.Combat, Attribute.Dex, 14 + d / 3)) dmg /= 2;
                HurtHero(c, Combat.ApplyAffinity(dmg, c.Resistances.For(DamageType.Fire)), DamageType.Fire);
            }
            if (Party.AllDown) Die();
        }
        foreach (var c in cloud.Cells) if (RootsStand(c)) LevelFlags[$"cut:{c}"] = "1";
    }

    // ---- Root tangles ----

    private bool CutRoots(Cell ahead)
    {
        if (!RootsStand(ahead)) return false;
        var cutter = Party.Standing.MaxBy(h => h.SkillLevel(SkillId.Survival) / 5 + h.Mod(Attribute.Str));
        if (cutter is null) return true;
        Cue("chop", ahead);
        int roll = cutter.RollD20(Rng.Combat) + cutter.SkillLevel(SkillId.Survival) / 5 + cutter.Mod(Attribute.Str);
        int tries = LevelFlags.TryGetValue($"hack:{ahead}", out var t) ? int.Parse(t, System.Globalization.CultureInfo.InvariantCulture) + 1 : 1;
        cutter.UseSkill(SkillId.Survival, 0, XpSource.Plain);
        if (roll >= 15 || tries >= 3)
        {
            LevelFlags[$"cut:{ahead}"] = "1";
            LevelFlags.Remove($"hack:{ahead}");
            Say($"{cutter.Name} hacks through the roots.");
        }
        else
        {
            LevelFlags[$"hack:{ahead}"] = tries.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Say($"{cutter.Name} hacks at the roots. They're tough; keep at it, or burn them.");
        }
        return true;
    }

    // ---- Burrowers ----

    /// <summary>An aware burrower out of reach comes up out of the wall behind the party, or beside it.</summary>
    private void Burrow(Monster m)
    {
        if (!m.Def.Has("burrower")) return;
        if (m.Cell.Distance(PartyCell) <= 1) { m.SpecialTimer = 0; return; }
        m.SpecialTimer += TickSeconds;
        if (m.SpecialTimer < BurrowSeconds || m.Action is not (MonsterAction.None or MonsterAction.Stepping or MonsterAction.Turning)) return;
        if (Time - _lastBurrow < BurrowGap) return;                          // one at a time comes up out of the earth
        if (Path(m.Cell) is not var (steps, _) || steps > 14) return;
        m.SpecialTimer = 0;
        foreach (var d in new[] { Facing.Opposite(), Facing.Left(), Facing.Right() })
        {
            var at = PartyCell.Step(d);
            if (!Level.IsPassable(at) || StationBlocks(at) || Level.ChestAt(at) is not null || _monsters.Any(o => o.IsAlive && o.Cell == at)) continue;
            m.Cell = m.From = at;
            m.Action = MonsterAction.None;
            m.ActionLeft = 0;
            m.Facing = d.Opposite();
            Say(d == Facing.Opposite() ? $"{m.TheName} bursts out of the earth behind you!" : $"{m.TheName} tunnels up beside you!");
            Cue("burrow", at);
            _lastBurrow = Time;
            return;
        }
    }

    private double _lastBurrow = -100;

    // ---- The Mycel Crown ----

    /// <summary>Whether any node still feeds the Crown.</summary>
    public bool CrownFed => _monsters.Any(o => o.Boss == "crownNode" && o.IsAlive);

    private void CrownTick(Monster m)
    {
        // Rooted, it calls and breathes only while the party is in its hall.
        if (m.Cell.Distance(PartyCell) > CrownReach) { m.SpecialTimer = Math.Min(m.SpecialTimer, CrownCall - 2); return; }
        if (m.SpecialTimer >= CrownCall)
        {
            m.SpecialTimer = 0;
            Summon(m, "thralledDelver", 2);
            // It breathes out spores round itself: a cloud a careless fire would set alight.
            _gas.Add(new GasCloud
            {
                Cells = [.. Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new Cell(m.Cell.X + dx, m.Cell.Y + dy)))],
                Variant = "spores", SecondsLeft = 8, Dc = 13 + Level.Depth / 3, Since = 1,
            });
            Say("The Mycel Crown exhales, and spores billow round it.");
            Cue("spore", m.Cell);
        }
    }

    /// <summary>A node's death: fire ends it, anything else and it grows back.</summary>
    private void NodeDied(Monster m)
    {
        if (m.LastHitType == DamageType.Fire)
        {
            m.SpecialTimer = 0;
            Say("The node shrivels in the flames. It won't grow back.");
        }
        else if (m.Raised)
        {
            m.SpecialTimer = 0;
            Say("The node falls a second time, and its threads go grey. It won't grow back.");
        }
        else
        {
            m.SpecialTimer = NodeRegrow;
            Say("The node falls, but its threads are still alive. Burn it, or it will grow back once.");
        }
        if (!CrownFed && LivingBoss is { Boss: "mycelCrown" }) Say("Its last node lies cut. The Mycel Crown is bare!");
    }

    private void RegrowNodes()
    {
        foreach (var s in _monsters)
        {
            if (s.Boss != "crownNode" || s.State != MonsterState.Dead || s.SpecialTimer <= 0) continue;
            s.SpecialTimer -= TickSeconds;
            if (s.SpecialTimer > 0) continue;
            s.SpecialTimer = 0;
            if (LivingBoss is not { Boss: "mycelCrown" }) continue; // the Crown is gone: nothing to grow for
            s.State = MonsterState.Aware;
            s.Hp = s.MaxHp;
            s.LastSeen = Time;
            s.Raised = true; // no second helping of loot
            Kills--;
            Say("A node of the Mycel Crown knits itself back together.");
            Cue("spore", s.Cell, 0.6);
        }
    }
}
