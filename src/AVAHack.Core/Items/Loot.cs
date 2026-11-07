using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.Items;

/// <summary>How generous a Delve is with magic items (spec 4).</summary>
public enum MagicItems { Rare, Normal, Plentiful }

/// <summary>Rolls loot tables into items (spec 13.3).</summary>
public sealed class LootGenerator(ContentDb db)
{
    public const double CurseChance = 15;

    /// <summary>Rolls a table for an item level. Gold comes out as a stack of gold.</summary>
    public List<Item> Roll(string table, int itemLevel, Rng rng, MagicItems magic = MagicItems.Normal)
    {
        var t = db.LootTable(table);
        var items = new List<Item>();
        int rolls = Math.Max(0, Dice.Parse(t.Rolls).Roll(rng));
        for (int i = 0; i < rolls; i++)
        {
            var e = Pick(t.Entries, rng);
            if (e.Nothing) continue;
            if (e.Gold is { } g) { items.Add(new Item { BaseId = "gold", Count = Math.Max(1, Dice.Parse(g).Roll(rng)) }); continue; }
            var baseId = e.Item ?? PickBase(e.Category!, rng);
            if (baseId is null) continue;
            items.Add(Make(baseId, itemLevel, rng, magic, t.RarityBonus, e.Count));
        }
        return items;
    }

    private static LootEntry Pick(LootEntry[] entries, Rng rng)
    {
        double total = entries.Sum(e => e.Weight), r = rng.Next(1_000_000) / 1_000_000.0 * total;
        foreach (var e in entries)
        {
            r -= e.Weight;
            if (r < 0) return e;
        }
        return entries[^1];
    }

    private string? PickBase(string category, Rng rng)
    {
        var options = db.Bases.Values.Where(b => b.Category == category && b.Id != "gold").OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        return options.Count == 0 ? null : options[rng.Next(options.Count)].Id;
    }

    /// <summary>Makes one item of a base: rolls its rarity and affixes. Consumables and books are always common.</summary>
    public Item Make(string baseId, int itemLevel, Rng rng, MagicItems magic = MagicItems.Normal, double rarityBonus = 0, int count = 1)
    {
        var b = db.Base(baseId);
        if (b.Kind is ItemKind.Tome or ItemKind.Scroll or ItemKind.Wand)
        {
            // A spell suited to the depth: anything up to rank 20 + 10 per item level.
            var spells = db.Spells.Where(s => !s.Cantrip && s.NeedsPerk is null && s.Rank <= 20 + 10 * itemLevel).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
            var sp = spells[rng.Next(spells.Count)];
            return new Item { BaseId = baseId, Level = itemLevel, SpellId = sp.Id, Charges = b.Kind == ItemKind.Wand ? 6 + rng.Next(5) : 0 };
        }
        if (b.Kind is not (ItemKind.Weapon or ItemKind.Shield or ItemKind.Armour or ItemKind.Gear))
            return new Item { BaseId = baseId, Count = count, Level = itemLevel, Charges = b.Consumable?.Amount ?? 0 };

        var rarity = RollRarity(rng, magic, rarityBonus);
        if (rarity == Rarity.Relic)
        {
            var relics = db.Uniques.Where(u => u.Rarity is Rarity.Relic or Rarity.Set && u.MinLevel <= itemLevel).OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
            if (relics.Count > 0) return MakeUnique(relics[rng.Next(relics.Count)].Id, itemLevel);
            rarity = Rarity.Masterwork;
        }
        int affixCount = rarity switch { Rarity.Fine => 1, Rarity.Enchanted => 2, Rarity.Masterwork => 3 + rng.Next(2), _ => 0 };
        string kind = b.Category;
        var pool = db.Affixes.Where(a => !a.Curse && a.On.Contains(kind)).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        var chosen = new List<AffixRoll>();
        bool havePrefix = false, haveSuffix = false;
        for (int i = 0; i < affixCount && pool.Count > 0; i++)
        {
            // One prefix and one suffix at most name the item; extra affixes on masterworks are allowed either way.
            var options = pool.Where(a => i >= 2 || (a.Prefix ? !havePrefix : !haveSuffix)).ToList();
            if (options.Count == 0) options = pool;
            var a = options[rng.Next(options.Count)];
            pool.Remove(a);
            pool.RemoveAll(x => x.Kind == a.Kind && x.Scope == a.Scope);
            havePrefix |= a.Prefix;
            haveSuffix |= !a.Prefix;
            chosen.Add(new AffixRoll(a.Id, RollValue(a, itemLevel, rng)));
        }
        if (rarity == Rarity.Masterwork && rng.Chance(CurseChance))
        {
            var curses = db.Affixes.Where(a => a.Curse && a.On.Contains(kind)).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            if (curses.Count > 0)
            {
                var c = curses[rng.Next(curses.Count)];
                chosen.Add(new AffixRoll(c.Id, RollValue(c, itemLevel, rng)));
            }
        }
        return new Item
        {
            BaseId = baseId, Rarity = rarity, Affixes = chosen, Level = itemLevel,
            Identified = rarity is Rarity.Common or Rarity.Fine,
        };
    }

    public Item MakeUnique(string uniqueId, int itemLevel)
    {
        var u = db.Unique(uniqueId);
        return new Item
        {
            BaseId = u.Base, Rarity = u.Rarity, UniqueId = u.Id, Level = itemLevel, Identified = false,
            Affixes = u.Affixes.Select(kv => new AffixRoll(kv.Key, kv.Value)).ToList(),
        };
    }

    /// <summary>Common 60, Fine 25, Enchanted 11, Masterwork 3.5, Relic 0.5 (Normal); the bonus moves weight upward.</summary>
    public static Rarity RollRarity(Rng rng, MagicItems magic, double bonus = 0)
    {
        double scale = magic switch { MagicItems.Rare => 0.5, MagicItems.Plentiful => 1.8, _ => 1 };
        double fine = 25 * scale + bonus * 0.4, ench = 11 * scale + bonus * 0.35, master = 3.5 * scale + bonus * 0.2, relic = 0.5 * scale + bonus * 0.05;
        double common = Math.Max(5, 100 - fine - ench - master - relic);
        double r = rng.Next(1_000_000) / 1_000_000.0 * (common + fine + ench + master + relic);
        if ((r -= relic) < 0) return Rarity.Relic;
        if ((r -= master) < 0) return Rarity.Masterwork;
        if ((r -= ench) < 0) return Rarity.Enchanted;
        if ((r -= fine) < 0) return Rarity.Fine;
        return Rarity.Common;
    }

    public static int RollValue(AffixDef a, int itemLevel, Rng rng) =>
        rng.Range(a.Min, a.Max) + (int)Math.Truncate(a.PerLevel * itemLevel);

    /// <summary>The Lore a hero needs to know an item: Enchanted 25, Masterwork, relics and sets 50; others are always known.</summary>
    public static int LoreToIdentify(Rarity r) => r switch
    {
        Rarity.Common or Rarity.Fine => 0,
        Rarity.Enchanted => 25,
        _ => 50,
    };
}
