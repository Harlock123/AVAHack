using Avalonia;
using Avalonia.Media;
using AVAHack.Characters;

namespace AVAHack.App.Views;

/// <summary>Placeholder portraits drawn in shapes, coloured by ancestry and calling.</summary>
public static class Portraits
{
    public static void Draw(DrawingContext dc, Rect r, Character h, bool down)
    {
        dc.FillRectangle(new SolidColorBrush(Color.FromRgb(40, 32, 26)), r);
        Color skin = h.Ancestry.Id switch
        {
            "dwarf" => Color.FromRgb(214, 160, 120), "elf" => Color.FromRgb(232, 208, 180), "halfling" => Color.FromRgb(226, 176, 136),
            "gnome" => Color.FromRgb(220, 170, 140), "orcBlooded" => Color.FromRgb(130, 156, 96), "saurian" => Color.FromRgb(96, 140, 110),
            "emberkin" => Color.FromRgb(196, 96, 70), _ => Color.FromRgb(210, 166, 130),
        };
        Color hair = h.Calling.Id switch
        {
            "wizard" or "druid" => Color.FromRgb(200, 200, 210), "rogue" or "ranger" => Color.FromRgb(50, 40, 30),
            "cleric" or "paladin" => Color.FromRgb(200, 170, 80), _ => Color.FromRgb(150, 74, 38),
        };
        if (down) { skin = Color.FromRgb(110, 110, 110); hair = Color.FromRgb(80, 80, 80); }
        var c = r.Center;
        double s = r.Width / 120;
        dc.DrawEllipse(new SolidColorBrush(hair), null, new Point(c.X, c.Y - 30 * s), 40 * s, 30 * s);
        dc.DrawEllipse(new SolidColorBrush(skin), null, new Point(c.X, c.Y - 14 * s), 32 * s, 38 * s);
        if (h.Ancestry.Id is "elf" or "gnome" or "orcBlooded")
        {
            dc.DrawEllipse(new SolidColorBrush(skin), null, new Point(c.X - 34 * s, c.Y - 18 * s), 8 * s, 14 * s);
            dc.DrawEllipse(new SolidColorBrush(skin), null, new Point(c.X + 34 * s, c.Y - 18 * s), 8 * s, 14 * s);
        }
        if (h.Ancestry.Id == "dwarf") dc.DrawEllipse(new SolidColorBrush(hair), null, new Point(c.X, c.Y + 22 * s), 34 * s, 36 * s);
        dc.DrawEllipse(Brushes.Black, null, new Point(c.X - 12 * s, c.Y - 18 * s), 4 * s, 4 * s);
        dc.DrawEllipse(Brushes.Black, null, new Point(c.X + 12 * s, c.Y - 18 * s), 4 * s, 4 * s);
        dc.FillRectangle(new SolidColorBrush(Color.FromRgb(90, 40, 30)), new Rect(c.X - 10 * s, c.Y + 6 * s, 20 * s, 4 * s));
        var cloth = h.Calling.Id switch
        {
            "wizard" => Color.FromRgb(50, 60, 130), "cleric" => Color.FromRgb(200, 200, 190), "rogue" => Color.FromRgb(50, 50, 50),
            "ranger" or "druid" => Color.FromRgb(60, 100, 50), "paladin" => Color.FromRgb(170, 170, 180), _ => Color.FromRgb(120, 124, 132),
        };
        if (down) cloth = Color.FromRgb(70, 70, 70);
        dc.FillRectangle(new SolidColorBrush(cloth), new Rect(r.X, r.Bottom - 22 * s, r.Width, 22 * s));
        dc.DrawRectangle(null, new Pen(GameView.PanelEdge, 2), r);
    }
}

/// <summary>Small icons drawn in shapes: what a hand holds.</summary>
public static class Icons
{
    public static void Hand(DrawingContext dc, Rect box, string icon)
    {
        var c = box.Center;
        double k = box.Width / 96;
        switch (icon)
        {
            case "shield":
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(120, 80, 44)), new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 156)), 4 * k), c, 30 * k, 30 * k);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(170, 170, 176)), null, c, 7 * k, 7 * k);
                break;
            case "sword":
            case "mace":
            {
                var metal = new SolidColorBrush(Color.FromRgb(200, 204, 212));
                if (icon == "mace")
                {
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(90, 60, 34)), 6 * k), new Point(c.X - 24 * k, c.Y + 24 * k), new Point(c.X + 10 * k, c.Y - 10 * k));
                    dc.DrawEllipse(metal, null, new Point(c.X + 16 * k, c.Y - 16 * k), 13 * k, 13 * k);
                    break;
                }
                var blade = new StreamGeometry();
                using (var g = blade.Open())
                {
                    g.BeginFigure(new Point(c.X + 30 * k, c.Y - 30 * k), true);
                    g.LineTo(new Point(c.X + 34 * k, c.Y - 34 * k));
                    g.LineTo(new Point(c.X - 14 * k, c.Y + 18 * k));
                    g.LineTo(new Point(c.X - 18 * k, c.Y + 14 * k));
                    g.EndFigure(true);
                }
                dc.DrawGeometry(metal, null, blade);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(140, 110, 60)), 5 * k), new Point(c.X - 24 * k, c.Y + 8 * k), new Point(c.X - 8 * k, c.Y + 24 * k));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(90, 60, 34)), 6 * k), new Point(c.X - 16 * k, c.Y + 16 * k), new Point(c.X - 28 * k, c.Y + 28 * k));
                break;
            }
            case "bow":
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(150, 110, 60)), 5 * k), new Point(c.X - 10 * k, c.Y - 32 * k), new Point(c.X - 10 * k, c.Y + 32 * k));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(220, 220, 220)), 1.5 * k), new Point(c.X + 14 * k, c.Y - 30 * k), new Point(c.X + 14 * k, c.Y + 30 * k));
                break;
            case "two":
                dc.DrawLine(new Pen(GameView.Dim, 2), box.TopLeft + new Point(14, 14), box.BottomRight - new Point(14, 14));
                break;
            default:
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(210, 166, 130)), null, c, 16 * k, 18 * k);
                break;
        }
    }
}
