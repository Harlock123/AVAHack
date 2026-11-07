using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

/// <summary>
/// Survival and crafting (spec 13.5–13.7): food and water, resting with wandering encounters, Alchemy, cooking,
/// Smithing and Enchanting. Each action returns what to tell the player.
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>Food lasts an in-game day, about 40 minutes of play; water a little less (spec 13.6).</summary>
    public const double FoodPerSecond = 100.0 / 2400, WaterPerSecond = 100.0 / 2000;
    /// <summary>Starving or parched: 1 HP every this many seconds.</summary>
    public const double StarveEvery = 10;
    /// <summary>The base chance of a wandering monster each hour of rest (spec 13.7).</summary>
    public const double EncounterChance = 0.15;
    public const int MaxQuality = 5;

    /// <summary>What the party has learned of each reagent: how many of its four effects, in order (spec 13.5).</summary>
    public Dictionary<string, int> ReagentKnown { get; } = new();
    /// <summary>Affixes the party's enchanters have learned by disenchanting.</summary>
    public List<string> Enchantments { get; } = new();

    private double _starveTimer;

    private void InitCrafting()
    {
        EatHook = EatFood;
        RestHook = () => Say(RestParty());
        OnHeroHit += Coating;
    }

    // ---- Food and water ----

    private void TickSurvival()
    {
        if (!FoodOn) return;
        bool starving = false;
        foreach (var c in Party.Members.Where(c => !c.IsDead))
        {
            bool wasHungry = c.Hungry;
            c.Food = Math.Max(0, c.Food - FoodPerSecond * TickSeconds);
            c.Water = Math.Max(0, c.Water - WaterPerSecond * TickSeconds);
            if (!wasHungry && c.Hungry) Say($"{c.Name} is {(c.Food < Character.HungryBelow ? "hungry" : "thirsty")}.");
            if (c.Food <= 0 || c.Water <= 0) starving = true;
        }
        if (!starving) { _starveTimer = 0; return; }
        _starveTimer += TickSeconds;
        if (_starveTimer < StarveEvery) return;
        _starveTimer = 0;
        foreach (var c in Party.Members.Where(c => !c.IsDead && !c.IsDown && (c.Food <= 0 || c.Water <= 0)).ToList())
            HurtHero(c, 1, DamageType.Necrotic);
        if (Party.AllDown) Die();
    }

    private string? EatFood(Character c, int index)
    {
        var it = c.Backpack[index]!;
        var b = Content.Base(it.BaseId);
        if (b.Consumable is { Special: "food" or "water" } k)
        {
            c.TakeOne(index);
            if (k.Special == "food") c.Food = Math.Min(100, c.Food + k.Amount);
            else c.Water = Math.Min(100, c.Water + k.Amount);
            return $"{c.Name} {(k.Special == "food" ? "eats" : "drinks")} the {b.Name.ToLowerInvariant()}.";
        }
        return null;
    }

    /// <summary>Eats a cooked meal: food, water and its buff, stronger from a better cook (the item's level).</summary>
    private string EatMeal(Character c, int index)
    {
        var it = c.TakeOne(index)!;
        var r = Content.Recipe(it.BaseId["meal:".Length..]);
        double cook = 1 + Math.Max(1, it.Level) / 10.0;
        c.Food = Math.Min(100, c.Food + r.Food);
        c.Water = Math.Min(100, c.Water + r.Water);
        if (r.Buff is { } buff) c.Buffs[buff] = Math.Max(c.Buffs.GetValueOrDefault(buff), r.BuffSeconds * cook);
        return $"{c.Name} eats the {r.Name.ToLowerInvariant()}. {r.Text}";
    }

    // ---- Resting (spec 13.7) ----

    /// <summary>Hours a rest takes: enough to heal the worst hurt, 1 to 8.</summary>
    public int RestHours => Math.Clamp((int)Math.Ceiling((1 - Party.Members.Where(c => !c.IsDead).Min(c => c.Hp / c.MaxHp)) * 8), 1, 8);

    /// <summary>Why the party can't rest now, or null.</summary>
    public string? WhyNotRest()
    {
        if (Mode != GameMode.Playing) return "There's no resting now.";
        if (InFight || _monsters.Any(m => m.IsAware && !m.Ally && m.IsAlive && m.Cell.Distance(PartyCell) <= 8))
            return "You can't rest with enemies near.";
        return null;
    }

    /// <summary>The chance of a wandering monster each hour here: halved behind a closed door, none at a Waystone or in a cleared, sealed room.</summary>
    public double EncounterChanceHere()
    {
        if (Level.Waystone is { } w && PartyCell.Distance(w) <= 1) return 0;
        var (cells, sealedIn) = RoomAround(PartyCell);
        if (sealedIn && !_monsters.Any(m => m.IsAlive && !m.Ally && cells.Contains(m.Cell))) return 0;
        double density = 1;
        var parts = Level.Source.Split(':');
        if (parts.Length >= 6 && parts[0] == "gen" && double.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) density = d;
        double region = Level.Theme switch { "works" => 1.2, "choir" => 1.4, "heart" => 2, _ => 1 };
        double chance = EncounterChance * density * region;
        return sealedIn || TouchesClosedDoor(cells) ? chance / 2 : chance;
    }

    /// <summary>The floor reachable from a cell without passing a door, and whether closed doors bound all of it.</summary>
    private (HashSet<Cell> Cells, bool Sealed) RoomAround(Cell from)
    {
        var seen = new HashSet<Cell> { from };
        var queue = new Queue<Cell>(seen);
        bool open = false;
        while (queue.Count > 0 && seen.Count < 120)
        {
            var c = queue.Dequeue();
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (seen.Contains(n) || Level.KindAt(n) == CellKind.Wall) continue;
                if (Level.DoorAt(n) is { } door)
                {
                    if (door.Open >= 1) open = true;
                    continue;
                }
                if (!Level.IsPassable(n)) continue;
                seen.Add(n);
                queue.Enqueue(n);
            }
        }
        return (seen, !open && seen.Count < 120 && TouchesClosedDoor(seen));
    }

    private bool TouchesClosedDoor(HashSet<Cell> cells) =>
        cells.Any(c => Enum.GetValues<Dir>().Any(d => Level.DoorAt(c.Step(d)) is { Open: < 1 }));

    /// <summary>
    /// R: the party rests until healed (spec 13.7). Each hour rolls for a wandering monster; if one comes, the party
    /// wakes part-healed with the creature close by. Otherwise everyone wakes whole, with pools and once-per-rest
    /// traits back. Food and water go down 4 an hour.
    /// </summary>
    public string RestParty()
    {
        if (WhyNotRest() is { } no) return no;
        int hours = RestHours;
        double chance = EncounterChanceHere();
        for (int h = 1; h <= hours; h++)
        {
            if (FoodOn)
                foreach (var c in Party.Members.Where(c => !c.IsDead)) { c.Food = Math.Max(0, c.Food - 4); c.Water = Math.Max(0, c.Water - 4); }
            if (Rng.Ai.Next(1_000_000) / 1_000_000.0 >= chance) continue;
            double share = (h - 1.0) / hours; // the hour it was woken in doesn't count
            foreach (var c in Party.Members.Where(c => !c.IsDead)) { c.GetUp(); c.HealShare(share); }
            if (Wanderer() is { } m) return $"After {h} hour{(h == 1 ? "" : "s")}, something finds you: a {m.Name}! The party scrambles up.";
            return $"After {h} hour{(h == 1 ? "" : "s")} a noise wakes you, but nothing comes.";
        }
        foreach (var c in Party.Members.Where(c => !c.IsDead)) { c.GetUp(); c.Rest(); }
        return $"The party rests {hours} hour{(hours == 1 ? "" : "s")} and wakes whole.";
    }

    /// <summary>A wandering creature arrives 4–10 cells away, already hunting.</summary>
    private Monster? Wanderer()
    {
        var table = Generator.SpawnTable(Content, Level.Theme, Level.Depth);
        if (table.Count == 0) return null;
        var dist = new Dictionary<Cell, int> { [PartyCell] = 0 };
        var q = new Queue<Cell>(dist.Keys);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (dist[c] >= 10) continue;
            foreach (var d in Enum.GetValues<Dir>())
            {
                var n = c.Step(d);
                if (dist.ContainsKey(n) || !Level.IsPassable(n) || Level.PitAt(n) is not null) continue;
                dist[n] = dist[c] + 1;
                q.Enqueue(n);
            }
        }
        var spots = dist.Where(kv => kv.Value >= 4 && !MonstersAt(kv.Key).Any() && CanPartyEnter(kv.Key)).Select(kv => kv.Key)
                        .OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        if (spots.Count == 0) return null;
        double total = table.Sum(t => t.Weight), r = Rng.Ai.Next(1_000_000) / 1_000_000.0 * total;
        string id = table[^1].Id;
        foreach (var t in table) if ((r -= t.Weight) < 0) { id = t.Id; break; }
        var def = Content.Creature(id);
        int lvl = Math.Max(1, (int)(Level.Depth * 1.6));
        if (lvl > def.Level) def = Bestiary.Scale(def, lvl);
        var m = AddMonster(def, spots[Rng.Ai.Next(spots.Count)], Dir.North);
        ApplyDifficulty(m);
        Alert(m);
        return m;
    }

    // ---- Materials across the party's packs ----

    /// <summary>How many of an item the whole party carries.</summary>
    public int Carried(string baseId) => Party.Members.Sum(c => c.Backpack.Where(i => i?.BaseId == baseId).Sum(i => i!.Count));

    public bool HasTool(string baseId) => Carried(baseId) > 0;

    /// <summary>Takes <paramref name="n"/> of an item from the party's packs. False (and nothing taken) if there aren't enough.</summary>
    private bool Spend(string baseId, int n)
    {
        if (Carried(baseId) < n) return false;
        foreach (var c in Party.Members)
            for (int i = 0; i < c.Backpack.Length && n > 0; i++)
                while (n > 0 && c.Backpack[i]?.BaseId == baseId) { c.TakeOne(i); n--; }
        return true;
    }

    /// <summary>Gives an item to the hero, or to whoever has room, or drops it at the party's feet.</summary>
    private void Give(Character c, Item item)
    {
        if (c.AddToBackpack(item)) return;
        foreach (var o in Party.Members) if (o.AddToBackpack(item)) return;
        Level.Drop(PartyCell, item);
    }

    // ---- Alchemy ----

    /// <summary>The effects of a reagent the party knows, in order.</summary>
    public IReadOnlyList<string> KnownEffects(string reagentId) =>
        Content.Reagent(reagentId) is { } r ? r.Effects.Take(ReagentKnown.GetValueOrDefault(reagentId)).ToList() : [];

    /// <summary>Tastes a reagent: it's used up, and its first unknown effects come to light (more with Alchemy: 1 + Alchemy / 34).</summary>
    public string Taste(Character c, int slot)
    {
        if (c.Backpack[slot] is not { } it || Content.Reagent(it.BaseId) is not { } r) return "That isn't a reagent.";
        int known = ReagentKnown.GetValueOrDefault(r.Id);
        if (known >= 4) return $"You know everything the {r.Name} can tell you.";
        c.TakeOne(slot);
        int learn = Math.Max(1, Math.Min(4, 1 + c.SkillLevel(SkillId.Alchemy) / 34) - known);
        ReagentKnown[r.Id] = Math.Min(4, known + learn);
        c.UseSkill(SkillId.Alchemy, 5, XpSource.Plain);
        return $"{c.Name} tastes the {r.Name}: {string.Join(", ", KnownEffects(r.Id).Skip(known).Select(e => Content.Effect(e).Name))}.";
    }

    /// <summary>
    /// Brews two or three reagents (a mortar in someone's pack). The effects shared by at least two of them make the
    /// potion, each as strong as the base × (1 + Alchemy / 50). No shared effect: the mixture is wasted.
    /// </summary>
    public string Brew(Character c, IReadOnlyList<int> slots)
    {
        if (!HasTool("mortar")) return "You need a mortar and pestle to brew.";
        if (slots.Count is < 2 or > 3 || slots.Distinct().Count() != slots.Count) return "Choose two or three reagents.";
        var reagents = slots.Select(s => c.Backpack[s] is { } it ? Content.Reagent(it.BaseId) : null).ToList();
        if (reagents.Any(r => r is null)) return "Only reagents go in the mortar.";
        foreach (var s in slots.OrderByDescending(s => s)) c.TakeOne(s);
        var shared = reagents.SelectMany(r => r!.Effects).GroupBy(e => e).Where(g => g.Count() >= 2).Select(g => g.Key)
                             .OrderBy(e => Array.IndexOf(Content.AlchemyEffects.Select(x => x.Id).ToArray(), e)).ToList();
        if (shared.Count == 0)
        {
            c.UseSkill(SkillId.Alchemy, 2, XpSource.Plain);
            return "The mixture turns to grey sludge. Nothing in it agrees.";
        }
        double power = 1 + c.SkillLevel(SkillId.Alchemy) / 50.0;
        var brew = new Item
        {
            BaseId = ItemBase.BrewId, Level = Math.Max(1, c.SkillLevel(SkillId.Alchemy) / 10),
            Effects = shared.Select(e => new AffixRoll(e, (int)Math.Round(Content.Effect(e).Amount * power))).ToList(),
        };
        // Brewing teaches what each reagent holds, up to the shared effects.
        foreach (var r in reagents)
        {
            int last = r!.Effects.Select((e, i) => (e, i)).Where(x => shared.Contains(x.e)).Select(x => x.i + 1).DefaultIfEmpty(0).Max();
            ReagentKnown[r.Id] = Math.Max(ReagentKnown.GetValueOrDefault(r.Id), last);
        }
        c.UseSkill(SkillId.Alchemy, 10 * shared.Count, XpSource.Plain);
        Give(c, brew);
        return $"{c.Name} brews a {brew.Name(Content)}.";
    }

    /// <summary>Drinks a brew, or, if every effect is harmful, coats the hero's weapon with it.</summary>
    private string DrinkBrew(Character c, int index)
    {
        var it = c.TakeOne(index)!;
        if (it.Effects.All(e => Content.Effect(e.Affix).Harmful) && it.Effects.Any(e => Content.Effect(e.Affix).Kind == "coat"))
        {
            foreach (var e in it.Effects.Where(e => Content.Effect(e.Affix).Kind == "coat"))
                c.Buffs["coat:" + Content.Effect(e.Affix).Scope] = 60 + e.Value; // the value is the save DC; it lasts a minute
            return $"{c.Name} coats their weapon with the {it.Name(Content).ToLowerInvariant()}.";
        }
        var said = new List<string>();
        foreach (var e in it.Effects)
        {
            var d = Content.Effect(e.Affix);
            switch (d.Kind)
            {
                case "heal": c.Heal(e.Value); said.Add($"+{e.Value} HP"); break;
                case "stamina": c.Stamina = Math.Min(c.MaxStamina, c.Stamina + e.Value); break;
                case "mana": c.Mana = Math.Min(c.MaxMana, c.Mana + e.Value); break;
                case "cure": if (Enum.TryParse<ConditionId>(d.Scope, true, out var cond)) c.Conditions.Cure(cond); break;
                case "buff": c.Buffs[d.Scope!] = Math.Max(c.Buffs.GetValueOrDefault(d.Scope!), e.Value); said.Add(d.Name); break;
                case "food": c.Food = Math.Min(100, c.Food + e.Value); break;
                case "water": c.Water = Math.Min(100, c.Water + e.Value); break;
                case "coat": if (Enum.TryParse<ConditionId>(d.Scope, true, out var bad)) c.ApplyCondition(bad, e.Value, 8); said.Add(d.Name + "!"); break;
                case "drain":
                    if (d.Scope == "mana") c.Mana = Math.Max(0, c.Mana - e.Value); else c.Stamina = Math.Max(0, c.Stamina - e.Value);
                    break;
                case "burn": HurtHero(c, e.Value, Enum.TryParse<DamageType>(d.Scope, true, out var t) ? t : DamageType.Fire); break;
            }
        }
        if (Party.AllDown) Die();
        return $"{c.Name} drinks the {it.Name(Content).ToLowerInvariant()}{(said.Count > 0 ? " (" + string.Join(", ", said) + ")" : "")}.";
    }

    /// <summary>A coated blade passes its poison (or sleep, or fear) on with each hit.</summary>
    private void Coating(Character c, Monster m, bool power)
    {
        foreach (var key in c.Buffs.Keys.Where(k => k.StartsWith("coat:", StringComparison.Ordinal)).ToList())
        {
            if (!Enum.TryParse<ConditionId>(key[5..], true, out var cond) || m.Def.ConditionImmune.Contains(cond)) continue;
            int dc = 10 + c.SkillLevel(SkillId.Alchemy) / 8;
            if (Checks.RollD20(Rng.Combat, Advantage.None) + m.SaveBonus(Rules.Attribute.Con) < dc)
            {
                m.Conditions.Apply(cond, 0, 8);
                Say($"The {m.Name} is {cond.ToString().ToLowerInvariant()}!");
            }
        }
    }

    // ---- Cooking ----

    /// <summary>Why a meal can't be cooked here, or null: a campfire kit, no enemies near, and the ingredients.</summary>
    public string? WhyNotCook(string recipeId)
    {
        var r = Content.Recipe(recipeId);
        if (!HasTool("campfireKit")) return "You need a campfire kit to cook.";
        if (WhyNotRest() is { } no) return no;
        foreach (var g in r.Ingredients.GroupBy(i => i))
            if (Carried(g.Key) < g.Count()) return $"You need {g.Count()} × {Content.Base(g.Key).Name}.";
        return null;
    }

    /// <summary>Cooks a meal: a better cook (Survival) makes its buff last longer.</summary>
    public string Cook(Character c, string recipeId)
    {
        if (WhyNotCook(recipeId) is { } no) return no;
        var r = Content.Recipe(recipeId);
        foreach (var g in r.Ingredients.GroupBy(i => i)) Spend(g.Key, g.Count());
        c.UseSkill(SkillId.Survival, 8 * r.Ingredients.Length, XpSource.Plain);
        Give(c, new Item { BaseId = "meal:" + r.Id, Level = Math.Max(1, c.SkillLevel(SkillId.Survival) / 10) });
        return $"{c.Name} cooks a {r.Name.ToLowerInvariant()}.";
    }

    // ---- Stations: anvils and enchanting altars ----

    /// <summary>A station next to the party or ahead of it, or a Waystone (which has both).</summary>
    public bool AtStation(string kind) =>
        AtWaystone || Level.Stations.Any(s => s.Value == kind && s.Key.Distance(PartyCell) <= 1);

    /// <summary>Raised when the party uses an anvil or altar (the crafting screen opens on that craft).</summary>
    public event Action<string>? StationTouched;

    private bool TouchStation(Cell ahead)
    {
        if (!Level.Stations.TryGetValue(ahead, out var kind)) return false;
        Say(kind == "anvil" ? "A forge, still warm, with an anvil beside it." : "An enchanter's altar. Spirit light pools in its bowl.");
        StationTouched?.Invoke(kind);
        return true;
    }

    // ---- Smithing ----

    /// <summary>The best quality a smith can work an item to: Smithing / 15, at most +5.</summary>
    public static int QualityCap(Character smith) => Math.Min(MaxQuality, smith.SkillLevel(SkillId.Smithing) / 15);

    public static int ImproveCost(Item it) => (it.Quality + 1) * 2;

    private static bool Smithable(ItemBase b) => b.Kind is ItemKind.Weapon or ItemKind.Shield or ItemKind.Armour || b.Gear is { Ac: > 0 };

    /// <summary>Raises an item's quality by one (+1 damage or AC), at an anvil, for scrap iron.</summary>
    public string Improve(Character smith, Item item)
    {
        if (!AtStation("anvil")) return "You need an anvil.";
        if (!Smithable(Content.Base(item.BaseId))) return "That can't be worked at an anvil.";
        if (item.Quality >= QualityCap(smith)) return $"{smith.Name} can't improve it further (Smithing {smith.SkillLevel(SkillId.Smithing)} reaches +{QualityCap(smith)}).";
        int cost = ImproveCost(item);
        if (!Spend("scrapIron", cost)) return $"That takes {cost} scrap iron.";
        item.Quality++;
        smith.UseSkill(SkillId.Smithing, 10 * item.Quality, XpSource.Plain);
        return $"{smith.Name} hammers the {Content.Base(item.BaseId).Name.ToLowerInvariant()} to +{item.Quality}.";
    }

    /// <summary>Melts an item in a hero's pack down to scrap iron: about one piece per kilogram.</summary>
    public string Melt(Character smith, int slot)
    {
        if (!AtStation("anvil")) return "You need a forge.";
        if (smith.Backpack[slot] is not { } it || !Smithable(Content.Base(it.BaseId))) return "Only weapons and armour melt down.";
        if (it.Cursed(Content)) return "The cursed thing won't melt.";
        smith.Backpack[slot] = null;
        int n = Math.Max(1, (int)Math.Round(Content.Base(it.BaseId).WeightKg));
        Give(smith, new Item { BaseId = "scrapIron", Count = n });
        smith.UseSkill(SkillId.Smithing, 3, XpSource.Plain);
        return $"{smith.Name} melts the {it.Name(Content).ToLowerInvariant()} into {n} scrap iron.";
    }

    /// <summary>What a smith can forge: weapons, shields and armour worth up to 20 + Smithing × 4.</summary>
    public IReadOnlyList<(ItemBase Base, int Scrap)> ForgeOptions(Character smith) =>
        Content.Bases.Values.Where(b => b.Kind is ItemKind.Weapon or ItemKind.Shield or ItemKind.Armour && b.Value <= 20 + smith.SkillLevel(SkillId.Smithing) * 4)
               .OrderBy(b => b.Kind).ThenBy(b => b.Value).ThenBy(b => b.Id, StringComparer.Ordinal)
               .Select(b => (b, Math.Max(2, (int)Math.Round(b.WeightKg * 2)))).ToList();

    public string Forge(Character smith, string baseId)
    {
        if (!AtStation("anvil")) return "You need an anvil.";
        var opt = ForgeOptions(smith).FirstOrDefault(o => o.Base.Id == baseId);
        if (opt.Base is null) return $"{smith.Name} doesn't know how to forge that yet.";
        if (!Spend("scrapIron", opt.Scrap)) return $"That takes {opt.Scrap} scrap iron.";
        var item = new Item { BaseId = baseId, Quality = Math.Min(QualityCap(smith), smith.SkillLevel(SkillId.Smithing) / 30), Level = Level.Depth };
        smith.UseSkill(SkillId.Smithing, 15, XpSource.Plain);
        Give(smith, item);
        return $"{smith.Name} forges a {item.Name(Content).ToLowerInvariant()}.";
    }

    // ---- Enchanting ----

    /// <summary>The most affixes an enchanter can bind into one item: 1, 2 from Enchanting 40, 3 from 75.</summary>
    public static int AffixCap(Character e) => e.SkillLevel(SkillId.Enchanting) >= 75 ? 3 : e.SkillLevel(SkillId.Enchanting) >= 40 ? 2 : 1;

    public static int EnchantCost(Item it) => 1 + it.Affixes.Count;

    /// <summary>Destroys an Enchanted or Masterwork item to learn one of its affixes.</summary>
    public string Disenchant(Character e, int slot)
    {
        if (!AtStation("altar")) return "You need an enchanting altar.";
        if (e.Backpack[slot] is not { } it || it.UniqueId is not null || it.Rarity is not (Rarity.Enchanted or Rarity.Masterwork or Rarity.Fine) || it.Affixes.Count == 0)
            return "Only enchanted and masterwork things give up their magic.";
        if (!it.Identified) return "Identify it first.";
        var learnable = it.Affixes.Select(a => a.Affix).Where(a => !Content.Affix(a).Curse).ToList();
        e.Backpack[slot] = null;
        e.UseSkill(SkillId.Enchanting, 10, XpSource.Plain);
        var fresh = learnable.FirstOrDefault(a => !Enchantments.Contains(a));
        if (fresh is null) return learnable.Count == 0 ? "Only a curse was in it. It's gone now, with the item." : "The magic unravels, but you knew it already.";
        Enchantments.Add(fresh);
        return $"{e.Name} unravels the {it.Name(Content).ToLowerInvariant()} and learns {Content.Affix(fresh).Name}.";
    }

    /// <summary>Binds a learned affix into an item with spirit shards; its strength scales with Enchanting.</summary>
    public string Enchant(Character e, Item item, string affixId)
    {
        if (!AtStation("altar")) return "You need an enchanting altar.";
        if (!Enchantments.Contains(affixId)) return "You haven't learned that enchantment.";
        var a = Content.Affix(affixId);
        var b = Content.Base(item.BaseId);
        string kind = b.Kind.ToString().ToLowerInvariant();
        if (!a.On.Contains(kind)) return $"{a.Name} doesn't go on a {kind}.";
        if (item.Affixes.Any(x => x.Affix == affixId)) return "It already has that.";
        if (item.UniqueId is not null) return "Its magic is too old to add to.";
        if (item.Affixes.Count >= AffixCap(e)) return $"{e.Name} can't bind more than {AffixCap(e)} into one thing yet.";
        int cost = EnchantCost(item);
        if (!Spend("spiritShard", cost)) return $"That takes {cost} spirit shard{(cost == 1 ? "" : "s")}.";
        int level = 1 + e.SkillLevel(SkillId.Enchanting) / 8;
        item.Affixes.Add(new AffixRoll(affixId, LootGenerator.RollValue(a, level, Rng.Loot)));
        if (item.Rarity is Rarity.Common or Rarity.Fine) item.Rarity = Rarity.Enchanted;
        item.Identified = true;
        e.UseSkill(SkillId.Enchanting, 20, XpSource.Plain);
        return $"{e.Name} binds {a.Name} into the {b.Name.ToLowerInvariant()}.";
    }

    /// <summary>Recharges a wand for one spirit shard: 6 + Enchanting / 10 charges.</summary>
    public string Recharge(Character e, int slot)
    {
        if (!AtStation("altar")) return "You need an enchanting altar.";
        if (e.Backpack[slot] is not { } it || Content.Base(it.BaseId).Kind != ItemKind.Wand) return "Only wands recharge.";
        if (!Spend("spiritShard", 1)) return "That takes a spirit shard.";
        it.Charges = Math.Max(it.Charges, 6 + e.SkillLevel(SkillId.Enchanting) / 10);
        e.UseSkill(SkillId.Enchanting, 8, XpSource.Plain);
        return $"The wand hums with {it.Charges:0} charges.";
    }

    // ---- What creatures leave ----

    /// <summary>Spirit shards and cooking parts from a kill (loot tables bring reagents).</summary>
    private void DropCraftingLoot(Monster m)
    {
        if (Content.AlchemyEffects.Count == 0 || m.Summoned) return;
        double shard = m.Def.Tier switch { Tier.Lesser => 0.03, Tier.Common => 0.1, Tier.Greater => 0.3, _ => 0.6 } + (m.Elite ? 0.2 : 0);
        int shards = m.Boss is not null || m.Affixes.Count > 0 ? 1 + Rng.Loot.Next(3) : Rng.Loot.Next(1000) < shard * 1000 ? 1 : 0;
        if (shards > 0) Level.Drop(m.Cell, new Item { BaseId = "spiritShard", Count = shards });
        foreach (var i in Content.Ingredients.Where(i => i.From.Contains(m.Def.Id)))
            if (Rng.Loot.Next(100) < i.Chance) Level.Drop(m.Cell, new Item { BaseId = i.Id });
    }
}
