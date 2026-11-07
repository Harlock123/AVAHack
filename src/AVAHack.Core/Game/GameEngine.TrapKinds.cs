using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

/// <summary>A boulder rolling down a corridor, crushing what it meets.</summary>
public sealed class Boulder
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public Dir Dir { get; init; }
    public int Depth { get; init; }
    internal Cell LastCell;
}

/// <summary>A cloud of poison or sleep from a vent.</summary>
public sealed class GasCloud
{
    public required IReadOnlyList<Cell> Cells { get; init; }
    public required string Variant { get; init; }
    public double SecondsLeft { get; internal set; }
    public int Dc { get; init; }
    internal double Since;
}

public sealed partial class GameEngine
{
    public const double BoulderSpeed = 4;
    public const double SpikeCycle = 3, BladeCycle = 2.4;

    private readonly List<Boulder> _boulders = new();
    private readonly List<GasCloud> _gas = new();

    public IReadOnlyList<Boulder> Boulders => _boulders;
    public IReadOnlyList<GasCloud> Gas => _gas;

    private void InitTraps() => AntiMagic = cell => Level.Traps.Any(t => t.Kind == TrapKind.AntiMagic && !t.Disarmed && t.Area.Contains(cell));

    private static string TrapName(TrapKind k) => k switch
    {
        TrapKind.DartWall => "a dart trap", TrapKind.Tripwire => "a tripwire", TrapKind.SpikeFloor => "spikes in the floor",
        TrapKind.SwingingBlade => "a swinging blade", TrapKind.FireSpout => "a fire spout", TrapKind.FrostGlyph => "a frost glyph",
        TrapKind.LightningRune => "a lightning rune", TrapKind.GasVent => "a gas vent", TrapKind.CollapsingCeiling => "a weak ceiling",
        TrapKind.RollingBoulder => "a pressure plate (and a groove in the floor)", TrapKind.Teleporter => "a strange sigil",
        TrapKind.Spinner => "a turning floor", TrapKind.AlarmPlate => "an alarm plate", TrapKind.CursedAltar => "a cursed altar",
        TrapKind.FloodingRoom => "a sluice in the floor", _ => "dead air",
    };

    /// <summary>Passive Perception (with Trap Sense) finds traps within 2 cells; blades and spikes are plain to see.</summary>
    private void LookForTraps(Cell c)
    {
        int best = Party.Best(h => h.PassivePerception() + (int)h.PerkTotalFor("perceptionBonus", "traps"));
        foreach (var t in Level.Traps)
        {
            if (t.Spotted || t.Cell.Distance(c) > 2) continue;
            if (t.HideDc == 0 || best >= t.HideDc || Detecting > 0)
            {
                t.Spotted = true;
                if (t.HideDc > 0)
                {
                    Party.Standing.MaxBy(h => h.PassivePerception())?.UseSkill(SkillId.Traps, t.HideDc / 2.0, XpSource.Plain);
                    Say($"You spot {TrapName(t.Kind)}.");
                }
            }
        }
        if (Level.Traps.FirstOrDefault(t => t.Kind == TrapKind.AntiMagic && t.Area.Contains(c) && !LevelFlags.ContainsKey("antimagic:" + t.Cell)) is { } am)
        {
            LevelFlags["antimagic:" + am.Cell] = "1";
            Say("The air goes dead. Your spells feel very far away.");
        }
    }

