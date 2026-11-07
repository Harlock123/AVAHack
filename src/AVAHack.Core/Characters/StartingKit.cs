using AVAHack.Content;
using AVAHack.Items;

namespace AVAHack.Characters;

/// <summary>What each calling carries into the dungeon (spec 5.2), and a little for everyone.</summary>
public static class StartingKit
{
    private static readonly Dictionary<string, (string Slot, string Item)[]> Kits = new()
    {
        ["fighter"] = [("right", "longsword"), ("left", "roundShield"), ("chest", "chainMail"), ("pack", "potionHealing")],
        ["barbarian"] = [("right", "greataxe"), ("chest", "hide"), ("pack", "potionHealing")],
        ["ranger"] = [("right", "shortbow"), ("chest", "leather"), ("pack", "shortsword")],
        ["rogue"] = [("right", "shortsword"), ("left", "dagger"), ("chest", "leather"), ("pack", "potionHealing")],
        ["cleric"] = [("right", "mace"), ("left", "roundShield"), ("chest", "scale"), ("pack", "potionHealing")],
        ["wizard"] = [("right", "quarterstaff"), ("chest", "padded"), ("pack", "potionMana")],
        ["paladin"] = [("right", "longsword"), ("left", "roundShield"), ("chest", "chainMail")],
        ["druid"] = [("right", "quarterstaff"), ("chest", "hide"), ("pack", "potionHealing")],
        ["monk"] = [("chest", "padded"), ("pack", "potionStamina")],
        ["spellblade"] = [("right", "longsword"), ("left", "buckler"), ("chest", "leather")],
    };

    private static readonly Dictionary<string, string> BackgroundItems = new()
    {
        ["acolyte"] = "potionHealing", ["apothecary"] = "antidote", ["cutpurse"] = "dagger", ["mercenary"] = "buckler",
        ["gravedigger"] = "bandage", ["hedgeWitch"] = "potionMana", ["miner"] = "club", ["cartographer"] = "torch",
    };

    public static void Give(ContentDb db, Character c)
    {
        if (Kits.TryGetValue(c.Calling.Id, out var kit))
            foreach (var (slot, id) in kit)
            {
                var item = new Item { BaseId = id };
                switch (slot)
                {
                    case "right": c.Wear(item, EquipSlot.RightHand); break;
                    case "left": c.Wear(item, EquipSlot.LeftHand); break;
                    case "chest": c.Wear(item, EquipSlot.Chest); break;
                    default: c.AddToBackpack(item); break;
                }
            }
        if (BackgroundItems.TryGetValue(c.Background.Id, out var extra)) c.AddToBackpack(new Item { BaseId = extra });
        c.AddToBackpack(new Item { BaseId = "torch", Count = 2 });
        c.AddToBackpack(new Item { BaseId = "bandage", Count = 2 });
        foreach (var sp in StartingSpells?.Invoke(c) ?? []) c.KnownSpells.Add(sp);
    }

    /// <summary>The spells a calling starts with; magic fills this in.</summary>
    public static Func<Character, IEnumerable<string>>? StartingSpells { get; set; }
}
