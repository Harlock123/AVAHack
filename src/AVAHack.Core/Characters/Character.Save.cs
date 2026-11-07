using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.Saves;

namespace AVAHack.Characters;

public sealed partial class Character
{
    public CharacterSave ToSave() => new()
    {
        Name = Name, Ancestry = Ancestry.Id, Calling = Calling.Id, Background = Background.Id,
        BaseAttributes = Enum.GetValues<Rules.Attribute>().Select(a => BaseAttributes[a]).ToArray(),
        Increases = Enum.GetValues<Rules.Attribute>().Select(a => _increases[a]).ToArray(),
        IncreasesThisLevel = (int[])_increasesThisLevel.Clone(),
        Level = Level, CharacterXp = CharacterXp, PerkPoints = PerkPoints, AttributePoints = AttributePoints,
        PendingLevelChoices = PendingLevelChoices, HealthChoices = HealthChoices, StaminaChoices = StaminaChoices,
        ManaChoices = ManaChoices, RecoverySpeedUps = RecoverySpeedUps, Respecs = Respecs, LessonsThisLevel = LessonsThisLevel,
        ExtraSkillXpPercent = ExtraSkillXpPercent - (LoneDelver ? 100 : 0), LoneDelver = LoneDelver,
        Hp = Hp, Stamina = Stamina, Mana = Mana, Ward = Ward, IsDead = IsDead,
        Ranks = (int[])_rank.Clone(), Xp = (double[])_xp.Clone(),
        Perks = new Dictionary<string, int>(_perks), BooksRead = _booksRead.Order(StringComparer.Ordinal).ToList(),
        Conditions = SaveConditions(Conditions),
        LeftHand = new HandSave(LeftHand.Recovery, LeftHand.RecoveryTotal),
        RightHand = new HandSave(RightHand.Recovery, RightHand.RecoveryTotal),
        InFight = InFight, LuckUsed = LuckUsed, RelentlessUsed = RelentlessUsed,
        RanksThisFight = (int[])_ranksThisFight.Clone(), RiposteUntil = RiposteUntil,
        Buffs = new Dictionary<string, double>(Buffs), AvoidDeathUsed = AvoidDeathUsed,
        Equipped = new Dictionary<EquipSlot, Items.Item>(_equipped),
        Backpack = (Items.Item?[])Backpack.Clone(),
        Spells = KnownSpells.ToList(),
        QuickSlots = (string?[])QuickSlots.Clone(),
        Food = Food, Water = Water, Sneaking = Sneaking,
    };