    /// <summary>Use, facing a trap you've spotted, tries to disarm it. A bad failure sets it off.</summary>
    private bool DisarmAhead(Cell ahead)
    {
        if (Level.Traps.FirstOrDefault(t => t.Cell == ahead && t.Spotted && t.Armed && t.Kind is not (TrapKind.CursedAltar or TrapKind.FloodingRoom or TrapKind.AntiMagic)) is not { } t)
            return false;
        var hands = Party.Standing.MaxBy(h => h.Mod(Attribute.Dex) + h.SkillLevel(SkillId.Traps) / 5 + (int)h.PerkTotal("trapBonus"));
        if (hands is null) return false;
        int roll = hands.RollD20(Rng.Combat) + hands.Mod(Attribute.Dex) + hands.SkillLevel(SkillId.Traps) / 5 + (int)hands.PerkTotal("trapBonus");
        if (roll >= t.DisarmDc)
        {
            t.Disarmed = true;
            hands.UseSkill(SkillId.Traps, t.DisarmDc, XpSource.Plain);
            Say($"{hands.Name} disarms {TrapName(t.Kind)}.");
            CueHere("unlock");
            if (hands.HasPerkFlag("salvage")) Level.Drop(PartyCell, new Items.Item { BaseId = "gold", Count = 5 + t.Depth * 3 });
            return true;
        }
        if (roll <= t.DisarmDc - 5 && !hands.HasPerkFlag("saboteur"))
        {
            Say($"{hands.Name} fumbles it!");
            Spring(t, null);
            return true;
        }
        Say($"{hands.Name} can't work out {TrapName(t.Kind)} (DC {t.DisarmDc}). Try again, or go round.");
        return true;
    }

    /// <summary>The party has stepped onto a cell: a trap there goes off. True if it moved the party.</summary>
    private bool TriggerTrapAt(Cell c)
    {
        var t = Level.Traps.FirstOrDefault(x => x.Cell == c && x.Armed && !x.Cyclic && x.Kind is not (TrapKind.CursedAltar or TrapKind.AntiMagic));
        if (t is null) return false;
        // Light Feet: a sneaking, light hero doesn't press plates.
        if (Sneaking && t.Kind is TrapKind.DartWall or TrapKind.AlarmPlate or TrapKind.RollingBoulder or TrapKind.FireSpout or TrapKind.GasVent
            && Party.Standing.All(h => h.HasPerkFlag("lightFeet") && h.CarriedKg < 60)) return false;
        return Spring(t, null);
    }

