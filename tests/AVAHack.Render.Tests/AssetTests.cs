using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AVAHack.Render.Tests;

/// <summary>Every third-party file the game ships is in assets.manifest.json, and everything listed exists (spec 15.3).</summary>
public sealed class AssetTests
{
    private static string Root([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    [Fact]
    public void The_manifest_lists_every_texture_and_every_listed_file_exists()
    {
        string root = Root();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets.manifest.json")));
        var listed = new HashSet<string>();
        foreach (var s in doc.RootElement.GetProperty("sources").EnumerateArray())
        {
            Assert.True(File.Exists(Path.Combine(root, s.GetProperty("licenceFile").GetString()!)), "a source's licence file is missing");
            foreach (var f in s.GetProperty("files").EnumerateArray())
            {
                string path = f.GetString()!;
                Assert.True(File.Exists(Path.Combine(root, path)), $"{path} is listed but missing");
                listed.Add(path);
            }
        }
        var shipped = Directory.GetFiles(Path.Combine(root, "src/AVAHack.Render/Assets/Textures"), "*.png")
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'));
        Assert.All(shipped, p => Assert.Contains(p, listed));
    }

    [Fact]
    public void The_credits_are_up_to_date_with_the_manifest()
    {
        string credits = File.ReadAllText(Path.Combine(Root(), "CREDITS.md"));
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "assets.manifest.json")));
        foreach (var s in doc.RootElement.GetProperty("sources").EnumerateArray())
            Assert.Contains($"## {s.GetProperty("title").GetString()}", credits);
    }
}