    public static Character FromSave(ContentDb content, CharacterSave s)
    {
        var attrs = new AttributeSet();
        var all = Enum.GetValues<Rules.Attribute>();
        for (int i = 0; i < 6; i++) attrs[all[i]] = s.BaseAttributes[i];
        var c = new Character(content, s.Name, content.Ancestry(s.Ancestry), content.Calling(s.Calling), content.Background(s.Background), attrs);
        for (int i = 0; i < 6; i++) c._increases[all[i]] = s.Increases[i];
        Array.Copy(s.IncreasesThisLevel, c._increasesThisLevel, Math.Min(6, s.IncreasesThisLevel.Length));
        c.Level = s.Level;
        c.CharacterXp = s.CharacterXp;
        c.PerkPoints = s.PerkPoints;
        c.AttributePoints = s.AttributePoints;
        c.PendingLevelChoices = s.PendingLevelChoices;
        c.HealthChoices = s.HealthChoices;
        c.StaminaChoices = s.StaminaChoices;
        c.ManaChoices = s.ManaChoices;
        c.RecoverySpeedUps = s.RecoverySpeedUps;
        c.Respecs = s.Respecs;
        c.LessonsThisLevel = s.LessonsThisLevel;
        c._lone = s.LoneDelver;
        c.ExtraSkillXpPercent = s.ExtraSkillXpPercent + (s.LoneDelver ? 100 : 0);
        Array.Copy(s.Ranks, c._rank, Math.Min(c._rank.Length, s.Ranks.Length));
        Array.Copy(s.Xp, c._xp, Math.Min(c._xp.Length, s.Xp.Length));
        foreach (var (k, v) in s.Perks) c._perks[k] = v;
        foreach (var b in s.BooksRead) c._booksRead.Add(b);
        foreach (var (slot, item) in s.Equipped) c._equipped[slot] = item;
        for (int i = 0; i < Math.Min(BackpackSize, s.Backpack.Length); i++) c.Backpack[i] = s.Backpack[i];
        LoadConditions(c.Conditions, s.Conditions);
        if (s.LeftHand is { } lh) { c.LeftHand.Recovery = lh.Recovery; c.LeftHand.RecoveryTotal = lh.Total; }
        if (s.RightHand is { } rh) { c.RightHand.Recovery = rh.Recovery; c.RightHand.RecoveryTotal = rh.Total; }
        c.InFight = s.InFight;
        c.LuckUsed = s.LuckUsed;
        c.RelentlessUsed = s.RelentlessUsed;
        c.RiposteUntil = s.RiposteUntil;
        foreach (var (k, v) in s.Buffs) c.Buffs[k] = v;
        c.AvoidDeathUsed = s.AvoidDeathUsed;
        Array.Copy(s.RanksThisFight, c._ranksThisFight, Math.Min(c._ranksThisFight.Length, s.RanksThisFight.Length));
        foreach (var sp in s.Spells) c.KnownSpells.Add(sp);
        for (int i = 0; i < Math.Min(c.QuickSlots.Length, s.QuickSlots.Length); i++) c.QuickSlots[i] = s.QuickSlots[i];
        c.Food = s.Food;
        c.Water = s.Water;
        c.Sneaking = s.Sneaking;
        c.IsDead = s.IsDead;
        c.Hp = s.Hp;
        c.Stamina = s.Stamina;
        c.Mana = s.Mana;
        c.Ward = s.Ward;
        return c;
    }

    public static List<ConditionSave> SaveConditions(ConditionSet set) =>
        set.Active.Select(c => new ConditionSave(c.Id, c.SecondsLeft, c.SaveDc, c.DamagePerSecond?.ToString(), c.SinceSave, c.SinceDamage)).ToList();

    public static void LoadConditions(ConditionSet set, IEnumerable<ConditionSave> saved)
    {
        foreach (var cond in saved)
        {
            set.Apply(cond.Id, cond.Dc, cond.SecondsLeft, cond.DamagePerSecond is { } d ? Dice.Parse(d) : null);
            if (set.Get(cond.Id) is { } a)
            {
                a.SecondsLeft = cond.SecondsLeft;
                a.SinceSave = cond.SinceSave;
                a.SinceDamage = cond.SinceDamage;
            }
        }
    }

    // Fields that later systems use, kept here so saves carry them from the start.

    /// <summary>Spells this hero knows (spec 10.1).</summary>
    public HashSet<string> KnownSpells { get; } = new();
    /// <summary>Quick slots 1–8: spell ids.</summary>
    public string?[] QuickSlots { get; } = new string?[8];
    /// <summary>Food and water, 0–100 (spec 13.6).</summary>
    public double Food { get; internal set; } = 100;
    public double Water { get; internal set; } = 100;
    /// <summary>Below this, food or water, a hero is hungry or thirsty: half regeneration, disadvantage on CON saves.</summary>
    public const double HungryBelow = 25;
    public bool Hungry => Food < HungryBelow || Water < HungryBelow;
    /// <summary>The spell being cast, and how long until it goes off.</summary>
    public string? CastingSpell { get; internal set; }
    public double CastLeft { get; internal set; }
    public double CastTotal { get; internal set; }
    /// <summary>A scroll or wand casts at this skill instead of the hero's own (no Mana).</summary>
    internal int? CastSkill;
    internal Items.Item? CastItem;
    /// <summary>Timed spell effects on this hero: bulwark, guidance, resistElements, regen, muffle, boundBlade, boundBow.</summary>
    public Dictionary<string, double> Buffs { get; } = new();
    /// <summary>Who cast the ward this hero carries, for their Warding XP.</summary>
    internal Character? WardFrom;

    /// <summary>Avoid Death's once-a-rest save.</summary>
    public bool AvoidDeathUsed { get; internal set; }

    /// <summary>Moving in sneak mode (spec 11.1).</summary>
    public bool Sneaking { get; internal set; }
}
