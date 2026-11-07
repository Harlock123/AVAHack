using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Game;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    /// <summary>The spellbook (B): the chosen hero's spells by school. Click to cast; right-click for a quick slot.</summary>
    partial void SpellbookBody(DrawingContext dc, Character h)
    {
        var known = h.KnownSpells.Select(Content.Spell).OrderBy(s => s.School).ThenBy(s => s.Rank).ToList();
        if (known.Count == 0)
        {
            DrawText(dc, $"{h.Name} knows no spells. Tomes teach them, if the school is good enough.", 24, 70, 15, Dim);
            return;
        }
        DrawText(dc, $"Mana {Fmt(h.Mana)} / {h.MaxMana} · click a spell to cast it · right-click to put it on a quick slot (F1–F8)", 24, 50, 13, Dim);
        double y = 80;
        foreach (var school in known.GroupBy(s => s.School))
        {
            DrawText(dc, $"{Content.Skill(school.Key).Name} ({h.SkillLevel(school.Key)})", 24, y, 15, Gold, face: Bold);
            y += 24;
            foreach (var s in school)
            {
                var spell = s;
                var why = Engine.WhyNotCast(h, spell.Id);
                int slot = Array.IndexOf(h.QuickSlots, spell.Id);
                var r = new Rect(24, y, 900, 40);
                Button(dc, r, "", () =>
                {
                    if (Engine.Cast(h, spell.Id) is { } no) Engine.Say(no);
                    else Overlay = Overlay.None;
                }, enabled: true, onRight: () => QuickAssign(h, spell.Id));
                DrawText(dc, spell.Name + (spell.Cantrip ? " (cantrip)" : ""), 36, y + 4, 14, why is null ? Parchment : Dim, face: Bold);
                DrawText(dc, $"{Engine.ManaCost(h, spell):0.#} mana · {spell.CastTime:0.0} s{(slot >= 0 ? $" · F{slot + 1}" : "")}", 36, y + 22, 11, Dim);
                DrawText(dc, spell.Text, 300, y + 4, 12, Dim, maxWidth: 610);
                y += 46;
                if (y > 560) return;
            }
            y += 6;
        }
    }

    private void QuickAssign(Character h, string spell)
    {
        int at = Array.IndexOf(h.QuickSlots, spell);
        if (at >= 0) { h.QuickSlots[at] = null; return; }
        for (int i = 0; i < h.QuickSlots.Length; i++)
            if (h.QuickSlots[i] is null) { h.QuickSlots[i] = spell; return; }
        h.QuickSlots[^1] = spell;
    }

    /// <summary>F1–F8 cast the chosen hero's quick spells.</summary>
    private bool QuickCast(PhysicalKey key)
    {
        int i = key switch
        {
            PhysicalKey.F1 => 0, PhysicalKey.F2 => 1, PhysicalKey.F3 => 2, PhysicalKey.F4 => 3,
            PhysicalKey.F5 => 4, PhysicalKey.F6 => 5, PhysicalKey.F7 => 6, PhysicalKey.F8 => 7, _ => -1,
        };
        if (i < 0) return false;
        var h = Engine.Selected;
        if (h.QuickSlots[i] is { } spell && Engine.Cast(h, spell) is { } why) Engine.Say(why);
        return true;
    }

    /// <summary>The casting bar and the quick slots on a hero's card.</summary>
    private void CardMagic(DrawingContext dc, Rect card, Character h)
    {
        if (h.CastingSpell is { } id)
        {
            var bar = new Rect(card.X + 232, card.Y + 140, 76, 8);
            dc.FillRectangle(Trough, bar);
            dc.FillRectangle(MpBrush, new Rect(bar.X, bar.Y, bar.Width * (1 - h.CastLeft / Math.Max(0.01, h.CastTotal)), bar.Height));
            DrawText(dc, Content.Spell(id).Name, card.X + 232, card.Y + 150, 10, Brushes.LightSkyBlue, maxWidth: 80);
        }
        if (h.Ward > 0) DrawText(dc, $"ward {Fmt(h.Ward)}", card.X + 232, card.Y + 162, 10, Brushes.LightSkyBlue);
        if (Engine.Selected == h)
        {
            var spells = h.QuickSlots.Select((s, i) => s is null ? null : $"F{i + 1} {Content.Spell(s).Name}").Where(x => x is not null).Take(3);
            DrawText(dc, string.Join("  ", spells), card.X + 92, card.Y + 166, 10, Dim, maxWidth: 220);
        }
    }
}
