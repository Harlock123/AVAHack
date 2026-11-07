using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;

namespace AVAHack.Characters;

public sealed partial class Character
{
    public const int BackpackSize = 24;

    private readonly Dictionary<EquipSlot, Item> _equipped = new();
    private ArmourWorn? _armourOverride;

    public IReadOnlyDictionary<EquipSlot, Item> Equipped => _equipped;
    public Item?[] Backpack { get; } = new Item?[BackpackSize];

    /// <summary>The hands' recovery and hold state, for the real-time fight.</summary>
    public Hand LeftHand { get; } = new();
    public Hand RightHand { get; } = new();

    /// <summary>Seconds left of the red flash on the portrait after a hit.</summary>
    public double HurtFlash { get; internal set; }

    public Item? ItemIn(EquipSlot slot) => _equipped.GetValueOrDefault(slot);

    /// <summary>The weapon in the right hand (null is a bare fist); a bound weapon spell replaces it.</summary>
    public WeaponDef? MainHand =>
        Buffs.ContainsKey("boundBlade") ? BoundBlade
        : Buffs.ContainsKey("boundBow") ? BoundBow
        : ItemIn(EquipSlot.RightHand) is { } i ? Content.Base(i.BaseId).Weapon : null;

    private static readonly WeaponDef BoundBlade = new()
    {
        Id = "boundBlade", Name = "Bound Blade", Skill = SkillId.Blade, Damage = "1d10", Type = Rules.DamageType.Force,
        Recovery = 1.8, Text = "A blade of force.",
    };

    private static readonly WeaponDef BoundBow = new()
    {
        Id = "boundBow", Name = "Bound Bow", Skill = SkillId.Archery, Damage = "1d10", Type = Rules.DamageType.Force,
        Recovery = 2.0, Kind = "bow", Range = 8, Finesse = true, TwoHanded = true, Text = "A bow of force.",
    };
    /// <summary>A shield on the left arm.</summary>
    public ShieldDef? OffHand => ItemIn(EquipSlot.LeftHand) is { } i ? Content.Base(i.BaseId).Shield : null;
    /// <summary>A second weapon in the left hand.</summary>
    public WeaponDef? OffWeapon => ItemIn(EquipSlot.LeftHand) is { } i ? Content.Base(i.BaseId).Weapon : null;
    public ArmourDef? Body => ItemIn(EquipSlot.Chest) is { } i ? Content.Base(i.BaseId).Armour : null;

    /// <summary>Smithing improvements on the item in a slot.</summary>
    public int QualityOf(EquipSlot slot) => ItemIn(slot)?.Quality ?? 0;

    /// <summary>What the armour class is built from: the equipment, unless a test has set it directly.</summary>
    public ArmourWorn Armour
    {
        get
        {
            if (_armourOverride is { } o) return o;
            var body = Body;
            int gear = _equipped.Where(kv => kv.Key is not (EquipSlot.Chest or EquipSlot.LeftHand))
                                .Sum(kv => (Content.Base(kv.Value.BaseId).Gear?.Ac ?? 0) + (Content.Base(kv.Value.BaseId).Gear is { Ac: > 0 } ? kv.Value.Quality : 0));
            int shield = OffHand is { } s ? s.Ac + QualityOf(EquipSlot.LeftHand) : 0;
            return new ArmourWorn((body?.Ac ?? 0) + (body is null ? 0 : QualityOf(EquipSlot.Chest)), body?.DexCap, shield, gear);
        }
        set => _armourOverride = value;
    }

    /// <summary>The armour skill that being hit trains: Heavy Armour in heavy armour, Light Armour otherwise.</summary>
    public SkillId ArmourSkill => Body?.Kind == ArmourKind.Heavy ? SkillId.HeavyArmour : SkillId.LightArmour;

    /// <summary>The sum of an affix kind over everything worn, and the set bonuses; a null scope matches all.</summary>
    public double ItemTotal(string kind, string? scope = null)
    {
        if (_equipped.Count == 0) return 0;
        double t = 0;
        foreach (var (a, v) in AllAffixes())
        {
            var def = Content.Affix(a);
            if (def.Kind == kind && (scope is null || string.Equals(def.Scope, scope, StringComparison.OrdinalIgnoreCase))) t += v;
        }
        return t;
    }

    /// <summary>The scopes of every affix of a kind worn (e.g. the damage types resisted).</summary>
    public IEnumerable<string> ItemScopes(string kind) =>
        AllAffixes().Select(x => Content.Affix(x.Affix)).Where(d => d.Kind == kind && d.Scope is not null).Select(d => d.Scope!);

