using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Game;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    private SkillId _treeSkill = SkillId.Blade;

    /// <summary>The character sheet (C): attributes and level-up choices, skills, and the perk trees.</summary>
    private void DrawCharacter(DrawingContext dc)
    {
        var h = Engine.Selected;
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(240, 22, 18, 14)), ViewArea);
        DrawText(dc, $"{h.Name}, level {h.Level} {h.Ancestry.Name} {h.Calling.Name}", 24, 14, 22, Gold, face: Bold);
        Button(dc, new Rect(860, 12, 80, 32), "Close", () => Overlay = Overlay.None);
        Button(dc, new Rect(700, 12, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); });
        Button(dc, new Rect(742, 12, 36, 32), "▶", CycleSelected);
        SheetBody(dc, h);
    }

    private void DrawSpellbook(DrawingContext dc)
    {
        dc.FillRectangle(new SolidColorBrush(Color.FromArgb(240, 22, 18, 14)), ViewArea);
        DrawText(dc, $"{Engine.Selected.Name}'s spellbook", 24, 14, 22, Gold, face: Bold);
        Button(dc, new Rect(860, 12, 80, 32), "Close", () => Overlay = Overlay.None);
        Button(dc, new Rect(700, 12, 36, 32), "◀", () => { CycleSelected(); CycleSelected(); CycleSelected(); });
        Button(dc, new Rect(742, 12, 36, 32), "▶", CycleSelected);
        SpellbookBody(dc, Engine.Selected);
    }

    partial void SpellbookBody(DrawingContext dc, Character h);

    private void SheetBody(DrawingContext dc, Character h)
    {
        // Attributes, with + buttons while there are points to spend.
        double x = 24, y = 56;
        DrawText(dc, h.AttributePoints > 0 ? $"Attributes ({h.AttributePoints} to spend)" : "Attributes", x, y, 15, Gold, face: Bold);
        y += 26;
        foreach (var a in Enum.GetValues<Attribute>())
        {
            var attr = a;
            DrawText(dc, $"{attr.ToString().ToUpperInvariant()}  {h.Score(attr)}  ({h.Mod(attr):+0;-0;0})", x, y, 14, Parchment);
            if (h.AttributePoints > 0) Button(dc, new Rect(x + 160, y - 3, 30, 24), "+", () => h.RaiseAttribute(attr));
            y += 28;
        }
        y += 8;
        if (h.PendingLevelChoices > 0)
        {
            DrawText(dc, $"Level up! Choose ({h.PendingLevelChoices} waiting):", x, y, 14, Brushes.Orange, face: Bold);
            y += 24;
            Button(dc, new Rect(x, y, 84, 30), "+5 HP", () => h.ChooseLevelBonus(LevelChoice.Health), size: 13);
            Button(dc, new Rect(x + 90, y, 84, 30), "+10 ST", () => h.ChooseLevelBonus(LevelChoice.Stamina), size: 13);
            Button(dc, new Rect(x + 180, y, 84, 30), "+10 MP", () => h.ChooseLevelBonus(LevelChoice.Mana), size: 13);
            y += 40;
        }
        string[] lines =
        [
            $"HP {Fmt(h.Hp)}/{h.MaxHp} · ST {Fmt(h.Stamina)}/{h.MaxStamina} · MP {Fmt(h.Mana)}/{h.MaxMana}",
            $"AC {h.ArmourClass} · Perception {h.PassivePerception()} · carry {h.CarryCapacityKg} kg",
            $"XP {h.CharacterXp} / {h.XpToNextLevel} to level {h.Level + 1}{(h.Heroic ? " (heroic)" : "")}",
            $"Skill ceiling {h.SkillCeiling} · perk points {h.PerkPoints}",
            $"Saves: " + string.Join(" ", Enum.GetValues<Attribute>().Select(a => $"{a.ToString()[..1]}{h.SaveBonus(a):+0;-0;0}")),
        ];
        foreach (var l in lines) { DrawText(dc, l, x, y, 12, Dim, maxWidth: 280); y += 20; }

        // Skills, each a button that shows its perk tree.
        double sx = 320, sy = 56;
        DrawText(dc, "Skills (click for perks)", sx, sy, 15, Gold, face: Bold);
        sy += 24;
        foreach (var s in Enum.GetValues<SkillId>())
        {
            var skill = s;
            var r = new Rect(sx, sy, 240, 21);
            bool sel = skill == _treeSkill;
            if (sel) dc.FillRectangle(SelectedBrush, r);
            int rank = h.Rank(skill), lvl = h.SkillLevel(skill);
            DrawText(dc, Content.Skill(skill).Name, sx + 4, sy + 2, 12, sel ? Gold : Parchment);
            DrawText(dc, lvl != rank ? $"{rank} ({lvl})" : rank.ToString(CultureInfo.InvariantCulture), sx + 128, sy + 2, 12, Parchment, face: Bold);
            double share = h.Xp(skill) / Progression.XpToNext(rank);
            dc.FillRectangle(Trough, new Rect(sx + 172, sy + 8, 60, 6));
            dc.FillRectangle(XpBrush, new Rect(sx + 172, sy + 8, 60 * Math.Clamp(share, 0, 1), 6));
            bool canBuy = Content.PerksFor(skill).Any(p => h.CanBuyPerk(p.Id));
            if (canBuy) dc.DrawEllipse(Gold, null, new Point(sx + 236, sy + 11), 3, 3);
            ClickArea(r, () => _treeSkill = skill);
            sy += 22;
        }

        // The selected skill's perk tree.
        double px = 580, py = 56;
        DrawText(dc, $"{Content.Skill(_treeSkill).Name} perks · {h.PerkPoints} point{(h.PerkPoints == 1 ? "" : "s")}", px, py, 15, Gold, face: Bold);
        py += 26;
        foreach (var p in Content.PerksFor(_treeSkill))
        {
            var perk = p;
            int have = h.PerkRank(perk.Id);
            var why = h.WhyNotPerk(perk.Id);
            var box = new Rect(px, py, 360, 58);
            var edge = have >= perk.MaxRank ? Gold : why is null ? Brushes.LightGreen : PanelEdge;
            dc.DrawRectangle(have > 0 ? SelectedBrush : ButtonBrush, new Pen(edge, 1.5), box, 4, 4);
            string ranks = perk.MaxRank > 1 ? $" {have}/{perk.MaxRank}" : have > 0 ? " ✓" : "";
            string need = have < perk.MaxRank ? $"needs {perk.Ranks[have]}" : "";
            DrawText(dc, perk.Name + ranks, px + 8, py + 4, 13, have > 0 ? Gold : Parchment, face: Bold, maxWidth: 230);
            DrawText(dc, need, px + 250, py + 4, 11, Dim);
            DrawText(dc, perk.Text, px + 8, py + 22, 11, Dim, maxWidth: 280);
            if (why is null) Button(dc, new Rect(px + 296, py + 26, 56, 26), "Take", () => h.BuyPerk(perk.Id), size: 12);
            py += 64;
            if (py > 560) break;
        }
    }
}
