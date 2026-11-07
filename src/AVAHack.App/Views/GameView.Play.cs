using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    internal static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromRgb(27, 22, 18));
    internal static readonly IBrush PanelEdge = new SolidColorBrush(Color.FromRgb(92, 74, 50));
    internal static readonly Pen EdgePen = new(PanelEdge, 2);
    internal static readonly IBrush Parchment = new SolidColorBrush(Color.FromRgb(226, 212, 182));
    internal static readonly IBrush Dim = new SolidColorBrush(Color.FromRgb(150, 136, 112));
    internal static readonly IBrush Gold = new SolidColorBrush(Color.FromRgb(222, 176, 74));
    internal static readonly IBrush HpBrush = new SolidColorBrush(Color.FromRgb(176, 40, 32));
    internal static readonly IBrush StBrush = new SolidColorBrush(Color.FromRgb(70, 150, 60));
    internal static readonly IBrush MpBrush = new SolidColorBrush(Color.FromRgb(56, 96, 180));
    internal static readonly IBrush XpBrush = new SolidColorBrush(Color.FromRgb(190, 150, 60));
    internal static readonly IBrush Trough = new SolidColorBrush(Color.FromRgb(12, 10, 8));
    internal static readonly IBrush Shadow = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
    internal static readonly IBrush ButtonBrush = new SolidColorBrush(Color.FromRgb(48, 38, 28));
    internal static readonly IBrush HoverBrush = new SolidColorBrush(Color.FromRgb(70, 56, 38));
    internal static readonly IBrush SelectedBrush = new SolidColorBrush(Color.FromRgb(86, 64, 34));
    internal static readonly IBrush DisabledBrush = new SolidColorBrush(Color.FromRgb(34, 30, 26));

    /// <summary>Hero <paramref name="i"/>'s card in the party bar (one per slot, front left first).</summary>
    private static Rect CardOf(int i) => new(i * 320, 600, 320, 200);

    private static Rect HandBox(int hero, HandSide side)
    {
        var c = CardOf(hero);
        return new Rect(c.X + (side == HandSide.Left ? 92 : 162), c.Y + 112, 62, 62);
    }

    private void DrawViewArea(DrawingContext dc)
    {
        dc.DrawImage(_bitmap, new Rect(0, 0, Renderer.Width, Renderer.Height), ViewArea);
        if (Engine.HeroHurt > 0)
            dc.FillRectangle(new SolidColorBrush(Color.FromArgb((byte)(Engine.HeroHurt / 0.3 * 90), 160, 0, 0)), ViewArea);

        double k = ViewArea.Width / Renderer.Width;
        foreach (var (f, age) in _floats)
        {
            if (Renderer.Project(f.X, f.Y, f.Z + age * 0.3) is not { } p) continue;
            var brush = f.Kind switch
            {
                TextKind.HeroHurt => Brushes.OrangeRed,
                TextKind.MonsterHurt => Brushes.White,
                TextKind.Critical => Brushes.Gold,
                TextKind.Blocked => Brushes.LightSkyBlue,
                TextKind.Miss => Brushes.LightGray,
                _ => Brushes.Wheat,
            };
            double size = Math.Clamp(26 / p.Depth, 12, 30);
            using (dc.PushOpacity(Math.Clamp(1.4 - age, 0, 1)))
                DrawText(dc, f.Text, p.X * k, p.Y * k - size, size, brush, centre: true, face: Bold);
        }

        var plate = new Rect(ViewArea.Center.X - 30, 10, 60, 32);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 16, 12)), new Pen(PanelEdge, 2), plate, 6, 6);
        DrawText(dc, Engine.Facing.Letter().ToString(), plate.Center.X, plate.Y + 3, 21, Gold, centre: true, face: Bold);
        ViewExtras(dc);

        if (ShowStats)
            DrawText(dc, string.Create(CultureInfo.InvariantCulture, $"{_fps:F0} fps  ·  view {LastRenderMs:F1} ms  ·  {Engine.PartyCell} {Engine.Facing}"),
                     12, 10, 14, Brushes.LightGreen);
    }

    /// <summary>Things later milestones draw over the view (the stealth eye, spell casting).</summary>
    partial void ViewExtras(DrawingContext dc);

    private void DrawSidePanel(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, SideArea);
        dc.DrawLine(EdgePen, SideArea.TopLeft, SideArea.BottomLeft);
        DrawText(dc, Engine.Level.Name, SideArea.X + 16, 12, 20, Gold, face: Bold);
        var t = TimeSpan.FromSeconds(Engine.Time);
        DrawText(dc, string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalMinutes}:{t.Seconds:00}  ·  {Engine.Kills} slain  ·  {Engine.Party.Gold} gold"), SideArea.X + 16, 40, 14, Dim);
        DrawAutomap(dc, new Rect(SideArea.X + 16, 66, 288, 288));
        DrawLog(dc, new Rect(SideArea.X + 16, 366, 288, 200));
        DrawText(dc, "I inventory · C sheet · Esc menu · F1 help", SideArea.X + 16, 574, 12, Dim);
    }

    private void DrawAutomap(DrawingContext dc, Rect area)
    {
        var level = Engine.Level;
        double cell = Math.Floor(Math.Min(area.Width / level.Width, area.Height / level.Height));
        double ox = area.X + (area.Width - cell * level.Width) / 2, oy = area.Y + (area.Height - cell * level.Height) / 2;
        dc.FillRectangle(Trough, area);
        var floor = new SolidColorBrush(Color.FromRgb(96, 86, 70));
        var wall = new SolidColorBrush(Color.FromRgb(44, 38, 32));
        var door = new SolidColorBrush(Color.FromRgb(140, 96, 50));
        var gate = new SolidColorBrush(Color.FromRgb(120, 120, 130));
        var pit = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        var plateBrush = new SolidColorBrush(Color.FromRgb(150, 130, 90));
        var chest = new SolidColorBrush(Color.FromRgb(200, 150, 60));
        foreach (var c in Engine.Explored)
        {
            var r = new Rect(ox + c.X * cell, oy + c.Y * cell, cell, cell);
            var kind = level.KindAt(c);
            IBrush b = kind == CellKind.Wall ? wall
                : kind == CellKind.Door ? (level.DoorAt(c)!.Kind == DoorKind.Hand ? door : gate)
                : floor;
            dc.FillRectangle(b, r);
            switch (level.FloorAt(c.X, c.Y))
            {
                case FloorLook.Pit: dc.FillRectangle(pit, r.Deflate(1)); break;
                case FloorLook.Plate: dc.FillRectangle(plateBrush, r.Deflate(2)); break;
                case FloorLook.Stairs: DrawText(dc, ">", r.Center.X, r.Y - 2, cell, Gold, centre: true, face: Bold); break;
            }
            if (level.ChestAt(c) is not null) dc.FillRectangle(chest, r.Deflate(2));
            if (level.PileAt(c).Count > 0) dc.DrawEllipse(Gold, null, r.Center, cell * 0.2, cell * 0.2);
            MapMarks(dc, c, r);
        }
        var centre = new Point(ox + (Engine.PartyCell.X + 0.5) * cell, oy + (Engine.PartyCell.Y + 0.5) * cell);
        double a = Engine.Facing.Yaw(), sz = Math.Max(6, cell * 0.9);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(centre + new Point(Math.Cos(a) * sz, Math.Sin(a) * sz), true);
            g.LineTo(centre + new Point(Math.Cos(a + 2.5) * sz, Math.Sin(a + 2.5) * sz));
            g.LineTo(centre + new Point(Math.Cos(a - 2.5) * sz, Math.Sin(a - 2.5) * sz));
            g.EndFigure(true);
        }
        dc.DrawGeometry(Gold, null, geo);
        dc.DrawRectangle(null, EdgePen, area);
    }

    /// <summary>Marks later milestones add to the automap (spotted traps, secrets).</summary>
    partial void MapMarks(DrawingContext dc, Cell c, Rect r);

    private void DrawLog(DrawingContext dc, Rect area)
    {
        var log = Engine.Log;
        double y = area.Bottom;
        for (int i = log.Count - 1; i >= 0 && y > area.Y; i--)
        {
            var ft = Text(log[i], 13, i == log.Count - 1 ? Parchment : Dim, maxWidth: area.Width);
            y -= ft.Height + 5;
            if (y < area.Y) break;
            dc.DrawText(ft, new Point(area.X, y));
        }
    }

    private void DrawBar(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, BarArea);
        dc.DrawLine(EdgePen, BarArea.TopLeft, BarArea.TopRight);
        for (int i = 0; i < 4; i++)
        {
            var card = CardOf(i);
            if (i > 0) dc.DrawLine(new Pen(PanelEdge, 1), card.TopLeft, card.BottomLeft);
            var label = (Slot)i switch { Slot.FrontLeft => "front left", Slot.FrontRight => "front right", Slot.BackLeft => "back left", _ => "back right" };
            if (Engine.Party.At(i) is not { } h)
            {
                DrawText(dc, $"({label}: empty)", card.Center.X, card.Center.Y - 8, 13, Dim, centre: true);
                continue;
            }
            DrawCard(dc, card, h, i, label);
        }
    }

    private void DrawCard(DrawingContext dc, Rect card, Character h, int i, string label)
    {
        bool selected = Engine.Selected == h;
        if (selected) dc.DrawRectangle(null, new Pen(Gold, 2), card.Deflate(2));
        var portrait = new Rect(card.X + 10, card.Y + 12, 72, 92);
        Portraits.Draw(dc, portrait, h, h.IsDown);
        if (h.HurtFlash > 0) dc.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 200, 0, 0)), portrait);
        ClickArea(portrait, () => Engine.Selected = h);

        double x = card.X + 92;
        DrawText(dc, h.Name, x, card.Y + 8, 17, selected ? Gold : Parchment, face: Bold, maxWidth: 220);
        DrawText(dc, $"Lv {h.Level} {h.Calling.Name} · AC {h.ArmourClass} · {label}", x, card.Y + 32, 11, Dim, maxWidth: 222);
        Bar(dc, x, card.Y + 52, 210, h.Hp, h.MaxHp, HpBrush);
        Bar(dc, x, card.Y + 70, 210, h.Stamina, h.MaxStamina, StBrush);
        Bar(dc, x, card.Y + 88, 210, h.Mana, h.MaxMana, MpBrush);
        DrawHand(dc, HandBox(i, HandSide.Left), h.LeftHand, (i * 2 + 1).ToString(CultureInfo.InvariantCulture), LeftIcon(h), Engine.IsBlocking(h));
        DrawHand(dc, HandBox(i, HandSide.Right), h.RightHand, (i * 2 + 2).ToString(CultureInfo.InvariantCulture), RightIcon(h), h.RightHand.Held);
        if (h.RightHand.Held)
        {
            var r = HandBox(i, HandSide.Right);
            double charge = Math.Clamp(h.RightHand.HeldFor / GameEngine.PowerHold, 0, 1);
            dc.FillRectangle(charge >= 1 ? Gold : Dim, new Rect(r.X, r.Bottom + 2, r.Width * charge, 4));
        }
        var notes = new List<string>();
        if (h.IsDead) notes.Add("DEAD");
        else if (h.IsDown) notes.Add("down");
        notes.AddRange(h.Conditions.Active.Select(c => Engine.Content.Condition(c.Id).Name));
        if (h.PendingLevelChoices > 0 || h.AttributePoints > 0) notes.Add("level up! (C)");
        else if (h.PerkPoints > 0) notes.Add("perk to spend (C)");
        if (notes.Count > 0) DrawText(dc, string.Join(" · ", notes), card.X + 10, card.Y + 180, 11, Brushes.Orange, maxWidth: 300);
        double xp = (double)h.CharacterXp / h.XpToNextLevel;
        dc.FillRectangle(Trough, new Rect(card.X + 232, card.Y + 116, 76, 6));
        dc.FillRectangle(XpBrush, new Rect(card.X + 232, card.Y + 116, 76 * xp, 6));
        DrawText(dc, "XP", card.X + 232, card.Y + 124, 10, Dim);
    }

    private static string LeftIcon(Character h) => h.OffHand is not null ? "shield" : h.OffWeapon is not null ? "sword" : h.MainHand is { TwoHanded: true } ? "two" : "fist";
    private static string RightIcon(Character h) => h.MainHand?.Kind == "bow" ? "bow" : h.MainHand is null ? "fist" : h.MainHand.Skill == SkillId.Blunt ? "mace" : "sword";

    private static void Bar(DrawingContext dc, double x, double y, double w, double value, double max, IBrush fill)
    {
        var trough = new Rect(x, y, w, 14);
        dc.FillRectangle(Trough, trough);
        dc.FillRectangle(fill, new Rect(x, y, w * Math.Clamp(value / Math.Max(1, max), 0, 1), 14));
        dc.DrawRectangle(null, new Pen(PanelEdge, 1), trough);
        DrawText(dc, $"{Fmt(Math.Ceiling(value))} / {Fmt(max)}", x + 6, y - 1, 11, Parchment);
    }

    private void DrawHand(DrawingContext dc, Rect box, Hand hand, string key, string icon, bool active)
    {
        dc.FillRectangle(Trough, box);
        Icons.Hand(dc, box, icon);
        if (!hand.Ready)
        {
            double share = hand.RecoveryShare;
            var c = box.Center;
            var pie = new StreamGeometry();
            using (var g = pie.Open())
            {
                double r = box.Width * 0.75;
                double a0 = -Math.PI / 2, a1 = a0 + share * Math.PI * 2;
                g.BeginFigure(c, true);
                g.LineTo(c + new Point(Math.Cos(a0) * r, Math.Sin(a0) * r));
                g.ArcTo(c + new Point(Math.Cos(a1) * r, Math.Sin(a1) * r), new Size(r, r), 0, share > 0.5, SweepDirection.Clockwise);
                g.EndFigure(true);
            }
            using (dc.PushClip(box)) dc.DrawGeometry(Shadow, null, pie);
        }
        dc.DrawRectangle(null, new Pen(active ? Gold : PanelEdge, active ? 3 : 1.5), box);
        DrawText(dc, key, box.X + 4, box.Y + 1, 12, Gold, face: Bold);
    }

    private void DrawOverlays(DrawingContext dc)
    {
        switch (Overlay)
        {
            case Overlay.Inventory: DrawInventory(dc); return;
            case Overlay.Menu: DrawMenu(dc); return;
            case Overlay.Character: DrawCharacter(dc); return;
            case Overlay.Spellbook: DrawSpellbook(dc); return;
            case Overlay.Help: DrawHelp(dc); return;
        }
        if (Engine.Paused) Banner(dc, "Paused", "Press Space to carry on.");
        else if (Engine.Mode == GameMode.Dead)
            Banner(dc, Engine.Party.Count == 1 ? $"{Engine.Hero.Name} has fallen" : "The party has fallen",
                   "The dark keeps what it takes. Press Enter (or click) to go on.");
        else if (Engine.Mode == GameMode.Won)
        {
            var t = TimeSpan.FromSeconds(Engine.Time);
            Banner(dc, WinTitle, string.Create(CultureInfo.InvariantCulture,
                $"{WinLine} {(int)t.TotalMinutes}:{t.Seconds:00}, {Engine.Kills} slain, the party at level {Engine.Party.Members.Max(m => m.Level)}. Press Enter to go on."));
        }
    }

    /// <summary>What the victory banner says (a Delve changes it).</summary>
    public string WinTitle { get; set; } = "The way down";
    public string WinLine { get; set; } = "Through the cellars in";

    private static void Banner(DrawingContext dc, string title, string line)
    {
        dc.FillRectangle(Shadow, ViewArea);
        var box = new Rect(ViewArea.Center.X - 330, ViewArea.Center.Y - 70, 660, 140);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        DrawText(dc, title, box.Center.X, box.Y + 22, 32, Gold, centre: true, face: Bold);
        DrawText(dc, line, box.X + 30, box.Y + 76, 15, Parchment, maxWidth: box.Width - 60);
    }
}
