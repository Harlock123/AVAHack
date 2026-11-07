namespace AVAHack.App.Audio;

/// <summary>
/// Plays the game's sounds (spec 16.3): one-shot effects, each with a gain and a pan worked out by
/// <see cref="Game.GameEngine.Hearing"/>, and a looping ambience bed for the region. Safe to call from any thread.
/// </summary>
public interface IAudioEngine : IDisposable
{
    /// <summary>False when no audio device could be opened (everything is then a no-op).</summary>
    bool IsAvailable { get; }

    /// <summary>Why the game is silent, in words for the player; null when sound is working or simply off.</summary>
    string? Problem { get; }

    /// <summary>Master volume and the ambience's share of it, each 0..1.</summary>
    void SetVolumes(float master, float ambience);

    /// <summary>Plays a cue once (one of its takes, chosen at random). <paramref name="pan"/>: −1 left … +1 right.</summary>
    void PlayEffect(string cue, float gain = 1f, float pan = 0f);

    /// <summary>Crossfades to an ambience loop (null: fade it out).</summary>
    void PlayAmbience(string? cue);

    string? CurrentAmbience { get; }
}

/// <summary>Silent: when audio is unavailable, and in tests, where it remembers what it was asked to play.</summary>
public sealed class NullAudioEngine(string? problem = null) : IAudioEngine
{
    public bool IsAvailable => false;
    public string? Problem { get; } = problem;
    public string? CurrentAmbience { get; private set; }

    /// <summary>Every effect asked for, with its gain and pan.</summary>
    public List<(string Cue, float Gain, float Pan)> Played { get; } = [];

    public void SetVolumes(float master, float ambience) { }
    public void PlayEffect(string cue, float gain = 1f, float pan = 0f) { lock (Played) Played.Add((cue, gain, pan)); }
    public void PlayAmbience(string? cue) => CurrentAmbience = cue;
    public void Dispose() { }
}
