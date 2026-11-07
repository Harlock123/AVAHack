using Avalonia.Controls;
using AVAHack.App.Audio;
using AVAHack.App.Views;
using AVAHack.Content;

namespace AVAHack.App;

public partial class MainWindow : Window
{
    private readonly IAudioEngine _audio;

    public MainWindow()
    {
        InitializeComponent();
        var view = new GameView(ContentDb.LoadBuiltIn(), startLoop: true);
        // Sound: OpenAL with the player's buffer choice (a bigger period in a virtual machine), or silence.
        string buffer = view.SoundSettings.Buffer;
        _audio = OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(buffer, VirtualMachine.Detect()));
        view.Audio = _audio;
        view.AudioStartedWith(buffer);
        Content = view;
        Closed += (_, _) => _audio.Dispose();
    }
}
