namespace AVAHack.App.Audio;

/// <summary>
/// The game's sounds, embedded from Assets/Sounds (made by tools/make_sounds.py): 22,050 Hz mono 16-bit WAVs.
/// A cue's takes are <c>cue.wav</c>, <c>cue-2.wav</c>, <c>cue-3.wav</c> ...
/// </summary>
public static class SoundBank
{
    private const string Prefix = "AVAHack.App.Sounds.";
    private static readonly Lazy<Dictionary<string, string[]>> Takes = new(Index);

    /// <summary>Every cue there is a sound for.</summary>
    public static IReadOnlyCollection<string> Cues => Takes.Value.Keys;

    /// <summary>The resource names of a cue's takes (empty if it has none).</summary>
    public static IReadOnlyList<string> TakesOf(string cue) => Takes.Value.TryGetValue(cue, out var t) ? t : [];

    public static Stream? Open(string resource) => typeof(SoundBank).Assembly.GetManifestResourceStream(resource);

    private static Dictionary<string, string[]> Index()
    {
        var all = typeof(SoundBank).Assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(".wav", StringComparison.Ordinal));
        return all.GroupBy(r => Cue(r[Prefix.Length..^4]), StringComparer.Ordinal)
                  .ToDictionary(g => g.Key, g => g.Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    /// <summary>"hit.flesh-2" → "hit.flesh".</summary>
    private static string Cue(string name)
    {
        int dash = name.LastIndexOf('-');
        return dash > 0 && int.TryParse(name[(dash + 1)..], out _) ? name[..dash] : name;
    }
}

/// <summary>Reads the PCM WAVs the sound tool writes.</summary>
public static class WavReader
{
    public static (short[] Samples, int Channels, int Rate) Read(Stream stream)
    {
        using var r = new BinaryReader(stream);
        if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a WAV file.");
        r.ReadInt32();
        if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV file.");
        int channels = 1, rate = 22050, bits = 16;
        while (r.BaseStream.Position < r.BaseStream.Length)
        {
            string id = new(r.ReadChars(4));
            int size = r.ReadInt32();
            if (id == "fmt ")
            {
                int format = r.ReadInt16();
                channels = r.ReadInt16();
                rate = r.ReadInt32();
                r.ReadInt32(); r.ReadInt16();
                bits = r.ReadInt16();
                if (format != 1 || bits != 16) throw new InvalidDataException("Only 16-bit PCM WAVs are read.");
                r.ReadBytes(size - 16);
            }
            else if (id == "data")
            {
                var bytes = r.ReadBytes(size);
                var samples = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
                return (samples, channels, rate);
            }
            else r.ReadBytes(size + (size & 1));
        }
        throw new InvalidDataException("The WAV file has no data.");
    }
}
