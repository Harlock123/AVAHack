using AVAHack.Content;
using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Characters;

/// <summary>What a character wears, for armour class. Unarmoured is no armour and no DEX cap.</summary>
public readonly record struct ArmourWorn(int Armour = 0, int? DexCap = null, int Shield = 0, int Other = 0)
{
    public static ArmourWorn None => new();
}

/// <summary>
/// One hero: ancestry, calling and background; attributes; the 22 skills and their XP; level, perks and the
/// level-up choices; HP, Stamina and Mana; conditions. The rules of spec sections 5–8 live here and in
/// <see cref="Progression"/>, <see cref="Checks"/> and <see cref="Combat"/>.
/// </summary>
public sealed partial class Character
{
    private readonly int[] _rank = new int[Enum.GetValues<SkillId>().Length];
    private readonly double[] _xp = new double[Enum.GetValues<SkillId>().Length];
    private readonly int[] _ranksThisFight = new int[Enum.GetValues<SkillId>().Length];
    private readonly Dictionary<string, int> _perks = new();
    private readonly HashSet<string> _booksRead = new();
    private readonly AttributeSet _increases = new(0, 0, 0, 0, 0, 0);
    private readonly int[] _increasesThisLevel = new int[6];

    public ContentDb Content { get; }
    public string Name { get; set; }
    public AncestryDef Ancestry { get; }
    public CallingDef Calling { get; }
    public BackgroundDef Background { get; }
    public AttributeSet BaseAttributes { get; }

    public int Level { get; private set; } = 1;
    public int CharacterXp { get; private set; }
    public int PerkPoints { get; private set; }
    public int AttributePoints { get; private set; }
    public int PendingLevelChoices { get; private set; }
    public int HealthChoices { get; private set; }
    public int StaminaChoices { get; private set; }
    public int ManaChoices { get; private set; }
    public int RecoverySpeedUps { get; private set; }
    public int Respecs { get; private set; }
    public int LessonsThisLevel { get; private set; }

    /// <summary>Extra skill XP from the Delve, in percent: the lore bonus, Lone Delver and so on.</summary>
    public double ExtraSkillXpPercent { get; set; }

    public double Hp { get; private set; }
    public double Stamina { get; private set; }
    public double Mana { get; private set; }
    public bool IsDown => Hp <= 0;
    /// <summary>Dead, not just down: failed death saves. Only a shrine or Recall Soul brings them back.</summary>
    public bool IsDead { get; internal set; }
    /// <summary>A Riposte perk's window: the next blade hit before this time is a critical.</summary>
    public double RiposteUntil { get; internal set; } = -1;
    /// <summary>Damage soaked before HP, from a Ward spell.</summary>
    public double Ward { get; internal set; }

    public bool InFight { get; private set; }
    public bool LuckUsed { get; private set; }
    public bool RelentlessUsed { get; private set; }
    /// <summary>
    /// Playing alone (spec 4): +50% HP, +2 to every save and twice the skill XP. Setting it adjusts
    /// <see cref="ExtraSkillXpPercent"/> and keeps the hero's share of HP.
    /// </summary>
    public bool LoneDelver
    {
        get => _lone;
        set
        {
            if (_lone == value) return;
            double share = MaxHp > 0 ? Hp / MaxHp : 1;
            _lone = value;
            ExtraSkillXpPercent += value ? 100 : -100;
            Hp = Math.Round(MaxHp * share, 6);
        }
    }
    private bool _lone;

    public ConditionSet Conditions { get; }

