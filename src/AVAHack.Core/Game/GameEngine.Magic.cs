using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Magic;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

/// <summary>A patch of burning or acid ground left by a spell.</summary>
public sealed class Hazard
{
    public required Cell Cell { get; init; }
    public required DamageType Type { get; init; }
    public required Dice Dice { get; init; }
    public double SecondsLeft { get; internal set; }
    public double Power { get; init; } = 1;
    internal Character? Caster { get; init; }
    internal SkillId School { get; init; }
    internal double Since;
}

public sealed partial class GameEngine
{
    /// <summary>Seconds after a spell before the caster's hand is free again.</summary>
    public const double CastRecovery = 1.0;
    public const double BoltSpeed = 8;
    public const double BoltRange = 8;

    private readonly List<Hazard> _hazards = new();
    private readonly Dictionary<Cell, double> _sanctuary = new();

    public IReadOnlyList<Hazard> Hazards => _hazards;
    public IReadOnlyDictionary<Cell, double> Sanctuary => _sanctuary;
    /// <summary>Seconds of the Light spell left.</summary>
    public double MagicLight { get; internal set; }
    /// <summary>False selves left to take blows (Mirror Selves).</summary>
    public int Mirrors { get; internal set; }
    /// <summary>Seconds of Spell Turning left.</summary>
    public double Turning { get; internal set; }
    /// <summary>Where Open the Way will take the party back to.</summary>
    public (Cell Cell, Dir Facing)? Portal { get; internal set; }
    /// <summary>Seconds left of Detect Traps' sight.</summary>
    public double Detecting { get; internal set; }

    private void InitMagic()
    {
        LightLevel = () =>
        {
            if (MagicLight > 0) return 1.2;
            if (LightLeft > 0) return 1.05;
            // In the dark only the dwarves see much, and only close.
            if (Party.Standing.Any(c => c.Buffs.ContainsKey("nightEye"))) return 0.85;
            return Party.Standing.Any(c => c.HasTrait(Traits.Darkvision)) ? 0.6 : 0.42;
        };
        SpellItemHook = UseSpellItem;
    }

    /// <summary>Whether the party carries a light, for the creatures that notice it from farther away.</summary>
    public bool Lit => LightLeft > 0 || MagicLight > 0;

    // ---- Casting ----

    /// <summary>What a spell costs this hero: −0.4% per school point (−40% at 100), and the Mana perks.</summary>
    public double ManaCost(Character c, SpellDef s)
    {
        double perk = c.PerkTotalFor("manaCostPct", Character.SkillKey(s.School)) + (s.Element is { } e ? c.PerkTotalFor("manaCostPct", e) : 0);
        return s.Mana * Math.Max(0.25, (1 + perk / 100) * (1 - c.SkillLevel(s.School) * 0.004));
    }

    /// <summary>How strong a hero's spell is: (1 + skill / 200), and the power perks.</summary>
    public static double SpellPower(Character c, SpellDef s, int? skill = null)
    {
        double perk = c.PerkTotalFor("spellPowerPct", Character.SkillKey(s.School)) + (s.Element is { } e ? c.PerkTotalFor("damagePct", e) : 0);
        return (1 + (skill ?? c.SkillLevel(s.School)) / 200.0) * (1 + perk / 100);
    }

    /// <summary>Why a hero can't cast a spell now, or null if they can.</summary>
    public string? WhyNotCast(Character c, string spellId)
    {
        if (Mode != GameMode.Playing) return "Not now.";
        var s = Content.Spells.FirstOrDefault(x => x.Id == spellId);
        if (s is null) return "No such spell.";
        if (!c.KnownSpells.Contains(spellId)) return $"{c.Name} doesn't know {s.Name}.";
        if (c.IsDown) return $"{c.Name} is down.";
        if (!c.Conditions.CanAct) return $"{c.Name} can't act.";
        if (c.CastingSpell is not null) return $"{c.Name} is already casting.";
        if (!c.RightHand.Ready) return $"{c.Name}'s hands aren't free yet.";
        if (c.Mana < ManaCost(c, s)) return $"{c.Name} hasn't the Mana for {s.Name}.";
        if (AntiMagic(PartyCell)) return "The air here swallows magic.";
        return null;
    }