    /// <summary>Extra damage of each type the main-hand weapon adds.</summary>
    public IEnumerable<(DamageType Type, int Amount)> ExtraDamage(EquipSlot hand = EquipSlot.RightHand)
    {
        if (ItemIn(hand) is not { } item) yield break;
        foreach (var r in item.Affixes)
        {
            var d = Content.Affix(r.Affix);
            if (d.Kind == "extraDamage" && Enum.TryParse<DamageType>(d.Scope, true, out var t)) yield return (t, r.Value);
        }
    }

    private IEnumerable<(string Affix, int Value)> AllAffixes()
    {
        foreach (var item in _equipped.Values)
            foreach (var r in item.Affixes)
                yield return (r.Affix, r.Value);
        // Set bonuses for the pieces worn.
        foreach (var g in _equipped.Values.Where(i => i.UniqueId is not null).Select(i => Content.Unique(i.UniqueId!).Set).Where(s => s is not null).GroupBy(s => s!))
        {
            var set = Content.Sets.FirstOrDefault(x => x.Id == g.Key);
            if (set is null) continue;
            foreach (var (pieces, bonus) in set.Bonuses)
                if (int.TryParse(pieces, out int n) && g.Count() >= n)
                    foreach (var (a, v) in bonus) yield return (a, v);
        }
    }

    // ---- Carrying ----

    public double CarriedKg => _equipped.Values.Sum(i => i.WeightKg(Content)) + Backpack.Where(i => i is not null).Sum(i => i!.WeightKg(Content));

    /// <summary>Weight carried over capacity: up to 1 is fine, over 1 slows, over 1.5 stops (spec 9.6).</summary>
    public double Encumbrance => CarriedKg / Math.Max(1, CarryCapacityKg);

    public int FreeSlots => Backpack.Count(i => i is null);

    /// <summary>Puts an item in the backpack, stacking it with its kind. Returns false (and keeps nothing) if there is no room.</summary>
    public bool AddToBackpack(Item item)
    {
        var b = Content.Base(item.BaseId);
        if (b.Stacks)
        {
            for (int i = 0; i < BackpackSize; i++)
                if (Backpack[i] is { } s && s.BaseId == item.BaseId && s.SpellId == item.SpellId)
                {
                    s.Count += item.Count;
                    return true;
                }
        }
        for (int i = 0; i < BackpackSize; i++)
            if (Backpack[i] is null)
            {
                Backpack[i] = item;
                return true;
            }
        return false;
    }

    /// <summary>Takes one of a stack (or the item) out of a backpack slot.</summary>
    public Item? TakeOne(int index)
    {
        if (Backpack[index] is not { } it) return null;
        if (it.Count > 1 && Content.Base(it.BaseId).Stacks)
        {
            it.Count--;
            return new Item { BaseId = it.BaseId, Count = 1, Level = it.Level, Charges = it.Charges, SpellId = it.SpellId, KeyId = it.KeyId, Effects = it.Effects.ToList() };
        }
        Backpack[index] = null;
        return it;
    }

    /// <summary>Why the item in backpack slot <paramref name="index"/> can't be put on, or null if it can.</summary>
    public string? WhyNotEquip(int index)
    {
        if (Backpack[index] is not { } it) return "Nothing there.";
        var b = Content.Base(it.BaseId);
        if (b.Slots.Length == 0) return $"{b.Name} isn't worn.";
        var target = PickSlot(b);
        if (ItemIn(target) is { } there && there.Cursed(Content)) return $"The {there.Name(Content)} won't come off. It's cursed.";
        if (b.Weapon is { TwoHanded: true } && ItemIn(EquipSlot.LeftHand) is { } left && left.Cursed(Content)) return "A cursed thing holds your other hand.";
        return null;
    }

    /// <summary>Puts on the item in a backpack slot; what it replaces goes back into the backpack.</summary>
    public bool Equip(int index)
    {
        if (WhyNotEquip(index) is not null) return false;
        var it = Backpack[index]!;
        var b = Content.Base(it.BaseId);
        var slot = PickSlot(b);
        Backpack[index] = null;
        // Everything the new item pushes off goes back into the pack (the freed slot first).
        var off = new List<Item>();
        if (b.Weapon is { TwoHanded: true } && _equipped.Remove(EquipSlot.LeftHand, out var left)) off.Add(left);
        if (slot == EquipSlot.LeftHand && MainHand is { TwoHanded: true } && _equipped.Remove(EquipSlot.RightHand, out var right)) off.Add(right);
        if (_equipped.Remove(slot, out var old)) off.Add(old);
        _equipped[slot] = it;
        foreach (var o in off) AddToBackpack(o);
        ClampPools();
        return true;
    }