    internal Character(ContentDb content, string name, AncestryDef ancestry, CallingDef calling, BackgroundDef background, AttributeSet baseAttributes)
    {
        Content = content;
        Name = name;
        Ancestry = ancestry;
        Calling = calling;
        Background = background;
        BaseAttributes = baseAttributes.Clone();
        Conditions = new ConditionSet(content);

        foreach (var s in Enum.GetValues<SkillId>()) _rank[(int)s] = Progression.StartingSkill;
        foreach (var s in calling.Major) _rank[(int)s] += Progression.MajorBonus;
        foreach (var s in calling.Minor) _rank[(int)s] += Progression.MinorBonus;
        foreach (var (s, bonus) in ContentDb.SkillBonuses(ancestry)) _rank[(int)s] += bonus;
        _rank[(int)background.Skill] += Progression.BackgroundBonus;
        for (int i = 0; i < _rank.Length; i++) _rank[i] = Math.Min(_rank[i], Progression.StartingCap);

        if (HasTrait(Traits.ExtraPerkPoint)) PerkPoints = 1;
        Hp = MaxHp;
        Stamina = MaxStamina;
        Mana = MaxMana;
    }

    public bool HasTrait(string trait) => Ancestry.Traits.Contains(trait);

    // ---- Attributes (spec 6.1) ----

    /// <summary>The score: base, plus ancestry, plus level-up increases (at most 20), plus items (at most 24).</summary>
    public int Score(Attribute a)
    {
        int ancestry = ContentDb.AttributeBonuses(Ancestry).Where(x => x.Attribute == a).Sum(x => x.Bonus);
        int own = Math.Min(AttributeSet.Cap, BaseAttributes[a] + ancestry + _increases[a]);
        int items = (int)ItemTotal("attribute", Character.AttrKey(a));
        return Math.Clamp(own + items, 1, Math.Max(own, AttributeSet.ItemCap));
    }

    public int Mod(Attribute a) => AttributeSet.Modifier(Score(a));

    // ---- Derived statistics (spec 6.2) ----

    /// <summary>The hit die's maximum at level 1, its average (rounded up) at each level after, CON every level, +5 per Health choice.</summary>
    public int MaxHp
    {
        get
        {
            int die = Calling.HitDie;
            int hp = die + (Level - 1) * Progression.HitDieAverage(die) + Level * Mod(Attribute.Con) + 5 * HealthChoices;
            hp = Math.Max(Level, hp);
            hp = LoneDelver ? hp * 3 / 2 : hp;
            return Math.Max(1, hp + (int)ItemTotal("maxHp"));
        }
    }

    public int MaxStamina => Math.Max(10, (int)((50 + 5 * Mod(Attribute.Con) + 10 * StaminaChoices + ItemTotal("maxStamina")) * (1 + PerkTotalFor("staminaPct", null) / 100)));

    public int MaxMana => Math.Max(0, 20 + 5 * Math.Max(Mod(Attribute.Int), Mod(Attribute.Wis)) + 10 * ManaChoices + (int)ItemTotal("maxMana"));

    /// <summary>10 + armour + DEX (up to the armour's cap) + shield + other. A Saurian's hide counts as 2 armour when unarmoured.</summary>
    public int ArmourClass
    {
        get
        {
            var w = Armour;
            int armour = w.Armour == 0 && HasTrait(Traits.ScaledHide) ? 2 : w.Armour;
            int dex = w.DexCap is { } cap ? Math.Min(Mod(Attribute.Dex), cap) : Mod(Attribute.Dex);
            return 10 + armour + dex + w.Shield + w.Other + (int)ItemTotal("ac") + (int)PerkArmour;
        }
    }

    /// <summary>10 + WIS + floor(Survival / 10). Keen senses add 5 (advantage) when looking for secret doors.</summary>
    public int PassivePerception(bool secretDoors = false) =>
        10 + Mod(Attribute.Wis) + SkillLevel(SkillId.Survival) / 10 + (secretDoors && HasTrait(Traits.KeenSenses) ? 5 : 0)
        + (int)PerkTotalFor("perceptionBonus", null);

    public int CarryCapacityKg => 15 * Score(Attribute.Str) + (int)PerkTotal("carryKg");

    /// <summary>What a merchant's price is multiplied by when buying: 5% less per point of CHA.</summary>
    public double BuyPriceFactor => Math.Clamp(1 - 0.05 * Mod(Attribute.Cha), 0.5, 1.5);

