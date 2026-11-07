using AVAHack.World;

namespace AVAHack.Game;

/// <summary>A sound the game makes: which one, where in the world, and how loud at its source.</summary>
/// <param name="Id">The cue (tools/make_sounds.py names the files after these).</param>
/// <param name="Local">At the party itself: heard at full strength, in the middle.</param>
public readonly record struct SoundCue(string Id, double X, double Y, double Gain = 1, bool Local = false);

/// <summary>
/// Sound (spec 16.3): the engine raises <see cref="Sound"/> cues and says how each reaches the party; the App
/// plays them. Sound travels along the dungeon's passages, not through rock: it fades with the number of steps
/// it has to go, is muffled by each shut door on the way, stops at <see cref="HearingRange"/>, and is panned to
/// the side it comes from. You hear a creature's footsteps and its bark before you see it.
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>The furthest a sound carries, in steps along the passages.</summary>
    public const int HearingRange = 16;
    /// <summary>What a shut door lets through.</summary>
    public const double DoorMuffle = 0.35;

    /// <summary>Raised for every sound the game makes.</summary>
    public event Action<SoundCue>? Sound;

    internal void Cue(string id, Cell at, double gain = 1) => Sound?.Invoke(new SoundCue(id, at.X + 0.5, at.Y + 0.5, gain));
    internal void CueHere(string id, double gain = 1) => Sound?.Invoke(new SoundCue(id, PartyCell.X + 0.5, PartyCell.Y + 0.5, gain, Local: true));

    /// <summary>
    /// How a sound reaches the party: its gain (0 if it doesn't) and its pan, −1 hard left to +1 hard right, as
    /// the party faces. A sound behind is a little quieter than one in front.
    /// </summary>
    public (double Gain, double Pan) Hearing(SoundCue cue)
    {
        if (cue.Local) return (cue.Gain, 0);
        var from = new Cell((int)Math.Floor(cue.X), (int)Math.Floor(cue.Y));
        if (Path(from) is not var (steps, doors)) return (0, 0);
        double gain = cue.Gain / (1 + 0.22 * steps) * Math.Pow(DoorMuffle, doors);
        // The direction it comes from, against the way the party faces.
        double dx = cue.X - (PartyCell.X + 0.5), dy = cue.Y - (PartyCell.Y + 0.5), len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01) return (gain, 0);
        double fx = Facing.Dx(), fy = Facing.Dy();
        double ahead = (dx * fx + dy * fy) / len, right = (dx * -fy + dy * fx) / len;
        if (ahead < 0) gain *= 0.85;
        return (gain, Math.Clamp(right, -1, 1) * 0.8);
    }

    /// <summary>The fewest steps from a cell to the party along open ground and doors, and the shut doors on
    /// the way; null past <see cref="HearingRange"/> or through solid rock.</summary>
    private (int Steps, int Doors)? Path(Cell from)
    {
        if (from == PartyCell) return (0, 0);
        // Fewest shut doors first, then fewest steps: a 0-1 search over (doors, steps).
        var best = new Dictionary<Cell, (int Doors, int Steps)> { [from] = (0, 0) };
        var queue = new PriorityQueue<Cell, (int, int)>();
        queue.Enqueue(from, (0, 0));
        while (queue.TryDequeue(out var c, out var cost))
        {
            if (best[c] != cost) continue;
            if (c == PartyCell) return (cost.Item2, cost.Item1);
            foreach (var d in Enum.GetValues<Dir>())
            {
                var next = c.Step(d);
                if (!Level.InBounds(next.X, next.Y)) continue;
                int doors = cost.Item1, steps = cost.Item2 + 1;
                if (steps > HearingRange) continue;
                if (Level.DoorAt(next) is { } door) { if (!door.IsPassable) doors++; }
                else if (Level.KindAt(next) == CellKind.Wall) continue;
                if (best.TryGetValue(next, out var seen) && (seen.Doors < doors || seen.Doors == doors && seen.Steps <= steps)) continue;
                best[next] = (doors, steps);
                queue.Enqueue(next, (doors, steps));
            }
        }
        return null;
    }

    // ---- Which sound for what ----

    /// <summary>A creature's cry when it notices the party, by family.</summary>
    internal static string BarkFor(Monster m) => m.Def.Family switch
    {
        "vermin" => m.Def.Sprite is "spider" or "spiderBig" ? "bark.spider" : m.Def.Size == AVAHack.Content.CreatureSize.Small ? "bark.vermin" : "bark.vermin.big",
        "restlessDead" => m.Def.Sprite is "hound" ? "bark.beast" : "bark.undead",
        "goblinKin" => m.Def.Sprite is "warg" ? "bark.beast" : "bark.goblin",
        "oozes" => "bark.ooze",
        "deepFolk" => "bark.deepFolk",
        "constructs" => "bark.constructs",
        "elementals" => "bark.elementals",
        "sporeKin" => "bark.sporeKin",
        "serpentCult" => "bark.serpentCult",
        "aberrations" => m.Def.Sprite is "stalker" ? "bark.beast" : "bark.aberrations",
        "unmade" => "bark.unmade",
        _ => "bark.goblin",
    };

    /// <summary>The sound of a blow landing on a creature, by what it is made of.</summary>
    internal static string HitSoundFor(Monster m) => m.Def.Family switch
    {
        "restlessDead" when m.Def.Sprite is not ("shambler" or "hound" or "wight") => "hit.bone",
        "constructs" => "hit.metal",
        "oozes" => "hit.slime",
        _ => m.Def.Sprite is "knight" ? "hit.metal" : "hit.flesh",
    };

    /// <summary>The sound of a creature falling, by what it is made of.</summary>
    internal static string DeathSoundFor(Monster m) => m.Def.Family switch
    {
        "restlessDead" when m.Def.Sprite is not ("shambler" or "hound") => m.Def.Sprite is "wight" ? "die.spirit" : "die.bones",
        "constructs" => "die.machine",
        "oozes" => "die.slime",
        "elementals" or "unmade" => "die.spirit",
        _ => "die.flesh",
    };

    /// <summary>Where a bolt, an arrow or a stone lands: a spell's element, or a blow on what it hit.</summary>
    internal static string ImpactFor(string look, Monster? victim) => look switch
    {
        "fire" => "impact.fire",
        "frost" => "impact.frost",
        "spark" or "light" => "impact.spark",
        "acid" => "impact.acid",
        "dark" => "impact.dark",
        _ => victim is null ? "bump" : HitSoundFor(victim),
    };

    private int _levelsHeard;

    /// <summary>Each tick: a fanfare when any hero has gone up a level since the last.</summary>
    private void ListenForLevels()
    {
        int levels = 0;
        foreach (var c in Party.Members) levels += c.Level;
        if (_levelsHeard != 0 && levels > _levelsHeard) CueHere("levelup");
        _levelsHeard = levels;
    }
}
