namespace AVAHack.Characters;

/// <summary>The 22 skills (spec 7.1). Their names, groups and use XP are in <c>Content/Data/skills.json</c>.</summary>
public enum SkillId
{
    // Combat
    Blade, Blunt, TwoHanded, Archery, Unarmed, Block, HeavyArmour, LightArmour, Athletics,
    // Magic
    Destruction, Restoration, Warding, Conjuration, Illusion, Enchanting,
    // Craft and stealth
    Stealth, Lockpicking, Traps, Alchemy, Smithing, Survival, Lore,
}

public enum SkillGroup { Combat, Magic, Craft }