    public int Initiative(Rng rng) => rng.Die(20) + Mod(Attribute.Dex);

    // ---- Checks and saves (spec 6.2, 6.3) ----

    public bool IsProficient(Attribute save) => Calling.Saves.Contains(save);

    public int SaveBonus(Attribute a) =>
        Mod(a) + (IsProficient(a) ? Checks.Proficiency(Level) : 0) - Conditions.SavePenalty + (LoneDelver ? 2 : 0);

    /// <summary>
    /// A saving throw. Dwarves have advantage against poison, gnomes against illusion: pass the
    /// <paramref name="against"/> tag.
    /// </summary>
    public bool Save(Rng rng, Attribute a, int dc, Advantage adv = Advantage.None, string? against = null)
    {
        if (against == "poison" && HasTrait(Traits.PoisonResistance)) adv = AdvantageRules.Combine(adv, Advantage.Advantage);
        if (against == "illusion" && HasTrait(Traits.IllusionWard)) adv = AdvantageRules.Combine(adv, Advantage.Advantage);
        int n = RollD20(rng, adv);
        return n + SaveBonus(a) >= dc;
    }

    /// <summary>A d20 for this character. A Halfling's luck rerolls the first natural 1 in each fight.</summary>
    public int RollD20(Rng rng, Advantage adv = Advantage.None)
    {
        int n = Checks.RollD20(rng, adv);
        if (n == 1 && HasTrait(Traits.Lucky) && !LuckUsed)
        {
            LuckUsed = true;
            n = Checks.RollD20(rng, adv);
        }
        return n;
    }

    /// <summary>Attack bonus with a weapon skill: the attribute, floor(skill / 10), minus Weakened's penalty on STR attacks.</summary>
    public int AttackBonus(SkillId weaponSkill, Attribute attribute, int other = 0) =>
        Combat.AttackBonus(Mod(attribute) - (attribute == Attribute.Str ? Conditions.StrengthPenalty : 0), SkillLevel(weaponSkill),
                           other + (int)ItemTotal("toHit"));

    /// <summary>Rolls an attack, taking advantage from the situation and disadvantage from this character's conditions.</summary>
    public AttackResult Attack(Rng rng, SkillId weaponSkill, Attribute attribute, int targetAc, Advantage situation = Advantage.None, int other = 0)
    {
        var adv = AdvantageRules.Combine(situation, Conditions.AttacksAtDisadvantage ? Advantage.Disadvantage : Advantage.None);
        return Combat.Attack(rng, AttackBonus(weaponSkill, attribute, other), targetAc, natural: RollD20(rng, adv));
    }

    /// <summary>A downed hero stands up again with 1 HP (after a fight, or a healing spell).</summary>
    public void GetUp()
    {
        if (IsDead || !IsDown) return;
        Hp = 1;
    }

    /// <summary>Brings back the dead (a shrine, Recall Soul): up with a quarter of their HP.</summary>
    public void Revive(double share = 0.25)
    {
        IsDead = false;
        Hp = Math.Max(1, MaxHp * share);
    }

    /// <summary>The perk multiplier on damage for a scope (a weapon skill or damage type): 1 + damagePct / 100.</summary>
    public double DamageMultiplier(string scope) => 1 + (PerkTotal("damagePct", scope) + ItemTotal("damagePct", "all")) / 100.0;

    /// <summary>Spell power: INT for arcane schools, WIS for divine ones, the better of INT and CHA for Illusion.</summary>
    public int CastingMod(SkillId school) => school switch
    {
        SkillId.Restoration or SkillId.Warding => Mod(Attribute.Wis),
        SkillId.Illusion => Math.Max(Mod(Attribute.Int), Mod(Attribute.Cha)),
        _ => Mod(Attribute.Int),
    };

