using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Game;
using AVAHack.Items;
using AVAHack.Render;
using AVAHack.Saves;
using AVAHack.World;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    private Delve? _delve;
    private Party? _pendingParty;
    private DelveOptions _options = new();
    private bool _typingSeed;
    private string _theme = "cellar";

    public Delve? CurrentDelve => _delve;
    public const string IronmanSlot = "ironman";

    /// <summary>A new party goes to the Delve options first.</summary>
    private void ChooseOptions(Party party)
    {
        _pendingParty = party;
        _options = new DelveOptions();
        Screen = Screen.Options;
    }

    public void StartDelve(Party party, DelveOptions options)
    {
        _delve = Delve.Start(Content, party, options);
        _delve.LevelChanged += _ => OnLevelChanged();
        _delve.Engine.GameOver += () => { if (_delve?.Options.Ironman == true) Store.Delete(IronmanSlot); };
        WinTitle = "The Delve is done";
        WinLine = $"Down through {options.Depth} levels (seed {_delve.SeedCode}) in";
        StartGame(_delve.Engine);
        OnLevelChanged();
    }

    private void OnLevelChanged()
    {
        if (_delve is { Options.Ironman: true } d && d.Engine.Mode == GameMode.Playing) Store.Save(IronmanSlot, d.ToSave());
    }

    private SaveGame SaveOf(GameEngine g) => _delve is { } d && d.Engine == g ? d.ToSave() : g.ToSave();

    private GameEngine LoadOf(SaveGame s)
    {
        if (!Delve.IsDelve(s)) { _delve = null; return GameEngine.FromSave(s, Content); }
        _delve = Delve.FromSave(s, Content);
        _delve.LevelChanged += _ => OnLevelChanged();
        WinTitle = "The Delve is done";
        WinLine = $"Down through {_delve.Options.Depth} levels (seed {_delve.SeedCode}) in";
        return _delve.Engine;
    }

    /// <summary>Swaps the textures when the party walks into another region.</summary>
    private void FitTextures()
    {
        if (Engine.Level.Theme == _theme) return;
        _theme = Engine.Level.Theme;
        Renderer.Textures = TextureFactory.ForTheme(_theme);
    }

    private void DrawOptions(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, new Rect(0, 0, CanvasWidth, CanvasHeight));
        DrawText(dc, "The Delve", 40, 24, 34, Gold, face: Bold);
        DrawText(dc, $"{string.Join(", ", _pendingParty?.Members.Select(m => m.Name) ?? [])} will go down. Choose how.", 40, 70, 15, Dim);
        double x = 60, y = 120;
        DrawText(dc, "Seed", x, y + 6, 15, Parchment, face: Bold);
        Button(dc, new Rect(x + 200, y, 360, 36), (_options.Seed.Length == 0 ? "(random)" : _options.Seed) + (_typingSeed ? "_" : ""), () => _typingSeed = true, selected: _typingSeed);
        DrawText(dc, "Any words, or a friend's 8-letter code.", x + 580, y + 8, 13, Dim);
        y += 56;
        Row(dc, x, ref y, "Depth", ["6", "12", "18", "24"], _options.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture), v => _options = _options with { Depth = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) });
        Row(dc, x, ref y, "Monster strength", Difficulty.All.Select(d => d.Name).ToArray(), _options.Strength.Name, v => _options = _options with { Strength = Difficulty.All.First(d => d.Name == v) });
        Row(dc, x, ref y, "Monster density", ["Sparse", "Normal", "Swarming"], _options.Density switch { < 0.8 => "Sparse", > 1.2 => "Swarming", _ => "Normal" },
            v => _options = _options with { Density = v switch { "Sparse" => 0.6, "Swarming" => 1.5, _ => 1 } });
        Row(dc, x, ref y, "Traps", ["Few", "Normal", "Many"], _options.Traps switch { < 0.8 => "Few", > 1.2 => "Many", _ => "Normal" },
            v => _options = _options with { Traps = v switch { "Few" => 0.5, "Many" => 1.6, _ => 1 } });
        Row(dc, x, ref y, "Magic items", ["Rare", "Normal", "Plentiful"], _options.Magic.ToString(), v => _options = _options with { Magic = Enum.Parse<MagicItems>(v) });
        Row(dc, x, ref y, "Food and water", ["On", "Off"], _options.Food ? "On" : "Off", v => _options = _options with { Food = v == "On" });
        Row(dc, x, ref y, "Pause in combat", ["On", "Off"], _options.PauseInCombat ? "On" : "Off", v => _options = _options with { PauseInCombat = v == "On" });
        Row(dc, x, ref y, "Ironman", ["Off", "On"], _options.Ironman ? "On" : "Off", v => _options = _options with { Ironman = v == "On" });
        DrawText(dc, "Ironman keeps one save, made at every stair, and deletes it when the party falls.", x + 200, y - 6, 12, Dim);
        string? strength = _options.Strength.DeathSaves ? "Cruel and Brutal: the fallen make death saves, and can die." : null;
        if (strength is not null) DrawText(dc, strength, x + 200, y + 14, 12, Brushes.Orange);
        Button(dc, new Rect(40, 730, 200, 46), "Back", () => Screen = Screen.Title);
        Button(dc, new Rect(260, 730, 300, 46), "Practise in The Cellars", () => StartGame(new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), (ulong)Environment.TickCount64, _pendingParty!, Content)), enabled: _pendingParty is not null);
        Button(dc, new Rect(980, 730, 260, 46), "Begin the Delve", () => StartDelve(_pendingParty!, _options), enabled: _pendingParty is not null, size: 18);
    }

    private void Row(DrawingContext dc, double x, ref double y, string label, string[] choices, string current, Action<string> pick)
    {
        DrawText(dc, label, x, y + 8, 15, Parchment, face: Bold);
        for (int i = 0; i < choices.Length; i++)
        {
            var c = choices[i];
            Button(dc, new Rect(x + 200 + i * 130, y, 120, 36), c, () => pick(c), selected: c == current, size: 14);
        }
        y += 52;
    }

    private bool OptionsKey(Avalonia.Input.KeyEventArgs e)
    {
        if (!_typingSeed) return false;
        if (e.PhysicalKey == PhysicalKey.Backspace && _options.Seed.Length > 0) _options = _options with { Seed = _options.Seed[..^1] };
        else if (e.PhysicalKey is PhysicalKey.Enter or PhysicalKey.Escape or PhysicalKey.Tab) _typingSeed = false;
        return true;
    }

    private void OptionsText(string text)
    {
        if (!_typingSeed) return;
        foreach (char ch in text)
            if ((char.IsLetterOrDigit(ch) || ch is ' ' or '-') && _options.Seed.Length < 24) _options = _options with { Seed = _options.Seed + ch };
        // Text arrives after the key's own redraw, so the box must ask for another or the letter shows only when the mouse moves.
        InvalidateVisual();
    }
}
