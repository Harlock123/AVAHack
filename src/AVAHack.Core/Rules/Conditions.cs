namespace AVAHack.Rules;

/// <summary>The 14 conditions (spec 6.4). What each one does is in <c>Content/Data/conditions.json</c>.</summary>
public enum ConditionId
{
    Poisoned, Diseased, Bleeding, Burning, Chilled, Frozen, Shocked, Stunned, Asleep, Blinded, Frightened,
    Paralysed, Cursed, Weakened,
}

/// <summary>A condition a creature has: how long is left, the DC to shake it off, and any damage it deals.</summary>
public sealed class ActiveCondition
{
    public required ConditionId Id { get; init; }
    public double SecondsLeft { get; internal set; }
    public int SaveDc { get; internal set; }
    public Dice? DamagePerSecond { get; internal set; }
    public double SinceSave { get; internal set; }
    public double SinceDamage { get; internal set; }
}

/// <summary>What happened when conditions were ticked.</summary>
public readonly record struct ConditionTick(int Damage, IReadOnlyList<ConditionId> Ended);

/// <summary>
/// The conditions on one creature. Every <see cref="SaveInterval"/> seconds it may save to end each one that
/// allows a save; damage-over-time conditions deal their damage once a second; and each runs out after its
/// duration. Conditions with no save (Cursed, Diseased) last until cured or until their time runs out.
/// </summary>
public sealed class ConditionSet(Content.ContentDb content)
{
    public const double SaveInterval = 3.0;

    private readonly Dictionary<ConditionId, ActiveCondition> _active = new();

    public IEnumerable<ActiveCondition> Active => _active.Values;
    public bool Has(ConditionId id) => _active.ContainsKey(id);
    public ActiveCondition? Get(ConditionId id) => _active.GetValueOrDefault(id);

    /// <summary>
    /// Adds a condition, or refreshes it: the longer duration, the higher DC and the bigger damage win.
    /// <paramref name="seconds"/> defaults to the condition's own duration.
    /// </summary>
    public void Apply(ConditionId id, int saveDc, double? seconds = null, Dice? damagePerSecond = null)
    {
        var def = content.Condition(id);
        double s = seconds ?? def.Seconds ?? double.PositiveInfinity;
        var dot = damagePerSecond ?? (def.DamagePerSecond is { } d ? Dice.Parse(d) : null);
        if (_active.TryGetValue(id, out var c))
        {
            c.SecondsLeft = Math.Max(c.SecondsLeft, s);
            c.SaveDc = Math.Max(c.SaveDc, saveDc);
            if (dot is { } nd && (c.DamagePerSecond is not { } od || nd.Average > od.Average)) c.DamagePerSecond = nd;
            return;
        }
        _active[id] = new ActiveCondition { Id = id, SecondsLeft = s, SaveDc = saveDc, DamagePerSecond = dot };
    }

    /// <summary>Removes a condition. Returns whether it was there.</summary>
    public bool Cure(ConditionId id) => _active.Remove(id);

    /// <summary>Removes every condition that the given cure ends ("rest", "cleanse", "dispel", "antidote" ...).</summary>
    public int CureBy(string cure)
    {
        var ids = _active.Keys.Where(k => content.Condition(k).Cures.Contains(cure)).ToList();
        foreach (var k in ids) _active.Remove(k);
        return ids.Count;
    }

    public void Clear() => _active.Clear();

    /// <summary>
    /// Runs the conditions forward. <paramref name="save"/> rolls a save with the given attribute against the
    /// given DC and says whether it succeeded.
    /// </summary>
    public ConditionTick Tick(double seconds, Rng rng, Func<Attribute, int, bool> save)
    {
        int damage = 0;
        var ended = new List<ConditionId>();
        foreach (var c in _active.Values.ToList())
        {
            var def = content.Condition(c.Id);
            if (c.DamagePerSecond is { } dot)
            {
                c.SinceDamage += seconds;
                while (c.SinceDamage >= 1.0 - 1e-9)
                {
                    c.SinceDamage -= 1.0;
                    damage += Math.Max(0, dot.Roll(rng));
                }
            }
            c.SecondsLeft -= seconds;
            bool end = c.SecondsLeft <= 1e-9;
            if (!end && def.Save is { } attr)
            {
                c.SinceSave += seconds;
                while (!end && c.SinceSave >= SaveInterval - 1e-9)
                {
                    c.SinceSave -= SaveInterval;
                    end = save(attr, c.SaveDc);
                }
            }
            if (end)
            {
                _active.Remove(c.Id);
                ended.Add(c.Id);
            }
        }
        return new ConditionTick(damage, ended);
    }

    // What the conditions add up to, for the rules that ask.

    /// <summary>False while stunned, asleep, paralysed or frozen.</summary>
    public bool CanAct => !_active.Keys.Any(k => content.Condition(k).Effects.Contains("noActions"));

    /// <summary>The chance, 0–100, that a spell fizzles.</summary>
    public double SpellFailurePercent => _active.Keys.Select(k => content.Condition(k).SpellFailurePercent).DefaultIfEmpty(0).Max();

    /// <summary>How much longer weapons and spells take to recover: 1 is normal, 1.5 is half as slow again.</summary>
    public double RecoveryFactor => _active.Keys.Select(k => content.Condition(k).RecoveryFactor).DefaultIfEmpty(1).Max();

    /// <summary>Whether this creature's own attacks have disadvantage.</summary>
    public bool AttacksAtDisadvantage => _active.Keys.Any(k => content.Condition(k).Effects.Contains("attackDisadvantage"));

    /// <summary>Whether attacks against this creature have advantage.</summary>
    public bool AttackedWithAdvantage => _active.Keys.Any(k => content.Condition(k).Effects.Contains("attackedWithAdvantage"));

    /// <summary>Whether natural healing is stopped.</summary>
    public bool StopsRegeneration => _active.Keys.Any(k => content.Condition(k).Effects.Contains("noRegen"));

    /// <summary>The sum of the conditions' penalties to saves.</summary>
    public int SavePenalty => _active.Keys.Sum(k => content.Condition(k).SavePenalty);

    /// <summary>The sum of the conditions' penalties to attacks and damage with STR.</summary>
    public int StrengthPenalty => _active.Keys.Sum(k => content.Condition(k).StrengthPenalty);
}
