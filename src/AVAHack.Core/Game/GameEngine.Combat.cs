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

    private static readonly WeaponDef Fist = new()
    {
        Id = "fist", Name = "Fist", Skill = SkillId.Unarmed, Damage = "1d2", Type = DamageType.Bludgeoning,
        Recovery = 1.2, Text = "Bare knuckles.",
    };

    private readonly List<Projectile> _projectiles = new();
    private double _calm;

    public Hand LeftHand { get; } = new();
    public Hand RightHand { get; } = new();
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public bool InFight => Hero.InFight;
    /// <summary>Seconds left of the red flash after the hero is hurt.</summary>
    public double HeroHurt { get; private set; }

    /// <summary>The shield is up: the left hand is held and there is a shield on it.</summary>
    public bool Blocking => LeftHand.Held && Hero.OffHand is not null && Hero.Conditions.CanAct && !Hero.IsDown;

    public Hand HandOf(HandSide side) => side == HandSide.Left ? LeftHand : RightHand;

    /// <summary>A hand's key or button went down. Holding the weapon hand charges a power attack; holding the shield blocks.</summary>
    public void HandDown(HandSide side)
    {
        var h = HandOf(side);
        if (h.Held) return; // key repeat
        h.Held = true;
        h.HeldFor = 0;
    }

    /// <summary>
    /// A hand's key or button came up: the weapon hand swings (a power attack if it was held long enough); the
    /// shield hand bashes if it was only tapped.
    /// </summary>
    public void HandUp(HandSide side)
    {
        var h = HandOf(side);
        if (!h.Held) return;
        h.Held = false;
        double held = h.HeldFor;
        h.HeldFor = 0;
        if (Mode != GameMode.Playing || Paused || !h.Ready || Hero.IsDown || !Hero.Conditions.CanAct) return;
        if (side == HandSide.Right) SwingWeapon(held >= PowerHold);
        else if (Hero.OffHand is { } shield && held < BashTap) Bash(shield);
    }

    private void TickHands()
    {
        foreach (var h in new[] { LeftHand, RightHand })
        {
            if (h.Recovery > 0) h.Recovery = Math.Max(0, h.Recovery - TickSeconds);
            if (h.Held) h.HeldFor += TickSeconds;
        }
        if (HeroHurt > 0) HeroHurt = Math.Max(0, HeroHurt - TickSeconds);
    }

    private void SwingWeapon(bool power)
    {
        var w = Hero.MainHand ?? Fist;
        if (power && !Hero.SpendStamina(PowerStamina))
        {
            power = false;
            Say("Too winded for a power attack.");
        }
        RightHand.Start(w.Recovery * Hero.RecoveryFactor(w.Skill) * (power ? 1.3 : 1));
        if (power) Hero.UseSkill(SkillId.Athletics, 0, XpSource.Plain);
        var attr = w.Finesse && Hero.Mod(Attribute.Dex) > Hero.Mod(Attribute.Str) ? Attribute.Dex : Attribute.Str;
        Strike(w.Skill, attr, Dice.Parse(w.Damage), w.Type, power ? 1.5 : 1, power ? 30 : 0);
    }

    private void Bash(ShieldDef shield)
    {
        if (!Hero.SpendStamina(BashStamina))
        {
            Say("Too winded to bash.");
            return;
        }
        LeftHand.Start(shield.BashRecovery * Hero.RecoveryFactor(SkillId.Block));
        Strike(SkillId.Block, Attribute.Str, Dice.Parse(shield.BashDamage), DamageType.Bludgeoning, 1, 20);
    }

    /// <summary>A blow at whatever stands in the cell ahead.</summary>
    private void Strike(SkillId skill, Attribute attr, Dice dice, DamageType type, double multiplier, double staggerChance)
    {
        var ahead = PartyCell.Step(Facing);
        AlertWithin(NoiseRange);
        var target = MonstersAt(ahead).FirstOrDefault();
        if (target is null)
        {
            if (_monsters.FirstOrDefault(m => m.State == MonsterState.Pile && m.Cell == ahead) is { } pile) StrikePile(pile, type);
            return;
        }

        bool unaware = !target.IsAware;
        bool flank = DirTo(target.Cell, PartyCell) != target.Facing;
        var adv = flank || unaware ? Advantage.Advantage : Advantage.None;
        Alert(target);
        var res = Hero.Attack(Rng.Combat, skill, attr, target.Def.Ac, adv);
        var (x, y) = target.Position();
        if (!res.Hit)
        {
            Float?.Invoke(new FloatText(x, y, FloatZ(target), "miss", TextKind.Miss));
            Event?.Invoke(GameEvent.Missed);
            return;
        }
        double mult = multiplier * Hero.DamageMultiplier(Character.SkillKey(skill)) * (unaware ? 2 : 1);
        int mod = Hero.Mod(attr) - (attr == Attribute.Str ? Hero.Conditions.StrengthPenalty : 0);
        int dmg = Combat.Damage(Rng.Combat, dice, mod, Hero.Rank(skill), mult, res.Critical, target.Resistances.For(type));
        Event?.Invoke(GameEvent.Hit);
        Hero.UseSkill(skill, dmg, XpSource.Against(target.Def.Level));
        DamageMonster(target, dmg, type, res.Critical, unaware);
        if (target.IsAlive && staggerChance > 0 && Rng.Combat.Chance(staggerChance)) Stagger(target);
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
            Event?.Invoke(GameEvent.MonsterDied);
        }
        else
        {
            Float?.Invoke(new FloatText(x, y, 0.2, "scattered", TextKind.Miss));
            Say("The bones scatter, but they are still twitching. They need crushing.");
        }
    }

    /// <summary>Damage to a creature, already through its resistances.</summary>
    private void DamageMonster(Monster m, int dmg, DamageType type, bool critical = false, bool sneak = false)
    {
        var (x, y) = m.Position();
        string text = dmg == 0 ? "immune" : sneak ? $"{dmg}!!" : critical ? $"{dmg}!" : dmg.ToString();
        Float?.Invoke(new FloatText(x, y, FloatZ(m), text, critical || sneak ? TextKind.Critical : TextKind.MonsterHurt));
        if (dmg <= 0) return;
        m.Hp -= dmg;
        m.HurtFlash = 0.2;
        m.LastHitType = type;
        Alert(m);
        if (m.Hp > 0) return;

        m.Hp = 0;
        m.Action = MonsterAction.None;
        m.Attack = null;
        var re = m.Def.Special.FirstOrDefault(s => s.Kind == "reassemble");
        if (re is not null && type != re.UnlessKilledBy)
        {
            m.State = MonsterState.Pile;
            m.PileLeft = re.Seconds;
            Say($"The {m.Def.Name} collapses into a heap of bones.");
            return;
        }
        m.State = MonsterState.Dead;
        Kills++;
        Say($"The {m.Def.Name} is slain.");
        Event?.Invoke(GameEvent.MonsterDied);
    }

    private static void Stagger(Monster m)
    {
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
        m.ActionLeft = m.ActionTotal = a.Recovery;
        m.StrikeTime = 0.2;
        if (a.Range > 1)
        {
            _projectiles.Add(new Projectile(m, a, m.Facing));
            return;
        }
        if (m.Cell.Step(m.Facing) != PartyCell)
        {
            // The party stepped out of reach during the tell.
            var (x, y) = m.Position();
            Float?.Invoke(new FloatText(x, y, FloatZ(m), "whiff", TextKind.Miss));
            return;
        }
        HitHero(m, a, m.Cell);
    }

    /// <summary>A creature's blow or missile reaches the hero, coming from cell <paramref name="from"/>.</summary>
    private void HitHero(Monster m, CreatureAttackDef a, Cell from)
    {
        var adv = Hero.Conditions.AttackedWithAdvantage ? Advantage.Advantage : Advantage.None;
        var res = Combat.Attack(Rng.Combat, a.ToHit, Hero.ArmourClass, adv);
        if (!res.Hit)
        {
            FloatAtHero("miss", TextKind.Miss);
            return;
        }
        var dice = Dice.Parse(a.Damage);
        int dmg = Math.Max(1, dice.RollDice(Rng.Combat) + (res.Critical ? dice.RollDice(Rng.Combat) : 0) + dice.Bonus);
        dmg = Combat.ApplyAffinity(dmg, Hero.Resistances.For(a.Type));

        bool fromFront = from == PartyCell.Step(Facing) || (a.Range > 1 && DirTo(PartyCell, from) == Facing);
        if (Blocking && fromFront && dmg > 0)
        {
            int blocked;
            if (LeftHand.HeldFor <= PerfectBlockWindow)
            {
                blocked = dmg;
                if (a.Range == 1) Stagger(m);
                FloatAtHero("perfect block", TextKind.Blocked);
            }
            else if (Hero.SpendStamina(BlockStamina))
            {
                double pct = Math.Min(90, Hero.OffHand!.BlockPercent + Hero.Rank(SkillId.Block) / 4.0);
                blocked = (int)Math.Round(dmg * pct / 100);
                FloatAtHero($"blocked {blocked}", TextKind.Blocked);
            }
            else blocked = 0;
            dmg -= blocked;
            if (blocked > 0)
            {
                Hero.UseSkill(SkillId.Block, blocked, XpSource.Against(m.Def.Level));
                Event?.Invoke(GameEvent.Blocked);
            }
        }
        if (dmg <= 0) return;

        Hero.TakeDamage(dmg);
        HeroHurt = 0.3;
        FloatAtHero(res.Critical ? $"-{dmg}!" : $"-{dmg}", TextKind.HeroHurt);
        Hero.UseSkill(Hero.ArmourSkill, dmg, XpSource.Against(m.Def.Level));
        Event?.Invoke(GameEvent.HeroHurt);
        if (a.OnHit is { } on && !Hero.IsDown && Rng.Combat.Chance(on.Chance))
        {
            var attr = Content.Condition(on.Condition).Save ?? Attribute.Con;
            string? against = on.Condition == ConditionId.Poisoned ? "poison" : null;
            if (!Hero.Save(Rng.Combat, attr, on.Dc, against: against) && Hero.ApplyCondition(on.Condition, on.Dc, on.Seconds))
                Say($"You are {Content.Condition(on.Condition).Name.ToLowerInvariant()}!");
        }
        if (Hero.IsDown) Die();
    }

    private void TickProjectiles()
    {
        foreach (var p in _projectiles.ToList())
        {
            double step = p.Attack.ProjectileSpeed * TickSeconds;
            p.X += p.Dir.Dx() * step;
            p.Y += p.Dir.Dy() * step;
            p.Travelled += step;
            var cell = new Cell((int)Math.Floor(p.X), (int)Math.Floor(p.Y));
            if (cell == p.LastCell) continue;
            p.LastCell = cell;
            if (Level.KindAt(cell) == CellKind.Wall || (Level.DoorAt(cell) is { } d && !d.IsPassable) || p.Travelled > p.Attack.Range + 0.5)
            {
                _projectiles.Remove(p);
                continue;
            }
            if (cell == PartyCell)
            {
                _projectiles.Remove(p);
                HitHero(p.Source, p.Attack, cell.Step(p.Dir.Opposite()));
                continue;
            }
            if (MonstersAt(cell).FirstOrDefault(m => m != p.Source) is { } victim)
            {
                // Stones don't care whose head they find.
                _projectiles.Remove(p);
                if (Combat.Attack(Rng.Combat, p.Attack.ToHit, victim.Def.Ac).Hit)
                    DamageMonster(victim, Combat.ApplyAffinity(Dice.Parse(p.Attack.Damage).Roll(Rng.Combat), victim.Resistances.For(p.Attack.Type)), p.Attack.Type);
            }
        }
    }

    private void TickHero()
    {
        int dot = Hero.Update(TickSeconds, Rng.Combat);
        if (dot > 0) FloatAtHero($"-{dot}", TextKind.HeroHurt);
        if (Hero.IsDown) Die(); // however the hero fell
    }

    private void UpdateFight(bool anyAware)
    {
        if (anyAware)
        {
            _calm = 0;
            if (!Hero.InFight) Hero.BeginFight();
        }
        else if (Hero.InFight)
        {
            _calm += TickSeconds;
            if (_calm >= CalmSeconds) Hero.EndFight();
        }
    }

    /// <summary>A blow is loud: anything within <paramref name="range"/> cells hears it, through doors and round corners.</summary>
    private void AlertWithin(int range)
    {
        foreach (var m in _monsters)
            if (m.IsAlive && !m.IsAware && m.Cell.Distance(PartyCell) <= range)
                Alert(m);
    }

    private void Die()
    {
        if (Mode != GameMode.Playing) return;
        Mode = GameMode.Dead;
        Say($"{Hero.Name} has fallen in the dark.");
    }

    /// <summary>Where over a creature its numbers appear: above a rat's back, or a goblin's head.</summary>
    private static double FloatZ(Monster m) => m.IsSmall ? 0.4 : 0.8;

    private void FloatAtHero(string text, TextKind kind)
    {
        var cam = Camera();
        Float?.Invoke(new FloatText(cam.X + cam.DirX * 0.6, cam.Y + cam.DirY * 0.6, 0.35, text, kind));
    }
}
