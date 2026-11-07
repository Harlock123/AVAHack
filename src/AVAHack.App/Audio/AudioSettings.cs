using System.Text.Json;

namespace AVAHack.App.Audio;

/// <summary>The sound settings, kept as <c>sound.settings</c> (JSON) in the saves folder, which lists only <c>*.json</c>.</summary>
public sealed class AudioSettings
{
    /// <summary>Master volume, 0..1.</summary>
    public float Volume { get; set; } = 0.8f;
    /// <summary>The ambience's share of the volume, 0..1.</summary>
    public float Ambience { get; set; } = 0.5f;
    /// <summary>The audio buffer choice (<see cref="AudioBuffer"/>); takes effect at the next start.</summary>
    public string Buffer { get; set; } = AudioBuffer.Automatic;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Where the settings live for a saves folder.</summary>
    public static string PathFor(string savesFolder) => System.IO.Path.Combine(savesFolder, "sound.settings");

    public static AudioSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"Could not save the settings: {ex.Message}");
        }
    }
}