    public int SpellSaveDc(SkillId school) => Checks.SpellSaveDc(CastingMod(school), SkillLevel(school));
    public int SpellAttackBonus(SkillId school) => Checks.SpellAttackBonus(CastingMod(school), SkillLevel(school));

    /// <summary>
    /// How long a weapon takes to recover, as a factor: −3% per DEX point, 10% faster per extra-attack level for
    /// the best weapon skill, perks, and conditions such as Chilled.
    /// </summary>
    public double RecoveryFactor(SkillId weaponSkill)
    {
        double f = 1 - 0.03 * Mod(Attribute.Dex);
        if (weaponSkill == BestWeaponSkill) f *= Math.Pow(0.9, RecoverySpeedUps);
        f *= 1 + (PerkTotal("recoveryPct", SkillKey(weaponSkill)) + ItemTotal("recoveryPct")) / 100.0;
        return f * Conditions.RecoveryFactor;
    }

    /// <summary>The highest of the five weapon skills (the first, on a tie).</summary>
    public SkillId BestWeaponSkill => Progression.WeaponSkills.MaxBy(Rank);

    public Resistances Resistances
    {
        get
        {
            var r = new Resistances();
            if (HasTrait(Traits.PoisonResistance)) r.Resist(DamageType.Poison);
            if (HasTrait(Traits.FireResistance)) r.Resist(DamageType.Fire);
            foreach (var t in ItemScopes("resist")) if (Enum.TryParse<DamageType>(t, true, out var d)) r.Resist(d);
            return r;
        }
    }

    // ---- Pools, damage and regeneration ----

