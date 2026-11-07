using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    /// <summary>The character sheet (C): attributes, skills, and (from M4) levelling and perks.</summary>
    private void DrawCharacter(DrawingContext dc)
    {
        var h = Engine.Selected;
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 22, 18, 14)), ViewArea);
        DrawText(dc, $"{h.Name}, level {h.Level} {h.Ancestry.Name} {h.Calling.Name}", 24, 14, 22, Gold, face: Bold);
        Button(dc, new Rect(860, 12, 80, 32), "Close", () => Overlay = Overlay.None);
        Button(dc, new Rect(700, 12, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); });
        Button(dc, new Rect(742, 12, 36, 32), "▶", CycleSelected);
        SheetBody(dc, h);
    }

    private void DrawSpellbook(DrawingContext dc)
    {
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 22, 18, 14)), ViewArea);
        DrawText(dc, $"{Engine.Selected.Name}'s spellbook", 24, 14, 22, Gold, face: Bold);
        Button(dc, new Rect(860, 12, 80, 32), "Close", () => Overlay = Overlay.None);
        SpellbookBody(dc, Engine.Selected);
    }

    partial void SpellbookBody(DrawingContext dc, Character h);

    /// <summary>The sheet's contents; M4's levelling and perks replace the simple version.</summary>
    private void SheetBody(DrawingContext dc, Character h)
    {
        if (SheetHook is { } hook) { hook(dc, h); return; }
        int y = 60;
        foreach (var s in Enum.GetValues<SkillId>())
        {
            DrawText(dc, $"{Content.Skill(s).Name}: {h.Rank(s)}", 24 + (y > 540 ? 300 : 0), y > 540 ? y - 480 : y, 13, Parchment);
            y += 22;
        }
    }

    public Action<DrawingContext, Character>? SheetHook { get; set; }
}
