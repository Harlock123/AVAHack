using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(AVAHack.App.Tests.TestAppBuilder))]

namespace AVAHack.App.Tests;

public static class TestAppBuilder
{
    // Saves made by the app's tests go to a throwaway folder, never the user's.
    static TestAppBuilder() =>
        Environment.SetEnvironmentVariable("AVAHACK_SAVE_DIR", Path.Combine(Path.GetTempPath(), "avahack-app-tests-" + Environment.ProcessId));

    // Skia rendering (not headless drawing) so frames can be captured as screenshots.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
