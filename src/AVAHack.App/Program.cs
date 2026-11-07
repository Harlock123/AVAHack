using Avalonia;

namespace AVAHack.App;

internal static class Program
{
    // Don't use Avalonia, third-party APIs or anything that needs a SynchronizationContext before AppMain is
    // called: things aren't initialised yet.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