    /// <summary>
    /// Takes damage that has already been through resistance. An Orc-blooded hero who would drop to 0 stays at 1,
    /// once per rest. Damage wakes a sleeper.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDown) return;
        if (Ward > 0)
        {
            double soak = Math.Min(Ward, amount);
            Ward -= soak;
            amount -= (int)soak;
            if (amount <= 0) return;
        }
        Conditions.Cure(ConditionId.Asleep);
        Hp -= amount;
        if (Hp <= 0 && HasTrait(Traits.Relentless) && !RelentlessUsed)
        {
            RelentlessUsed = true;
            Hp = 1;
        }
        if (Hp < 0) Hp = 0;
    }

    public void Heal(double amount) => Hp = Math.Min(MaxHp, Hp + Math.Max(0, amount));
    public bool SpendStamina(double amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public bool SpendMana(double amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        return true;
    }

    /// <summary>
    /// Puts a condition on the character. Elves can't be put to sleep. Returns whether it took hold.
    /// </summary>
    public bool ApplyCondition(ConditionId id, int saveDc, double? seconds = null, Dice? damagePerSecond = null)
    {
        if (id == ConditionId.Asleep && HasTrait(Traits.SleepImmunity)) return false;
        Conditions.Apply(id, saveDc, seconds, damagePerSecond);
        return true;
    }

    /// <summary>
    /// Runs the character forward: conditions (saves every 3 s, damage over time), then natural regeneration
    /// (spec 6.2). Returns the damage the conditions dealt.
    /// </summary>
    public int Update(double seconds, Rng rng)
    {
        var tick = Conditions.Tick(seconds, rng, (a, dc) => Save(rng, a, dc, against: a == Attribute.Con && Conditions.Has(ConditionId.Poisoned) ? "poison" : null));
        int dealt = 0;
        if (tick.Damage > 0)
        {
            // Poison is the only damage-over-time an ancestry resists.
            bool poison = Conditions.Has(ConditionId.Poisoned) || tick.Ended.Contains(ConditionId.Poisoned);
            dealt = poison ? Combat.ApplyAffinity(tick.Damage, Resistances.For(DamageType.Poison)) : tick.Damage;
            TakeDamage(dealt);
        }
        Regenerate(seconds);
        return dealt;
    }

    /// <summary>
    /// Per second, out of combat: HP 1% of max, Stamina 10%, Mana 3% + 0.5% per WIS point. In a fight HP stops and
    /// the others are quartered. A downed hero and a diseased one don't heal.
    /// </summary>
    public void Regenerate(double seconds)
    {
        double scale = InFight ? 0.25 : 1;
        if (!InFight && !IsDown && !Conditions.StopsRegeneration) Hp = Math.Min(MaxHp, Hp + MaxHp * 0.01 * seconds);
        if (!IsDown && ItemTotal("hpRegen") is > 0 and var regen) Hp = Math.Min(MaxHp, Hp + regen / 10.0 * seconds);
        Stamina = Math.Min(MaxStamina, Stamina + MaxStamina * 0.10 * scale * seconds * (1 + PerkTotalFor("regenPct", "stamina") / 100));
        double manaRate = Math.Max(0, 0.03 + 0.005 * Mod(Attribute.Wis));
        Mana = Math.Min(MaxMana, Mana + MaxMana * manaRate * scale * seconds);
    }

    /// <summary>A full rest: pools refilled, conditions that rest cures removed, once-per-rest traits back.</summary>
    public void Rest()
    {
        Conditions.CureBy("rest");
        if (!Conditions.StopsRegeneration) Hp = MaxHp;
        Stamina = MaxStamina;
        Mana = MaxMana;
        RelentlessUsed = false;
        AvoidDeathUsed = false;
    }

    public void BeginFight()
    {
        InFight = true;
        LuckUsed = false;
        Array.Clear(_ranksThisFight);
    }

    public void EndFight()
    {
        InFight = false;
        Array.Clear(_ranksThisFight);
    }

    // ---- Skills (spec 7.1, 7.2, 7.5) ----

    public int Rank(SkillId s) => _rank[(int)s];

    /// <summary>The skill as the rules use it: the rank plus item bonuses, at most 100. Progress uses the bare rank.</summary>
    public int SkillLevel(SkillId s) => Math.Min(Progression.MaxSkill, _rank[(int)s] + (int)ItemTotal("skill", SkillKey(s)));
    public double Xp(SkillId s) => _xp[(int)s];
    public int SkillCeiling => Progression.SkillCeiling(Level);

    /// <summary>The use XP a skill's use is worth before multipliers: base + per × amount, never below 0.</summary>
    public double UseXp(SkillId s, double amount)
    {
        var u = Content.Skill(s).UseXp;
        return Math.Max(0, u.Base + u.Per * amount);
    }

    /// <summary>One use of a skill: works out its use XP from the amount (damage, DC ...) and gains it.</summary>
    public Progress UseSkill(SkillId s, double amount, XpSource source) => GainSkillXp(s, UseXp(s, amount), source);

    /// <summary>
    /// Gains use XP in a skill, after the anti-grind rules and the multipliers; ranks rise while the XP lasts,
    /// up to the skill ceiling and at most 3 ranks per skill in one fight. Each new rank gives character XP.
    /// </summary>
    public Progress GainSkillXp(SkillId s, double xp, XpSource source)
    {
        xp = EffectiveXp(s, xp, source);
        if (xp <= 0) return Progress.None;

        int i = (int)s;
        var ups = new List<(SkillId, int)>();
        int levels = 0;
        _xp[i] += xp;
        while (true)
        {
            if (_rank[i] >= SkillCeiling || (InFight && _ranksThisFight[i] >= Progression.MaxRanksPerFight))
            {
                _xp[i] = 0; // Banked XP is lost at a ceiling: you learn nothing more for now.
                break;
            }
            double need = Progression.XpToNext(_rank[i]);
            if (_xp[i] < need) break;
            _xp[i] -= need;
            levels += RaiseRank(s, ups);
            if (InFight) _ranksThisFight[i]++;
        }
        return new Progress(ups, levels);
    }

    /// <summary>
    /// Use XP after the anti-grind rules (nothing from objects, allies or spells that did nothing; a tenth from
    /// targets more than 5 levels below) and the multipliers (Human +10%, the Delve's extras).
    /// </summary>
    public double EffectiveXp(SkillId s, double xp, XpSource source)
    {
        var group = Content.Skill(s).Group;
        if (group == SkillGroup.Combat && source.TargetIsObjectOrAlly) return 0;
        if (group == SkillGroup.Magic && !source.SpellHadEffect) return 0;
        if (source.TargetLevel is { } tl && tl < Level - Progression.TrivialTargetGap) xp *= 0.10;
        double pct = (HasTrait(Traits.Versatile) ? 10 : 0) + ExtraSkillXpPercent + ItemTotal("skillXpPct") + PerkTotal("xpPct");
        return Math.Max(0, xp * (1 + pct / 100));
    }

    /// <summary>Reads a skill book: +1 rank, once per title. Returns null if read before or the skill is at its ceiling.</summary>
    public Progress? ReadBook(string title, SkillId s)
    {
        if (_booksRead.Contains(title) || Rank(s) >= SkillCeiling) return null;
        _booksRead.Add(title);
        var ups = new List<(SkillId, int)>();
        int levels = RaiseRank(s, ups);
        if (HasPerkFlag("booklore") && Rank(s) < SkillCeiling) levels += RaiseRank(s, ups);
        return new Progress(ups, levels);
    }

    /// <summary>
    /// A lesson from a trainer: +1 rank for <see cref="Progression.TrainingCost"/> gold, at most 5 lessons per
    /// level. Returns null (and takes nothing) if it isn't allowed or can't be afforded.
    /// </summary>
    public Progress? Train(SkillId s, ref int gold)
    {
        int cost = Progression.TrainingCost(Rank(s));
        if (LessonsThisLevel >= Progression.LessonsPerLevel || Rank(s) >= SkillCeiling || gold < cost) return null;
        gold -= cost;
        LessonsThisLevel++;
        var ups = new List<(SkillId, int)>();
        int levels = RaiseRank(s, ups);
        return new Progress(ups, levels);
    }

    public bool HasRead(string title) => _booksRead.Contains(title);

    private int RaiseRank(SkillId s, List<(SkillId, int)> ups)
    {
        int r = ++_rank[(int)s];
        ups.Add((s, r));
        return AddCharacterXp(Progression.CharacterXpFor(r));
    }

    // ---- Levels (spec 7.3, 7.4) ----

    public int XpToNextLevel => Progression.LevelXp(Level);

    private int AddCharacterXp(int amount)
    {
        CharacterXp += amount;
        int gained = 0;
        while (CharacterXp >= Progression.LevelXp(Level))
        {
            CharacterXp -= Progression.LevelXp(Level);
            int oldMax = MaxHp;
            Level++;
            gained++;
            PerkPoints++;
            PendingLevelChoices++;
            LessonsThisLevel = 0;
            Array.Clear(_increasesThisLevel);
            if (Progression.IsAttributeLevel(Level)) AttributePoints += 2;
            if (Progression.RecoveryLevels.Contains(Level)) RecoverySpeedUps++;
            Hp += MaxHp - oldMax;
        }
        return gained;
    }

    /// <summary>Takes a level-up's extra: +5 HP, +10 Stamina or +10 Mana. False if there is no choice waiting.</summary>
    public bool ChooseLevelBonus(LevelChoice choice)
    {
        if (PendingLevelChoices == 0) return false;
        PendingLevelChoices--;
        switch (choice)
        {
            case LevelChoice.Health: HealthChoices++; Hp += 5; break;
            case LevelChoice.Stamina: StaminaChoices++; Stamina += 10; break;
            default: ManaChoices++; Mana += 10; break;
        }
        return true;
    }

    /// <summary>
    /// Spends one attribute point: +1 to an attribute. The two points of a 4th level go +2 to one attribute or +1
    /// to two, so at most 2 into one attribute per level. Scores stop at 20.
    /// </summary>
    public bool RaiseAttribute(Attribute a)
    {
        if (AttributePoints == 0 || Score(a) >= AttributeSet.Cap || _increasesThisLevel[(int)a] >= 2) return false;
        int oldMax = MaxHp;
        AttributePoints--;
        _increases[a]++;
        _increasesThisLevel[(int)a]++;
        Hp += Math.Max(0, MaxHp - oldMax);
        return true;
    }

    // ---- Perks (spec 8) ----

    public int PerkRank(string id) => _perks.GetValueOrDefault(id);
    public IReadOnlyDictionary<string, int> Perks => _perks;

    /// <summary>Why a perk's next rank can't be bought, or null if it can.</summary>
    public string? WhyNotPerk(string id)
    {
        var p = Content.Perk(id);
        int have = PerkRank(id);
        if (have >= p.MaxRank) return "You have every rank of it.";
        if (PerkPoints == 0) return "No perk points to spend.";
        if (Rank(p.Skill) < p.Ranks[have]) return $"Needs {Content.Skill(p.Skill).Name} {p.Ranks[have]}.";
        foreach (var r in p.Requires)
            if (PerkRank(r) == 0) return $"Needs {Content.Perk(r).Name} first.";
        return null;
    }

    public bool CanBuyPerk(string id) => WhyNotPerk(id) is null;

    public bool BuyPerk(string id)
    {
        if (!CanBuyPerk(id)) return false;
        PerkPoints--;
        _perks[id] = PerkRank(id) + 1;
        return true;
    }

    /// <summary>The sum of a perk effect over the perks owned, counted once per rank. A null scope matches all.</summary>
    public double PerkTotal(string kind, string? scope = null)
    {
        double total = 0;
        foreach (var (id, rank) in _perks)
            foreach (var e in Content.Perk(id).Effects)
                if (e.Kind == kind && (scope is null || e.Scope is null || e.Scope == scope))
                    total += e.Value * rank;
        return total;
    }

    /// <summary>A perk effect's total for exactly this scope (null for the unscoped ones only).</summary>
    public double PerkTotalFor(string kind, string? scope)
    {
        double total = 0;
        foreach (var (id, rank) in _perks)
            foreach (var e in Content.Perk(id).Effects)
                if (e.Kind == kind && e.Scope == scope)
                    total += e.Value * rank;
        return total;
    }

    /// <summary>AC from armour perks: Juggernaut in heavy armour, Agile Defender in light or none, Stone Skin with none.</summary>
    private double PerkArmour
    {
        get
        {
            if (_perks.Count == 0) return 0;
            var kind = Body?.Kind;
            double ac = kind == ArmourKind.Heavy ? PerkTotalFor("ac", "heavyArmour") : PerkTotalFor("ac", "lightArmour");
            if (Body is null) ac += PerkTotalFor("ac", "unarmoured");
            return ac;
        }
    }

    /// <summary>Whether an owned perk grants this named behaviour.</summary>
    public bool HasPerkFlag(string flag) =>
        _perks.Keys.Any(id => Content.Perk(id).Effects.Any(e => e.Kind == "flag" && e.Scope == flag));

    public int RespecCost => Progression.RespecCost(Respecs);

    /// <summary>Resets every perk and refunds the points, for gold at a Waystone shrine. Each reset costs double.</summary>
    public bool Respec(ref int gold)
    {
        int cost = RespecCost;
        if (gold < cost || _perks.Count == 0) return false;
        gold -= cost;
        PerkPoints += _perks.Values.Sum();
        _perks.Clear();
        Respecs++;
        return true;
    }

    public static string AttrKey(Attribute a) => a.ToString().ToLowerInvariant();

    /// <summary>The scope string for a skill in content files: camelCase, e.g. twoHanded.</summary>
    public static string SkillKey(SkillId s)
    {
        string n = s.ToString();
        return char.ToLowerInvariant(n[0]) + n[1..];
    }
}
