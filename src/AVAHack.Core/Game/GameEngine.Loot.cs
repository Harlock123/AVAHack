using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>Rolls every chest's contents when the level starts.</summary>
    private void FillChests()
    {
        foreach (var chest in Level.Chests)
            chest.Contents.AddRange(Loot.Roll(chest.Table, Level.Depth, Rng.Loot, Magic));
    }

    /// <summary>A creature's loot falls where it died.</summary>
    private void DropLoot(Monster m)
    {
        if (m.Def.Loot is not { } table || m.Summoned) return;
        var items = Loot.Roll(table, Level.Depth, Rng.Loot, Magic);
        if (m.Elite || m.Affixes.Count > 0)
            items.AddRange(Loot.Roll(m.Affixes.Count > 0 ? "chest.rich" : "chest.cellar", Level.Depth, Rng.Loot, Magic));
        foreach (var item in items) Level.Drop(m.Cell, item);
    }

    /// <summary>Opens a chest ahead: its contents spill onto the floor in front of the party.</summary>
    private void OpenChest(Chest chest)
    {
        if (chest.Opened)
        {
            Say("The chest is empty.");
            return;
        }
        if (chest.LockDc > 0 && !TryUnlockChest(chest)) return;
        chest.Opened = true;
        if (chest.Mimic)
        {
            SpringMimic(chest);
            return;
        }
        var pile = PartyCell;
        foreach (var item in chest.Contents) Level.Drop(pile, item);
        chest.Contents.Clear();
        int n = Level.PileAt(pile).Count;
        Say(n == 0 ? "The chest creaks open. Nothing but dust." : $"The chest creaks open. {n} thing{(n == 1 ? "" : "s")} spill out at your feet (G to pick up).");
    }

    /// <summary>Picks up everything in the party's cell: gold to the purse, the rest to whoever has room.</summary>
    public int PickUpAll()
    {
        var pile = Level.PileAt(PartyCell);
        int taken = 0;
        foreach (var item in pile.ToList())
            if (TakeFromFloor(item, Selected)) taken++;
        if (taken > 0) IdentifyAll();
        return taken;
    }

    /// <summary>Picks up one item from the party's cell for a hero (or whoever has room). False if nobody does.</summary>
    public bool TakeFromFloor(Item item, Character? who = null)
    {
        var pile = Level.PileAt(PartyCell);
        if (!pile.Contains(item)) return false;
        if (item.BaseId == "gold")
        {
            Party.Gold += item.Count;
            pile.Remove(item);
            Say($"{item.Count} gold.");
            return true;
        }
        foreach (var c in new[] { who ?? Hero }.Concat(Party.Members))
        {
            if (c is null || !c.AddToBackpack(item)) continue;
            pile.Remove(item);
            Say($"{c.Name} takes {item.Name(Content)}.");
            if (pile.Count == 0) Level.ClearPile(PartyCell);
            return true;
        }
        Say("Nobody has room for that.");
        return false;
    }

    /// <summary>Drops an item from a hero's backpack onto the floor of the party's cell.</summary>
    public bool DropItem(Character c, int index)
    {
        if (c.Backpack[index] is not { } it) return false;
        c.Backpack[index] = null;
        Level.Drop(PartyCell, it);
        return true;
    }

    /// <summary>Uses an item from a hero's backpack: drink, read, light, eat. Returns what happened.</summary>
    public string? UseItem(Character c, int index)
    {
        if (c.Backpack[index] is not { } it) return null;
        var b = Content.Base(it.BaseId);
        string? said = b.Consumable?.Special switch
        {
            "identify" => ReadIdentify(c, index),
            "light" or "oil" => LightTorch(c, index),
            "food" or "water" => Eat(c, index),
            _ => null,
        };
        said ??= UseSpellItem(c, index);
        said ??= c.Use(index, Rng.Combat);
        if (said is not null) Say(said);
        return said;
    }

    private string ReadIdentify(Character c, int index)
    {
        c.TakeOne(index);
        int n = 0;
        foreach (var hero in Party.Members)
            foreach (var item in hero.Backpack.Concat(hero.Equipped.Values).Where(i => i is { Identified: false }))
            {
                item!.Identified = true;
                n++;
            }
        return $"{c.Name} reads the scroll. {n} thing{(n == 1 ? " is" : "s are")} made plain.";
    }

    /// <summary>Learns what the party's best Lore can tell about everything carried. Lore improves with each.</summary>
    public void IdentifyAll()
    {
        int lore = Party.Best(h => h.SkillLevel(SkillId.Lore) + (int)h.PerkTotal("identifyBonus") + (h.HasPerkFlag("loremaster") ? 100 : 0));
        var sage = Party.Standing.MaxBy(h => h.SkillLevel(SkillId.Lore));
        foreach (var hero in Party.Members)
            foreach (var item in hero.Backpack.Concat(hero.Equipped.Values).Where(i => i is { Identified: false }))
            {
                if (LootGenerator.LoreToIdentify(item!.Rarity) > lore) continue;
                item.Identified = true;
                sage?.UseSkill(SkillId.Lore, item.Level, XpSource.Plain);
                Say($"{sage?.Name ?? "You"} knows this: {item.Name(Content)}.");
            }
    }

    /// <summary>The hero the inventory and the pick-up key act for.</summary>
    public Character Selected
    {
        get => _selected is { } s && Party.Members.Contains(s) ? s : Hero;
        set => _selected = value;
    }
    private Character? _selected;
}

