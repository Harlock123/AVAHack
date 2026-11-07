using AVAHack.Content;
using AVAHack.Items;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Saves;

// Everything a save holds (spec 17): the party, the engine's state, and for each level visited what has changed
// since it was made. The level itself is rebuilt from its source (a built-in name, or a seed and depth).

public sealed record ConditionSave(ConditionId Id, double SecondsLeft, int Dc, string? DamagePerSecond, double SinceSave = 0, double SinceDamage = 0);

/// <summary>A hand's cooldown.</summary>
public sealed record HandSave(double Recovery, double Total);

public sealed record CharacterSave
{
    public required string Name { get; init; }
    public required string Ancestry { get; init; }
    public required string Calling { get; init; }
    public required string Background { get; init; }
    public required int[] BaseAttributes { get; init; }
    public required int[] Increases { get; init; }
    public int[] IncreasesThisLevel { get; init; } = new int[6];
    public int Level { get; init; } = 1;
    public int CharacterXp { get; init; }
    public int PerkPoints { get; init; }
    public int AttributePoints { get; init; }
    public int PendingLevelChoices { get; init; }
    public int HealthChoices { get; init; }
    public int StaminaChoices { get; init; }
    public int ManaChoices { get; init; }
    public int RecoverySpeedUps { get; init; }
    public int Respecs { get; init; }
    public int LessonsThisLevel { get; init; }
    public double ExtraSkillXpPercent { get; init; }
    public bool LoneDelver { get; init; }
    public double Hp { get; init; }
    public double Stamina { get; init; }
    public double Mana { get; init; }
    public double Ward { get; init; }
    public bool IsDead { get; init; }
    public required int[] Ranks { get; init; }
    public required double[] Xp { get; init; }
    public Dictionary<string, int> Perks { get; init; } = [];
    public List<string> BooksRead { get; init; } = [];
    public List<ConditionSave> Conditions { get; init; } = [];
    public Dictionary<EquipSlot, Item> Equipped { get; init; } = [];
    public Item?[] Backpack { get; init; } = [];
    public List<string> Spells { get; init; } = [];
    public string?[] QuickSlots { get; init; } = [];
    public double Food { get; init; } = 100;
    public double Water { get; init; } = 100;
    public bool Sneaking { get; init; }
    public HandSave? LeftHand { get; init; }
    public HandSave? RightHand { get; init; }
    public bool InFight { get; init; }
    public bool LuckUsed { get; init; }
    public bool RelentlessUsed { get; init; }
    public int[] RanksThisFight { get; init; } = [];
    public double RiposteUntil { get; init; } = -1;
    public Dictionary<string, double> Buffs { get; init; } = [];
    public bool AvoidDeathUsed { get; init; }
}

