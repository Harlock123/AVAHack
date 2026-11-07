namespace AVAHack.Rules;

/// <summary>A source of random numbers. Tests can use <see cref="ScriptedRng"/> to fix every roll.</summary>
public abstract class Rng
{
    /// <summary>A whole number from 0 to <paramref name="maxExclusive"/> − 1.</summary>
    public abstract int Next(int maxExclusive);

    /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    public int Range(int min, int max) => min + Next(max - min + 1);

    /// <summary>One die with <paramref name="sides"/> sides: 1 to sides.</summary>
    public int Die(int sides) => 1 + Next(sides);

    /// <summary>True with chance <paramref name="percent"/> out of 100.</summary>
    public bool Chance(double percent) => Next(10_000) < percent * 100;
}

/// <summary>
/// The PCG32 generator (O'Neill's pcg32: 64-bit state, XSH RR output). The same seed and stream give the same
/// numbers on every machine.
/// </summary>
public sealed class Pcg32 : Rng
{
    private ulong _state;
    private ulong _inc;

    /// <summary>The generator's whole state, for saves.</summary>
    public (ulong State, ulong Inc) GetState() => (_state, _inc);

    public void SetState(ulong state, ulong inc)
    {
        _state = state;
        _inc = inc;
    }

    public Pcg32(ulong seed, ulong stream = 54)
    {
        _inc = (stream << 1) | 1;
        _state = 0;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong old = _state;
        _state = unchecked(old * 6364136223846793005UL + _inc);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << (-rot & 31));
    }

    /// <summary>Unbiased: values that would favour low numbers are thrown away and drawn again.</summary>
    public override int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        uint bound = (uint)maxExclusive;
        uint threshold = (uint)(-(int)bound) % bound;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return (int)(r % bound);
        }
    }
}

/// <summary>
/// The separate random streams of one run (spec 18.2). Each subsystem draws from its own, so a change to how
/// loot is rolled never changes the shape of a level.
/// </summary>
public sealed class RngStreams(ulong runSeed)
{
    public Pcg32 World { get; } = new(Mix(runSeed, 1), 1);
    public Pcg32 Loot { get; } = new(Mix(runSeed, 2), 2);
    public Pcg32 Combat { get; } = new(Mix(runSeed, 3), 3);
    public Pcg32 Ai { get; } = new(Mix(runSeed, 4), 4);

    public ulong Seed { get; } = runSeed;
    public Pcg32[] All => [World, Loot, Combat, Ai];

    /// <summary>SplitMix64 of the seed and a salt.</summary>
    public static ulong Mix(ulong seed, ulong salt)
    {
        ulong z = unchecked(seed + salt * 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
}

/// <summary>For tests: hands out the given numbers in order. <see cref="Die"/> takes them as die faces.</summary>
public sealed class ScriptedRng(params int[] values) : Rng
{
    private readonly Queue<int> _values = new(values);

    public int Remaining => _values.Count;

    /// <summary>
    /// Returns the next scripted value minus one when used for a die, so scripts read as die faces: a script of
    /// 20 gives a natural 20 from <c>Die(20)</c>.
    /// </summary>
    public override int Next(int maxExclusive)
    {
        if (_values.Count == 0) throw new InvalidOperationException("The script ran out of numbers.");
        int v = _values.Dequeue() - 1;
        if (v < 0 || v >= maxExclusive) throw new InvalidOperationException($"Scripted {v + 1} doesn't fit a range of 1..{maxExclusive}.");
        return v;
    }
}
