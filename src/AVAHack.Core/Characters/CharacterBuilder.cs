using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.Characters;

/// <summary>Makes heroes (spec 5): ancestry, calling, background and attributes, checked.</summary>
public static class CharacterBuilder
{
    /// <summary>
    /// Creates a level-1 hero. <paramref name="scores"/> are before ancestry bonuses. Unless
    /// <paramref name="rolled"/>, they must be a legal 27-point buy; rolled scores must be 3–18.
    /// </summary>
    public static Character Create(ContentDb content, string name, string ancestry, string calling, string background,
                                   AttributeSet scores, bool rolled = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A hero needs a name.", nameof(name));
        var all = Enum.GetValues<Rules.Attribute>();
        if (rolled)
        {
            if (all.Any(a => scores[a] is < 3 or > 18)) throw new ArgumentException("Rolled scores run 3–18.", nameof(scores));
        }
        else if (!AttributeGen.IsValidPointBuy(scores))
        {
            throw new ArgumentException($"Not a legal point buy: scores 8–15 costing at most {AttributeGen.PointBuyBudget} points.", nameof(scores));
        }
        return new Character(content, name.Trim(), content.Ancestry(ancestry), content.Calling(calling), content.Background(background), scores);
    }

    /// <summary>The quick-start party (spec 5.5): a fighter, a rogue, a cleric and a wizard.</summary>
    public static IReadOnlyList<Character> QuickStartParty(ContentDb content) =>
    [
        Create(content, "Brannoc", "dwarf", "fighter", "mercenary", new AttributeSet(15, 12, 14, 8, 12, 10)),
        Create(content, "Pell", "halfling", "rogue", "cutpurse", new AttributeSet(8, 15, 13, 14, 12, 10)),
        Create(content, "Sister Ama", "human", "cleric", "acolyte", new AttributeSet(12, 10, 14, 8, 15, 12)),
        Create(content, "Ithren", "elf", "wizard", "cartographer", new AttributeSet(8, 14, 13, 15, 12, 10)),
    ];
}
