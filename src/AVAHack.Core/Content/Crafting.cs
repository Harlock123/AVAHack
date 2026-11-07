namespace AVAHack.Content;

/// <summary>One of the effects a reagent hides (spec 13.5).</summary>
public sealed record AlchemyEffectDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>heal, stamina, mana, cure (a condition), buff (a buff key), food, water, coat (a condition for a blade), drain (mana or stamina) or burn (a damage type).</summary>
    public required string Kind { get; init; }
    public string? Scope { get; init; }
    /// <summary>The strength at Alchemy 0: HP, points, seconds of a buff, or a coating's save DC.</summary>
    public required double Amount { get; init; }
    public bool Harmful { get; init; }
    public required string Text { get; init; }
}

/// <summary>A reagent: four hidden effects (spec 13.5).</summary>
public sealed record ReagentDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Effects { get; init; }
    public int MinDepth { get; init; } = 1;
    public int Value { get; init; } = 8;
    public required string Text { get; init; }
}

/// <summary>A cooking ingredient: a creature's part (from, with a chance in %) or a find.</summary>
public sealed record IngredientDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string[] From { get; init; } = [];
    public double Chance { get; init; }
    public required string Text { get; init; }
}

/// <summary>A meal cooked at a campfire: food, water and a buff for a while.</summary>
public sealed record RecipeDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Ingredients { get; init; }
    public double Food { get; init; }
    public double Water { get; init; }
    public string? Buff { get; init; }
    public double BuffSeconds { get; init; }
    public required string Text { get; init; }
}