public sealed record MonsterSave
{
    public required string Def { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public Dir Facing { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public Game.MonsterState State { get; init; }
    public int Slot { get; init; }
    public double PileLeft { get; init; }
    public bool Fell { get; init; }
    public bool Ally { get; init; }
    public bool Summoned { get; init; }
    public double SummonLeft { get; init; }
    public List<string> Affixes { get; init; } = [];
    public bool Elite { get; init; }
    public string? Title { get; init; }
    public int AcBonus { get; init; }
    public int ToHitBonus { get; init; }
    public double DamageFactor { get; init; } = 1;
    public int Ward { get; init; }
    public string? Boss { get; init; }
    public int Phase { get; init; }
    public Game.MonsterAction Action { get; init; }
    public double ActionLeft { get; init; }
    public double ActionTotal { get; init; }
    public string? Attack { get; init; }
    public int FromX { get; init; }
    public int FromY { get; init; }
    public double StrikeTime { get; init; }
    public double IdleLeft { get; init; }
    public double FleeLeft { get; init; }
    public double AffixTimer { get; init; }
    public DamageType? LastHitType { get; init; }
    public List<ConditionSave> Conditions { get; init; } = [];
    public double LastSeen { get; init; } = -100;
    public double Awareness { get; init; }
    public double CalmLeft { get; init; }
    public double FrenzyLeft { get; init; }
    /// <summary>The level it was scaled to (spec 12.1); its definition is scaled again on load.</summary>
    public int Level { get; init; }
    public double SpecialTimer { get; init; }
    public int Id { get; init; }
    public int SummonedBy { get; init; }
    public bool Raised { get; init; }
    public bool SplitHalf { get; init; }
}

public sealed record HazardSave(int X, int Y, DamageType Type, string Dice, double SecondsLeft, double Power);

public sealed record CellSave(int X, int Y);
public sealed record DoorSave(int X, int Y, double Open, bool Opening, bool Locked = false);
public sealed record TrapSave(int X, int Y, TrapKind Kind, bool Spotted, bool Disarmed, bool Sprung);
public sealed record LeverSave(int X, int Y, Dir Face, bool Down);
public sealed record ChestSave(int X, int Y, bool Opened, int LockDc, List<Item> Contents);
public sealed record PileSave(int X, int Y, List<Item> Items);

/// <summary>What has changed in one level since it was made.</summary>
public sealed record LevelStateSave
{
    public required string Source { get; init; }
    public List<DoorSave> Doors { get; init; } = [];
    public List<LeverSave> Levers { get; init; } = [];
    public List<CellSave> PlatesPressed { get; init; } = [];
    public List<CellSave> PitsSpotted { get; init; } = [];
    public List<CellSave> NotesRead { get; init; } = [];
    public List<ChestSave> Chests { get; init; } = [];
    public List<PileSave> Piles { get; init; } = [];
    public List<CellSave> Explored { get; init; } = [];
    public List<TrapSave> Traps { get; init; } = [];
    public List<CellSave> SecretsFound { get; init; } = [];
    public List<CellSave> Rubble { get; init; } = [];
    public List<MonsterSave> Monsters { get; init; } = [];
    /// <summary>Changes the generator's traps and secrets keep (sprung, disarmed, found), by key.</summary>
    public Dictionary<string, string> Flags { get; init; } = [];
    public int PartyX { get; init; }
    public int PartyY { get; init; }
    public Dir Facing { get; init; }
}

public sealed record SaveGame
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public DateTime SavedAt { get; init; } = DateTime.UtcNow;
    public required ulong Seed { get; init; }
    public required ulong[] RngStates { get; init; }
    public required List<CharacterSave> Party { get; init; }
    /// <summary>Slot of each hero, in the order of <see cref="Party"/>.</summary>
    public required int[] Slots { get; init; }
    public int Gold { get; init; }
    public long TickCount { get; init; }
    public int Kills { get; init; }
    public List<string> Log { get; init; } = [];
    public double LightLeft { get; init; }
    public double Invisible { get; init; }
    public int Selected { get; init; }
    public double Accumulator { get; init; }
    public double MagicLight { get; init; }
    public int Mirrors { get; init; }
    public double Turning { get; init; }
    public double Detecting { get; init; }
    public int[]? Portal { get; init; }
    public List<HazardSave> Hazards { get; init; } = [];
    public List<CellSave> Sanctuary { get; init; } = [];
    public bool InFight { get; init; }
    public double Calm { get; init; }
    /// <summary>The level the party is in.</summary>
    public required LevelStateSave Current { get; init; }
    /// <summary>Other levels visited (a Delve), with what changed in each.</summary>
    public List<LevelStateSave> Visited { get; init; } = [];
    /// <summary>The Delve's options and progress, when the game is a generated Delve.</summary>
    public Dictionary<string, string> Delve { get; init; } = [];
    public bool Ironman { get; init; }
    /// <summary>The Waystones' stash, shared by all of them.</summary>
    public List<Items.Item> Stash { get; init; } = [];
    /// <summary>Every affix the run's champions have shown (the Unmaker borrows them).</summary>
    public List<string> ChampionAffixes { get; init; } = [];
    /// <summary>For the slot list: who, where, how long.</summary>
    public string Summary { get; init; } = "";
}
