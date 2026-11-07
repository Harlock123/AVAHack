using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>Holding the weapon hand this long, then letting go, makes a power attack.</summary>
    public const double PowerHold = 0.5;
    /// <summary>Letting go of the shield sooner than this is a bash; holding it longer is a block.</summary>
    public const double BashTap = 0.25;
    /// <summary>A block raised no more than this long before the blow lands is perfect.</summary>
    public const double PerfectBlockWindow = 0.2;
    public const double PowerStamina = 15;
    public const double BlockStamina = 5;
    public const double BashStamina = 5;
    /// <summary>How long a staggered creature reels.</summary>
    public const double StaggerSeconds = 0.8;
    /// <summary>Seconds with no creature after the party before the fight is over.</summary>
    public const double CalmSeconds = 3;
    /// <summary>The off hand's penalty to hit when fighting with two weapons (spec 9.3).</summary>
    public const int OffHandPenalty = 2;

    private static readonly WeaponDef Fist = new()
    {
        Id = "fist", Name = "Fist", Skill = SkillId.Unarmed, Damage = "1d4", Type = DamageType.Bludgeoning,
        Recovery = 1.2, Text = "Bare knuckles.",
    };

    /// <summary>Unarmed damage grows with the skill (spec 9.3): 1d4 + floor(Unarmed / 20) × 1d4.</summary>
    public static Dice FistDice(Character c) => new(1 + c.SkillLevel(SkillId.Unarmed) / 20, 4);

    /// <summary>Stamina costs after perks: power attacks, blocks and bashes.</summary>
    private static double StaminaCost(Character c, double basic, string scope) =>
        basic * Math.Max(0.1, 1 + (c.PerkTotalFor("staminaCostPct", scope) + (c.Body?.Kind == ArmourKind.Heavy ? c.PerkTotalFor("staminaCostPct", null) : 0)) / 100);

    private readonly List<Projectile> _projectiles = new();
    private double _calm;
    private bool _inFight;

    /// <summary>The first hero's hands (the single-hero parts of the game use these).</summary>
    public Hand LeftHand => Hero.LeftHand;
    public Hand RightHand => Hero.RightHand;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public bool InFight => _inFight;
    /// <summary>Seconds left of the red flash after a hero is hurt.</summary>
    public double HeroHurt { get; private set; }

    /// <summary>The first hero's shield is up.</summary>
    public bool Blocking => IsBlocking(Hero);

    public bool IsBlocking(Character c) => c.LeftHand.Held && c.OffHand is not null && c.Conditions.CanAct && !c.IsDown;

    public Hand HandOf(HandSide side) => HandOf(Hero, side);
    public static Hand HandOf(Character c, HandSide side) => side == HandSide.Left ? c.LeftHand : c.RightHand;

    public void HandDown(HandSide side) => HandDown(Hero, side);
    public void HandUp(HandSide side) => HandUp(Hero, side);

    /// <summary>The hand of hero <paramref name="index"/> (in slot order; 0 is front left) went down.</summary>
    public void HandDown(int index, HandSide side)
    {
        if (Party.At(index) is { } c) HandDown(c, side);
    }

    public void HandUp(int index, HandSide side)
    {
        if (Party.At(index) is { } c) HandUp(c, side);
    }

    /// <summary>A hand's key or button went down. Holding the weapon hand charges a power attack; holding a shield blocks.</summary>
    public void HandDown(Character c, HandSide side)
    {
        var h = HandOf(c, side);
        if (h.Held) return; // key repeat
        h.Held = true;
        h.HeldFor = 0;
    }

    /// <summary>
    /// A hand came up: a weapon swings (a power attack if it was held long enough); a shield bashes if it was
    /// only tapped.
    /// </summary>
    public void HandUp(Character c, HandSide side)
    {
        var h = HandOf(c, side);
        if (!h.Held) return;
        h.Held = false;
        double held = h.HeldFor;
        h.HeldFor = 0;
        if (Mode != GameMode.Playing || Paused || !h.Ready || c.IsDown || !c.Conditions.CanAct) return;
        if (side == HandSide.Right) SwingWeapon(c, c.MainHand ?? Fist, EquipSlot.RightHand, held >= PowerHold);
        else if (c.OffHand is { } shield) { if (held < BashTap) Bash(c, shield); }
        else if (c.OffWeapon is { } off) SwingWeapon(c, off, EquipSlot.LeftHand, held >= PowerHold);
        else if (c.MainHand is null || c.MainHand.Skill == SkillId.Unarmed) SwingWeapon(c, Fist, EquipSlot.LeftHand, false);
    }

    private void TickHands()
    {
        foreach (var c in Party.Members)
        {
            foreach (var h in new[] { c.LeftHand, c.RightHand })
            {
                if (h.Recovery > 0) h.Recovery = Math.Max(0, h.Recovery - TickSeconds);
                if (h.Held) h.HeldFor += TickSeconds;
            }
            if (c.HurtFlash > 0) c.HurtFlash = Math.Max(0, c.HurtFlash - TickSeconds);
        }
        if (HeroHurt > 0) HeroHurt = Math.Max(0, HeroHurt - TickSeconds);
    }

    private void SwingWeapon(Character c, WeaponDef w, EquipSlot hand, bool power)
    {
        var handState = hand == EquipSlot.RightHand ? c.RightHand : c.LeftHand;
        bool ranged = w.Kind == "bow";
        if (!ranged && w.Kind != "reach" && !Party.IsFront(c))
        {
            Say($"{c.Name} can't reach from the back row.");
            return;
        }
        if (power && !c.SpendStamina(StaminaCost(c, PowerStamina, "power")))
        {
            power = false;
            Say($"{c.Name} is too winded for a power attack.");
        }
        handState.Start(w.Recovery * c.RecoveryFactor(w.Skill) * (power ? 1.3 : 1));
        if (power) c.UseSkill(SkillId.Athletics, 0, XpSource.Plain);
        var attr = w.Finesse && c.Mod(Attribute.Dex) > c.Mod(Attribute.Str) ? Attribute.Dex : Attribute.Str;
        int offPenalty = hand == EquipSlot.LeftHand && w != Fist ? OffHandPenalty : 0;
        var dice = w == Fist ? FistDice(c) : Dice.Parse(w.Damage);
        if (c.QualityOf(hand) > 0) dice = dice with { Bonus = dice.Bonus + c.QualityOf(hand) };
        int range = w.Range + (ranged && c.HasPerkFlag("longShot") ? 2 : 0);
        Monster? target = ranged ? FirstInLine(range) : null;
        int crit = w.CritRange - (int)c.PerkTotalFor("critRange", Character.SkillKey(w.Skill));
        double stagger = power ? (w.Skill == SkillId.TwoHanded && c.HasPerkFlag("sweepStagger") ? 100 : 30) : 0;
        _power = power;
        Strike(c, target, w.Skill, attr, dice, w.Type, power ? 1.5 : 1, stagger, crit, -offPenalty, hand, w.Kind);
        // Sweeping blows: Whirl, Ground Slam and Cleave hit the other creatures beside the party too.
        if (power && !ranged && SweepFlag(c, w.Skill))
            foreach (var other in AdjacentFoes().Where(m => m.Cell != PartyCell.Step(Facing)).ToList())
                Strike(c, other, w.Skill, attr, dice, w.Type, 1.5, stagger, crit, -offPenalty, hand, w.Kind);
        if (ranged && c.HasPerkFlag("piercingShot") && target is not null && SecondInLine(target, range) is { } behind)
            Strike(c, behind, w.Skill, attr, dice, w.Type, 1, 0, crit, -offPenalty, hand, w.Kind);
        _power = false;
    }

    private void Bash(Character c, ShieldDef shield)
    {
        if (!Party.IsFront(c))
        {
            Say($"{c.Name} can't reach from the back row.");
            return;
        }
        if (!c.SpendStamina(StaminaCost(c, BashStamina, "block")))
        {
            Say($"{c.Name} is too winded to bash.");
            return;
        }
        c.LeftHand.Start(shield.BashRecovery * c.RecoveryFactor(SkillId.Block));
        Strike(c, null, SkillId.Block, Attribute.Str, Dice.Parse(shield.BashDamage), DamageType.Bludgeoning, 1,
               c.HasPerkFlag("disarmingBash") ? 100 : 20, 20, 0, null, "");
    }

    private bool _power;

    private static bool SweepFlag(Character c, SkillId skill) => skill switch
    {
        SkillId.Blade => c.HasPerkFlag("whirl"),
        SkillId.Blunt => c.HasPerkFlag("groundSlam"),
        SkillId.TwoHanded => c.HasPerkFlag("cleave"),
        _ => false,
    };

    /// <summary>The creatures in the cells beside the party: ahead, left and right.</summary>
    private IEnumerable<Monster> AdjacentFoes() =>
        new[] { Facing, Facing.Left(), Facing.Right() }.SelectMany(d => MonstersAt(PartyCell.Step(d)).Take(1)).Where(m => !m.Ally);

    private Monster? SecondInLine(Monster first, int range)
    {
        var c = first.Cell;
        for (int i = first.Cell.Distance(PartyCell); i < range; i++)
        {
            c = c.Step(Facing);
            if (Level.KindAt(c) == CellKind.Wall) return null;
            if (MonstersAt(c).FirstOrDefault() is { } m) return m;
        }
        return null;
    }

    /// <summary>The first creature straight ahead within <paramref name="range"/> cells, with nothing in between.</summary>
    private Monster? FirstInLine(int range)
    {
        var c = PartyCell;
        for (int i = 0; i < range; i++)
        {
            c = c.Step(Facing);
            if (Level.KindAt(c) == CellKind.Wall || Level.DoorAt(c) is { IsPassable: false, Kind: DoorKind.Hand } || Level.ChestAt(c) is not null) return null;
            if (MonstersAt(c).FirstOrDefault(x => !x.Ally) is { } m) return m;
        }
        return null;
    }

    /// <summary>A blow (or arrow) at <paramref name="target"/>, or at whatever stands in the cell ahead.</summary>
    private void Strike(Character c, Monster? target, SkillId skill, Attribute attr, Dice dice, DamageType type, double multiplier,
                        double staggerChance, int critRange, int toHit, EquipSlot? hand, string weaponKind)
    {
        var ahead = PartyCell.Step(Facing);
        AlertWithin(NoiseRange);
        target ??= MonstersAt(ahead).FirstOrDefault(m => !m.Ally);
        if (target is null)
        {
            if (weaponKind != "bow" && _monsters.FirstOrDefault(m => m.State == MonsterState.Pile && m.Cell == ahead) is { } pile) StrikePile(pile, type);
            return;
        }

        bool unaware = !target.IsAware;
        bool flank = DirTo(target.Cell, PartyCell) != target.Facing;
        bool execute = skill == SkillId.Blade && c.HasPerkFlag("executioner") && target.Hp * 4 < target.MaxHp;
        var adv = flank || unaware || execute || target.AttackedWithAdvantage ? Advantage.Advantage : Advantage.None;
        Alert(target);
        var res = c.Attack(Rng.Combat, skill, attr, target.Ac, adv, toHit);
        bool crit = res.Critical || (res.Hit && res.Roll.Natural >= critRange);
        if (res.Hit && skill == SkillId.Blade && c.HasPerkFlag("riposte") && c.RiposteUntil > Time)
        {
            crit = true;
            c.RiposteUntil = -1;
        }
        var (x, y) = target.Position();
        if (!res.Hit)
        {
            Float?.Invoke(new FloatText(x, y, FloatZ(target), "miss", TextKind.Miss));
            Event?.Invoke(GameEvent.Missed);
            return;
        }
        double sneak = unaware ? SneakMultiplier(c, weaponKind) : 1;
        double mult = multiplier * c.DamageMultiplier(Character.SkillKey(skill)) * c.DamageMultiplier(type.ToString().ToLowerInvariant()) / (1 + c.ItemTotal("damagePct", "all") / 100.0) * sneak;
        if (execute) mult *= 1.5;
        if (type == DamageType.Bludgeoning && target.Def.Family == "restlessDead") mult *= 1 + c.PerkTotalFor("damagePct", "bludgeoningVsUndead") / 100;
        if (skill == SkillId.Unarmed && _power && c.HasPerkFlag("quiveringPalm") && target.Conditions.Has(ConditionId.Stunned)) mult *= 3;
        int mod = c.Mod(attr) - (attr == Attribute.Str ? c.Conditions.StrengthPenalty : 0);
        int dmg = Combat.Damage(Rng.Combat, dice, mod, c.SkillLevel(skill), mult, crit, target.Resistances.For(type));
        if (hand is { } h)
            foreach (var (t, amount) in c.ExtraDamage(h))
                dmg += Combat.ApplyAffinity(amount, target.Resistances.For(t));
        Event?.Invoke(GameEvent.Hit);
        c.UseSkill(skill, dmg, XpSource.Against(target.Def.Level));
        if (unaware) c.UseSkill(SkillId.Stealth, 0, XpSource.Against(target.Def.Level));
        OnHeroHit?.Invoke(c, target, crit);
        PerkOnHit(c, target, skill, crit);
        DamageMonster(target, dmg, type, crit, unaware && sneak > 1);
        if (target.IsAlive && staggerChance > 0 && Rng.Combat.Chance(staggerChance)) Stagger(target);
    }

    /// <summary>What the combat perks do when a blow lands.</summary>
    private void PerkOnHit(Character c, Monster target, SkillId skill, bool crit)
    {
        if (crit && ((skill == SkillId.Blade && c.HasPerkFlag("critBleeds")) || (skill == SkillId.TwoHanded && c.HasPerkFlag("critBleeds2h"))))
            if (!target.Def.ConditionImmune.Contains(ConditionId.Bleeding)) target.Conditions.Apply(ConditionId.Bleeding, 12, 6, Dice.Parse("1d4"));
        if (skill == SkillId.Blunt)
        {
            if (crit && c.HasPerkFlag("crushingBlows")) Stagger(target);
            if (_power && c.HasPerkFlag("skullCracker") && Rng.Combat.Chance(25)) target.Conditions.Apply(ConditionId.Stunned, 12, 1.5);
            if (c.HasPerkFlag("armourBreak") && target.AcBonus > -3) target.AcBonus--;
        }
        if (skill == SkillId.Unarmed && crit && c.HasPerkFlag("stunningFist")) target.Conditions.Apply(ConditionId.Stunned, 12, 2);
        if (skill == SkillId.Archery)
        {
            if (c.HasPerkFlag("pinning") && Rng.Combat.Chance(25)) target.Conditions.Apply(ConditionId.Stunned, 12, 1);
            if (crit && c.HasPerkFlag("bullseye")) target.Conditions.Apply(ConditionId.Paralysed, 12, 2);
        }
        if (skill == SkillId.TwoHanded && _power && c.HasPerkFlag("warcry") && !_warcryUsed)
        {
            _warcryUsed = true;
            foreach (var m in AdjacentFoes().ToList())
                if (!m.Def.Has("mindless")) { m.State = MonsterState.Fleeing; m.FleeLeft = 6; }
            Say($"{c.Name}'s war cry rings off the stones!");
        }
    }

    private bool _warcryUsed;

    /// <summary>Raised after a hero's blow lands, before its damage: perks hook in here.</summary>
    internal event Action<Character, Monster, bool>? OnHeroHit;

    /// <summary>A sneak attack's multiplier: ×2 for anyone, ×3 (×5 with a dagger) for a Rogue, more with Backstab.</summary>
    public static double SneakMultiplier(Character c, string weaponKind)
    {
        double m = 2;
        if (c.Calling.Signature == "sneakAttack") m = weaponKind == "dagger" ? 5 : 3;
        foreach (var (id, _) in c.Perks)
            foreach (var e in c.Content.Perk(id).Effects)
                if (e.Kind == "sneakMultiplier" && (e.Scope == "any" || e.Scope == weaponKind)) m = Math.Max(m, e.Value);
        return m;
    }

    private void StrikePile(Monster pile, DamageType type)
    {
        var (x, y) = pile.Position();
        if (type == DamageType.Bludgeoning)
        {
            pile.State = MonsterState.Dead;
            Kills++;
            Float?.Invoke(new FloatText(x, y, 0.2, "crushed", TextKind.Critical));
            Say($"You grind the {pile.Def.Name}'s bones to splinters. It won't rise again.");
            DropLoot(pile);
            Event?.Invoke(GameEvent.MonsterDied);
        }
        else
        {
            Float?.Invoke(new FloatText(x, y, 0.2, "scattered", TextKind.Miss));
            Say("The bones scatter, but they are still twitching. They need crushing.");
        }
    }

    /// <summary>Damage to a creature, already through its resistances.</summary>
    internal void DamageMonster(Monster m, int dmg, DamageType type, bool critical = false, bool sneak = false)
    {
        dmg = BossShield(m, dmg);
        var (x, y) = m.Position();
        string text = dmg == 0 ? "immune" : sneak ? $"{dmg}!!" : critical ? $"{dmg}!" : dmg.ToString();
        Float?.Invoke(new FloatText(x, y, FloatZ(m), text, critical || sneak ? TextKind.Critical : TextKind.MonsterHurt));
        if (dmg <= 0 || !m.IsAlive) return;
        if (m.Ward > 0)
        {
            int soak = Math.Min(m.Ward, dmg);
            m.Ward -= soak;
            dmg -= soak;
            if (dmg <= 0) return;
        }
        m.Hp -= dmg;
        m.HurtFlash = 0.2;
        m.LastHitType = type;
        m.Conditions.Cure(ConditionId.Asleep);
        Alert(m);
        if (m.Hp > 0) return;

        m.Hp = 0;
        m.Action = MonsterAction.None;
        m.Attack = null;
        var re = m.Def.Special.FirstOrDefault(s => s.Kind == "reassemble");
        if (re is not null && type != re.UnlessKilledBy && !m.Summoned)
        {
            m.State = MonsterState.Pile;
            m.PileLeft = re.Seconds;
            Say($"The {m.Def.Name} collapses into a heap of bones.");
            return;
        }
        m.State = MonsterState.Dead;
        if (m.Ally) { Say($"Your {m.Def.Name} is gone."); return; }
        Kills++;
        Say($"The {m.Name} is slain.");
        if (!(m.Boss == "choir" && m.Raised)) DropLoot(m);
        MonsterDied?.Invoke(m);
        BestiaryDeath(m);
        Event?.Invoke(GameEvent.MonsterDied);
    }

    /// <summary>Raised when a creature is killed (bosses and champions listen).</summary>
    public event Action<Monster>? MonsterDied;

    internal static void Stagger(Monster m)
    {
        if (m.Def.Has("unyielding") || m.Affixes.Contains("unyielding")) return;
        m.Attack = null;
        m.Action = MonsterAction.Staggered;
        m.ActionLeft = m.ActionTotal = StaggerSeconds;
    }

    /// <summary>The tell is over: the blow lands, or the stone flies.</summary>
    private void ReleaseAttack(Monster m)
    {
        var a = m.Attack!;
        m.Attack = null;
        m.Action = MonsterAction.Recovering;
        m.ActionLeft = m.ActionTotal = a.Recovery * m.RecoveryFactor;
        m.StrikeTime = 0.2;
        if (a.Range > 1)
        {
            _projectiles.Add(new Projectile(m, a, m.Facing));
            return;
        }
        var front = m.Cell.Step(m.Facing);
        if (m.Ally || front != PartyCell)
        {
            // An ally strikes the creature ahead of it; a foe whose target moved off whiffs.
            if (MonstersAt(front).FirstOrDefault(o => o.Ally != m.Ally) is { } victim) MonsterHitsMonster(m, a, victim);
            else
            {
                var (x, y) = m.Position();
                Float?.Invoke(new FloatText(x, y, FloatZ(m), "whiff", TextKind.Miss));
            }
            return;
        }
        if (a.Area) foreach (var c in Party.Standing.ToList()) HitHero(m, a, m.Cell, c);
        else HitHero(m, a, m.Cell);
    }

    private void MonsterHitsMonster(Monster attacker, CreatureAttackDef a, Monster victim)
    {
        if (!Combat.Attack(Rng.Combat, a.ToHit + attacker.ToHitBonus, victim.Ac).Hit)
        {
            var (x, y) = victim.Position();
            Float?.Invoke(new FloatText(x, y, FloatZ(victim), "miss", TextKind.Miss));
            return;
        }
        int dmg = (int)Math.Max(1, Dice.Parse(a.Damage).Roll(Rng.Combat) * attacker.DamageFactor);
        DamageMonster(victim, Combat.ApplyAffinity(dmg, victim.Resistances.For(a.Type)), a.Type);
    }

    /// <summary>Which side of the formation a blow from cell <paramref name="from"/> comes from.</summary>
    private RelativeSide SideOf(Cell from)
    {
        var d = DirTo(PartyCell, from);
        if (d == Facing || d is null) return RelativeSide.Ahead;
        if (d == Facing.Left()) return RelativeSide.Left;
        if (d == Facing.Right()) return RelativeSide.Right;
        return RelativeSide.Behind;
    }

    /// <summary>A creature's blow or missile reaches the party, coming from cell <paramref name="from"/>.</summary>
    private void HitHero(Monster m, CreatureAttackDef a, Cell from, Character? target = null)
    {
        var side = SideOf(from);
        var exposed = Party.Exposed(side);
        if (exposed.Count == 0) return;
        var c = target ?? (exposed.Count == 1 ? exposed[0] : exposed[Rng.Combat.Next(exposed.Count)]);
        if (Mirrors > 0 && target is null)
        {
            Mirrors--;
            FloatAtHero("a false self shatters", TextKind.Blocked);
            return;
        }
        var adv = c.Conditions.AttackedWithAdvantage ? Advantage.Advantage : Advantage.None;
        if (Invisible > 0) adv = AdvantageRules.Combine(adv, Advantage.Disadvantage);
        var res = Combat.Attack(Rng.Combat, a.ToHit + m.ToHitBonus, c.ArmourClass, adv);
        if (!res.Hit)
        {
            FloatAtHero("miss", TextKind.Miss);
            return;
        }
        var dice = Dice.Parse(a.Damage);
        int dmg = Math.Max(1, (int)((dice.RollDice(Rng.Combat) + (res.Critical ? dice.RollDice(Rng.Combat) : 0) + dice.Bonus) * m.DamageFactor));
        dmg = Combat.ApplyAffinity(dmg, c.Resistances.For(a.Type));
        dmg = AbsorbWithWard(c, dmg);

        if (side == RelativeSide.Ahead && IsBlocking(c) && dmg > 0)
        {
            int blocked;
            if (c.LeftHand.HeldFor <= PerfectBlockWindow + c.PerkTotal("perfectWindow"))
            {
                blocked = dmg;
                if (a.Range == 1) Stagger(m);
                FloatAtHero("perfect block", TextKind.Blocked);
            }
            else if (c.SpendStamina(StaminaCost(c, BlockStamina, "block")))
            {
                double pct = Math.Min(90, c.OffHand!.BlockPercent + c.SkillLevel(SkillId.Block) / 4.0 + c.PerkTotal("blockPct"));
                blocked = (int)Math.Round(dmg * pct / 100);
                FloatAtHero($"blocked {blocked}", TextKind.Blocked);
            }
            else blocked = 0;
            dmg -= blocked;
            if (blocked > 0)
            {
                c.UseSkill(SkillId.Block, blocked, XpSource.Against(m.Def.Level));
                c.RiposteUntil = Time + 2;
                Event?.Invoke(GameEvent.Blocked);
            }
        }
        if (dmg <= 0) return;
        if (a.Range == 1 && c.ArmourSkill == SkillId.LightArmour && c.HasPerkFlag("dodge") && Rng.Combat.Chance(10))
        {
            FloatAtHero("dodged", TextKind.Blocked);
            return;
        }
        if (a.Range == 1 && c.Body?.Kind == ArmourKind.Heavy && c.HasPerkFlag("reflectBlows") && Rng.Combat.Chance(10))
        {
            DamageMonster(m, dmg, a.Type);
            FloatAtHero("reflected", TextKind.Blocked);
            return;
        }
        if (a.Area && c.HasPerkFlag("evasion")) dmg = Math.Max(1, dmg / 2);
        if (c.HasPerkFlag("avoidDeath") && dmg >= c.Hp && !c.AvoidDeathUsed)
        {
            c.AvoidDeathUsed = true;
            c.Heal(25);
            Say($"{c.Name} refuses to fall!");
        }

        double ward = c.Ward;
        c.TakeDamage(dmg);
        if (ward > c.Ward && c.WardFrom is { } warder) warder.UseSkill(SkillId.Warding, warder.UseXp(SkillId.Warding, ward - c.Ward), XpSource.Plain);
        Concentrate(c, dmg);
        c.HurtFlash = 0.3;
        HeroHurt = 0.3;
        FloatAtHero(res.Critical ? $"-{dmg}!" : $"-{dmg}", TextKind.HeroHurt);
        c.UseSkill(c.ArmourSkill, dmg, XpSource.Against(m.Def.Level));
        Event?.Invoke(GameEvent.HeroHurt);
        if (m.Affixes.Contains("vampiric") || m.Affixes.Contains("bloodthirsty")) m.Hp = Math.Min(m.MaxHp, m.Hp + Math.Max(1, dmg / 2));
        if (a.OnHit is { } on && !c.IsDown && Rng.Combat.Chance(on.Chance)) Afflict(c, on.Condition, on.Dc, on.Seconds);
        foreach (var cond in m.OnHitConditions)
            if (!c.IsDown) Afflict(c, cond, 12 + m.Def.Level / 2, null);
        if (c.IsDown) Say($"{c.Name} falls!");
        if (Party.AllDown) Die();
    }

    /// <summary>A condition that a save may resist, on a hero.</summary>
    internal void Afflict(Character c, ConditionId id, int dc, double? seconds)
    {
        var attr = Content.Condition(id).Save ?? Attribute.Con;
        string? against = id == ConditionId.Poisoned ? "poison" : id is ConditionId.Frightened or ConditionId.Asleep ? "illusion" : null;
        if (!c.Save(Rng.Combat, attr, dc, against: against) && c.ApplyCondition(id, dc, seconds))
            Say($"{c.Name} is {Content.Condition(id).Name.ToLowerInvariant()}!");
    }

    private void TickProjectiles()
    {
        foreach (var p in _projectiles.ToList())
        {
            double step = p.Speed * TickSeconds;
            p.X += p.Dir.Dx() * step;
            p.Y += p.Dir.Dy() * step;
            p.Travelled += step;
            var cell = new Cell((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
            if (cell == p.LastCell) continue;
            p.LastCell = cell;
            if (Level.KindAt(cell) == CellKind.Wall || (Level.DoorAt(cell) is { } d && !d.IsPassable) || p.Travelled > p.Range + 0.5)
            {
                _projectiles.Remove(p);
                p.OnExpire?.Invoke(p.LastCell.Step(p.Dir.Opposite()));
                continue;
            }
            if (cell == PartyCell && !p.FromParty)
            {
                _projectiles.Remove(p);
                if (p.Attack is { } atk && Turning > 0)
                {
                    // Spell Turning: back it goes.
                    var src = p.Source;
                    _projectiles.Add(new Projectile(PartyCell, p.Dir.Opposite())
                    {
                        Look = p.Look, Speed = p.Speed, Range = p.Range,
                        OnHit = (m, _) => DamageMonster(m, Combat.ApplyAffinity(Dice.Parse(atk.Damage).Roll(Rng.Combat), m.Resistances.For(atk.Type)), atk.Type),
                    });
                    FloatAtHero("turned", TextKind.Blocked);
                    continue;
                }
                if (p.Attack is { } atk2) HitHero(p.Source!, atk2, cell.Step(p.Dir.Opposite()));
                continue;
            }
            if (MonstersAt(cell).FirstOrDefault(m => m != p.Source && (!p.FromParty || !m.Ally)) is { } victim)
            {
                _projectiles.Remove(p);
                if (p.OnHit is { } hit) { hit(victim, cell); continue; }
                // Stones don't care whose head they find.
                if (p.Attack is { } atk && Combat.Attack(Rng.Combat, atk.ToHit, victim.Ac).Hit)
                    DamageMonster(victim, Combat.ApplyAffinity(Dice.Parse(atk.Damage).Roll(Rng.Combat), victim.Resistances.For(atk.Type)), atk.Type);
            }
        }
    }

    private void TickParty()
    {
        foreach (var c in Party.Members)
        {
            bool wasDown = c.IsDown;
            int dot = c.Update(TickSeconds, Rng.Combat);
            if (dot > 0) FloatAtHero($"-{dot}", TextKind.HeroHurt);
            if (c.IsDown && !wasDown) Say($"{c.Name} falls!");
        }
        if (Party.AllDown) Die(); // however they fell
    }

    private void UpdateFight(bool anyAware)
    {
        if (anyAware)
        {
            _calm = 0;
            if (!_inFight)
            {
                _inFight = true;
                foreach (var c in Party.Members) c.BeginFight();
            }
        }
        else if (_inFight)
        {
            _calm += TickSeconds;
            if (_calm >= CalmSeconds) EndFight();
        }
    }

    private void EndFight()
    {
        _inFight = false;
        _warcryUsed = false;
        foreach (var c in Party.Members) c.EndFight();
        // The fallen get up with 1 HP if anyone is still standing (spec 9.5).
        if (!Party.AllDown)
            foreach (var c in Party.Members.Where(c => c.IsDown && !c.IsDead))
            {
                c.GetUp();
                Say($"{c.Name} staggers back up.");
            }
    }

    /// <summary>A blow is loud: anything within <paramref name="range"/> cells hears it, through doors and round corners.</summary>
    private void AlertWithin(int range)
    {
        foreach (var m in _monsters)
            if (m.IsAlive && !m.IsAware && !m.Ally && m.Cell.Distance(PartyCell) <= range)
                Alert(m);
    }

    private void Die()
    {
        if (Mode != GameMode.Playing) return;
        Mode = GameMode.Dead;
        Say(Party.Count == 1 ? $"{Hero.Name} has fallen in the dark." : "The whole party has fallen in the dark.");
        GameOver?.Invoke();
    }

    /// <summary>Raised when the party is wiped out (Ironman deletes its save).</summary>
    public event Action? GameOver;

    /// <summary>Where over a creature its numbers appear: above a rat's back, or a goblin's head.</summary>
    private static double FloatZ(Monster m) => m.IsSmall ? 0.4 : m.Def.Size == CreatureSize.Large ? 1.0 : 0.8;

    private void FloatAtHero(string text, TextKind kind)
    {
        var cam = Camera();
        Float?.Invoke(new FloatText(cam.X + cam.DirX * 0.6, cam.Y + cam.DirY * 0.6, 0.35, text, kind));
    }
}
