using AVAHack.Characters;
using AVAHack.Content;

namespace AVAHack.Items;

/// <summary>An item base, whatever file it came from, with what the item system needs to know.</summary>
public sealed record ItemBase(string Id, string Name, ItemKind Kind, string Category, double WeightKg, int Value, EquipSlot[] Slots)
{
    public WeaponDef? Weapon { get; init; }
    public ShieldDef? Shield { get; init; }
    public ArmourDef? Armour { get; init; }
    public GearDef? Gear { get; init; }
    public ConsumableDef? Consumable { get; init; }
    public BookDef? Book { get; init; }
    public string Text { get; init; } = "";
    /// <summary>Several of the same can share one backpack slot.</summary>
    public bool Stacks => Kind is ItemKind.Consumable or ItemKind.Gold or ItemKind.Reagent or ItemKind.Food or ItemKind.Shard;

    public static Dictionary<string, ItemBase> All(ContentDb db)
    {
        var d = new Dictionary<string, ItemBase>();
        void Add(ItemBase b) => d.TryAdd(b.Id, b);
        foreach (var w in db.Weapons)
            Add(new(w.Id, w.Name, ItemKind.Weapon, "weapon", w.WeightKg, w.Value,
                w.TwoHanded ? [EquipSlot.RightHand] : [EquipSlot.RightHand, EquipSlot.LeftHand]) { Weapon = w, Text = w.Text });
        foreach (var s in db.Shields) Add(new(s.Id, s.Name, ItemKind.Shield, "shield", s.WeightKg, s.Value, [EquipSlot.LeftHand]) { Shield = s, Text = s.Text });
        foreach (var a in db.Armours) Add(new(a.Id, a.Name, ItemKind.Armour, "armour", a.WeightKg, a.Value, [EquipSlot.Chest]) { Armour = a, Text = a.Text });
        foreach (var g in db.Gear)
        {
            EquipSlot[] slots = g.Slot switch
            {
                "head" => [EquipSlot.Head], "neck" => [EquipSlot.Neck], "cloak" => [EquipSlot.Cloak], "hands" => [EquipSlot.Hands],
                "belt" => [EquipSlot.Belt], "legs" => [EquipSlot.Legs], "feet" => [EquipSlot.Feet], _ => [EquipSlot.Ring1, EquipSlot.Ring2],
            };
            Add(new(g.Id, g.Name, ItemKind.Gear, "gear", g.WeightKg, g.Value, slots) { Gear = g, Text = g.Text });
        }
        foreach (var c in db.Consumables)
        {
            var kind = c.Special switch { "food" or "water" => ItemKind.Food, _ => ItemKind.Consumable };
            Add(new(c.Id, c.Name, kind, "consumable", c.WeightKg, c.Value, []) { Consumable = c, Text = c.Text });
        }
        foreach (var b in db.Books) Add(new(b.Id, b.Title, ItemKind.Book, "book", 1, b.Value, []) { Book = b, Text = b.Text });
        Add(new("gold", "Gold", ItemKind.Gold, "gold", 0.01, 1, []) { Text = "Coins of a dozen kingdoms." });
        Add(new("key", "Key", ItemKind.Key, "key", 0.1, 0, []) { Text = "An iron key. It opens a door somewhere on this level." });
        if (db.Spells.Count > 0)
        {
            Add(new("tome", "Tome", ItemKind.Tome, "tome", 1.5, 60, []) { Text = "Read it to learn the spell, if your school is good enough." });
            Add(new("scroll", "Scroll", ItemKind.Scroll, "scroll", 0.1, 30, []) { Text = "Read it once to cast the spell; anyone can." });
            Add(new("wand", "Wand", ItemKind.Wand, "wand", 0.3, 120, []) { Text = "Holds charges of a spell. Enchanting recharges it." });
        }
        foreach (var x in ExtraBases) Add(x);
        return d;
    }

    /// <summary>Bases added by later systems (keys, tomes, wands, reagents, shards); filled in as they register.</summary>
    internal static readonly List<ItemBase> ExtraBases = [];
}

/// <summary>A rolled affix on an item.</summary>
public sealed record AffixRoll(string Affix, int Value);

/// <summary>One item: a base, a rarity and its affixes, whether it is known and whether it is cursed.</summary>
public sealed class Item
{
    public required string BaseId { get; init; }
    public Rarity Rarity { get; init; }
    public List<AffixRoll> Affixes { get; init; } = [];
    public bool Identified { get; set; } = true;
    public string? UniqueId { get; init; }
    public int Level { get; init; } = 1;
    /// <summary>Smithing improvements: +1 damage or AC each.</summary>
    public int Quality { get; set; }
    /// <summary>How many, for things that stack (gold, potions).</summary>
    public int Count { get; set; } = 1;
    /// <summary>What is left: a wand's charges, a torch's seconds.</summary>
    public double Charges { get; set; }
    /// <summary>For a key: the lock it opens.</summary>
    public string? KeyId { get; init; }
    /// <summary>For a tome, wand or scroll: the spell.</summary>
    public string? SpellId { get; init; }

