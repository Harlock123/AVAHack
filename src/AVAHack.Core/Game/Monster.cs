using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public enum MonsterState { Idle, Aware, Fleeing, Pile, Dead }

public enum MonsterAction { None, Stepping, Turning, WindingUp, Recovering, Staggered, Opening }

/// <summary>
/// A creature in the level. Like the party it holds a grid cell and a facing; it steps, turns, winds up an
/// attack (the tell) and recovers. Small creatures share a cell, up to four, each in its own corner
/// (<see cref="Slot"/>).
/// </summary>
public sealed class Monster
{
    public int Id { get; }
    public CreatureDef Def { get; }
    public Cell Cell { get; internal set; }
    public Dir Facing { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; internal set; }
    public MonsterState State { get; internal set; } = MonsterState.Idle;
    public int Slot { get; internal set; }
    public Resistances Resistances { get; }
    public ConditionSet Conditions { get; }

    /// <summary>Fights for the party (a summon or a frenzied foe turned).</summary>
    public bool Ally { get; internal set; }
    /// <summary>Called up by a spell; it fades when its time runs out.</summary>
    public bool Summoned { get; internal set; }
    public double SummonLeft { get; internal set; }
    /// <summary>A champion's affixes (spec 12.2), and an elite's tint.</summary>
    public HashSet<string> Affixes { get; } = new();
    public bool Elite { get; internal set; }
    /// <summary>A champion's name ("Grisk the Vampiric"); otherwise the creature's.</summary>
    public string Name => Title ?? Def.Name;
    /// <summary>The name to start a sentence with: "The Bone Sentry", but "The Unmaker" (not "The The Unmaker").</summary>
    public string TheName => WithThe(Name);
    /// <summary>The same for the kind of creature, ignoring a champion's title.</summary>
    public string TheKind => WithThe(Def.Name);
    private static string WithThe(string n) => n.StartsWith("The ", StringComparison.Ordinal) ? n : "The " + n;
    public string? Title { get; internal set; }
    /// <summary>Damage soaked before HP, from a ward or a Shielded affix.</summary>
    public int Ward { get; internal set; }
    public int AcBonus { get; internal set; }
    public int Ac => Def.Ac + AcBonus;
    public int ToHitBonus { get; internal set; }
    /// <summary>The Delve's monster strength and the creature's own (elite, champion).</summary>
    public double DamageFactor { get; internal set; } = 1;
    /// <summary>A boss's script, if it is one.</summary>
    public string? Boss { get; internal set; }
    internal double AffixTimer;
    internal int Phase;
    /// <summary>A second timer for summoning, raising, a boss's cycle and a singer's return.</summary>
    internal double SpecialTimer;
    /// <summary>The creature that called this one up (summoners), or 0.</summary>
    internal int SummonedBy;
    /// <summary>Raised already by a Spore Shepherd, so it can't be raised twice.</summary>
    internal bool Raised;
    /// <summary>Half of a creature that split; it won't split again.</summary>
    internal bool SplitHalf;

    /// <summary>Invisible creatures (Gloomstalker, Mirror Ooze) show only within 2 cells, or while hurt.</summary>
    public bool HiddenFrom(Cell party) => Def.Has("invisible") && IsAlive && HurtFlash <= 0 && Cell.Distance(party) > 2;

    /// <summary>How long recoveries take: Frenzied champions are quicker, Chilled ones slower.</summary>
    public double RecoveryFactor => (Affixes.Contains("frenzied") ? 0.6 : 1) * Conditions.RecoveryFactor;
    public bool AttackedWithAdvantage => Conditions.AttackedWithAdvantage;

    /// <summary>Conditions its blows carry because of its affixes.</summary>
    public IEnumerable<ConditionId> OnHitConditions
    {
        get
        {
            if (Affixes.Contains("venomous")) yield return ConditionId.Poisoned;
            if (Affixes.Contains("frostbound")) yield return ConditionId.Chilled;
            if (Affixes.Contains("thunderous")) yield return ConditionId.Shocked;
        }
    }

    public MonsterAction Action { get; internal set; }
    internal double ActionLeft;
    internal double ActionTotal;
    public Cell From { get; internal set; }
    public CreatureAttackDef? Attack { get; internal set; }

    /// <summary>Seconds left of the red flash after a hit.</summary>
    public double HurtFlash { get; internal set; }
    /// <summary>While the party sneaks: how close this creature is to noticing it, 0 to 1 (the eye over its head).</summary>
    public double Awareness { get; internal set; }
    /// <summary>When it last saw (or heard) the party, in game seconds.</summary>
    public double LastSeen { get; internal set; } = -100;
    /// <summary>Seconds left of Calm: it won't notice the party unless hurt.</summary>
    public double CalmLeft { get; internal set; }
    /// <summary>Seconds left of Hush (spec 10.3): no song, spell, shot or summons, only its own hands.</summary>
    public double HushLeft { get; internal set; }
    /// <summary>Seconds left of Frenzy: it fights its own side.</summary>
    public double FrenzyLeft { get; internal set; }
    /// <summary>Seconds left of the lunge after a blow lands.</summary>
    public double StrikeTime { get; internal set; }
    /// <summary>Seconds until a pile of bones stands up again.</summary>
    public double PileLeft { get; internal set; }
    public bool FellInPit { get; internal set; }
    internal double FleeLeft;
    internal double IdleLeft;
    internal DamageType? LastHitType;
    public double ActionLeftSeconds => ActionLeft;

