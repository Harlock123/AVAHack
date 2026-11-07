using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>M8: food and water, resting, Alchemy, cooking, Smithing and Enchanting (spec 13.5–13.7).</summary>
public sealed class CraftingTests
{
    private const string Hall = """
        start: 2 3 east
        map:
        ###########
        #.........#
        #.........#
        #.........#
        #.........#
        #.........#
        ###########
        """;

    /// <summary>A small room shut by a door: the party starts inside.</summary>
    private const string Cell = """
        start: 2 2 east
        map:
        #########
        #...#...#
        #...+...#
        #...#...#
        #########
        """;

    private static readonly ContentDb Db = ContentDb.LoadBuiltIn();

    private static GameEngine Game(string map = Hall, ulong seed = 3)
    {
        var g = new GameEngine(LevelLoader.Parse(map), seed, PartyTests.QuickParty(Db), Db);
        foreach (var h in g.Party.Members) Array.Clear(h.Backpack);
        return g;
    }

    private static void Run(GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60 && g.Mode == GameMode.Playing; i++) g.Tick();
    }

    private static Character Hero(GameEngine g) => g.Party.Members.First();

    /// <summary>Raises a skill to a rank, as mid-game play would.</summary>
    private static void Skill(Character c, SkillId s, int rank)
    {
        while (c.Rank(s) < rank) c.GainSkillXp(s, 1_000_000, XpSource.Plain);
    }

    private static int Give(Character c, string baseId, int count = 1)
    {
        c.AddToBackpack(new Item { BaseId = baseId, Count = count });
        return Array.FindIndex(c.Backpack, i => i?.BaseId == baseId);
    }

    // ---- Content ----

    [Fact]
    public void The_crafting_content_is_there_and_every_effect_can_be_brewed()
    {
        Assert.Equal(30, Db.AlchemyEffects.Count);
        Assert.Equal(40, Db.Reagents.Count);
        Assert.All(Db.AlchemyEffects, e => Assert.True(Db.Reagents.Count(r => r.Effects.Contains(e.Id)) >= 2, e.Id));
        Assert.True(Db.Recipes.Count >= 8);
        Assert.Empty(Db.Validate());
        foreach (var id in new[] { "spiritShard", "scrapIron", "mortar", "campfireKit", ItemBase.BrewId }) Assert.True(Db.Bases.ContainsKey(id), id);
    }

    // ---- Food and water ----

    [Fact]
    public void Food_and_water_drain_and_hunger_slows_healing_then_starving_hurts()
    {
        var g = Game();
        g.FoodOn = true;
        var h = Hero(g);
        Run(g, 60);
        Assert.InRange(h.Food, 100 - 60 * GameEngine.FoodPerSecond - 0.1, 100 - 60 * GameEngine.FoodPerSecond + 0.1);
        Assert.True(h.Water < h.Food);
        h.Food = Character.HungryBelow + 0.001;
        Run(g, 1);
        Assert.Contains(g.Log, l => l.Contains("hungry", StringComparison.Ordinal));

        h.Food = 10;
        h.TakeDamage(10);
        double before = h.Hp;
        h.Regenerate(1);
        Assert.Equal(before + h.MaxHp * 0.01 * 0.5, h.Hp, 3);
        Assert.True(h.Hungry);

        h.Food = 0;
        double hp = h.Hp;
        Run(g, GameEngine.StarveEvery + 0.1);
        Assert.True(h.Hp < hp + h.MaxHp * 0.01 * 0.5 * 10.2);

        int slot = Give(h, "ration");
        g.UseItem(h, slot);
        Assert.Equal(50, h.Food, 1);
    }

    [Fact]
    public void With_food_off_nothing_drains()
    {
        var g = Game();
        Run(g, 120);
        Assert.Equal(100, Hero(g).Food);
    }

    // ---- Resting ----

    [Fact]
    public void Resting_heals_the_party_unless_something_is_near()
    {
        var g = Game(Cell);
        var h = Hero(g);
        h.TakeDamage(h.MaxHp / 2);
        Assert.Equal(0, g.EncounterChanceHere()); // a cleared room behind a shut door
        Assert.Contains("wakes whole", g.RestParty());
        Assert.Equal(h.MaxHp, h.Hp);

        var m = g.AddMonster("shambler", new Cell(3, 1), Dir.West);
        m.State = MonsterState.Aware;
        Assert.Contains("enemies near", g.RestParty());
    }

    [Fact]
    public void Resting_in_the_open_brings_wandering_monsters_some_of_the_time()
    {
        int encounters = 0, quiet = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var g = Game(Hall, seed);
            var h = Hero(g);
            h.TakeDamage(h.MaxHp - 1); // a long, eight-hour rest
            Assert.Equal(GameEngine.EncounterChance, g.EncounterChanceHere(), 3);
            string said = g.RestParty();
            if (g.Monsters.Any(x => x.IsAware)) { encounters++; Assert.True(h.Hp < h.MaxHp); }
            else if (said.Contains("wakes whole", StringComparison.Ordinal)) quiet++;
        }
        // 1 − 0.85⁸ ≈ 73% of eight-hour rests are interrupted.
        Assert.InRange(encounters, 14, 29);
        Assert.True(quiet > 0);
    }

    [Fact]
    public void A_Waystone_is_a_safe_place_to_rest()
    {
        var g = Game();
        g.Level.Waystone = new Cell(3, 3);
        Assert.Equal(0, g.EncounterChanceHere());
    }

    // ---- Alchemy ----

    private static (string A, string B) HealingPair() =>
        Db.Reagents.SelectMany(a => Db.Reagents.Where(b => string.CompareOrdinal(a.Id, b.Id) < 0 && a.Effects.Contains("heal") && b.Effects.Contains("heal")
                                                         && a.Effects.Intersect(b.Effects).All(e => !Db.Effect(e).Harmful))
                                     .Select(b => (a.Id, b.Id))).First();

    [Fact]
    public void Tasting_a_reagent_reveals_its_first_effect()
    {
        var g = Game();
        var h = Hero(g);
        var r = Db.Reagents[5];
        int slot = Give(h, r.Id, 2);
        string said = g.Taste(h, slot);
        Assert.Equal([r.Effects[0]], g.KnownEffects(r.Id));
        Assert.Contains(Db.Effect(r.Effects[0]).Name, said);
        Assert.Equal(1, h.Backpack[slot]!.Count);
    }

    [Fact]
    public void Brewing_needs_a_mortar_and_makes_what_the_reagents_share()
    {
        var g = Game();
        var h = Hero(g);
        var (a, b) = HealingPair();
        int sa = Give(h, a), sb = Give(h, b);
        Assert.Contains("mortar", g.Brew(h, [sa, sb]));
        Give(h, "mortar");
        g.Brew(h, [sa, sb]);
        var brew = h.Backpack.Single(i => i?.BaseId == ItemBase.BrewId)!;
        var shared = Db.Reagent(a)!.Effects.Intersect(Db.Reagent(b)!.Effects).ToList();
        Assert.Equal(shared.Order(), brew.Effects.Select(e => e.Affix).Order());
        Assert.StartsWith("Potion of", brew.Name(Db));
        Assert.Contains("heal", g.KnownEffects(a));
        Assert.Equal(0, g.Carried(a));

        h.TakeDamage(20);
        double hp = h.Hp;
        g.UseItem(h, Array.IndexOf(h.Backpack, brew));
        Assert.True(h.Hp > hp);
    }

    [Fact]
    public void Reagents_with_nothing_in_common_make_sludge()
    {
        var g = Game();
        var h = Hero(g);
        Give(h, "mortar");
        var (a, b) = Db.Reagents.SelectMany(x => Db.Reagents.Where(y => x.Id != y.Id && !x.Effects.Intersect(y.Effects).Any()).Select(y => (x.Id, y.Id))).First();
        int sa = Give(h, a), sb = Give(h, b);
        Assert.Contains("sludge", g.Brew(h, [sa, sb]));
        Assert.DoesNotContain(h.Backpack, i => i?.BaseId == ItemBase.BrewId);
    }

    [Fact]
    public void A_poison_brew_coats_the_blade_instead_of_being_drunk()
    {
        var g = Game();
        var h = Hero(g);
        var brew = new Item { BaseId = ItemBase.BrewId, Effects = [new("venom", 14)] };
        h.AddToBackpack(brew);
        g.UseItem(h, Array.IndexOf(h.Backpack, brew));
        Assert.True(h.Buffs.ContainsKey("coat:poisoned"));
        Assert.False(h.Conditions.Has(ConditionId.Poisoned));
    }

    // ---- Cooking ----

    [Fact]
    public void Cooking_needs_a_kit_and_the_ingredients_and_a_meal_feeds_and_strengthens()
    {
        var g = Game();
        g.FoodOn = true;
        var h = Hero(g);
        Give(h, "wargSteak");
        Give(h, "salt");
        Assert.Contains("campfire", g.Cook(h, "wargRoast"));
        Give(h, "campfireKit");
        Skill(h, SkillId.Survival, 40);
        g.Cook(h, "wargRoast");
        Assert.Equal(0, g.Carried("wargSteak"));
        h.Food = 20;
        g.UseItem(h, Array.FindIndex(h.Backpack, i => i?.BaseId == "meal:wargRoast"));
        Assert.Equal(85, h.Food, 1);
        Assert.True(h.Buffs["might"] > 300); // a mid-game cook's meal lasts longer
        Assert.Contains("need", g.Cook(h, "wargRoast"));
    }

    // ---- Smithing ----

    private static GameEngine AtForge()
    {
        var g = Game();
        g.Level.Stations[new Cell(3, 3)] = "anvil";
        g.Level.Stations[new Cell(2, 4)] = "altar";
        return g;
    }

    [Fact]
    public void Smiths_melt_down_improve_and_forge_up_to_their_skill()
    {
        var g = AtForge();
        var h = Hero(g);
        var sword = h.ItemIn(EquipSlot.RightHand)!;
        g.Improve(h, sword); // no scrap yet
        Assert.Equal(0, sword.Quality);
        Skill(h, SkillId.Smithing, 45);
        Assert.True(h.SkillLevel(SkillId.Smithing) >= 45, $"rank {h.Rank(SkillId.Smithing)} level {h.SkillLevel(SkillId.Smithing)} ceiling {h.SkillCeiling}");
        Assert.Equal(3, GameEngine.QualityCap(h));
        int slot = Give(h, "chainMail");
        g.Melt(h, slot);
        Assert.True(g.Carried("scrapIron") >= 10);
        Give(h, "scrapIron", 20);
        for (int i = 0; i < 4; i++) g.Improve(h, sword);
        Assert.Equal(3, sword.Quality);
        Assert.Contains("+3", sword.Name(Db));
        Assert.NotEmpty(g.ForgeOptions(h));
        var best = g.ForgeOptions(h).First(o => o.Base.Kind == ItemKind.Weapon);
        Give(h, "scrapIron", best.Scrap);
        g.Forge(h, best.Base.Id);
        Assert.Contains(h.Backpack, i => i?.BaseId == best.Base.Id && i.Quality == 1);

        g.PlaceParty(new Cell(8, 1), Dir.East);
        Assert.Contains("anvil", g.Improve(h, sword));
    }

    // ---- Enchanting ----

    [Fact]
    public void Enchanters_unravel_items_to_learn_affixes_and_bind_them_with_shards()
    {
        var g = AtForge();
        var h = Hero(g);
        var magic = new Item { BaseId = "ring", Rarity = Rarity.Enchanted, Affixes = [new("ofVigour", 4)] };
        int slot = Array.IndexOf(h.Backpack, null);
        h.Backpack[slot] = magic;
        g.Disenchant(h, slot);
        Assert.Contains("ofVigour", g.Enchantments);
        Assert.Null(h.Backpack[slot]);

        var helm = new Item { BaseId = "ironHelm" };
        h.AddToBackpack(helm);
        Assert.Contains("shard", g.Enchant(h, helm, "ofVigour"));
        Give(h, "spiritShard", 5);
        Skill(h, SkillId.Enchanting, 50);
        g.Enchant(h, helm, "ofVigour");
        Assert.Single(helm.Affixes);
        Assert.Equal(Rarity.Enchanted, helm.Rarity);
        Assert.Equal(4, g.Carried("spiritShard"));

        var wand = new Item { BaseId = "wand", SpellId = Db.Spells.First(s => !s.Cantrip).Id, Charges = 0 };
        h.AddToBackpack(wand);
        g.Recharge(h, Array.IndexOf(h.Backpack, wand));
        Assert.Equal(11, wand.Charges);
    }

    // ---- What creatures leave ----

    [Fact]
    public void Champions_leave_spirit_shards_and_beasts_leave_meat()
    {
        var g = Game();
        var m = g.AddMonster("wargRider", new Cell(6, 3), Dir.West);
        g.MakeChampion(m, 1, "vampiric");
        g.DamageMonster(m, m.Hp + 100, DamageType.Slashing);
        Assert.Contains(g.Level.PileAt(new Cell(6, 3)), i => i.BaseId == "spiritShard");
        int meat = 0;
        for (int i = 0; i < 20; i++)
        {
            var w = g.AddMonster("wargRider", new Cell(7, 1 + i % 4), Dir.West);
            g.DamageMonster(w, w.Hp + 100, DamageType.Slashing);
        }
        foreach (var pile in g.Level.Piles.Values) meat += pile.Where(i => i.BaseId == "wargSteak").Sum(i => i.Count);
        Assert.InRange(meat, 8, 20);
    }

    [Fact]
    public void Crafting_knowledge_and_brews_survive_a_save()
    {
        var g = new GameEngine(LevelLoader.LoadBuiltIn("test-hall"), 4, PartyTests.QuickParty(Db), Db);
        g.ReagentKnown["gravecap"] = 2;
        g.Enchantments.Add("ofVigour");
        Hero(g).AddToBackpack(new Item { BaseId = ItemBase.BrewId, Effects = [new("heal", 20), new("mending", 80)] });
        var back = GameEngine.FromSave(g.ToSave(), Db);
        Assert.Equal(2, back.ReagentKnown["gravecap"]);
        Assert.Contains("ofVigour", back.Enchantments);
        var brew = back.Party.Members.First().Backpack.Single(i => i?.BaseId == ItemBase.BrewId)!;
        Assert.Equal(2, brew.Effects.Count);
        Assert.Equal("Potion of Healing and Mending", brew.Name(Db));
    }

    // ---- M8's exit test: each craft yields an item that matters by mid-game ----

    [Fact]
    public void At_mid_game_skill_every_craft_beats_what_you_find()
    {
        var g = AtForge();
        var h = Hero(g);
        foreach (var s in new[] { SkillId.Alchemy, SkillId.Smithing, SkillId.Enchanting, SkillId.Survival }) Skill(h, s, 45);

        // Alchemy: a brewed healing potion heals more than a bought Potion of Healing (2d4+2, at most 10).
        Give(h, "mortar");
        var (a, b) = HealingPair();
        g.Brew(h, [Give(h, a), Give(h, b)]);
        var brew = h.Backpack.First(i => i?.BaseId == ItemBase.BrewId)!;
        Assert.True(brew.Effects.Single(e => e.Affix == "heal").Value > 10);

        // Smithing: a +3 weapon, three more damage on every blow.
        var sword = h.ItemIn(EquipSlot.RightHand)!;
        Give(h, "scrapIron", 30);
        string said;
        while ((said = g.Improve(h, sword)).Contains("hammers", StringComparison.Ordinal)) { }
        Assert.True(sword.Quality >= 3, said);

        // Enchanting: two affixes bound into one helm.
        g.Enchantments.AddRange(["ofVigour", Db.Affixes.First(x => x.On.Contains("gear") && !x.Curse && x.Id != "ofVigour").Id]);
        var helm = new Item { BaseId = "ironHelm" };
        h.AddToBackpack(helm);
        Give(h, "spiritShard", 10);
        foreach (var e in g.Enchantments) g.Enchant(h, helm, e);
        Assert.Equal(2, helm.Affixes.Count);

        // Cooking: a meal that feeds and gives a buff for longer than five minutes.
        Give(h, "campfireKit");
        Give(h, "beetleMeat");
        Give(h, "flour");
        g.Cook(h, "beetleBake");
        g.UseItem(h, Array.FindIndex(h.Backpack, i => i?.BaseId == "meal:beetleBake"));
        Assert.True(h.Buffs["bulwark"] > 300);
    }
}
