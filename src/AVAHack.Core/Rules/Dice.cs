using System.Globalization;
using System.Text.RegularExpressions;

namespace AVAHack.Rules;

/// <summary>A dice expression such as <c>2d6+3</c>, <c>1d8</c>, <c>d4-1</c> or a flat <c>5</c>.</summary>
public readonly partial record struct Dice(int Count, int Sides, int Bonus = 0)
{
    public int Min => Count + Bonus;
    public int Max => Count * Sides + Bonus;
    public double Average => Count * (Sides + 1) / 2.0 + Bonus;

    /// <summary>Rolls the dice and adds the bonus. Each die is rolled separately.</summary>
    public int Roll(Rng rng) => RollDice(rng) + Bonus;

    /// <summary>Rolls only the dice, without the bonus.</summary>
    public int RollDice(Rng rng)
    {
        int total = 0;
        for (int i = 0; i < Count; i++) total += rng.Die(Sides);
        return total;
    }

    public static Dice Parse(string text)
    {
        var m = Pattern().Match(text.Replace(" ", ""));
        if (!m.Success) throw new FormatException($"Not a dice expression: '{text}'");
        if (!m.Groups["sides"].Success) return new Dice(0, 0, int.Parse(m.Groups["flat"].Value, CultureInfo.InvariantCulture));
        int count = m.Groups["count"].Length > 0 ? int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        int sides = int.Parse(m.Groups["sides"].Value, CultureInfo.InvariantCulture);
        int bonus = m.Groups["bonus"].Success ? int.Parse(m.Groups["bonus"].Value, CultureInfo.InvariantCulture) : 0;
        if (count < 1 || sides < 2) throw new FormatException($"Not a dice expression: '{text}'");
        return new Dice(count, sides, bonus);
    }

    public override string ToString() =>
        Sides == 0 ? Bonus.ToString(CultureInfo.InvariantCulture)
        : Bonus == 0 ? $"{Count}d{Sides}"
        : $"{Count}d{Sides}{Bonus:+0;-0}";

    [GeneratedRegex(@"^(?:(?<count>\d*)d(?<sides>\d+)(?<bonus>[+-]\d+)?|(?<flat>-?\d+))$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