    public bool IsAlive => State is MonsterState.Idle or MonsterState.Aware or MonsterState.Fleeing;
    public bool IsAware => State is MonsterState.Aware or MonsterState.Fleeing;
    public bool IsSmall => Def.Size == CreatureSize.Small;

    /// <summary>How far through its current action it is, 0 to 1.</summary>
    public double ActionProgress => ActionTotal <= 0 ? 1 : Math.Clamp(1 - ActionLeft / ActionTotal, 0, 1);

    internal Monster(int id, CreatureDef def, Cell cell, Dir facing, int hp, ContentDb content)
    {
        Conditions = new ConditionSet(content);
        Id = id;
        Def = def;
        Cell = From = cell;
        Facing = facing;
        Hp = MaxHp = Math.Max(1, hp);
        Resistances = new Resistances().Resist(def.Resist).Vulnerable(def.Vulnerable).Immune(def.Immune);
    }

    /// <summary>Where to draw it, <paramref name="alpha"/> ticks into the next tick, in world units.</summary>
    public (double X, double Y) Position(double alpha = 0)
    {
        double t = Action == MonsterAction.Stepping
            ? Math.Clamp(1 - (ActionLeft - alpha * GameEngine.TickSeconds) / ActionTotal, 0, 1)
            : 1;
        t = t * t * (3 - 2 * t);
        double x = From.X + (Cell.X - From.X) * t + 0.5, y = From.Y + (Cell.Y - From.Y) * t + 0.5;
        if (IsSmall)
        {
            x += (Slot & 1) == 0 ? -0.2 : 0.2;
            y += (Slot & 2) == 0 ? -0.2 : 0.2;
        }
        if (StrikeTime > 0)
        {
            double lunge = Math.Sin(Math.PI * StrikeTime / 0.2) * 0.18;
            x += Facing.Dx() * lunge;
            y += Facing.Dy() * lunge;
        }
        return (x, y);
    }

    public int SaveBonus(Rules.Attribute a) => Def.Saves.TryGetValue(a.ToString().ToLowerInvariant(), out int v) ? v : 0;

    public override string ToString() => $"{Def.Name} #{Id} at {Cell} facing {Facing} ({State}, {Hp}/{MaxHp})";
}

/// <summary>A thrown stone, an arrow or a spell, flying straight along a row or column of the grid.</summary>
public sealed class Projectile
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public Dir Dir { get; }
    public Monster? Source { get; }
    public CreatureAttackDef? Attack { get; }
    /// <summary>How it looks: stone, arrow, fire, frost, spark, acid, light, dark.</summary>
    public string Look { get; init; } = "stone";
    public double Speed { get; init; } = 6;
    public double Range { get; init; } = 6;
    public bool FromParty { get; init; }
    /// <summary>What happens when it reaches a creature (spells), instead of a creature's attack.</summary>
    internal Action<Monster, Cell>? OnHit { get; init; }
    /// <summary>What happens when it stops against a wall or runs out (spell blasts).</summary>
    internal Action<Cell>? OnExpire { get; init; }
    internal double Travelled;
    internal Cell LastCell;

    internal Projectile(Monster source, CreatureAttackDef attack, Dir dir)
    {
        Source = source;
        Attack = attack;
        Dir = dir;
        Speed = attack.ProjectileSpeed;
        Range = attack.Range;
        X = source.Cell.X + 0.5;
        Y = source.Cell.Y + 0.5;
        LastCell = source.Cell;
    }

    /// <summary>A projectile from the party's cell.</summary>
    internal Projectile(Cell from, Dir dir)
    {
        Dir = dir;
        FromParty = true;
        X = from.X + 0.5;
        Y = from.Y + 0.5;
        LastCell = from;
    }
}

public enum HandSide { Left, Right }

/// <summary>One of the hero's hands: what it's recovering from, and whether it's held down.</summary>
public sealed class Hand
{
    public double Recovery { get; internal set; }
    public double RecoveryTotal { get; internal set; }
    public bool Held { get; internal set; }
    public double HeldFor { get; internal set; }
    public bool Ready => Recovery <= 0;

    /// <summary>How much of the recovery is left, 1 (just used) to 0 (ready), for the cooldown sweep.</summary>
    public double RecoveryShare => RecoveryTotal <= 0 ? 0 : Math.Clamp(Recovery / RecoveryTotal, 0, 1);

    internal void Start(double seconds)
    {
        Recovery = RecoveryTotal = seconds;
    }
}

public enum TextKind { MonsterHurt, HeroHurt, Miss, Blocked, Critical, Info }

/// <summary>A number or word to float up from a place in the world for a moment.</summary>
public readonly record struct FloatText(double X, double Y, double Z, string Text, TextKind Kind);