    /// <summary>Takes off the item in a slot, into the backpack. Cursed items won't come off.</summary>
    public bool Unequip(EquipSlot slot)
    {
        if (ItemIn(slot) is not { } it || it.Cursed(Content) || FreeSlots == 0) return false;
        _equipped.Remove(slot);
        AddToBackpack(it);
        ClampPools();
        return true;
    }

    /// <summary>Puts an item straight on (for starting kit and tests), replacing what was there.</summary>
    public void Wear(Item item, EquipSlot? slot = null)
    {
        var b = Content.Base(item.BaseId);
        var s = slot ?? PickSlot(b);
        if (b.Weapon is { TwoHanded: true }) _equipped.Remove(EquipSlot.LeftHand);
        _equipped[s] = item;
        ClampPools();
    }

    /// <summary>Lifts every curse on what is worn (a shrine or Dispel). Returns how many items it freed.</summary>
    public int LiftCurses()
    {
        int n = 0;
        foreach (var item in _equipped.Values)
            n += item.Affixes.RemoveAll(a => Content.Affix(a.Affix).Curse);
        return n;
    }

    /// <summary>The M2 way to arm a hero: plain items of these bases.</summary>
    public void Equip(WeaponDef? mainHand, ShieldDef? offHand, ArmourDef? body)
    {
        _equipped.Remove(EquipSlot.RightHand);
        _equipped.Remove(EquipSlot.LeftHand);
        _equipped.Remove(EquipSlot.Chest);
        if (mainHand is not null) _equipped[EquipSlot.RightHand] = new Item { BaseId = mainHand.Id };
        if (offHand is not null) _equipped[EquipSlot.LeftHand] = new Item { BaseId = offHand.Id };
        if (body is not null) _equipped[EquipSlot.Chest] = new Item { BaseId = body.Id };
        _armourOverride = null;
        ClampPools();
    }

    private EquipSlot PickSlot(ItemBase b)
    {
        if (b.Slots.Length == 1) return b.Slots[0];
        // Rings and one-handed weapons: an empty slot first (the right hand before the left).
        foreach (var s in b.Slots) if (!_equipped.ContainsKey(s)) return s;
        return b.Slots[0];
    }

    private void MoveToBackpack(EquipSlot slot)
    {
        if (_equipped.Remove(slot, out var it) && !AddToBackpack(it)) _equipped[slot] = it;
    }

    private void ClampPools()
    {
        Hp = Math.Min(Hp, MaxHp);
        Stamina = Math.Min(Stamina, MaxStamina);
        Mana = Math.Min(Mana, MaxMana);
    }

    /// <summary>
    /// Uses a consumable or reads a book from the backpack. Returns what happened, or null if the item can't be
    /// used like that.
    /// </summary>
    public string? Use(int index, Rng rng)
    {
        if (Backpack[index] is not { } it) return null;
        var b = Content.Base(it.BaseId);
        if (b.Book is { } book)
        {
            if (ReadBook(book.Id, book.Skill) is null) return HasRead(book.Id) ? $"{Name} has read {book.Title} already." : $"{Name} can learn nothing more from it yet.";
            Backpack[index] = null;
            return $"{Name} reads {book.Title}: {Content.Skill(book.Skill).Name} rises to {Rank(book.Skill)}.";
        }
        if (b.Consumable is not { } c || c.Special is "identify" or "light" or "oil" or "food" or "water") return null;
        if (IsDown) return null;
        TakeOne(index);
        var said = new List<string>();
        if (c.Heal is { } h)
        {
            int amount = Dice.Parse(h).Roll(rng);
            Heal(amount);
            said.Add($"heals {amount}");
        }
        if (c.Stamina > 0) { Stamina = Math.Min(MaxStamina, Stamina + c.Stamina); said.Add("feels fresh"); }
        if (c.Mana > 0) { Mana = Math.Min(MaxMana, Mana + c.Mana); said.Add("feels clear-headed"); }
        foreach (var cure in c.Cures) if (Conditions.Cure(cure)) said.Add($"is no longer {Content.Condition(cure).Name.ToLowerInvariant()}");
        return $"{Name} uses the {b.Name}{(said.Count > 0 ? " and " + string.Join(", ", said) : "")}.";
    }
}
