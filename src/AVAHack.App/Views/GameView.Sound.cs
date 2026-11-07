using AVAHack.App.Audio;
using AVAHack.Game;

namespace AVAHack.App.Views;

/// <summary>Sound (spec 16.3): the engine's cues, heard from where the party stands, and the region's ambience.</summary>
public sealed partial class GameView
{
    private IAudioEngine _audio = new NullAudioEngine();
    private AudioSettings? _sound;

    /// <summary>Where sounds go: OpenAL in the game, a silent recorder in tests.</summary>
    public IAudioEngine Audio
    {
        get => _audio;
        set
        {
            _audio = value;
            _audio.SetVolumes(SoundSettings.Volume, SoundSettings.Ambience);
        }
    }

    /// <summary>The sound settings, beside the saves folder.</summary>
    public AudioSettings SoundSettings => _sound ??= AudioSettings.Load(AudioSettings.PathFor(Store.Folder));

    private void OnSound(SoundCue cue)
    {
        if (Screen != Screen.Play) return;
        var (gain, pan) = Engine.Hearing(cue);
        if (gain > 0.02) Audio.PlayEffect(cue.Id, (float)gain, (float)pan);
    }

    /// <summary>Each frame: the region's ambience under the play, and under the title screen's cellar.</summary>
    private void KeepAmbience()
    {
        string? wanted = Screen is Screen.Play or Screen.Title ? "amb." + Engine.Level.Theme : null;
        if (Audio.CurrentAmbience != wanted) Audio.PlayAmbience(wanted);
    }

    private void ChangeVolume(float masterBy, float ambienceBy)
    {
        var s = SoundSettings;
        s.Volume = MathF.Round(Math.Clamp(s.Volume + masterBy, 0, 1), 1);
        s.Ambience = MathF.Round(Math.Clamp(s.Ambience + ambienceBy, 0, 1), 1);
        Audio.SetVolumes(s.Volume, s.Ambience);
        s.Save(AudioSettings.PathFor(Store.Folder));
        Audio.PlayEffect("ui.click");
    }

    private void CycleBuffer()
    {
        var s = SoundSettings;
        var ids = AudioBuffer.Choices.Select(c => c.Id).ToList();
        s.Buffer = ids[(ids.IndexOf(s.Buffer) + 1) % ids.Count];
        s.Save(AudioSettings.PathFor(Store.Folder));
    }

    /// <summary>The Menu's sound row: volume, ambience and the audio buffer (spec 16.3).</summary>
    private void DrawSoundRow(Avalonia.Media.DrawingContext dc, Avalonia.Rect box, double y)
    {
        var s = SoundSettings;
        DrawText(dc, "Sound", box.X + 40, y + 9, 16, Gold, face: Bold);
        Button(dc, new Avalonia.Rect(box.X + 110, y, 34, 34), "−", () => ChangeVolume(-0.1f, 0), size: 14);
        DrawText(dc, $"Volume {s.Volume * 100:0}%", box.X + 150, y + 9, 14, Parchment);
        Button(dc, new Avalonia.Rect(box.X + 258, y, 34, 34), "+", () => ChangeVolume(0.1f, 0), size: 14);
        Button(dc, new Avalonia.Rect(box.X + 310, y, 34, 34), "−", () => ChangeVolume(0, -0.1f), size: 14);
        DrawText(dc, $"Ambience {s.Ambience * 100:0}%", box.X + 350, y + 9, 14, Parchment);
        Button(dc, new Avalonia.Rect(box.X + 472, y, 34, 34), "+", () => ChangeVolume(0, 0.1f), size: 14);
        string label = AudioBuffer.Choices.FirstOrDefault(c => c.Id == s.Buffer).Label ?? s.Buffer;
        Button(dc, new Avalonia.Rect(box.X + 520, y, 240, 34), "Buffer: " + label.Split(' ')[0], CycleBuffer, size: 13);
        if (Audio.Problem is { } why) DrawText(dc, why, box.X + 40, y + 40, 12, Dim, maxWidth: 720);
        else if (s.Buffer != _startBuffer) DrawText(dc, "The new buffer size takes effect when the game next starts.", box.X + 40, y + 40, 12, Dim);
    }

    private string? _startBuffer;

    /// <summary>The buffer choice the audio was opened with, to tell the player when a change needs a restart.</summary>
    public void AudioStartedWith(string buffer) => _startBuffer = buffer;
}