    /// <summary>Begins casting a known spell. Returns why not, or null when the casting has begun.</summary>
    public string? Cast(Character c, string spellId)
    {
        if (WhyNotCast(c, spellId) is { } why) return why;
        var s = Content.Spell(spellId);
        c.Mana -= ManaCost(c, s);
        Begin(c, s, null, null);
        return null;
    }

    private void Begin(Character c, SpellDef s, int? skill, Item? item)
    {
        c.CastingSpell = s.Id;
        c.CastLeft = c.CastTotal = s.CastTime;
        c.CastSkill = skill;
        c.CastItem = item;
        c.RightHand.Start(s.CastTime + CastRecovery);
    }

    /// <summary>Anti-magic zones (traps) swallow spells; the generator registers them.</summary>
    internal Func<Cell, bool> AntiMagic = _ => false;

    private void TickCasting()
    {
        foreach (var c in Party.Members)
        {
            foreach (var key in c.Buffs.Keys.ToList())
                if ((c.Buffs[key] -= TickSeconds) <= 0) c.Buffs.Remove(key);
            if (c.CastingSpell is not { } id) continue;
            if (c.IsDown || !c.Conditions.CanAct)
            {
                c.CastingSpell = null;
                continue;
            }
            c.CastLeft -= TickSeconds;
            if (c.CastLeft > 0) continue;
            c.CastingSpell = null;
            var s = Content.Spell(id);
            if (c.Conditions.SpellFailurePercent > 0 && Rng.Combat.Chance(c.Conditions.SpellFailurePercent))
            {
                Say($"{c.Name}'s {s.Name} fizzles.");
                continue;
            }
            Resolve(c, s, c.CastSkill);
        }
        if (MagicLight > 0) MagicLight = Math.Max(0, MagicLight - TickSeconds);
        if (Turning > 0) Turning = Math.Max(0, Turning - TickSeconds);
        if (Detecting > 0) Detecting = Math.Max(0, Detecting - TickSeconds);
        foreach (var cell in _sanctuary.Keys.ToList())
            if ((_sanctuary[cell] -= TickSeconds) <= 0) _sanctuary.Remove(cell);
        TickHazards();
    }

    /// <summary>A blow on a caster may break the spell: CON save against 10 or half the damage.</summary>
    private void Concentrate(Character c, int damage)
    {
        if (c.CastingSpell is not { } id) return;
        if (c.Save(Rng.Combat, Attribute.Con, Math.Max(10, damage / 2))) return;
        c.CastingSpell = null;
        Say($"{c.Name} loses the {Content.Spell(id).Name}.");
    }

    // ---- What spells do ----

    private void Resolve(Character c, SpellDef s, int? skill)
    {
        double power = SpellPower(c, s, skill);
        int mod = c.CastingMod(s.School);
        int dc = Checks.SpellSaveDc(mod, skill ?? c.SkillLevel(s.School)) + (int)(c.PerkTotalFor("spellPowerPct", "illusion") / 15 * (s.School == SkillId.Illusion ? 1 : 0));
        var fx = new SpellCast(c, s, power, mod, dc, skill);
        switch (s.Effect)
        {
            case "damage": CastDamage(fx); break;
            case "condition": foreach (var m in Targets(s)) MindOrBody(fx, m); break;
            case "heal": CastHeal(fx); break;
            case "ward": CastWard(fx); break;
            case "cure": CastCure(fx); break;
            case "buff": CastBuff(fx); break;
            case "summon": CastSummon(fx); break;
            case "hazard": CastHazard(fx); break;
            case "revive": CastRevive(fx); break;
            case "utility": CastUtility(fx); break;
        }
        if (!(s.Effect is "damage" or "condition" or "hazard" or "utility" or "heal" or "cure" or "revive" or "ward")) Practice(fx, s.Seconds / 4 + 1);
    }

    private sealed record SpellCast(Character Caster, SpellDef Spell, double Power, int Mod, int Dc, int? Skill);

    private void Practice(SpellCast fx, double amount)
    {
        if (fx.Skill is not null) return; // scrolls and wands teach nothing
        fx.Caster.UseSkill(fx.Spell.School, amount, new XpSource { SpellHadEffect = amount > 0 });
    }

