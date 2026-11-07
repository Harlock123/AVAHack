namespace AVAHack.Characters;

/// <summary>Where a hero stands in the party's cell (spec 9.2).</summary>
public enum Slot { FrontLeft, FrontRight, BackLeft, BackRight }

/// <summary>Up to four heroes in a 2×2 formation, and what they share: gold.</summary>
public sealed class Party
{
    private readonly Character?[] _slots = new Character?[4];

    public int Gold { get; set; }

    public Party(params Character[] heroes)
    {
        if (heroes.Length is < 1 or > 4) throw new ArgumentException("A party has one to four heroes.", nameof(heroes));
        for (int i = 0; i < heroes.Length; i++) _slots[i] = heroes[i];
        if (heroes.Length == 1) heroes[0].LoneDelver = true;
    }

    public Character? this[Slot s] => _slots[(int)s];
    public Character? At(int i) => i is >= 0 and < 4 ? _slots[i] : null;

    /// <summary>The heroes in slot order.</summary>
    public IEnumerable<Character> Members => _slots.Where(c => c is not null)!;
    public IEnumerable<Character> Standing => Members.Where(c => !c.IsDown);
    public int Count => Members.Count();
    public bool AllDown => Members.All(c => c.IsDown);

    /// <summary>The first hero in slot order: the one the single-hero parts of the game talk about.</summary>
    public Character Leader => Members.First();

    public Slot SlotOf(Character c) => (Slot)Array.IndexOf(_slots, c);
    public static bool IsFront(Slot s) => s is Slot.FrontLeft or Slot.FrontRight;
    public bool IsFront(Character c) => IsFront(SlotOf(c));

    /// <summary>Swaps two slots (either may be empty).</summary>
    public void Swap(Slot a, Slot b) => (_slots[(int)a], _slots[(int)b]) = (_slots[(int)b], _slots[(int)a]);

    /// <summary>The best of a skill-like number across the standing heroes (perception, Lore ...).</summary>
    public int Best(Func<Character, int> f) => Standing.Select(f).DefaultIfEmpty(0).Max();

    /// <summary>
    /// The heroes a blow from a side reaches: from ahead the front row, from the left or right that column, from
    /// behind the back row. If they are all down or the slots empty, anyone standing.
    /// </summary>
    public IReadOnlyList<Character> Exposed(RelativeSide side)
    {
        Slot[] pick = side switch
        {
            RelativeSide.Ahead => [Slot.FrontLeft, Slot.FrontRight],
            RelativeSide.Left => [Slot.FrontLeft, Slot.BackLeft],
            RelativeSide.Right => [Slot.FrontRight, Slot.BackRight],
            _ => [Slot.BackLeft, Slot.BackRight],
        };
        var hit = pick.Select(s => this[s]).Where(c => c is { IsDown: false }).Cast<Character>().ToList();
        return hit.Count > 0 ? hit : Standing.ToList();
    }
}

public enum RelativeSide { Ahead, Left, Right, Behind }
