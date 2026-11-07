using System.Text.Json;

namespace AVAHack.Saves;

/// <summary>
/// Save files on disk, one JSON file per slot. The folder is AVAHACK_SAVE_DIR if set (tests always set it),
/// otherwise the user's local application data folder.
/// </summary>
public sealed class SaveStore(string? folder = null)
{
    public const string QuickSlot = "quick";
    public const string AutoSlot = "auto";

    public string Folder { get; } = folder ?? DefaultFolder();

    public static string DefaultFolder() =>
        Environment.GetEnvironmentVariable("AVAHACK_SAVE_DIR") is { Length: > 0 } env
            ? env
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVAHack", "saves");

    private static readonly JsonSerializerOptions Json = new(Content.ContentDb.JsonOptions)
    {
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    private string PathOf(string slot) => System.IO.Path.Combine(Folder, $"{slot}.json");

    public void Save(string slot, SaveGame game)
    {
        Directory.CreateDirectory(Folder);
        string tmp = PathOf(slot) + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(game, Json));
        File.Move(tmp, PathOf(slot), overwrite: true);
    }

    public SaveGame? Load(string slot)
    {
        var p = PathOf(slot);
        return File.Exists(p) ? JsonSerializer.Deserialize<SaveGame>(File.ReadAllText(p), Json) : null;
    }

    public bool Exists(string slot) => File.Exists(PathOf(slot));

    public void Delete(string slot)
    {
        if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
    }

    /// <summary>The slots there are, newest first, with each save's summary.</summary>
    public IReadOnlyList<(string Slot, DateTime SavedAt, string Summary)> List()
    {
        if (!Directory.Exists(Folder)) return [];
        var list = new List<(string, DateTime, string)>();
        foreach (var f in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var s = JsonSerializer.Deserialize<SaveGame>(File.ReadAllText(f), Json);
                if (s is not null) list.Add((System.IO.Path.GetFileNameWithoutExtension(f), s.SavedAt, s.Summary));
            }
            catch (JsonException) { /* a broken file is skipped, not fatal */ }
        }
        return list.OrderByDescending(x => x.Item2).ToList();
    }
}