    /// <summary>A trap goes off, on the party (<paramref name="m"/> null) or on a creature that stepped on it.</summary>
    private bool Spring(Trap t, Monster? m)
    {
        t.Spotted = true;
        int d = t.Depth;
        bool moved = false;
        switch (t.Kind)
        {
            case TrapKind.DartWall:
            case TrapKind.Tripwire when t.Variant == "darts":
                Say("Click. Darts hiss out of the walls!");
                Cue("trap.dart", t.Cell);
                for (int i = 0; i < 3; i++)
                {
                    if (m is not null) { if (m.IsAlive) DamageMonster(m, Dice.Parse("1d4").Roll(Rng.Combat) + d / 3, DamageType.Piercing); continue; }
                    var hit = Party.Standing.ToList();
                    if (hit.Count == 0) break;
                    var c = hit[Rng.Combat.Next(hit.Count)];
                    if (!Combat.Attack(Rng.Combat, 4 + d / 2, c.ArmourClass).Hit) continue;
                    HurtHero(c, Dice.Parse("1d4").Roll(Rng.Combat) + d / 3, DamageType.Piercing);
                    Afflict(c, ConditionId.Poisoned, 11 + d / 2, 12);
                }
                t.Sprung = true;
                break;
            case TrapKind.Tripwire:
            case TrapKind.AlarmPlate:
                Say("A bell clangs somewhere in the dark. Everything nearby knows you're here.");
                Cue("trap.alarm", t.Cell);
                foreach (var o in _monsters.Where(o => o.IsAlive && !o.Ally && o.Cell.Distance(t.Cell) <= 12)) { Alert(o); o.LastSeen = Time; }
                t.Sprung = true;
                break;
            case TrapKind.FireSpout:
            {
                Say("Fire roars out of the wall!");
                Cue("trap.fire", t.Cell);
                var cells = new List<Cell> { t.Cell };
                for (var c = t.Cell.Step(t.Facing); cells.Count < 3 && Level.KindAt(c) != CellKind.Wall; c = c.Step(t.Facing)) cells.Add(c);
                foreach (var c in cells)
                {
                    foreach (var o in MonstersAt(c).ToList()) DamageMonster(o, Combat.ApplyAffinity(Dice.Parse("3d6").Roll(Rng.Combat) + d, o.Resistances.For(DamageType.Fire)), DamageType.Fire);
                    _hazards.Add(new Hazard { Cell = c, Type = DamageType.Fire, Dice = Dice.Parse("1d4"), SecondsLeft = 4 });
                }
                if (m is null && cells.Contains(PartyCell))
                    foreach (var c in Party.Standing.ToList())
                    {
                        int dmg = Dice.Parse("3d6").Roll(Rng.Combat) + d;
                        if (c.Save(Rng.Combat, Attribute.Dex, 12 + d / 2)) dmg /= 2;
                        HurtHero(c, Combat.ApplyAffinity(dmg, c.Resistances.For(DamageType.Fire)), DamageType.Fire);
                    }
                t.Sprung = true;
                break;
            }
            case TrapKind.FrostGlyph:
                Say("A glyph flares blue under your feet!");
                Cue("impact.frost", t.Cell);
                if (m is not null) m.Conditions.Apply(ConditionId.Frozen, 12 + d / 2, 4);
                else foreach (var c in Party.Standing) if (!c.Save(Rng.Combat, Attribute.Dex, 12 + d / 2)) { c.ApplyCondition(ConditionId.Frozen, 12 + d / 2, 4); Say($"{c.Name} is frozen solid!"); }
                t.Sprung = true;
                break;
            case TrapKind.LightningRune:
            {
                var metal = Party.Standing.Where(c => c.Body?.Kind == ArmourKind.Heavy).ToList();
                if (m is null && metal.Count == 0) break; // nothing to call the lightning
                Say("The rune answers the iron you carry: lightning!");
                Cue("trap.lightning", t.Cell);
                if (m is not null) DamageMonster(m, Dice.Parse("4d6").Roll(Rng.Combat) + d, DamageType.Lightning);
                foreach (var c in metal) HurtHero(c, Combat.ApplyAffinity(Dice.Parse("4d6").Roll(Rng.Combat) + d, c.Resistances.For(DamageType.Lightning)), DamageType.Lightning);
                foreach (var c in Party.Standing.Except(metal).ToList()) HurtHero(c, Dice.Parse("1d6").Roll(Rng.Combat), DamageType.Lightning);
                t.Sprung = true;
                break;
            }
            case TrapKind.GasVent:
                Say(t.Variant == "sleep" ? "A sweet-smelling mist hisses from the floor." : "Green gas billows up from a vent!");
                Cue("trap.gas", t.Cell);
                _gas.Add(new GasCloud
                {
                    Cells = [.. Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new Cell(t.Cell.X + dx, t.Cell.Y + dy)))],
                    Variant = t.Variant, SecondsLeft = 10, Dc = 12 + d / 2, Since = 2,
                });
                t.Sprung = true;
                break;
            case TrapKind.CollapsingCeiling:
                Say("The ceiling gives way!");
                Cue("trap.boulder", t.Cell);
                if (m is not null) DamageMonster(m, Dice.Parse("4d10").Roll(Rng.Combat), DamageType.Bludgeoning);
                else foreach (var c in Party.Standing.ToList())
                {
                    if (c.Save(Rng.Combat, Attribute.Dex, 13 + d / 2)) { Say($"{c.Name} leaps clear."); continue; }
                    HurtHero(c, Dice.Parse("4d10").Roll(Rng.Combat), DamageType.Bludgeoning);
                }
                Level.MakeRubble(t.Cell);
                t.Sprung = true;
                break;
            case TrapKind.RollingBoulder:
                if (t.Target is { } from && DirTo(from, t.Cell) is { } dir)
                {
                    Say("Something heavy starts to roll, up the slope behind you!");
                    Cue("trap.boulder", from);
                    _boulders.Add(new Boulder { X = from.X + 0.5, Y = from.Y + 0.5, Dir = dir, Depth = d, LastCell = from });
                }
                t.Sprung = true;
                break;
            case TrapKind.Teleporter:
                if (t.Target is not { } to) break;
                if (m is not null)
                {
                    if (!_monsters.Any(o => o.IsAlive && o.Cell == to) && to != PartyCell) { m.Cell = m.From = to; }
                    break;
                }
                Say("The sigil flares. The world lurches, and you are somewhere else.");
                CueHere("trap.teleport");
                Stand(to, Facing);
                EnterCell(to);
                moved = true;
                break;
            case TrapKind.Spinner:
                // Silently. The compass gives it away.
                if (m is not null) m.Facing = (Dir)Rng.Combat.Next(4);
                else Facing = (Dir)Rng.Combat.Next(4);
                break;
            case TrapKind.FloodingRoom:
                if (m is not null) break;
                foreach (var door in Level.Doors.Where(dr => t.Area.Any(a => a.Distance(dr.Cell) == 1)))
                {
                    door.Opening = false;
                    door.Locked = true;
                }
                t.Active = true;
                t.Timer = 0;
                t.Sprung = true;
                Say("The door slams shut behind you, and water starts to pour in! Find the drain.");
                CueHere("trap.flood");
                break;
        }
        if (m is null && Party.AllDown) Die();
        return moved;
    }

    /// <summary>Damage from a trap or hazard to a hero (no attack roll), with what being hurt always brings.</summary>
    private void HurtHero(Character c, int dmg, DamageType type)
    {
        if (dmg <= 0 || c.IsDown) return;
        c.TakeDamage(dmg);
        c.HurtFlash = 0.3;
        HeroHurt = 0.3;
        FloatAtHero($"-{dmg}", TextKind.HeroHurt);
        Concentrate(c, dmg);
        if (c.IsDown) Say($"{c.Name} falls!");
    }

    /// <summary>A creature finished a step onto a trap (only the mindless walk onto them).</summary>
    private void MonsterTrap(Monster m)
    {
        if (Level.Traps.FirstOrDefault(t => t.Cell == m.Cell && t.Armed && !t.Cyclic && t.Kind is not (TrapKind.CursedAltar or TrapKind.AntiMagic or TrapKind.FloodingRoom)) is { } t)
            Spring(t, m);
    }

    private void TickTraps()
    {
        foreach (var t in Level.Traps)
        {
            if (!t.Armed) continue;
            if (t.Cyclic)
            {
                double cycle = t.Kind == TrapKind.SpikeFloor ? SpikeCycle : BladeCycle, window = t.Kind == TrapKind.SpikeFloor ? 1.0 : 0.4;
                t.Timer += TickSeconds;
                bool was = t.Active;
                t.Active = t.Timer % cycle >= cycle - window;
                if (t.Active && !was)
                {
                    Cue(t.Kind == TrapKind.SpikeFloor ? "trap.spikes" : "trap.blade", t.Cell, 0.7); // you hear it cycle before you reach it
                    var dice = t.Kind == TrapKind.SpikeFloor ? "3d6" : "3d8";
                    if (PartyCell == t.Cell)
                    {
                        Say(t.Kind == TrapKind.SpikeFloor ? "Spikes stab up through the floor!" : "The blade sweeps through!");
                        var front = Party.Exposed(RelativeSide.Ahead);
                        foreach (var c in (t.Kind == TrapKind.SpikeFloor ? Party.Standing.ToList() : front.ToList()))
                            HurtHero(c, Dice.Parse(dice).Roll(Rng.Combat) + t.Depth, t.Kind == TrapKind.SpikeFloor ? DamageType.Piercing : DamageType.Slashing);
                        if (Party.AllDown) Die();
                    }
                    foreach (var o in MonstersAt(t.Cell).ToList()) DamageMonster(o, Dice.Parse(dice).Roll(Rng.Combat) + t.Depth, DamageType.Slashing);
                }
            }
        }
        foreach (var t in Level.Traps.Where(t => t.Kind == TrapKind.FloodingRoom && t.Active))
        {
            t.Timer += TickSeconds;
            if (t.Timer > 30 && t.Area.Contains(PartyCell) && (int)(t.Timer * 60) % 60 == 0)
            {
                foreach (var c in Party.Standing.ToList()) HurtHero(c, Dice.Parse("1d6").Roll(Rng.Combat), DamageType.Bludgeoning);
                Say("The water is over your heads!");
                if (Party.AllDown) Die();
            }
        }
        foreach (var b in _boulders.ToList())
        {
            double step = BoulderSpeed * TickSeconds;
            b.X += b.Dir.Dx() * step;
            b.Y += b.Dir.Dy() * step;
            var cell = new Cell((int)Math.Floor(b.X), (int)Math.Floor(b.Y));
            if (cell == b.LastCell) continue;
            if (Level.KindAt(cell) == CellKind.Wall || Level.DoorAt(cell) is { IsPassable: false } || Level.ChestAt(cell) is not null)
            {
                _boulders.Remove(b);
                Say("The boulder grinds to a halt with a boom.");
                continue;
            }
            b.LastCell = cell;
            foreach (var o in MonstersAt(cell).ToList()) DamageMonster(o, Dice.Parse("6d6").Roll(Rng.Combat) + b.Depth, DamageType.Bludgeoning);
            if (cell == PartyCell)
            {
                Say("The boulder crashes through the party!");
                foreach (var c in Party.Standing.ToList())
                {
                    int dmg = Dice.Parse("6d6").Roll(Rng.Combat) + b.Depth;
                    if (c.Save(Rng.Combat, Attribute.Dex, 13 + b.Depth / 2)) dmg /= 2;
                    HurtHero(c, dmg, DamageType.Bludgeoning);
                }
                if (Party.AllDown) Die();
            }
        }
        foreach (var g in _gas.ToList())
        {
            g.SecondsLeft -= TickSeconds;
            g.Since += TickSeconds;
            if (g.Since >= 2)
            {
                g.Since = 0;
                var id = g.Variant == "sleep" ? ConditionId.Asleep : ConditionId.Poisoned;
                if (g.Cells.Contains(PartyCell))
                    foreach (var c in Party.Standing) Afflict(c, id, g.Dc, id == ConditionId.Asleep ? 10 : 12);
                foreach (var o in _monsters.Where(o => o.IsAlive && g.Cells.Contains(o.Cell) && !o.Def.ConditionImmune.Contains(id)))
                    if (Checks.RollD20(Rng.Combat, Advantage.None) + o.SaveBonus(Attribute.Con) < g.Dc) o.Conditions.Apply(id, g.Dc, 10, id == ConditionId.Poisoned ? Dice.Parse("1d2") : null);
            }
            if (g.SecondsLeft <= 0) _gas.Remove(g);
        }
    }

    /// <summary>Taking an altar's offering: gold, and then a curse on the taker or a guardian woken (spec 11.3).</summary>
    private void TakeOffering(Trap altar)
    {
        if (!altar.Armed)
        {
            Say("The altar is bare.");
            return;
        }
        altar.Sprung = true;
        int gold = Dice.Parse("3d20").Roll(Rng.Loot) * altar.Depth;
        Party.Gold += gold;
        Say($"You take the offering: {gold} gold.");
        CueHere("gold");
        if (Rng.Combat.Chance(60))
        {
            var who = Party.Standing.FirstOrDefault();
            if (who is not null && !who.Save(Rng.Combat, Attribute.Wis, 13 + altar.Depth / 2, against: "spell"))
            {
                who.ApplyCondition(ConditionId.Cursed, 0);
                Say($"A cold weight settles on {who.Name}. Cursed!");
            }
            else Say("Something cold brushes past you, and is gone.");
            return;
        }
        var table = SpawnTables.For(Content, Level.Theme, Level.Depth);
        var spot = Enum.GetValues<Dir>().Select(dd => altar.Cell.Step(dd)).FirstOrDefault(c => CanPartyEnter(c) && c != PartyCell);
        if (table.Count == 0 || spot == default) return;
        var def = Bestiary.Scale(Content.Creature(table[Rng.World.Next(table.Count)].Id), (int)(altar.Depth * 1.6) + 2);
        var g = AddMonster(def, spot, DirTo(spot, PartyCell) ?? Dir.North);
        ApplyDifficulty(g);
        g.Elite = true;
        Alert(g);
        Say($"A guardian rises beside the altar: {g.Name}!");
    }
}
