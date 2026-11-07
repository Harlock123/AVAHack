using System.Globalization;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

/// <summary>
/// The Waystones (spec 3): safe stones after each boss with a merchant, a trainer, a shrine and a stash. They are
/// worked through these methods, which the Waystone screen calls; each returns what to tell the player.
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>The merchant sells at twice an item's worth and buys at half.</summary>
    public const int BuyMarkup = 2;
    public const int StockSize = 10;

    /// <summary>Raised when the party touches a Waystone (the screen opens).</summary>
    public event Action? WaystoneTouched;

    /// <summary>Whether the party stands by this level's Waystone.</summary>
    public bool AtWaystone => Level.Waystone is { } w && PartyCell.Distance(w) <= 1 && !InFight;

    /// <summary>An item's worth in gold: its base value, raised by rarity, affixes and quality.</summary>
    public static int Worth(Content.ContentDb db, Item it)
    {
        var b = db.Base(it.BaseId);
        double f = it.Rarity switch { Rarity.Common => 1, Rarity.Fine => 1.5, Rarity.Enchanted => 2, Rarity.Masterwork => 4, Rarity.Relic => 8, Rarity.Set => 6, _ => 1 } + it.Affixes.Count * 0.5 + it.Quality * 0.25;
        return Math.Max(1, (int)(b.Value * f) * Math.Max(1, it.Count));
    }

    private void TouchWaystone()
    {
        CueHere("waystone");
        Say("The Waystone hums. The air here is still and safe. A merchant, a trainer and a shrine-keeper wait in its light.");
        WaystoneTouched?.Invoke();
    }

    /// <summary>The merchant's goods on this level, made from the level's seed; sold ones are gone.</summary>
    public IReadOnlyList<(int Index, Item Item, int Price)> Stock()
    {
        if (Level.Waystone is null) return [];
        ulong h = 1469598103934665603UL;
        foreach (char ch in Level.Source) { h ^= ch; h *= 1099511628211UL; }
        var rng = new Pcg32(h, 91);
        var gen = new LootGenerator(Content);
        var items = new List<Item>();
        while (items.Count < StockSize)
        {
            var batch = gen.Roll(items.Count % 3 == 0 ? "chest.rich" : "chest.cellar", Level.Depth + 1, rng, Magic);
            items.AddRange(batch.Where(i => Content.Base(i.BaseId).Kind is not (ItemKind.Gold or ItemKind.Key)));
        }
        foreach (var i in items) i.Identified = true;
        return items.Take(StockSize).Select((it, i) => (i, it, Worth(Content, it) * BuyMarkup))
                    .Where(x => !LevelFlags.ContainsKey($"sold:{x.i}")).ToList();
    }

    public string Buy(int index, Character to)
    {
        if (!AtWaystone) return "There is no merchant here.";
        var s = Stock().FirstOrDefault(x => x.Index == index);
        if (s.Item is null) return "That's already sold.";
        if (Party.Gold < s.Price) return $"That costs {s.Price} gold; you have {Party.Gold}.";
        if (!to.AddToBackpack(s.Item)) return $"{to.Name} has no room for it.";
        Party.Gold -= s.Price;
        LevelFlags[$"sold:{index}"] = "1";
        return $"{to.Name} buys the {s.Item.Name(Content)} for {s.Price} gold.";
    }

    public string Sell(Character from, int slot)
    {
        if (!AtWaystone) return "There is no merchant here.";
        if (from.Backpack[slot] is not { } it) return "Nothing there to sell.";
        if (Content.Base(it.BaseId).Kind == ItemKind.Key) return "The merchant has no use for keys.";
        int price = Math.Max(1, Worth(Content, it) / BuyMarkup);
        from.Backpack[slot] = null;
        Party.Gold += price;
        return $"The merchant gives {price} gold for the {it.Name(Content)}.";
    }

    /// <summary>A lesson with the trainer (spec 7.2): rank² × 2 gold, five lessons a level.</summary>
    public string TrainAt(Character c, SkillId skill)
    {
        if (!AtWaystone) return "There is no trainer here.";
        int gold = Party.Gold;
        int cost = Progression.TrainingCost(c.Rank(skill));
        var p = c.Train(skill, ref gold);
        if (p is null)
            return c.LessonsThisLevel >= Progression.LessonsPerLevel ? $"{c.Name} has had all the lessons this level allows."
                 : c.Rank(skill) >= c.SkillCeiling ? $"{c.Name} can't learn more {Content.Skill(skill).Name} yet."
                 : $"A lesson costs {cost} gold.";
        Party.Gold = gold;
        return $"{c.Name} trains {Content.Skill(skill).Name} to {c.Rank(skill)} for {cost} gold.{(p.LevelsGained > 0 ? " Level up!" : "")}";
    }

    /// <summary>What the shrine asks to raise the dead: 100 gold a level.</summary>
    public static int ReviveCost(Character c) => 100 * c.Level;
    /// <summary>What the shrine asks to lift a hero's curses: 50 gold a level.</summary>
    public static int CleanseCost(Character c) => 50 * c.Level;

    public string ReviveAt(Character c)
    {
        if (!AtWaystone) return "There is no shrine here.";
        if (!c.IsDead && !c.IsDown) return $"{c.Name} is alive and well.";
        int cost = c.IsDead ? ReviveCost(c) : 0;
        if (Party.Gold < cost) return $"Raising {c.Name} costs {cost} gold.";
        Party.Gold -= cost;
        c.Revive(1);
        return cost > 0 ? $"The shrine-keeper sings, and {c.Name} breathes again. ({cost} gold)" : $"{c.Name} is tended and stands.";
    }

    public string CleanseAt(Character c)
    {
        if (!AtWaystone) return "There is no shrine here.";
        int cost = CleanseCost(c);
        if (Party.Gold < cost) return $"Lifting curses costs {cost} gold.";
        int n = c.LiftCurses() + (c.Conditions.Cure(ConditionId.Cursed) ? 1 : 0);
        if (n == 0) return $"{c.Name} carries no curse.";
        Party.Gold -= cost;
        return $"The curse lifts from {c.Name}. ({cost} gold)";
    }

    public string RespecAt(Character c)
    {
        if (!AtWaystone) return "There is no shrine here.";
        int gold = Party.Gold, cost = c.RespecCost;
        if (!c.Respec(ref gold)) return c.Perks.Count == 0 ? $"{c.Name} has no perks to forget." : $"Forgetting costs {cost} gold.";
        Party.Gold = gold;
        return $"{c.Name} forgets every perk; the points are free to spend again. ({cost} gold)";
    }

    /// <summary>The whole party rests at the stone: everyone up, every pool full. Free, and safe.</summary>
    public string RestAtStone()
    {
        if (!AtWaystone) return "There is no Waystone here.";
        foreach (var c in Party.Members.Where(c => !c.IsDead))
        {
            c.GetUp();
            c.Rest();
        }
        return "The party rests in the Waystone's light and wakes whole.";
    }

    public string Deposit(Character c, int slot)
    {
        if (!AtWaystone) return "There is no stash here.";
        if (c.Backpack[slot] is not { } it) return "Nothing there.";
        c.Backpack[slot] = null;
        Stash.Add(it);
        return $"The {it.Name(Content)} goes into the stash.";
    }

    public string Withdraw(int index, Character c)
    {
        if (!AtWaystone) return "There is no stash here.";
        if (index < 0 || index >= Stash.Count) return "Nothing there.";
        var it = Stash[index];
        if (!c.AddToBackpack(it)) return $"{c.Name} has no room.";
        Stash.RemoveAt(index);
        return $"{c.Name} takes the {it.Name(Content)} from the stash.";
    }

    internal static string Gold(int g) => g.ToString("N0", CultureInfo.InvariantCulture);
}
