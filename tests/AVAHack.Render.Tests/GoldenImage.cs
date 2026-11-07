using System.Runtime.CompilerServices;

namespace AVAHack.Render.Tests;

/// <summary>
/// Compares a rendered frame with its golden PNG in <c>Golden/</c>. Set AVAHACK_UPDATE_GOLDEN=1 to write the
/// goldens from the current renderer instead (then look at them before committing). A small tolerance allows
/// for floating-point differences between machines.
/// </summary>
internal static class GoldenImage
{
    /// <summary>Largest difference allowed in any colour channel of a pixel.</summary>
    public const int ChannelTolerance = 24;
    /// <summary>Share of pixels allowed to differ by more than that.</summary>
    public const double MaxBadShare = 0.005;

    public static void Check(string name, IViewRenderer r, [CallerFilePath] string here = "")
    {
        string dir = Path.Combine(Path.GetDirectoryName(here)!, "Golden");
        string path = Path.Combine(dir, name + ".png");

        if (Environment.GetEnvironmentVariable("AVAHACK_SHOTS") is { Length: > 0 } shots)
            Png.Save(Path.Combine(shots, $"view-{name}.png"), r.Pixels, r.Width, r.Height);

        if (Environment.GetEnvironmentVariable("AVAHACK_UPDATE_GOLDEN") == "1")
        {
            Png.Save(path, r.Pixels, r.Width, r.Height);
            return;
        }

        Assert.True(File.Exists(path), $"No golden image {path}. Run the tests with AVAHACK_UPDATE_GOLDEN=1 to make it.");
        var (golden, gw, gh) = Png.Load(path);
        Assert.Equal((r.Width, r.Height), (gw, gh));
        int bad = 0;
        for (int i = 0; i < golden.Length; i++)
        {
            uint a = golden[i], b = r.Pixels[i];
            int dr = Math.Abs((int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF));
            int dg = Math.Abs((int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF));
            int db = Math.Abs((int)(a & 0xFF) - (int)(b & 0xFF));
            if (Math.Max(dr, Math.Max(dg, db)) > ChannelTolerance) bad++;
        }
        double share = (double)bad / golden.Length;
        if (share > MaxBadShare)
        {
            string actual = Path.Combine(Path.GetTempPath(), $"avahack-{name}-actual.png");
            Png.Save(actual, r.Pixels, r.Width, r.Height);
            Assert.Fail($"{name}: {share:P2} of pixels differ from the golden image (allowed {MaxBadShare:P1}). This frame: {actual}");
        }
    }
}
