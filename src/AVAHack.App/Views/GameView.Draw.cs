using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;
using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromRgb(27, 22, 18));
    private static readonly IBrush PanelEdge = new SolidColorBrush(Color.FromRgb(92, 74, 50));
    private static readonly Pen EdgePen = new(PanelEdge, 2);
    private static readonly IBrush Parchment = new SolidColorBrush(Color.FromRgb(226, 212, 182));
    private static readonly IBrush Dim = new SolidColorBrush(Color.FromRgb(150, 136, 112));
    private static readonly IBrush Gold = new SolidColorBrush(Color.FromRgb(222, 176, 74));
    private static readonly IBrush HpBrush = new SolidColorBrush(Color.FromRgb(176, 40, 32));
    private static readonly IBrush StBrush = new SolidColorBrush(Color.FromRgb(70, 150, 60));
    private static readonly IBrush MpBrush = new SolidColorBrush(Color.FromRgb(56, 96, 180));
    private static readonly IBrush XpBrush = new SolidColorBrush(Color.FromRgb(190, 150, 60));
    private static readonly IBrush Trough = new SolidColorBrush(Color.FromRgb(12, 10, 8));
    private static readonly IBrush Shadow = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));

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
            var opacity = dc.PushOpacity(Math.Clamp(1.4 - age, 0, 1));
            DrawText(dc, f.Text, p.X * k, p.Y * k - size, size, brush, centre: true, face: Bold);
            opacity.Dispose();
        }

        // The compass.
        var plate = new Rect(ViewArea.Center.X - 30, 10, 60, 32);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 20, 16, 12)), new Pen(PanelEdge, 2), plate, 6, 6);
        DrawText(dc, Engine.Facing.Letter().ToString(), plate.Center.X, plate.Y + 3, 21, Gold, centre: true, face: Bold);

        if (ShowStats)
            DrawText(dc, string.Create(CultureInfo.InvariantCulture, $"{_fps:F0} fps  ·  view {LastRenderMs:F1} ms  ·  {Engine.PartyCell} {Engine.Facing}"),
                     12, 10, 14, Brushes.LightGreen);
    }

    private void DrawSidePanel(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, SideArea);
        dc.DrawLine(EdgePen, SideArea.TopLeft, SideArea.BottomLeft);
        DrawText(dc, Engine.Level.Name, SideArea.X + 16, 12, 20, Gold, face: Bold);
        var t = TimeSpan.FromSeconds(Engine.Time);
        DrawText(dc, string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalMinutes}:{t.Seconds:00}  ·  {Engine.Kills} slain"), SideArea.X + 16, 40, 14, Dim);
        DrawAutomap(dc, new Rect(SideArea.X + 16, 66, 288, 288));
        DrawLog(dc, new Rect(SideArea.X + 16, 366, 288, 222));
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
        }
        // The party: a gold arrowhead pointing the way it faces.
        var centre = new Point(ox + (Engine.PartyCell.X + 0.5) * cell, oy + (Engine.PartyCell.Y + 0.5) * cell);
        double a = Engine.Facing.Yaw(), sz = Math.Max(6, cell * 0.9);
        var tip = centre + new Point(Math.Cos(a) * sz, Math.Sin(a) * sz);
        var l = centre + new Point(Math.Cos(a + 2.5) * sz, Math.Sin(a + 2.5) * sz);
        var r2 = centre + new Point(Math.Cos(a - 2.5) * sz, Math.Sin(a - 2.5) * sz);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(l);
            g.LineTo(r2);
            g.EndFigure(true);
        }
        dc.DrawGeometry(Gold, null, geo);
        dc.DrawRectangle(null, EdgePen, area);
    }

    private void DrawLog(DrawingContext dc, Rect area)
    {
        var log = Engine.Log;
        double y = area.Bottom;
        for (int i = log.Count - 1; i >= 0 && y > area.Y; i--)
        {
            var brush = i == log.Count - 1 ? Parchment : Dim;
            var ft = Text(log[i], 13, brush, maxWidth: area.Width);
            y -= ft.Height + 6;
            if (y < area.Y) break;
            dc.DrawText(ft, new Point(area.X, y));
        }
    }

    private void DrawBar(DrawingContext dc)
    {
        dc.FillRectangle(PanelBrush, BarArea);
        dc.DrawLine(EdgePen, BarArea.TopLeft, BarArea.TopRight);
        var h = Engine.Hero;

        // Portrait.
        var portrait = new Rect(20, 620, 120, 160);
        DrawPortrait(dc, portrait, h.IsDown);
        if (Engine.HeroHurt > 0) dc.FillRectangle(new SolidColorBrush(Color.FromArgb(110, 200, 0, 0)), portrait);

        double x = 160;
        DrawText(dc, h.Name, x, 616, 22, Gold, face: Bold);
        DrawText(dc, $"Level {h.Level} {h.Ancestry.Name} {h.Calling.Name}  ·  AC {h.ArmourClass}", x, 646, 14, Dim);
        DrawBarLine(dc, x, 674, "HP", h.Hp, h.MaxHp, HpBrush);
        DrawBarLine(dc, x, 700, "ST", h.Stamina, h.MaxStamina, StBrush);
        DrawBarLine(dc, x, 726, "MP", h.Mana, h.MaxMana, MpBrush);
        DrawBarLine(dc, x, 752, "XP", h.CharacterXp, h.XpToNextLevel, XpBrush, small: true);

        var conds = string.Join("  ", h.Conditions.Active.Select(c => Engine.Content.Condition(c.Id).Name));
        if (h.PendingLevelChoices > 0) conds = (conds + "  Level up!").Trim();
        if (conds.Length > 0) DrawText(dc, conds, x, 774, 13, Brushes.Orange);

        DrawHand(dc, LeftHandBox, Engine.LeftHand, "1", h.OffHand is null ? "—" : "shield", Engine.Blocking);
        DrawHand(dc, RightHandBox, Engine.RightHand, "2", "sword", Engine.RightHand.Held);
        DrawText(dc, h.OffHand?.Name ?? "Empty", LeftHandBox.Center.X, LeftHandBox.Bottom + 4, 12, Dim, centre: true);
        DrawText(dc, h.MainHand?.Name ?? "Fist", RightHandBox.Center.X, RightHandBox.Bottom + 4, 12, Dim, centre: true);
        if (Engine.RightHand.Held)
        {
            double charge = Math.Clamp(Engine.RightHand.HeldFor / GameEngine.PowerHold, 0, 1);
            dc.FillRectangle(Trough, new Rect(RightHandBox.X, RightHandBox.Bottom + 22, RightHandBox.Width, 6));
            dc.FillRectangle(charge >= 1 ? Gold : Dim, new Rect(RightHandBox.X, RightHandBox.Bottom + 22, RightHandBox.Width * charge, 6));
        }

        string[] help =
        [
            "W A S D  walk and strafe     Q E  turn",
            "2  swing (hold for a power attack)",
            "1  tap to bash, hold to block",
            "F  use: doors and levers     Space  pause",
            "Right-click the view to swing too",
        ];
        for (int i = 0; i < help.Length; i++) DrawText(dc, help[i], 830, 620 + i * 26, 14, Dim);
    }

    private static void DrawBarLine(DrawingContext dc, double x, double y, string label, double value, double max, IBrush fill, bool small = false)
    {
        double w = 250, hgt = small ? 10 : 18;
        DrawText(dc, label, x, y - 1, 14, Dim, face: Bold);
        var trough = new Rect(x + 34, y + (small ? 4 : 0), w, hgt);
        dc.FillRectangle(Trough, trough);
        dc.FillRectangle(fill, new Rect(trough.X, trough.Y, w * Math.Clamp(value / Math.Max(1, max), 0, 1), hgt));
        dc.DrawRectangle(null, new Pen(PanelEdge, 1), trough);
        if (!small) DrawText(dc, $"{Fmt(Math.Ceiling(value))} / {Fmt(max)}", trough.Right + 10, y - 1, 14, Parchment);
    }

    private static void DrawPortrait(DrawingContext dc, Rect r, bool down)
    {
        // A placeholder face: a stout dwarf with a braided beard, drawn in shapes.
        dc.FillRectangle(new SolidColorBrush(Color.FromRgb(40, 32, 26)), r);
        var skin = new SolidColorBrush(down ? Color.FromRgb(110, 110, 110) : Color.FromRgb(214, 160, 120));
        var beard = new SolidColorBrush(down ? Color.FromRgb(80, 80, 80) : Color.FromRgb(150, 74, 38));
        var helm = new SolidColorBrush(down ? Color.FromRgb(70, 70, 70) : Color.FromRgb(120, 124, 132));
        var c = r.Center;
        dc.DrawEllipse(skin, null, new Point(c.X, c.Y - 18), 34, 38);
        dc.DrawEllipse(helm, null, new Point(c.X, c.Y - 46), 40, 22);
        dc.FillRectangle(helm, new Rect(c.X - 42, c.Y - 46, 84, 10));
        dc.DrawEllipse(beard, null, new Point(c.X, c.Y + 22), 36, 40);
        dc.FillRectangle(skin, new Rect(c.X - 20, c.Y - 2, 40, 8));
        dc.DrawEllipse(Brushes.Black, null, new Point(c.X - 13, c.Y - 18), 4, 4);
        dc.DrawEllipse(Brushes.Black, null, new Point(c.X + 13, c.Y - 18), 4, 4);
        dc.DrawRectangle(null, new Pen(PanelEdge, 2), r);
    }

    private void DrawHand(DrawingContext dc, Rect box, Hand hand, string key, string icon, bool active)
    {
        dc.FillRectangle(Trough, box);
        var c = box.Center;
        switch (icon)
        {
            case "shield":
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(120, 80, 44)), new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 156)), 5), c, 30, 30);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(170, 170, 176)), null, c, 7, 7);
                break;
            case "sword":
            {
                var blade = new StreamGeometry();
                using (var g = blade.Open())
                {
                    g.BeginFigure(new Point(c.X + 30, c.Y - 30), true);
                    g.LineTo(new Point(c.X + 34, c.Y - 34));
                    g.LineTo(new Point(c.X - 14, c.Y + 18));
                    g.LineTo(new Point(c.X - 18, c.Y + 14));
                    g.EndFigure(true);
                }
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(200, 204, 212)), null, blade);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(140, 110, 60)), 5), new Point(c.X - 24, c.Y + 8), new Point(c.X - 8, c.Y + 24));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(90, 60, 34)), 6), new Point(c.X - 16, c.Y + 16), new Point(c.X - 28, c.Y + 28));
                break;
            }
        }
        if (!hand.Ready)
        {
            // The cooldown: a dark slice that shrinks clockwise as the hand recovers.
            double share = hand.RecoveryShare;
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
        dc.DrawRectangle(null, new Pen(active ? Gold : PanelEdge, active ? 3 : 2), box);
        DrawText(dc, key, box.X + 6, box.Y + 2, 14, Gold, face: Bold);
    }

    private void DrawOverlays(DrawingContext dc)
    {
        if (Engine.Paused) Banner(dc, "Paused", "Press Space to carry on.");
        else if (Engine.Mode == GameMode.Dead)
            Banner(dc, $"{Engine.Hero.Name} has fallen", "The dark keeps what it takes. Press Enter (or click) to try again.");
        else if (Engine.Mode == GameMode.Won)
        {
            var t = TimeSpan.FromSeconds(Engine.Time);
            Banner(dc, "The way down",
                string.Create(CultureInfo.InvariantCulture, $"Through the cellars in {(int)t.TotalMinutes}:{t.Seconds:00}, with {Engine.Kills} slain, at level {Engine.Hero.Level}. Press Enter to play again."));
        }
    }

    private static void Banner(DrawingContext dc, string title, string line)
    {
        dc.FillRectangle(Shadow, ViewArea);
        var box = new Rect(ViewArea.Center.X - 330, ViewArea.Center.Y - 70, 660, 140);
        dc.DrawRectangle(PanelBrush, new Pen(PanelEdge, 3), box, 8, 8);
        DrawText(dc, title, box.Center.X, box.Y + 22, 32, Gold, centre: true, face: Bold);
        DrawText(dc, line, box.X + 30, box.Y + 76, 15, Parchment, maxWidth: box.Width - 60);
    }
}
