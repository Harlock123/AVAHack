using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.Core.Tests;

public sealed class ContentTests
{
    [Fact]
    public void The_built_in_content_loads_and_is_consistent()
    {
        var db = ContentDb.LoadBuiltIn();
        Assert.Empty(db.Validate());
        Assert.Equal(22, db.Skills.Count);
        Assert.Equal(8, db.Ancestries.Count);
        Assert.Equal(10, db.Callings.Count);
        Assert.Equal(8, db.Backgrounds.Count);
        Assert.Equal(14, db.Conditions.Count);
        Assert.Equal(22, db.Perks.Select(p => p.Skill).Distinct().Count()); // every skill has its tree
        Assert.True(db.Perks.Count >= 140);
    }

    [Fact]
    public void Every_combination_of_ancestry_calling_and_background_makes_a_hero()
    {
        var db = ContentDb.LoadBuiltIn();
        foreach (var a in db.Ancestries)
        foreach (var c in db.Callings)
        foreach (var b in db.Backgrounds)
        {
            var h = CharacterBuilder.Create(db, "Test", a.Id, c.Id, b.Id, new AttributeSet(8, 8, 8, 8, 8, 8));
            Assert.True(h.MaxHp >= 1);
            Assert.All(Enum.GetValues<SkillId>(), s => Assert.InRange(h.Rank(s), 5, Progression.StartingCap));
        }
    }

    [Fact]
    public void Reading_is_strict()
    {
        const string ok = """[{ "id": "x", "name": "X", "skill": "blade", "item": "i", "text": "t" }]""";
        Assert.Single(ContentDb.Parse<BackgroundDef>(ok, "test"));
        // An unknown field, a missing required field, an unknown enum value and a number for an enum all fail.
        Assert.Throws<InvalidDataException>(() => ContentDb.Parse<BackgroundDef>("""[{ "id": "x", "name": "X", "skill": "blade", "item": "i", "text": "t", "colour": "red" }]""", "test"));
        Assert.Throws<InvalidDataException>(() => ContentDb.Parse<BackgroundDef>("""[{ "id": "x", "name": "X", "skill": "blade", "text": "t" }]""", "test"));
        Assert.Throws<InvalidDataException>(() => ContentDb.Parse<BackgroundDef>("""[{ "id": "x", "name": "X", "skill": "juggling", "item": "i", "text": "t" }]""", "test"));
        Assert.Throws<InvalidDataException>(() => ContentDb.Parse<BackgroundDef>("""[{ "id": "x", "name": "X", "skill": 3, "item": "i", "text": "t" }]""", "test"));
        Assert.Throws<InvalidDataException>(() => ContentDb.Parse<BackgroundDef>("""[{ "id": "x", "name": null, "skill": "blade", "item": "i", "text": "t" }]""", "test"));
    }

    [Fact]
    public void The_validator_finds_broken_content()
    {
        var db = ContentDb.LoadBuiltIn();
        var badCalling = db.Callings[0] with { HitDie = 7, Minor = [SkillId.Blade, SkillId.Blade, SkillId.Lore] };
        var badPerks = db.Perks.Select(p => p.Id switch
        {
            "blade.quickDraw" => p with { Requires = ["stealth.hush"], Ranks = [30, 20] },
            "blade.bleeder" => p with { Requires = ["blade.whirl"] }, // whirl already requires bleeder
            _ => p,
        }).ToList();
        badPerks.Add(new PerkDef { Id = "blade.edgeDiscipline", Skill = SkillId.Blade, Name = "Again", Ranks = [0], Text = "dup" });
        var badAncestry = db.Ancestries[0] with { Traits = ["wings"], Attributes = new() { ["luck"] = 1 } };
        var badCondition = db.Conditions[0] with { DamagePerSecond = "lots", Effects = ["glowing"] };
        var broken = new ContentDb(db.Skills.Skip(1).ToList(), [badAncestry, .. db.Ancestries.Skip(1)], [badCalling, .. db.Callings.Skip(1)],
                                   db.Backgrounds, badPerks, [badCondition, .. db.Conditions.Skip(1)]);
        var problems = string.Join('\n', broken.Validate());
        Assert.Contains("Blade has no definition", problems);
        Assert.Contains("hit die is d7", problems);
        Assert.Contains("three different minor skills", problems);
        Assert.Contains("which is in another tree", problems);
        Assert.Contains("requirements must rise", problems);
        Assert.Contains("'blade.edgeDiscipline' is defined 2 times", problems);
        Assert.Contains("unknown trait 'wings'", problems);
        Assert.Contains("'luck', which is not an attribute", problems);
        Assert.Contains("is not dice", problems);
        Assert.Contains("unknown effect 'glowing'", problems);
        Assert.Contains("requires itself", problems);
    }

    [Fact]
    public void The_validator_checks_the_regions()
    {
        var db = ContentDb.LoadBuiltIn();
        var regions = db.Regions.Select(r => r.Id switch
        {
            "works" => r with { Families = ["oozes", "dragons"] },
            "choir" => r with { Boss = "cellarRat" },
            "stacks" => r with { Final = true },
            "rootdark" => r with { PackMax = 1 },
            "glass" => r with { MaxLevel = 0, Beneath = "theKnell" },
            _ => r,
        }).ToList();
        var broken = new ContentDb(db.Skills, db.Ancestries, db.Callings, db.Backgrounds, db.Perks, db.Conditions)
            { Creatures = db.Creatures, Regions = regions };
        var problems = string.Join('\n', broken.Validate());
        Assert.Contains("family 'dragons', which has no creatures", problems);
        Assert.Contains("boss 'cellarRat' is not a boss creature", problems);
        Assert.Contains("exactly one region must be final", problems);
        Assert.Contains("'rootdark' has packMax 1", problems);
        Assert.Contains("'glass' has maxLevel 0", problems);
        Assert.Contains("'glass''s beneath 'theKnell' must be a boss creature, under the final region", problems);
    }
}
