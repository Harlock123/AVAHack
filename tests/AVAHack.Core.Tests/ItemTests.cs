using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Core.Tests;

public sealed class ItemTests
{
    private static ContentDb Db => Heroes.Content;

    [Fact]
    public void Every_item_base_is_known_and_the_content_is_consistent()
    {
        Assert.Empty(Db.Validate());
        Assert.True(Db.Bases.Count > 60);
        Assert.Equal(22, Db.Books.Count);
        Assert.All(Db.Uniques, u => Assert.True(Db.Bases.ContainsKey(u.Base)));
    }

    [Fact]
    public void Rarity_follows_the_weights()
    {
        var rng = new Pcg32(5);
        var counts = new Dictionary<Rarity, int>();
        for (int i = 0; i < 20_000; i++)
        {
            var r = LootGenerator.RollRarity(rng, MagicItems.Normal);
            counts[r] = counts.GetValueOrDefault(r) + 1;
        }
        Assert.InRange(counts[Rarity.Common] / 200.0, 56, 64);
        Assert.InRange(counts[Rarity.Fine] / 200.0, 22, 28);
        Assert.InRange(counts[Rarity.Enchanted] / 200.0, 9, 13);
        Assert.InRange(counts[Rarity.Masterwork] / 200.0, 2.5, 4.5);
        var plenty = Enumerable.Range(0, 5000).Count(_ => LootGenerator.RollRarity(rng, MagicItems.Plentiful) != Rarity.Common);
        Assert.True(plenty > 5000 * 0.55);
    }

    [Fact]
    public void Magic_items_get_affixes_by_rarity_and_level()
    {
        var gen = new LootGenerator(Db);
        var rng = new Pcg32(9);
        var made = Enumerable.Range(0, 400).Select(_ => gen.Make("longsword", 5, rng)).ToList();
        foreach (var it in made)
        {
            int nonCurse = it.Affixes.Count(a => !Db.Affix(a.Affix).Curse);
            int expected = it.Rarity switch { Rarity.Common => 0, Rarity.Fine => 1, Rarity.Enchanted => 2, Rarity.Masterwork => -1, _ => -2 };
            if (expected >= 0) Assert.Equal(expected, nonCurse);
            if (it.Rarity == Rarity.Masterwork) Assert.InRange(nonCurse, 3, 4);
            Assert.All(it.Affixes, a => Assert.Contains("weapon", Db.Affix(a.Affix).On));
        }
        Assert.Contains(made, i => i.Rarity == Rarity.Masterwork && i.Cursed(Db));
        Assert.All(made.Where(i => i.Rarity is Rarity.Enchanted or Rarity.Masterwork), i => Assert.False(i.Identified));
    }

    [Fact]
    public void Affixes_change_the_heros_numbers()
    {
        var b = Heroes.Brannoc(); // STR 16, AC 11 bare
        b.Wear(new Item { BaseId = "amulet", Affixes = [new("ofTheBear", 2), new("ofVigour", 5)] });
        Assert.Equal(18, b.Score(Attribute.Str));
        Assert.Equal(13 + 5, b.MaxHp);
        b.Wear(new Item { BaseId = "ring", Affixes = [new("sturdy", 1), new("fireproof", 1)] });
        Assert.Equal(12, b.ArmourClass);
        Assert.Equal(Affinity.Resistant, b.Resistances.For(DamageType.Fire));
        b.Wear(new Item { BaseId = "longsword", Affixes = [new("keen", 2), new("ofTheBlade", 5)] });
        Assert.Equal(3 + 1 + 2 + 2, b.AttackBonus(SkillId.Blade, Attribute.Str)); // STR 18 (+4), Blade 20+5 (2), keen 2
        Assert.Equal(25, b.SkillLevel(SkillId.Blade));
        Assert.Equal(20, b.Rank(SkillId.Blade)); // progress is the bare rank
    }

    [Fact]
    public void Items_can_lift_a_score_past_20_but_not_past_24()
    {
        var b = Heroes.Brannoc();
        b.Wear(new Item { BaseId = "amulet", Affixes = [new("ofTheBear", 12)] });
        Assert.Equal(24, b.Score(Attribute.Str));
    }

    [Fact]
    public void Chest_armour_and_gear_make_the_armour_class()
    {
        var p = Heroes.Pell(); // DEX +3
        p.Wear(new Item { BaseId = "scale" });  // medium: 4, DEX up to 2
        p.Wear(new Item { BaseId = "ironHelm" });
        p.Wear(new Item { BaseId = "buckler" });
        Assert.Equal(10 + 4 + 2 + 1 + 1, p.ArmourClass);
        Assert.Equal(SkillId.LightArmour, p.ArmourSkill);
    }

    [Fact]
    public void Equipping_from_the_backpack_swaps_and_two_handers_take_both_hands()
    {
        var b = Heroes.Brannoc();
        b.Equip(Db.Weapon("longsword"), Db.Shield("roundShield"), Db.ArmourPiece("chainMail"));
        b.AddToBackpack(new Item { BaseId = "greatsword" });
        Assert.True(b.Equip(0));
        Assert.Equal("greatsword", b.MainHand!.Id);
        Assert.Null(b.OffHand);
        Assert.Contains(b.Backpack, i => i?.BaseId == "longsword");
        Assert.Contains(b.Backpack, i => i?.BaseId == "roundShield");
        int shield = Array.FindIndex(b.Backpack, i => i?.BaseId == "roundShield");
        Assert.True(b.Equip(shield)); // the shield pushes the greatsword back to the pack
        Assert.Null(b.MainHand);
        Assert.Equal("roundShield", b.OffHand!.Id);
    }

