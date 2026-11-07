using Avalonia;
using Avalonia.Media;
using AVAHack.Game;
using AVAHack.Render;
using AVAHack.World;

namespace AVAHack.App.Views;

public sealed partial class GameView
{
    /// <summary>Over the view: the eyes of creatures that are starting to notice a sneaking party.</summary>
    partial void ViewExtras(DrawingContext dc)
    {
        double k = ViewArea.Width / Renderer.Width;
        if (Engine.Sneaking)
        {
            DrawText(dc, "sneaking", 16, 560, 15, Brushes.LightSteelBlue, face: Bold);
            foreach (var m in Engine.Monsters)
            {
                if (!m.IsAlive || m.IsAware || m.Awareness <= 0) continue;
                var (x, y) = m.Position();
                if (Renderer.Project(x, y, Scene.SizeOf(m.Def.Sprite) + 0.12) is not { } p) continue;
                var c = new Point(p.X * k, p.Y * k);
                double w = Math.Clamp(24 / p.Depth, 8, 26);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)), new Pen(Brushes.White, 1.5), c, w, w * 0.55);
                using (dc.PushClip(new Rect(c.X - w, c.Y - w, 2 * w * m.Awareness, 2 * w)))
                    dc.DrawEllipse(Brushes.Gold, null, c, w * 0.6, w * 0.4);
            }
        }
        foreach (var m in Engine.Monsters)
        {
            if (!m.IsAlive || m.Title is null) continue;
            var (x, y) = m.Position();
            if (Renderer.Project(x, y, Scene.SizeOf(m.Def.Sprite) + 0.25) is { } p && p.Depth < 6)
                DrawText(dc, m.Title, p.X * k, p.Y * k, Math.Clamp(18 / p.Depth, 10, 16), Brushes.Orange, centre: true, face: Bold);
        }
    }
}