    public bool Cursed(ContentDb db) => Affixes.Any(a => db.Affix(a.Affix).Curse);

    /// <summary>The name a player sees: unknown items keep their secrets.</summary>
    public string Name(ContentDb db)
    {
        var b = db.Base(BaseId);
        string baseName = SpellId is { } sp && db.Spells.Any(s => s.Id == sp) ? $"{b.Name}: {Magic.SpellNames.Of(db, sp)}" : b.Name;
        if (Count > 1 && b.Stacks) return $"{Count} × {baseName}";
        if (!Identified) return $"Unidentified {baseName}";
        if (UniqueId is { } u) return db.Unique(u).Name;
        var prefix = Affixes.Select(a => db.Affix(a.Affix)).FirstOrDefault(a => a.Prefix);
        var suffix = Affixes.Select(a => db.Affix(a.Affix)).FirstOrDefault(a => !a.Prefix);
        string q = Quality > 0 ? $" +{Quality}" : "";
        return $"{(prefix is null ? "" : prefix.Name + " ")}{baseName}{q}{(suffix is null ? "" : " " + suffix.Name)}";
    }

    public double WeightKg(ContentDb db) => db.Base(BaseId).WeightKg * Count;

    /// <summary>The lines describing it, for the inventory's tooltip.</summary>
    public IEnumerable<string> Describe(ContentDb db)
    {
        var b = db.Base(BaseId);
        yield return $"{Rarity} {b.Kind.ToString().ToLowerInvariant()}  ·  {b.WeightKg * Count:0.#} kg  ·  worth {b.Value * Count}";
        if (b.Weapon is { } w) yield return $"{w.Damage}{(Quality > 0 ? "+" + Quality : "")} {w.Type.ToString().ToLowerInvariant()}, {w.Recovery:0.0} s{(w.TwoHanded ? ", two hands" : "")}{(w.Kind == "reach" ? ", reach" : "")}{(w.Kind == "bow" ? $", range {w.Range}" : "")}";
        if (b.Shield is { } s) yield return $"AC +{s.Ac + Quality}, blocks {s.BlockPercent}%, bash {s.BashDamage}";
        if (b.Armour is { } a) yield return $"AC +{a.Ac + Quality} ({a.Kind.ToString().ToLowerInvariant()}{(a.DexCap is { } c ? $", DEX up to {c}" : "")})";
        if (b.Gear is { Ac: > 0 } g) yield return $"AC +{g.Ac + Quality}";
        if (b.Consumable is { } k) yield return k.Text;
        if (b.Book is { } bk) yield return $"Teaches {db.Skill(bk.Skill).Name}.";
        if (!Identified) { yield return "Its properties are unknown (needs Lore, or a scroll of Identify)."; yield break; }
        foreach (var r in Affixes) yield return ItemRules.Describe(db, r);
        if (UniqueId is { } u) yield return db.Unique(u).Text;
    }
}

/// <summary>What affixes mean in words.</summary>
public static class ItemRules
{
    public static string Describe(ContentDb db, AffixRoll r)
    {
        var a = db.Affix(r.Affix);
        string sign = r.Value >= 0 ? "+" : "";
        string text = a.Kind switch
        {
            "toHit" => $"{sign}{r.Value} to hit",
            "damagePct" => $"{sign}{r.Value}% damage",
            "extraDamage" => $"+{r.Value} {a.Scope} damage",
            "ac" => $"{sign}{r.Value} AC",
            "resist" => $"resists {a.Scope}",
            "recoveryPct" => r.Value < 0 ? $"recovers {-r.Value}% faster" : $"recovers {r.Value}% slower",
            "attribute" => $"{sign}{r.Value} {a.Scope!.ToUpperInvariant()}",
            "maxHp" => $"{sign}{r.Value} HP",
            "maxStamina" => $"{sign}{r.Value} Stamina",
            "maxMana" => $"{sign}{r.Value} Mana",
            "hpRegen" => $"heals {r.Value} HP every 10 s",
            "skillXpPct" => $"+{r.Value}% skill XP",
            "skill" => $"{sign}{r.Value} {db.Skill(Enum.Parse<SkillId>(a.Scope!, true)).Name}",
            _ => a.Kind,
        };
        return a.Curse ? $"{text} (cursed)" : text;
    }
}