public sealed partial class GameEngine
{
    /// <summary>Seconds of torchlight left (spec 10.3). The view's light follows it.</summary>
    public double LightLeft { get; internal set; }
    /// <summary>Seconds left of the party's invisibility.</summary>
    public double Invisible { get; internal set; }

    /// <summary>Picks a lock with the party's best hands, or opens it with a key carried. False if it stays shut.</summary>
    private bool TryUnlockChest(Chest chest)
    {
        if (chest.Key is { } key && Party.Members.Any(h => h.Backpack.Any(i => i?.KeyId == key)))
        {
            chest.LockDc = 0;
            Say("The key turns. The lock opens.");
            return true;
        }
        var picker = Party.Standing.MaxBy(h => h.SkillLevel(SkillId.Lockpicking) / 5 + h.Mod(Rules.Attribute.Dex));
        if (picker is null) return false;
        int roll = picker.RollD20(Rng.Combat) + picker.Mod(Rules.Attribute.Dex) + picker.SkillLevel(SkillId.Lockpicking) / 5 + (int)picker.PerkTotal("lockBonus");
        if (roll >= chest.LockDc)
        {
            picker.UseSkill(SkillId.Lockpicking, chest.LockDc, XpSource.Plain);
            Say($"{picker.Name} picks the lock (DC {chest.LockDc}).");
            chest.LockDc = 0;
            return true;
        }
        Say($"{picker.Name} can't pick the lock (DC {chest.LockDc}). Practice, or find the key.");
        return false;
    }

    private void SpringMimic(Chest chest)
    {
        if (Content.Creatures.Any(c => c.Id == "mimic"))
        {
            Level.RemoveChest(chest.Cell);
            var m = AddMonster("mimic", chest.Cell, DirTo(chest.Cell, PartyCell) ?? Dir.North);
            Alert(m);
            Say("The chest has teeth!");
        }
    }

    private string? LightTorch(Character c, int index)
    {
        var it = c.TakeOne(index)!;
        double seconds = Content.Base(it.BaseId).Consumable!.Amount;
        LightLeft = Math.Max(LightLeft, 0) + seconds;
        return $"{c.Name} lights a {Content.Base(it.BaseId).Name.ToLowerInvariant()}.";
    }

    private int AbsorbWithWard(Character c, int dmg) => dmg; // a hero's ward soaks inside TakeDamage

    /// <summary>Hooks for later systems: food (survival) and spell items (magic) fill these in.</summary>
    private string? Eat(Character c, int index) => EatHook?.Invoke(c, index);
    private string? UseSpellItem(Character c, int index) => SpellItemHook?.Invoke(c, index);
    internal Func<Character, int, string?>? EatHook;
    internal Func<Character, int, string?>? SpellItemHook;
}

public sealed partial class GameEngine
{
    private void TickLight()
    {
        if (LightLeft > 0) LightLeft = Math.Max(0, LightLeft - TickSeconds);
        if (Invisible > 0) Invisible = Math.Max(0, Invisible - TickSeconds);
    }
}
