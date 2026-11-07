using AVAHack.Characters;
using AVAHack.Rules;

namespace AVAHack.Content;

public enum ItemKind { Weapon, Shield, Armour, Gear, Consumable, Book, Gold, Key, Tome, Wand, Scroll, Reagent, Food, Shard, Junk }

public enum EquipSlot { Head, Neck, Chest, Cloak, Hands, Belt, Legs, Feet, Ring1, Ring2, LeftHand, RightHand }

public enum Rarity { Common, Fine, Enchanted, Masterwork, Relic, Set }

/// <summary>Wearables other than chest armour and shields: helms, gloves, boots, rings, amulets, cloaks, belts.</summary>
public sealed record GearDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>head, neck, cloak, hands, belt, legs, feet or ring.</summary>
    public required string Slot { get; init; }
    public int Ac { get; init; }
    public double WeightKg { get; init; }
    public int Value { get; init; } = 5;
    public required string Text { get; init; }
}

/// <summary>Something used up: a potion, a bandage, food, a scroll.</summary>
public sealed record ConsumableDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Heal { get; init; }
    public double Stamina { get; init; }
    public double Mana { get; init; }
    public ConditionId[] Cures { get; init; } = [];
    /// <summary>A special use: identify, light (burn time), food (meals), water.</summary>
    public string? Special { get; init; }
    public double Amount { get; init; }
    public double WeightKg { get; init; } = 0.3;
    public int Value { get; init; } = 10;
    public required string Text { get; init; }
}

public sealed record BookDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required SkillId Skill { get; init; }
    public int Value { get; init; } = 40;
    public required string Text { get; init; }
}

/// <summary>A magic property an item can roll (spec 13.3).</summary>
public sealed record AffixDef
{
    public required string Id { get; init; }
    /// <summary>The word in the item's name: a prefix ("Keen") or a suffix ("of the Bear").</summary>
    public required string Name { get; init; }
    public required bool Prefix { get; init; }
    /// <summary>The kinds of item it can appear on: weapon, shield, armour, gear.</summary>
    public required string[] On { get; init; }
    /// <summary>toHit, damagePct, extraDamage, ac, resist, recoveryPct, attribute, maxHp, maxStamina, maxMana, hpRegen, skillXpPct, skill.</summary>
    public required string Kind { get; init; }
    /// <summary>A damage type, attribute or skill, for the kinds that need one.</summary>
    public string? Scope { get; init; }
    public required int Min { get; init; }
    public required int Max { get; init; }
    /// <summary>Extra value per item level, rounded down.</summary>
    public double PerLevel { get; init; }
    /// <summary>A curse's upside or downside is written in its text.</summary>
    public bool Curse { get; init; }
}

/// <summary>A fixed named item (a relic), or a piece of a set.</summary>
public sealed record UniqueDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Base { get; init; }
    public required Rarity Rarity { get; init; }
    /// <summary>Fixed affixes: affix id and value.</summary>
    public required Dictionary<string, int> Affixes { get; init; }
    public string? Set { get; init; }
    public int MinLevel { get; init; } = 1;
    public required string Text { get; init; }
}

/// <summary>A set's bonuses for wearing several of its pieces.</summary>
public sealed record SetDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Pieces worn → affix id and value gained.</summary>
    public required Dictionary<string, Dictionary<string, int>> Bonuses { get; init; }
}

public sealed record LootEntry
{
    public double Weight { get; init; } = 1;
    /// <summary>A specific item base.</summary>
    public string? Item { get; init; }
    /// <summary>Any base of a kind: weapon, shield, armour, gear, consumable, book.</summary>
    public string? Category { get; init; }
    /// <summary>A dice expression of gold.</summary>
    public string? Gold { get; init; }
    /// <summary>Nothing at all.</summary>
    public bool Nothing { get; init; }
    public int Count { get; init; } = 1;
}

public sealed record LootTableDef
{
    public required string Id { get; init; }
    /// <summary>How many times the table is rolled.</summary>
    public string Rolls { get; init; } = "1";
    /// <summary>Added to every rarity roll: chests and bosses roll better.</summary>
    public double RarityBonus { get; init; }
    public required LootEntry[] Entries { get; init; }
}