    /// <summary>The cells a spell aims at: ahead, around the party, or a 3×3 blast two cells ahead.</summary>
    private IEnumerable<Cell> Cells(SpellDef s)
    {
        var ahead = PartyCell.Step(Facing);
        switch (s.Target)
        {
            case "ahead": yield return ahead; break;
            case "around":
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0) yield return new Cell(PartyCell.X + dx, PartyCell.Y + dy);
                break;
            case "blast":
            {
                var centre = Level.KindAt(ahead.Step(Facing)) == CellKind.Wall || !HasLineOfSight(PartyCell, ahead.Step(Facing)) ? ahead : ahead.Step(Facing);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = new Cell(centre.X + dx, centre.Y + dy);
                    if (c != PartyCell) yield return c;
                }
                break;
            }
        }
    }

    private IEnumerable<Monster> Targets(SpellDef s) =>
        Cells(s).SelectMany(c => MonstersAt(c)).Where(m => !m.Ally && (s.Name2 != "undead" || m.Def.Family == "restlessDead")).ToList();

    private void CastDamage(SpellCast fx)
    {
        var s = fx.Spell;
        if (s.Target == "bolt")
        {
            _projectiles.Add(new Projectile(PartyCell, Facing)
            {
                Look = s.Look, Speed = BoltSpeed, Range = BoltRange,
                OnHit = (m, cell) =>
                {
                    SpellHit(fx, m, attackRoll: true);
                    if (s.Amount > 0) Chain(fx, m, s.Amount);
                },
            });
            return;
        }
        foreach (var m in Targets(s)) SpellHit(fx, m, attackRoll: s.Save is null);
    }

    /// <summary>Lightning leaps on to the nearest creatures (Chain Storm, and the Arc perk).</summary>
    private void Chain(SpellCast fx, Monster from, int more)
    {
        var hit = new HashSet<Monster> { from };
        var at = from;
        for (int i = 0; i < more; i++)
        {
            var next = _monsters.Where(m => m.IsAlive && !m.Ally && !hit.Contains(m) && m.Cell.Distance(at.Cell) <= 3).OrderBy(m => m.Cell.Distance(at.Cell)).FirstOrDefault();
            if (next is null) break;
            hit.Add(next);
            SpellHit(fx, next, attackRoll: false);
            at = next;
        }
    }

    /// <summary>A damage spell reaching a creature: an attack roll or a save, then damage and any condition.</summary>
    private void SpellHit(SpellCast fx, Monster m, bool attackRoll)
    {
        var s = fx.Spell;
        var c = fx.Caster;
        bool saved = false;
        if (attackRoll && s.Save is null)
        {
            int bonus = fx.Mod + (fx.Skill ?? c.SkillLevel(s.School)) / 10;
            if (!Combat.Attack(Rng.Combat, bonus, m.Ac).Hit)
            {
                var (x, y) = m.Position();
                Float?.Invoke(new FloatText(x, y, FloatZ(m), "miss", TextKind.Miss));
                Alert(m);
                return;
            }
        }
        else if (s.Save is { } attr) saved = Checks.RollD20(Rng.Combat, Advantage.None) + m.SaveBonus(attr) >= fx.Dc;

        double mult = fx.Power;
        if (s.Name2 == "banish" && m.Def.Family is not ("summoned" or "unmade" or "elemental")) mult *= 0.5;
        if (s.Type == DamageType.Radiant && m.Def.Family == "restlessDead" && c.HasPerkFlag("undeadBane")) mult *= 1.5;
        int raw = (int)Math.Max(1, (Dice.Parse(s.Dice!).Roll(Rng.Combat) + fx.Mod) * mult);
        if (saved) raw /= 2;
        var aff = m.Resistances.For(s.Type!.Value);
        if (aff == Affinity.Resistant && c.HasPerkFlag("ignoreResistance")) aff = Affinity.Normal;
        int dmg = Combat.ApplyAffinity(raw, aff);
        Alert(m);
        DamageMonster(m, dmg, s.Type.Value);
        Practice(fx, c.UseXp(s.School, dmg) + 0.01);
        if (!saved && m.IsAlive) MindOrBody(fx, m, alreadySaved: s.Save is not null);
        if (m.IsAlive && s.Element == "fire" && c.HasPerkFlag("lingeringFlame") && fx.Skill is null)
            _hazards.Add(new Hazard { Cell = m.Cell, Type = DamageType.Fire, Dice = Dice.Parse("1d4"), SecondsLeft = 4, Power = fx.Power, Caster = c, School = s.School });
        if (m.IsAlive && s.Element == "cold" && c.HasPerkFlag("deepFreeze") && m.Conditions.Has(ConditionId.Chilled)
            && Checks.RollD20(Rng.Combat, Advantage.None) + m.SaveBonus(Attribute.Con) < fx.Dc)
            m.Conditions.Apply(ConditionId.Frozen, fx.Dc, 3);
        if (m.IsAlive && s.Element == "lightning" && s.Amount == 0 && c.HasPerkFlag("arc")) Chain(fx with { Spell = s with { Amount = 1 } }, m, 1);
    }

    /// <summary>A condition spell (or a damage spell's rider): a save resists it; Frightened sends the creature running.</summary>
    private void MindOrBody(SpellCast fx, Monster m, bool alreadySaved = false)
    {
        var s = fx.Spell;
        if (s.Condition is not { } cond || !m.IsAlive) return;
        if (m.Def.ConditionImmune.Contains(cond)) return;
        bool mind = cond is ConditionId.Frightened or ConditionId.Asleep or ConditionId.Stunned && s.School == SkillId.Illusion;
        if (mind && m.Def.Has("mindless") && !fx.Caster.HasPerkFlag("mindMaster")) return;
        if (!alreadySaved && s.Save is { } attr && Checks.RollD20(Rng.Combat, Advantage.None) + m.SaveBonus(attr) >= fx.Dc) return;
        double secs = s.Seconds * (1 + fx.Caster.PerkTotalFor("durationPct", Character.SkillKey(s.School)) / 100);
        m.Conditions.Apply(cond, fx.Dc, secs);
        if (cond == ConditionId.Frightened)
        {
            m.State = MonsterState.Fleeing;
            m.FleeLeft = secs;
        }
        Alert(m);
        if (s.Effect == "condition") Practice(fx, m.Def.Level + 2);
    }

    private Character? MostHurt() => Party.Standing.OrderBy(c => c.Hp / c.MaxHp).FirstOrDefault();

    private void CastHeal(SpellCast fx)
    {
        var who = fx.Spell.Target == "party" ? Party.Standing.ToList() : MostHurt() is { } h ? [h] : [];
        double healed = 0;
        foreach (var c in who)
        {
            double before = c.Hp;
            c.Heal((Dice.Parse(fx.Spell.Dice!).Roll(Rng.Combat) + fx.Mod) * fx.Power * (1 + fx.Caster.PerkTotal("healPct") / 100));
            healed += c.Hp - before;
            if (fx.Caster.HasPerkFlag("respite")) c.Stamina = Math.Min(c.MaxStamina, c.Stamina + (c.Hp - before));
            if (c.Hp > before) FloatAtHero($"+{c.Hp - before:0}", TextKind.Info);
        }
        Practice(fx, fx.Caster.UseXp(fx.Spell.School, healed));
    }

    private void CastWard(SpellCast fx)
    {
        var s = fx.Spell;
        var who = s.Target switch
        {
            "party" => Party.Standing.ToList(),
            "self" => [fx.Caster],
            _ => [Selected.IsDown ? fx.Caster : Selected],
        };
        double size = (s.Amount + (fx.Skill ?? fx.Caster.SkillLevel(SkillId.Warding)) / 2.0) * fx.Power;
        foreach (var c in who)
        {
            c.Ward = Math.Max(c.Ward, size);
            c.WardFrom = fx.Caster;
        }
        Say($"A shimmer of force settles on {(who.Count == 1 ? who[0].Name : "the party")}.");
        Practice(fx, 1);
    }

    private void CastCure(SpellCast fx)
    {
        var s = fx.Spell;
        var who = Party.Members.Where(c => !c.IsDead).OrderByDescending(c => s.Cures.Count(c.Conditions.Has)).FirstOrDefault();
        int cured = 0;
        if (who is not null)
            foreach (var id in s.Cures.Where(who.Conditions.Has).Take(Math.Max(1, s.Amount)).ToList())
            {
                who.Conditions.Cure(id);
                cured++;
                Say($"{who.Name} is no longer {Content.Condition(id).Name.ToLowerInvariant()}.");
            }
        if (cured == 0) Say("There is nothing to cure.");
        Practice(fx, cured * 8);
    }

    private void CastBuff(SpellCast fx)
    {
        var s = fx.Spell;
        double secs = s.Seconds * (1 + fx.Caster.PerkTotalFor("durationPct", Character.SkillKey(s.School)) / 100);
        switch (s.Name2)
        {
            case "invisible": Invisible = Math.Max(Invisible, secs); Say("The party fades from sight."); return;
            case "mirror": Mirrors = s.Amount; Say("Three false selves shimmer into being around the party."); return;
            case "turning": Turning = Math.Max(Turning, secs); Say("The air before the party bends."); return;
        }
        var who = s.Target == "self" ? [fx.Caster] : Party.Standing.ToList();
        foreach (var c in who) c.Buffs[s.Name2!] = Math.Max(c.Buffs.GetValueOrDefault(s.Name2!), secs);
        if (s.Name2 is "boundBlade" or "boundBow") Say($"A {s.Name.Replace("Bound ", "").ToLowerInvariant()} of force forms in {fx.Caster.Name}'s hand.");
        else Say($"{s.Name} settles on {(who.Count == 1 ? who[0].Name : "the party")}.");
    }

    private void CastSummon(SpellCast fx)
    {
        var s = fx.Spell;
        var mine = _monsters.Where(m => m.IsAlive && m.Summoned && m.Ally).ToList();
        int limit = fx.Caster.HasPerkFlag("twinSouls") ? 2 : 1;
        foreach (var old in mine.Take(Math.Max(0, mine.Count - limit + 1))) old.State = MonsterState.Dead;
        var spot = new[] { Facing, Facing.Left(), Facing.Right(), Facing.Opposite() }.Select(d => PartyCell.Step(d))
            .FirstOrDefault(c => Level.IsPassable(c) && Level.ChestAt(c) is null && !_monsters.Any(m => m.IsAlive && m.Cell == c));
        if (spot == default)
        {
            Say("There is no room for it to come through.");
            return;
        }
        var def = Bestiary.Scale(Content.Creature(s.Name2!), Math.Max(Content.Creature(s.Name2!).Level, fx.Caster.Level / 2 + 1));
        var a = AddMonster(def, spot, Facing);
        a.Ally = true;
        a.Summoned = true;
        a.State = MonsterState.Aware;
        a.DamageFactor = 1 + fx.Caster.PerkTotalFor("spellPowerPct", "conjuration") / 100;
        a.SummonLeft = s.Seconds * (1 + fx.Caster.PerkTotalFor("durationPct", "conjuration") / 100);
        if (fx.Caster.HasPerkFlag("masterOfGates")) a.Affixes.Add("frenzied");
        Say($"A {a.Def.Name} answers {fx.Caster.Name}'s call.");
        Practice(fx, a.SummonLeft / 4);
    }

    private void CastHazard(SpellCast fx)
    {
        var s = fx.Spell;
        foreach (var cell in Cells(s).Where(c => Level.KindAt(c) != CellKind.Wall))
            _hazards.Add(new Hazard { Cell = cell, Type = s.Type!.Value, Dice = Dice.Parse(s.Dice!), SecondsLeft = s.Seconds, Power = fx.Power, Caster = fx.Skill is null ? fx.Caster : null, School = s.School });
        foreach (var m in Targets(s)) Alert(m);
    }

    private void TickHazards()
    {
        foreach (var h in _hazards.ToList())
        {
            h.SecondsLeft -= TickSeconds;
            h.Since += TickSeconds;
            if (h.Since >= 1)
            {
                h.Since -= 1;
                foreach (var m in MonstersAt(h.Cell).Where(m => !m.Ally).ToList())
                {
                    int dmg = Combat.ApplyAffinity((int)Math.Max(1, h.Dice.Roll(Rng.Combat) * h.Power), m.Resistances.For(h.Type));
                    DamageMonster(m, dmg, h.Type);
                    h.Caster?.UseSkill(h.School, h.Caster.UseXp(h.School, dmg), new XpSource { SpellHadEffect = dmg > 0 });
                }
            }
            if (h.SecondsLeft <= 0) _hazards.Remove(h);
        }
    }

    private void CastRevive(SpellCast fx)
    {
        int n = 0;
        foreach (var c in Party.Members)
        {
            if (fx.Spell.Name2 == "dead" ? c.IsDead : c.IsDown && !c.IsDead)
            {
                c.Revive(0.25);
                n++;
                Say($"{c.Name} rises.");
            }
        }
        if (n == 0) Say("Nobody needs raising.");
        Practice(fx, n * 25);
    }

    private void CastUtility(SpellCast fx)
    {
        var s = fx.Spell;
        var ahead = PartyCell.Step(Facing);
        var target = MonstersAt(ahead).FirstOrDefault(m => !m.Ally);
        switch (s.Name2)
        {
            case "light":
                MagicLight = Math.Max(MagicLight, s.Seconds);
                Say("A cold, steady light floats above the party.");
                break;
            case "dispel":
                if (target is not null)
                {
                    target.Ward = 0;
                    target.Conditions.Clear();
                    target.Affixes.RemoveWhere(a => a is "shielded" or "warded" or "stoneskin");
                    if (target.Summoned) { target.State = MonsterState.Dead; Say($"The {target.Name} is unmade."); }
                    else Say($"The magic on the {target.Name} unravels.");
                }
                int lifted = Party.Members.Sum(c => c.LiftCurses() + (c.Conditions.Cure(ConditionId.Cursed) ? 1 : 0));
                if (lifted > 0) Say("A curse lifts.");
                if (Level.DoorAt(ahead) is { Arcane: true, Locked: true } sealed_) { sealed_.Locked = false; Say("The seal on the door breaks."); }
                DispelHook?.Invoke(ahead);
                break;
            case "sanctuary":
                _sanctuary[ahead] = s.Seconds;
                Say("A line of light bars the way ahead.");
                break;
            case "portal":
                if (Portal is { } p)
                {
                    Stand(p.Cell, p.Facing);
                    EnterCell(PartyCell);
                    Portal = null;
                    Say("The world folds, and the party is back where it marked.");
                }
                else
                {
                    Portal = (PartyCell, Facing);
                    Say("This place is marked. Cast Open the Way again to return.");
                }
                break;
            case "calm":
                if (target is not null && Checks.RollD20(Rng.Combat, Advantage.None) + target.SaveBonus(Attribute.Wis) < fx.Dc
                    && (!target.Def.Has("mindless") || fx.Caster.HasPerkFlag("mindMaster")))
                {
                    target.State = MonsterState.Idle;
                    target.Awareness = 0;
                    target.CalmLeft = s.Seconds * (1 + fx.Caster.PerkTotalFor("durationPct", "illusion") / 100);
                    Say($"The {target.Name} loses interest.");
                    Practice(fx, target.Def.Level + 2);
                }
                else Say("It shrugs off the spell.");
                break;
            case "frenzy":
                if (target is not null && Checks.RollD20(Rng.Combat, Advantage.None) + target.SaveBonus(Attribute.Wis) < fx.Dc
                    && (!target.Def.Has("mindless") || fx.Caster.HasPerkFlag("mindMaster")))
                {
                    target.Ally = true;
                    target.FrenzyLeft = s.Seconds;
                    Say($"The {target.Name} turns on its own kind!");
                    Practice(fx, target.Def.Level + 4);
                }
                else Say("It shrugs off the spell.");
                break;
            case "mageHand":
                if (Level.LeverAt(ahead, Facing) is { } lever) { Pull(lever); break; }
                if (Level.DoorAt(ahead.Step(Facing)) is { Kind: DoorKind.Hand } far) { far.Opening = !far.Opening; Say("An unseen hand works the door."); break; }
                if (Level.DoorAt(ahead) is { Kind: DoorKind.Hand } near) { near.Opening = !near.Opening; break; }
                MageHandHook?.Invoke(ahead);
                break;
            case "detectTraps":
                Detecting = 30;
                foreach (var pit in Level.Pits.Where(p => !p.Spotted && p.Cell.Distance(PartyCell) <= 6)) Level.Spot(pit);
                foreach (var t in Level.Traps.Where(t => !t.Spotted && t.Cell.Distance(PartyCell) <= 6)) t.Spotted = true;
                DetectHook?.Invoke(PartyCell);
                Say("Hidden dangers glimmer faintly for a moment.");
                break;
            case "unlock":
                if (Level.ChestAt(ahead) is { LockDc: > 0 } chest && chest.LockDc <= s.Amount) { chest.LockDc = 0; Say("The lock clicks open."); }
                else if (Level.DoorAt(ahead) is { Locked: true, Arcane: false } ld && ld.LockDc > 0 && ld.LockDc <= s.Amount) { ld.Locked = false; Say("The lock clicks open."); }
                else if (UnlockHook?.Invoke(ahead, s.Amount) != true) Say("Nothing here that simple to open.");
                break;
            case "beacon":
                if (Level.Stairs.Any())
                {
                    var st = Level.Stairs.OrderBy(c => c.Distance(PartyCell)).First();
                    Say($"A whisper: the stairs down lie {Compass(st.X - PartyCell.X, st.Y - PartyCell.Y)}, about {st.Distance(PartyCell)} cells away.");
                }
                else Say("The whisper says nothing.");
                break;
        }
    }

    internal Action<Cell>? DispelHook, MageHandHook, DetectHook;
    internal Func<Cell, int, bool>? UnlockHook;

    private static string Compass(int dx, int dy)
    {
        string ns = dy < 0 ? "north" : dy > 0 ? "south" : "", ew = dx < 0 ? "west" : dx > 0 ? "east" : "";
        if (Math.Abs(dx) > 2 * Math.Abs(dy)) ns = "";
        if (Math.Abs(dy) > 2 * Math.Abs(dx)) ew = "";
        return ns.Length > 0 && ew.Length > 0 ? $"{ns}-{ew}" : ns + ew is { Length: > 0 } one ? one : "right here";
    }

    // ---- Tomes, scrolls and wands ----

    private string? UseSpellItem(Character c, int index)
    {
        if (c.Backpack[index] is not { SpellId: { } sp } item) return null;
        var b = Content.Base(item.BaseId);
        var s = Content.Spell(sp);
        switch (b.Kind)
        {
            case ItemKind.Tome:
                if (c.KnownSpells.Contains(sp)) return $"{c.Name} already knows {s.Name}.";
                if (c.SkillLevel(s.School) < s.Rank) return $"{c.Name} needs {Content.Skill(s.School).Name} {s.Rank} to learn {s.Name}.";
                if (s.NeedsPerk is { } perk && !c.HasPerkFlag(perk)) return $"{s.Name} needs a perk {c.Name} hasn't got.";
                c.KnownSpells.Add(sp);
                c.Backpack[index] = null;
                for (int i = 0; i < c.QuickSlots.Length; i++) if (c.QuickSlots[i] is null) { c.QuickSlots[i] = sp; break; }
                return $"{c.Name} learns {s.Name}.";
            case ItemKind.Scroll:
                if (c.CastingSpell is not null || !c.RightHand.Ready) return $"{c.Name}'s hands aren't free.";
                c.TakeOne(index);
                Begin(c, s, 50, null);
                return $"{c.Name} reads the scroll of {s.Name}.";
            case ItemKind.Wand:
                if (item.Charges < 1) return "The wand is spent.";
                if (c.CastingSpell is not null || !c.RightHand.Ready) return $"{c.Name}'s hands aren't free.";
                item.Charges--;
                Begin(c, s, 40 + c.SkillLevel(SkillId.Enchanting) / 2, item);
                return $"{c.Name} points the wand ({item.Charges:0} charges left).";
        }
        return null;
    }

    /// <summary>The spells each calling starts with (spec 10.1), and the Emberkin's spark.</summary>
    public static IEnumerable<string> StartingSpells(Character c)
    {
        string[] spells = c.Calling.Id switch
        {
            "wizard" => ["spark", "firedart", "ward", "light", "boundBlade", "mageHand"],
            "cleric" => ["mend", "mendMinor", "ward", "guidance"],
            "paladin" => ["mend", "ward", "guidance"],
            "druid" => ["mend", "mendMinor", "summonFamiliar", "frostTouch"],
            "spellblade" => ["spark", "firedart", "minorWard", "frostTouch"],
            "ranger" => ["mendMinor", "detectTraps"],
            "monk" => ["mendMinor"],
            "rogue" => ["unlockTrivial", "light"],
            _ => [],
        };
        foreach (var s in spells) yield return s;
        if (c.HasTrait(Traits.SparkCantrip)) yield return "spark";
    }
}
