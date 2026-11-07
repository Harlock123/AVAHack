using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using AVAHack.App.Audio;
using AVAHack.App.Views;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.App.Tests;

/// <summary>Sound (spec 16.3): every cue has a sound, every sound is used, and the view plays what the engine raises.</summary>
public sealed class SoundTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    /// <summary>Every cue id the engine can raise: the literals passed to Cue and CueHere, the ids the sound
    /// rules pick from, a cast for each school and an ambience for each region.</summary>
    private static HashSet<string> EngineCues()
    {
        var cues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src/AVAHack.Core/Game"), "*.cs"))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"Cue(?:Here)?\((?:[^()""]*\? )?""([a-zA-Z.\-]+)""")) if (!m.Groups[1].Value.EndsWith('.')) cues.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"""((?:step|bark|hit|die|impact|door|trap|cast|sting|hero|swing|party)\.[a-zA-Z.]+|step|bump|miss|swing|shoot|block|portcullis|lever|pit|chest|pickup|gold|eat|read|drink|torch|locked|unlock|secret|stairs|waystone|brew|cook|anvil|enchant|bell|levelup)""")) cues.Add(m.Groups[1].Value);
        }
        // And what the App plays itself (clicks, screens, equipping).
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src/AVAHack.App"), "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"PlayEffect\(""([a-zA-Z.\-]+)""")) cues.Add(m.Groups[1].Value);
        var db = ContentDb.LoadBuiltIn();
        foreach (var s in db.Spells) cues.Add("cast." + s.School.ToString().ToLowerInvariant());
        foreach (var region in db.Regions) cues.Add("amb." + region.Id);
        cues.Add("ui.click");
        return cues;
    }

    [Fact]
    public void Every_cue_the_engine_raises_has_a_sound()
    {
        var missing = EngineCues().Where(c => SoundBank.TakesOf(c).Count == 0).Order().ToList();
        Assert.True(missing.Count == 0, "No sound for: " + string.Join(", ", missing));
        Assert.True(EngineCues().Count > 60);
    }

    [Fact]
    public void Every_sound_is_used_and_every_one_reads()
    {
        var used = EngineCues();
        var unused = SoundBank.Cues.Where(c => !used.Contains(c)).Order().ToList();
        Assert.True(unused.Count == 0, "Sounds nothing plays: " + string.Join(", ", unused));
        foreach (var cue in SoundBank.Cues)
        foreach (var take in SoundBank.TakesOf(cue))
        {
            using var s = SoundBank.Open(take)!;
            var (samples, channels, rate) = WavReader.Read(s);
            Assert.Equal((1, 22050), (channels, rate)); // mono, so OpenAL can place it
            Assert.True(samples.Length > 500, $"{take} is nearly empty");
        }
    }

    [AvaloniaFact]
    public void The_view_plays_what_the_engine_raises_and_the_region_s_ambience()
    {
        var db = ContentDb.LoadBuiltIn();
        var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 3, GameView.DefaultParty(db), db);
        var audio = new NullAudioEngine();
        var v = new GameView(g, startLoop: false, store: new SaveStore(Path.Combine(Path.GetTempPath(), "avahack-sound-" + Guid.NewGuid().ToString("N")))) { Audio = audio };
        v.Advance(0.1);
        Assert.Equal("amb.cellar", audio.CurrentAmbience);
        g.Press(GameAction.StepForward);
        v.Advance(1);
        Assert.Contains(audio.Played, p => p.Cue == "step" && p.Pan == 0);
        // A creature off to one side is heard from that side.
        var rat = g.AddMonster("cellarRat", g.PartyCell.Step(g.Facing.Right()), g.Facing.Left());
        audio.Played.Clear();
        g.Sound += _ => { };
        v.Advance(0.5);
        Assert.Contains(audio.Played, p => p.Cue.StartsWith("bark.", StringComparison.Ordinal) && p.Pan > 0.3);
    }

    [Fact]
    public void The_sound_settings_are_kept_with_the_saves()
    {
        string dir = Path.Combine(Path.GetTempPath(), "avahack-settings-" + Guid.NewGuid().ToString("N"));
        var s = new AudioSettings { Volume = 0.3f, Ambience = 0.9f, Buffer = AudioBuffer.Large };
        s.Save(AudioSettings.PathFor(dir));
        var back = AudioSettings.Load(AudioSettings.PathFor(dir));
        Assert.Equal((0.3f, 0.9f, AudioBuffer.Large), (back.Volume, back.Ambience, back.Buffer));
        Assert.Empty(new SaveStore(dir).List()); // not mistaken for a save
        Assert.Equal(0.8f, AudioSettings.Load(Path.Combine(dir, "none.settings")).Volume);
        Assert.Equal(4096, AudioBuffer.PeriodFrames(AudioBuffer.Automatic, inVirtualMachine: true));
        Assert.Equal(0, AudioBuffer.PeriodFrames(AudioBuffer.Automatic, inVirtualMachine: false));
    }

    /// <summary>
    /// Opt-in (AVAHACK_AUDIO_SMOKE=1): opens the real OpenAL device, plays every take and the ambience at zero
    /// volume, and closes it. Silent, but it runs the OpenAL code for real. Off by default: tests don't touch
    /// the machine's sound device, and CI has none.
    /// </summary>
    [Fact]
    public void OpenAL_plays_every_take_when_asked_to_try()
    {
        if (Environment.GetEnvironmentVariable("AVAHACK_AUDIO_SMOKE") != "1") return;
        using var audio = OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        Assert.True(audio.IsAvailable, (audio as NullAudioEngine)?.Problem);
        audio.SetVolumes(0, 0);
        foreach (var cue in SoundBank.Cues)
            for (int i = 0; i < SoundBank.TakesOf(cue).Count * 3; i++) audio.PlayEffect(cue, 1, i % 3 - 1);
        audio.PlayAmbience("amb.works");
        Thread.Sleep(300);
        audio.PlayAmbience("amb.choir");
        Thread.Sleep(300);
        Assert.Equal("amb.choir", audio.CurrentAmbience);
    }
}