    [Fact]
    public void Cursed_things_stick_until_lifted()
    {
        var b = Heroes.Brannoc();
        b.Wear(new Item { BaseId = "ring", Rarity = Rarity.Masterwork, Affixes = [new("ofTheBear", 2), new("cursedFrail", -4)] }, EquipSlot.Ring1);
        Assert.False(b.Unequip(EquipSlot.Ring1));
        Assert.Equal(13 - 4, b.MaxHp);
        Assert.Equal(1, b.LiftCurses());
        Assert.True(b.Unequip(EquipSlot.Ring1));
    }

    [Fact]
    public void Potions_stack_and_heal_and_books_teach()
    {
        var b = Heroes.Brannoc();
        b.AddToBackpack(new Item { BaseId = "potionHealing", Count = 2 });
        b.AddToBackpack(new Item { BaseId = "potionHealing" });
        Assert.Equal(3, b.Backpack[0]!.Count);
        b.TakeDamage(10);
        var said = b.Use(0, new ScriptedRng(4, 4));
        Assert.Contains("heals 10", said);
        Assert.Equal(13, b.Hp);
        Assert.Equal(2, b.Backpack[0]!.Count);
        b.AddToBackpack(new Item { BaseId = "book.lore" });
        int i = Array.FindIndex(b.Backpack, x => x?.BaseId == "book.lore");
        Assert.Contains("Lore rises to 6", b.Use(i, new Pcg32(1)));
        Assert.Null(b.Backpack[i]);
    }

    [Fact]
    public void Names_hide_what_is_unknown()
    {
        var it = new Item { BaseId = "longsword", Rarity = Rarity.Enchanted, Affixes = [new("keen", 1), new("ofTheBear", 1)], Identified = false };
        Assert.Equal("Unidentified Longsword", it.Name(Db));
        it.Identified = true;
        Assert.Equal("Keen Longsword of the Bear", it.Name(Db));
        var relic = new LootGenerator(Db).MakeUnique("relic.wardensMace", 1);
        Assert.False(relic.Identified);
        relic.Identified = true;
        Assert.Equal("The Warden's Mace", relic.Name(Db));
    }

    [Fact]
    public void A_set_gives_more_the_more_pieces_are_worn()
    {
        var b = Heroes.Brannoc();
        var gen = new LootGenerator(Db);
        b.Wear(gen.MakeUnique("set.watch.helm", 1));
        int one = b.ArmourClass;
        b.Wear(gen.MakeUnique("set.watch.gauntlets", 1));
        Assert.Equal(one + 1 + 1, b.ArmourClass); // the gauntlets' AC and the two-piece bonus
    }

    [Fact]
    public void Chests_spill_their_loot_and_the_party_picks_it_up()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nchest: 2 1 chest.rich\nmap:\n#####\n#...#\n#####");
        var g = new GameEngine(l, 3);
        Assert.False(g.CanPartyEnter(new Cell(2, 1)));
        g.Press(GameAction.Use);
        Assert.True(l.ChestAt(new Cell(2, 1))!.Opened);
        int lying = l.PileAt(g.PartyCell).Count;
        Assert.True(lying >= 3);
        int gold = g.Party.Gold;
        g.Press(GameAction.PickUp);
        Assert.Empty(l.PileAt(g.PartyCell));
        Assert.True(g.Party.Gold > gold || g.Hero.FreeSlots < Character.BackpackSize);
    }

    [Fact]
    public void Lore_identifies_what_it_knows()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n####\n#..#\n####");
        var g = new GameEngine(l);
        var hero = g.Hero; // Lore 5
        l.Drop(g.PartyCell, new Item { BaseId = "ring", Rarity = Rarity.Enchanted, Affixes = [new("ofTheOwl", 1), new("sturdy", 1)], Identified = false });
        g.PickUpAll();
        var ring = hero.Backpack.First(i => i?.BaseId == "ring")!;
        Assert.False(ring.Identified);
        hero.GainSkillXp(SkillId.Lore, 1_000_000, XpSource.Plain); // up to the ceiling: 40 or more
        Assert.True(hero.SkillLevel(SkillId.Lore) >= 25);
        g.IdentifyAll();
        Assert.True(ring.Identified);
    }

    [Fact]
    public void Creatures_drop_their_loot_where_they_die()
    {
        var l = LevelLoader.Parse("start: 1 1 east\nmap:\n#####\n#...#\n#####");
        int drops = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var lv = LevelLoader.Parse("start: 1 1 east\nmap:\n#####\n#...#\n#####");
            var g = new GameEngine(lv, seed);
            var gob = g.AddMonster("gutterGoblin", new Cell(2, 1), Dir.West);
            for (int i = 0; i < 60 * 30 && gob.IsAlive; i++)
            {
                if (g.RightHand.Ready) { g.HandDown(HandSide.Right); g.HandUp(HandSide.Right); }
                g.Tick();
            }
            if (lv.PileAt(new Cell(2, 1)).Count > 0) drops++;
        }
        Assert.InRange(drops, 10, 30); // goblins drop something most of the time
        Assert.NotNull(l);
    }
}
