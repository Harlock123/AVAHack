using System.Text.Json;
using System.Text.Json.Serialization;
using AVAHack.Characters;
using AVAHack.Rules;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Content;

/// <summary>
/// The game's content: skills, ancestries, callings, backgrounds, perks and conditions, read from the JSON files
/// embedded under <c>Content/Data/</c>. Reading is strict, and <see cref="Validate"/> checks that the files agree
/// with each other; <see cref="LoadBuiltIn"/> refuses content with any problem.
/// </summary>
public sealed class ContentDb
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static ContentDb? _builtIn;

    public IReadOnlyList<SkillDef> Skills { get; }
    public IReadOnlyList<AncestryDef> Ancestries { get; }
    public IReadOnlyList<CallingDef> Callings { get; }
    public IReadOnlyList<BackgroundDef> Backgrounds { get; }
    public IReadOnlyList<PerkDef> Perks { get; }
    public IReadOnlyList<ConditionDef> Conditions { get; }
    public IReadOnlyList<CreatureDef> Creatures { get; private init; } = [];
    public IReadOnlyList<WeaponDef> Weapons { get; private init; } = [];
    public IReadOnlyList<ShieldDef> Shields { get; private init; } = [];
    public IReadOnlyList<ArmourDef> Armours { get; private init; } = [];

    private readonly Dictionary<SkillId, SkillDef> _skills;
    private readonly Dictionary<string, AncestryDef> _ancestries;
    private readonly Dictionary<string, CallingDef> _callings;
    private readonly Dictionary<string, BackgroundDef> _backgrounds;
    private readonly Dictionary<string, PerkDef> _perks;
    private readonly Dictionary<ConditionId, ConditionDef> _conditions;

    public ContentDb(IReadOnlyList<SkillDef> skills, IReadOnlyList<AncestryDef> ancestries, IReadOnlyList<CallingDef> callings,
                     IReadOnlyList<BackgroundDef> backgrounds, IReadOnlyList<PerkDef> perks, IReadOnlyList<ConditionDef> conditions)
    {
        Skills = skills;
        Ancestries = ancestries;
        Callings = callings;
        Backgrounds = backgrounds;
        Perks = perks;
        Conditions = conditions;
        _skills = skills.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        _ancestries = ancestries.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
        _callings = callings.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        _backgrounds = backgrounds.GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
        _perks = perks.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        _conditions = conditions.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>A copy with creatures and equipment added (they arrived in M2).</summary>
    public ContentDb With(IReadOnlyList<CreatureDef> creatures, IReadOnlyList<WeaponDef> weapons,
                          IReadOnlyList<ShieldDef> shields, IReadOnlyList<ArmourDef> armours) =>
        new(Skills, Ancestries, Callings, Backgrounds, Perks, Conditions)
        {
            Creatures = creatures,
            Weapons = weapons,
            Shields = shields,
            Armours = armours,
        };

    /// <summary>The content shipped with the game, loaded once and checked.</summary>
    public static ContentDb LoadBuiltIn()
    {
        if (_builtIn is { } db) return db;
        db = new ContentDb(
            Read<SkillDef>("skills"), Read<AncestryDef>("ancestries"), Read<CallingDef>("callings"),
            Read<BackgroundDef>("backgrounds"), Read<PerkDef>("perks"), Read<ConditionDef>("conditions"))
            .With(Read<CreatureDef>("creatures"), Read<WeaponDef>("weapons"), Read<ShieldDef>("shields"), Read<ArmourDef>("armour"));
        var problems = db.Validate();
        if (problems.Count > 0) throw new InvalidDataException("The game's content has problems:\n" + string.Join('\n', problems));
        return _builtIn = db;
    }

    /// <summary>Reads one embedded data file, a JSON array of <typeparamref name="T"/>.</summary>
    public static List<T> Read<T>(string name) => Parse<T>(ReadText(name), name);

    public static List<T> Parse<T>(string json, string name)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? throw new InvalidDataException($"{name}.json is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{name}.json: {e.Message}", e);
        }
    }

    public static string ReadText(string name)
    {
        var asm = typeof(ContentDb).Assembly;
        using var stream = asm.GetManifestResourceStream($"AVAHack.Content.Data/{name}.json")
                           ?? asm.GetManifestResourceStream($"AVAHack.Content.Data\\{name}.json")
                           ?? throw new FileNotFoundException($"No content file {name}.json.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public SkillDef Skill(SkillId id) => _skills[id];
    public AncestryDef Ancestry(string id) => _ancestries.TryGetValue(id, out var a) ? a : throw new KeyNotFoundException($"No ancestry '{id}'.");
    public CallingDef Calling(string id) => _callings.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"No calling '{id}'.");
    public BackgroundDef Background(string id) => _backgrounds.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException($"No background '{id}'.");
    public PerkDef Perk(string id) => _perks.TryGetValue(id, out var p) ? p : throw new KeyNotFoundException($"No perk '{id}'.");
    public ConditionDef Condition(ConditionId id) => _conditions[id];
    public IEnumerable<PerkDef> PerksFor(SkillId skill) => Perks.Where(p => p.Skill == skill);
    public CreatureDef Creature(string id) => Creatures.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No creature '{id}'.");
    public WeaponDef Weapon(string id) => Weapons.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No weapon '{id}'.");
    public ShieldDef Shield(string id) => Shields.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No shield '{id}'.");
    public ArmourDef ArmourPiece(string id) => Armours.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No armour '{id}'.");

    /// <summary>An ancestry's attribute bonuses as attributes.</summary>
    public static IEnumerable<(Attribute Attribute, int Bonus)> AttributeBonuses(AncestryDef a) =>
        a.Attributes.Select(kv => (ParseEnum<Attribute>(kv.Key), kv.Value));

    /// <summary>An ancestry's starting skill bonuses as skills.</summary>
    public static IEnumerable<(SkillId Skill, int Bonus)> SkillBonuses(AncestryDef a) =>
        a.Skills.Select(kv => (ParseEnum<SkillId>(kv.Key), kv.Value));

    private static T ParseEnum<T>(string key) where T : struct, Enum =>
        Enum.TryParse<T>(key, ignoreCase: true, out var v) && Enum.IsDefined(v) && !int.TryParse(key, out _)
            ? v : throw new InvalidDataException($"'{key}' is not a {typeof(T).Name}.");

    /// <summary>Everything wrong with the content, one line each; empty when it is all consistent.</summary>
    public List<string> Validate()
    {
        var p = new List<string>();

        void Unique<T>(IEnumerable<T> items, Func<T, string> id, string file)
        {
            foreach (var g in items.GroupBy(id).Where(g => g.Count() > 1)) p.Add($"{file}: '{g.Key}' is defined {g.Count()} times.");
        }

        Unique(Skills, s => s.Id.ToString(), "skills");
        Unique(Ancestries, a => a.Id, "ancestries");
        Unique(Callings, c => c.Id, "callings");
        Unique(Backgrounds, b => b.Id, "backgrounds");
        Unique(Perks, x => x.Id, "perks");
        Unique(Conditions, c => c.Id.ToString(), "conditions");

        foreach (var s in Enum.GetValues<SkillId>().Where(s => !_skills.ContainsKey(s))) p.Add($"skills: {s} has no definition.");
        foreach (var c in Enum.GetValues<ConditionId>().Where(c => !_conditions.ContainsKey(c))) p.Add($"conditions: {c} has no definition.");

        foreach (var s in Skills)
            if (!Traits.UseXpAmounts.Contains(s.UseXp.Of)) p.Add($"skills: {s.Id}'s use XP is of '{s.UseXp.Of}', which is not a known amount.");

        foreach (var a in Ancestries)
        {
            foreach (var key in a.Attributes.Keys)
                if (!TryEnum<Attribute>(key)) p.Add($"ancestries: {a.Id} has a bonus to '{key}', which is not an attribute.");
            foreach (var key in a.Skills.Keys)
                if (!TryEnum<SkillId>(key)) p.Add($"ancestries: {a.Id} has a bonus to '{key}', which is not a skill.");
            foreach (var t in a.Traits.Where(t => !Traits.All.Contains(t))) p.Add($"ancestries: {a.Id} has an unknown trait '{t}'.");
        }

        foreach (var c in Callings)
        {
            if (c.HitDie is not (6 or 8 or 10 or 12)) p.Add($"callings: {c.Id}'s hit die is d{c.HitDie}; it must be d6, d8, d10 or d12.");
            if (c.Saves.Length != 2 || c.Saves.Distinct().Count() != 2) p.Add($"callings: {c.Id} needs two different save proficiencies.");
            if (c.Major.Length != 6 || c.Major.Distinct().Count() != 6) p.Add($"callings: {c.Id} needs six different major skills.");
            if (c.Minor.Length != 3 || c.Minor.Distinct().Count() != 3) p.Add($"callings: {c.Id} needs three different minor skills.");
            foreach (var s in c.Major.Intersect(c.Minor)) p.Add($"callings: {c.Id} has {s} as both a major and a minor skill.");
        }

        foreach (var x in Perks)
        {
            if (x.Ranks.Length == 0) p.Add($"perks: {x.Id} has no ranks.");
            for (int i = 0; i < x.Ranks.Length; i++)
            {
                if (x.Ranks[i] is < 0 or > 100) p.Add($"perks: {x.Id} rank {i + 1} needs skill {x.Ranks[i]}; it must be 0–100.");
                if (i > 0 && x.Ranks[i] <= x.Ranks[i - 1]) p.Add($"perks: {x.Id}'s rank requirements must rise.");
            }
            foreach (var r in x.Requires)
            {
                if (!_perks.TryGetValue(r, out var req)) p.Add($"perks: {x.Id} requires '{r}', which doesn't exist.");
                else if (req.Skill != x.Skill) p.Add($"perks: {x.Id} requires '{r}', which is in another tree ({req.Skill}).");
            }
            foreach (var e in x.Effects)
            {
                if (!Traits.PerkEffectKinds.Contains(e.Kind)) p.Add($"perks: {x.Id} has an unknown effect kind '{e.Kind}'.");
                if (e.Kind == "flag" && string.IsNullOrEmpty(e.Scope)) p.Add($"perks: {x.Id} has a flag effect with no name (scope).");
            }
            if (HasCycle(x)) p.Add($"perks: {x.Id} requires itself, through its prerequisites.");
        }
        foreach (var tree in Perks.GroupBy(x => x.Skill))
        {
            int n = tree.Count();
            if (n is < 6 or > 10) p.Add($"perks: the {tree.Key} tree has {n} perks; a tree has 6 to 10.");
            if (!tree.Any(x => x.Effects.Any(e => e.Kind == "flag"))) p.Add($"perks: the {tree.Key} tree has no perk that changes how you play (a flag).");
        }

        foreach (var c in Conditions)
        {
            foreach (var e in c.Effects.Where(e => !Traits.ConditionEffects.Contains(e))) p.Add($"conditions: {c.Id} has an unknown effect '{e}'.");
            if (c.DamagePerSecond is { } d)
            {
                try { Dice.Parse(d); }
                catch (FormatException) { p.Add($"conditions: {c.Id}'s damage '{d}' is not dice."); }
            }
            if (c.Seconds is <= 0) p.Add($"conditions: {c.Id} must last more than 0 seconds (or null for until cured).");
            if (c.Save is null && c.Seconds is null && c.Cures.Length == 0) p.Add($"conditions: {c.Id} can never end.");
            if (c.RecoveryFactor < 1) p.Add($"conditions: {c.Id}'s recovery factor must be at least 1.");
        }
        ValidateCreatures(p);
        Unique(Weapons, w => w.Id, "weapons");
        Unique(Shields, w => w.Id, "shields");
        Unique(Armours, w => w.Id, "armour");
        foreach (var w in Weapons)
        {
            CheckDice(p, $"weapons: {w.Id}'s damage", w.Damage);
            if (w.Recovery <= 0) p.Add($"weapons: {w.Id} must take some time to recover.");
            if (!Progression.WeaponSkills.Contains(w.Skill)) p.Add($"weapons: {w.Id} uses {w.Skill}, which is not a weapon skill.");
        }
        foreach (var s in Shields)
        {
            CheckDice(p, $"shields: {s.Id}'s bash", s.BashDamage);
            if (s.BlockPercent is < 0 or > 90) p.Add($"shields: {s.Id} blocks {s.BlockPercent}%; it must be 0–90.");
        }
        foreach (var a in Armours)
            if (a.Kind == ArmourKind.Heavy && a.DexCap is not 0) p.Add($"armour: {a.Id} is heavy, so its DEX cap must be 0.");
        return p;
    }

    private void ValidateCreatures(List<string> p)
    {
        foreach (var g in Creatures.GroupBy(c => c.Id).Where(g => g.Count() > 1)) p.Add($"creatures: '{g.Key}' is defined {g.Count()} times.");
        foreach (var c in Creatures)
        {
            CheckDice(p, $"creatures: {c.Id}'s hit dice", c.HitDice);
            foreach (var key in c.Saves.Keys)
                if (!TryEnum<Attribute>(key)) p.Add($"creatures: {c.Id} has a save for '{key}', which is not an attribute.");
            foreach (var b in c.Behaviour.Where(b => !Behaviours.All.Contains(b))) p.Add($"creatures: {c.Id} has an unknown behaviour '{b}'.");
            if (c.Attacks.Length == 0) p.Add($"creatures: {c.Id} has no attacks.");
            if (c.StepSeconds <= 0 || c.TurnSeconds <= 0) p.Add($"creatures: {c.Id} must take time to step and turn.");
            if (c.Has("ranged") && !c.Attacks.Any(a => a.Range > 1)) p.Add($"creatures: {c.Id} is ranged but has no attack with range.");
            if (c.Has("melee") && !c.Attacks.Any(a => a.Range == 1)) p.Add($"creatures: {c.Id} is melee but has no attack with range 1.");
            foreach (var a in c.Attacks)
            {
                CheckDice(p, $"creatures: {c.Id}'s {a.Id} damage", a.Damage);
                if (a.Windup is < 0.4 or > 1.5) p.Add($"creatures: {c.Id}'s {a.Id} tell is {a.Windup} s; tells run 0.4–1.5 s.");
                if (a.Recovery <= 0) p.Add($"creatures: {c.Id}'s {a.Id} must take time to recover.");
                if (a.Range < 1) p.Add($"creatures: {c.Id}'s {a.Id} has range {a.Range}.");
            }
            foreach (var s in c.Special.Where(s => s.Kind != "reassemble")) p.Add($"creatures: {c.Id} has an unknown special '{s.Kind}'.");
            if (c.FleeAtPercent is < 0 or > 90) p.Add($"creatures: {c.Id} flees at {c.FleeAtPercent}%; it must be 0–90.");
            if (c.FleeAtPercent > 0 && c.Has("mindless")) p.Add($"creatures: {c.Id} is mindless, so it can't flee.");
        }
    }

    private static void CheckDice(List<string> p, string what, string dice)
    {
        try { Dice.Parse(dice); }
        catch (FormatException) { p.Add($"{what} '{dice}' is not dice."); }
    }

    private bool HasCycle(PerkDef start)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<string>(start.Requires);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (id == start.Id) return true;
            if (!seen.Add(id) || !_perks.TryGetValue(id, out var x)) continue;
            foreach (var r in x.Requires) stack.Push(r);
        }
        return false;
    }

    private static bool TryEnum<T>(string key) where T : struct, Enum =>
        Enum.TryParse<T>(key, ignoreCase: true, out var v) && Enum.IsDefined(v) && !int.TryParse(key, out _);
}
