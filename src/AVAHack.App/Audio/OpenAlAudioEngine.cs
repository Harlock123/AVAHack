using System.Diagnostics;
using Silk.NET.OpenAL;

namespace AVAHack.App.Audio;

/// <summary>
/// OpenAL Soft playback (the same audio layer as the other AVA games). Each take is decoded once into a mono
/// buffer and played on a pool of voices placed left or right of the listener for its pan, so OpenAL does the
/// stereo. The ambience is a looping buffer on one of two sources, crossfaded by a background pump when the
/// region changes. All AL calls are serialised by a lock.
/// </summary>
public sealed unsafe class OpenAlAudioEngine : IAudioEngine
{
    private const int Voices = 24;
    private const float FadeStep = 0.04f; // per 40 ms pump tick: about a second

    private readonly object _lock = new();
    private readonly AL _al;
    private readonly ALContext _alc;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly uint[] _voices = new uint[Voices];
    private readonly Dictionary<string, uint?> _buffers = new(StringComparer.Ordinal);
    private readonly Random _takes = new();
    private readonly (uint Source, string? Cue, float Fade)[] _beds = new (uint, string?, float)[2];
    private readonly Timer _pump;
    private int _nextVoice, _bed;
    private float _master = 0.8f, _ambience = 0.5f;

    private OpenAlAudioEngine(AL al, ALContext alc, Device* device, Context* context)
    {
        _al = al;
        _alc = alc;
        _device = device;
        _context = context;
        for (int i = 0; i < Voices; i++)
        {
            _voices[i] = _al.GenSource();
            _al.SetSourceProperty(_voices[i], SourceBoolean.SourceRelative, true);
            _al.SetSourceProperty(_voices[i], SourceFloat.RolloffFactor, 0f); // the engine works out the distance
        }
        for (int i = 0; i < 2; i++)
        {
            _beds[i] = (_al.GenSource(), null, 0);
            _al.SetSourceProperty(_beds[i].Source, SourceBoolean.Looping, true);
            _al.SetSourceProperty(_beds[i].Source, SourceBoolean.SourceRelative, true);
            _al.SetSourceProperty(_beds[i].Source, SourceFloat.Gain, 0f);
        }
        _pump = new Timer(_ => Pump(), null, 40, 40);
    }

    public bool IsAvailable => true;
    public string? Problem => null;
    public string? CurrentAmbience => _beds[_bed].Cue;

    /// <summary>The mixing period asked for at start-up (0: OpenAL's default).</summary>
    public int PeriodFrames { get; private init; }

    /// <summary>Opens the default device, or returns a silent engine that says why it couldn't.</summary>
    public static IAudioEngine CreateOrSilent(int periodFrames = 0)
    {
        try
        {
            // Before OpenAL Soft first reads its configuration (on the first call below).
            if (periodFrames > 0) OpenAlConfig.UsePeriod(periodFrames);
            var alc = ALContext.GetApi(soft: true);
            var al = AL.GetApi(soft: true);
            var device = alc.OpenDevice(null);
            if (device == null) return new NullAudioEngine(AudioFailure.NoDevice);
            var context = alc.CreateContext(device, null);
            if (context == null || !alc.MakeContextCurrent(context))
            {
                alc.CloseDevice(device);
                return new NullAudioEngine(AudioFailure.NoContext);
            }
            return new OpenAlAudioEngine(al, alc, device, context) { PeriodFrames = periodFrames };
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Audio unavailable: {ex}");
            return new NullAudioEngine(AudioFailure.Describe(ex, AudioFailure.MissingVcRuntimeHere()));
        }
    }

    public void SetVolumes(float master, float ambience)
    {
        lock (_lock)
        {
            _master = Math.Clamp(master, 0, 1);
            _ambience = Math.Clamp(ambience, 0, 1);
            for (int i = 0; i < 2; i++) _al.SetSourceProperty(_beds[i].Source, SourceFloat.Gain, _master * _ambience * _beds[i].Fade);
        }
    }

    public void PlayEffect(string cue, float gain = 1f, float pan = 0f)
    {
        lock (_lock)
        {
            var takes = SoundBank.TakesOf(cue);
            if (takes.Count == 0 || gain <= 0.005f) return;
            if (Buffer(takes[_takes.Next(takes.Count)]) is not { } buffer) return;
            // An idle voice if there is one; otherwise the next in turn.
            uint voice = _voices[_nextVoice];
            for (int i = 0; i < Voices; i++)
            {
                uint candidate = _voices[(_nextVoice + i) % Voices];
                _al.GetSourceProperty(candidate, GetSourceInteger.SourceState, out int state);
                if (state != (int)SourceState.Playing) { voice = candidate; break; }
            }
            _nextVoice = (_nextVoice + 1) % Voices;
            pan = Math.Clamp(pan, -1, 1);
            _al.SourceStop(voice);
            _al.SetSourceProperty(voice, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(voice, SourceVector3.Position, pan, 0f, -MathF.Sqrt(1 - pan * pan));
            _al.SetSourceProperty(voice, SourceFloat.Gain, _master * Math.Clamp(gain, 0, 2));
            _al.SetSourceProperty(voice, SourceFloat.Pitch, 0.96f + (float)_takes.NextDouble() * 0.08f); // no two quite alike
            _al.SourcePlay(voice);
        }
    }

    public void PlayAmbience(string? cue)
    {
        lock (_lock)
        {
            if (cue == _beds[_bed].Cue) return;
            // The current bed fades out; the other source takes the new one and fades in.
            _bed ^= 1;
            var (source, _, _) = _beds[_bed];
            _al.SourceStop(source);
            _beds[_bed] = (source, cue, 0);
            if (cue is null || SoundBank.TakesOf(cue) is not { Count: > 0 } takes || Buffer(takes[0]) is not { } buffer) return;
            _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(source, SourceFloat.Gain, 0f);
            _al.SourcePlay(source);
        }
    }

    private uint? Buffer(string resource)
    {
        if (_buffers.TryGetValue(resource, out var cached)) return cached;
        uint? made = null;
        try
        {
            using var stream = SoundBank.Open(resource) ?? throw new FileNotFoundException(resource);
            var (samples, channels, rate) = WavReader.Read(stream);
            if (samples.Length > 0)
            {
                uint b = _al.GenBuffer();
                _al.BufferData(b, channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, samples, rate);
                made = b;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Trace.WriteLine($"Could not load sound '{resource}': {ex.Message}");
        }
        return _buffers[resource] = made;
    }

    /// <summary>Fades the ambience beds towards the current one.</summary>
    private void Pump()
    {
        lock (_lock)
        {
            for (int i = 0; i < 2; i++)
            {
                var (source, cue, fade) = _beds[i];
                float target = i == _bed && cue is not null ? 1 : 0;
                if (fade == target) continue;
                fade = Math.Clamp(fade + (target > fade ? FadeStep : -FadeStep), 0, 1);
                _beds[i] = (source, cue, fade);
                _al.SetSourceProperty(source, SourceFloat.Gain, _master * _ambience * fade);
                if (fade <= 0 && i != _bed) _al.SourceStop(source);
            }
        }
    }

    public void Dispose()
    {
        _pump.Dispose();
        lock (_lock)
        {
            foreach (var v in _voices) { _al.SourceStop(v); _al.DeleteSource(v); }
            foreach (var (s, _, _) in _beds) { _al.SourceStop(s); _al.DeleteSource(s); }
            foreach (var b in _buffers.Values.OfType<uint>()) _al.DeleteBuffer(b);
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _alc.CloseDevice(_device);
        }
    }
}
