namespace AVAHack.Rules;

public enum Attribute { Str, Dex, Con, Int, Wis, Cha }

/// <summary>Six attribute scores.</summary>
public sealed class AttributeSet
{
    public const int Cap = 20;
    public const int ItemCap = 24;

    private readonly int[] _scores = new int[6];

    public AttributeSet() { }

    public AttributeSet(int str, int dex, int con, int @int, int wis, int cha)
    {
        _scores[0] = str; _scores[1] = dex; _scores[2] = con;
        _scores[3] = @int; _scores[4] = wis; _scores[5] = cha;
    }

    public int this[Attribute a]
    {
        get => _scores[(int)a];
        set => _scores[(int)a] = value;
    }

    public AttributeSet Clone()
    {
        var c = new AttributeSet();
        Array.Copy(_scores, c._scores, 6);
        return c;
    }

    /// <summary>floor((score − 10) / 2): 10–11 give 0, 8–9 give −1, 18–19 give +4.</summary>
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public override string ToString() => string.Join(' ', Enum.GetValues<Attribute>().Select(a => $"{a.ToString().ToUpperInvariant()} {this[a]}"));
}

/// <summary>Setting attributes at creation (spec 5.3): point buy or 4d6 drop the lowest.</summary>
public static class AttributeGen
{
    public const int PointBuyBudget = 27;
    public const int PointBuyMin = 8;
    public const int PointBuyMax = 15;

    /// <summary>What a score costs in point buy: 8 is free, 13 costs 5, 14 costs 7 and 15 costs 9.</summary>
    public static int PointCost(int score) => score switch
    {
        < PointBuyMin or > PointBuyMax => throw new ArgumentOutOfRangeException(nameof(score), $"Point buy scores run {PointBuyMin}–{PointBuyMax}."),
        <= 13 => score - 8,
        14 => 7,
        _ => 9,
    };

    public static int PointsSpent(AttributeSet a) => Enum.GetValues<Attribute>().Sum(x => PointCost(a[x]));

    public static bool IsValidPointBuy(AttributeSet a) =>
        Enum.GetValues<Attribute>().All(x => a[x] is >= PointBuyMin and <= PointBuyMax) && PointsSpent(a) <= PointBuyBudget;

    /// <summary>Rolls 4d6 and drops the lowest die, once per attribute, in order. Returns the rolls too.</summary>
    public static (AttributeSet Scores, int[][] Rolls) Roll4d6DropLowest(Rng rng)
    {
        var set = new AttributeSet();
        var rolls = new int[6][];
        foreach (var a in Enum.GetValues<Attribute>())
        {
            var four = new[] { rng.Die(6), rng.Die(6), rng.Die(6), rng.Die(6) };
            rolls[(int)a] = four;
            set[a] = four.Sum() - four.Min();
        }
        return (set, rolls);
    }
}
