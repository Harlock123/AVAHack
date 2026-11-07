using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;

namespace AVAHack.Core.Tests;

internal static class Heroes
{
    public static ContentDb Content => ContentDb.LoadBuiltIn();

    /// <summary>Dwarf fighter, mercenary: STR 16, DEX 12, CON 16, INT 8, WIS 12, CHA 10.</summary>
    public static Character Brannoc() => CharacterBuilder.QuickStartParty(Content)[0];
    /// <summary>Halfling rogue, cutpurse: STR 8, DEX 17, CON 13, INT 14, WIS 12, CHA 11.</summary>
    public static Character Pell() => CharacterBuilder.QuickStartParty(Content)[1];
    /// <summary>Human cleric, acolyte: STR 13, DEX 11, CON 15, INT 9, WIS 16, CHA 13.</summary>
    public static Character Ama() => CharacterBuilder.QuickStartParty(Content)[2];
    /// <summary>Elf wizard, cartographer: STR 8, DEX 16, CON 13, INT 16, WIS 12, CHA 10.</summary>
    public static Character Ithren() => CharacterBuilder.QuickStartParty(Content)[3];

    public static Character Make(string ancestry, string calling, string background = "miner") =>
        CharacterBuilder.Create(Content, "Test", ancestry, calling, background, new AttributeSet(10, 14, 14, 10, 12, 12));

    /// <summary>Raises skills (outside a fight) until the hero reaches <paramref name="level"/>.</summary>
    public static void LevelTo(Character c, int level, SkillId? except = null)
    {
        while (c.Level < level)
            foreach (var s in Enum.GetValues<SkillId>())
            {
                if (c.Level >= level) break;
                if (s == except) continue;
                c.GainSkillXp(s, Progression.XpToNext(c.Rank(s)) + 0.01, XpSource.Plain);
            }
    }
}
