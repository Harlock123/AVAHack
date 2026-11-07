namespace AVAHack.Render;

/// <summary>
/// The Glass Roads' creatures (spec 39): the Shardborn, made of facets of black glass that catch a violet light, and
/// the Lens. The Hollowed are people with glass eyes, drawn by <see cref="Humanoid"/>; the rest reuse the bestiary's
/// shapes in <see cref="DrawMore"/>.
/// </summary>
public sealed partial class SpriteFactory
{
    private static readonly uint GlassDark = C(26, 22, 36), GlassMid = C(58, 52, 82), GlassLight = C(150, 136, 200);

    /// <summary>One facet: a kite of glass, dark on one side and lit on the other.</summary>
    private static void Facet(Paint p, double x, double y, double w, double h, double tilt = 0)
    {
        p.Mat = Material.Gloss;
        p.Poly(GlassMid, (x + tilt, y - h), (x + w, y), (x - tilt * 0.5, y + h), (x - w, y));
        p.Poly(GlassDark, false, (x + tilt, y - h), (x, y), (x - tilt * 0.5, y + h), (x - w, y));
        p.Line(x + tilt, y - h, x + w, y, 0.4, GlassLight);
        p.Mat = Material.Matte;
    }

    /// <summary>A shardling: a knot of splinters scuttling on six spiked legs.</summary>
    private static void Shardling(Paint p, SpriteView v, SpriteFrame f, double s)
    {
        if (f == SpriteFrame.Dead)
        {
            for (int i = 0; i < 7; i++) Facet(p, 14 + i * 6, 60 - i % 2 * 2, 2.6, 1.6, i % 3 - 1);
            return;
        }
        double lift = f == SpriteFrame.Windup ? -3 : f == SpriteFrame.Strike ? 3 : 0, cy = 50 - 10 * s + lift;
        for (int i = 0; i < 6; i++)
        {
            double side = i < 3 ? -1 : 1, k = i % 3;
            p.Limb(32 + side * 4, cy + 2, 32 + side * (12 + k * 3) * s, cy - 6 + k * 2, 32 + side * (16 + k * 4) * s, 62, 1.0, 0.4, GlassDark);
        }
        for (int i = 0; i < 6; i++)
            Facet(p, 32 + (i - 2.5) * 3.4 * s, cy - (i % 2) * 5 * s, 4 * s, (7 + i % 3 * 2) * s, i % 3 - 1);
        if (v != SpriteView.Back)
        {
            p.Ellipse(28, cy - 2, 1.3, 1.3, C(210, 170, 255), false);
            p.Ellipse(36, cy - 2, 1.3, 1.3, C(210, 170, 255), false);
        }
    }

    /// <summary>A floating prism: two pyramids of glass point to point, with a light at the heart.</summary>
    private static void Prism(Paint p, SpriteFrame f, double s, uint glow, double hover = 30, bool shards = false)
    {
        if (f == SpriteFrame.Dead)
        {
            for (int i = 0; i < 6; i++) Facet(p, 16 + i * 6.5, 60 - i % 2 * 2, 3, 1.8, i % 3 - 1);
            return;
        }
        double bob = f == SpriteFrame.Windup ? -3 : f == SpriteFrame.Strike ? 2 : 0, cy = hover + bob, w = 12 * s, h = 18 * s;
        p.Ellipse(32, 62, 8 * s, 1.6, C(14, 12, 20), false);                                       // its shadow
        p.Mat = Material.Gloss;
        p.Poly(GlassMid, (32, cy - h), (32 + w, cy), (32, cy + h), (32 - w, cy));
        p.Poly(GlassDark, false, (32, cy - h), (32, cy + h), (32 - w, cy));
        p.Mat = Material.Matte;
        p.Line(32, cy - h, 32 + w, cy, 0.6, GlassLight);
        p.Line(32 - w, cy, 32 + w, cy, 0.5, C(90, 80, 120));
        double r = (f == SpriteFrame.Windup ? 5 : 3.5) * s;
        p.Ellipse(32, cy, r, r, glow, false);
        p.Ellipse(32, cy, r * 0.45, r * 0.45, C(255, 250, 255), false);
        if (shards)
            for (int i = 0; i < 5; i++)
            {
                double a = i * Math.PI * 2 / 5 + (f == SpriteFrame.Strike ? 0.4 : 0);
                Facet(p, 32 + Math.Cos(a) * w * 1.7, cy + Math.Sin(a) * h * 0.9, 2.4 * s, 4.5 * s, 1);
            }
    }

    /// <summary>
    /// The Lens: a vast disc of black glass edge-on to the hall, a cracked rim of facets, and a white-violet light
    /// gathering in its centre.
    /// </summary>
    private static void Lens(Paint p, SpriteView v, SpriteFrame f)
    {
        if (f == SpriteFrame.Dead)
        {
            for (int i = 0; i < 9; i++) Facet(p, 8 + i * 6, 59 - i % 3 * 2, 3.4, 2.2, i % 3 - 1);
            return;
        }
        double cy = 30 + (f == SpriteFrame.Windup ? -2 : 0), rx = v == SpriteView.Side ? 8 : 26, ry = 26;
        p.Ellipse(32, 62, 20, 2.4, C(14, 12, 20), false);
        p.Mat = Material.Gloss;
        p.Ellipse(32, cy, rx, ry, GlassDark);
        p.Ellipse(32 - rx * 0.15, cy - ry * 0.15, rx * 0.75, ry * 0.75, GlassMid);
        p.Mat = Material.Matte;
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            Facet(p, 32 + Math.Cos(a) * rx, cy + Math.Sin(a) * ry, 3, 4.5, i % 3 - 1);
        }
        p.Line(32 - rx * 0.5, cy - ry * 0.7, 32 + rx * 0.2, cy + ry * 0.4, 0.6, GlassLight);     // a crack across it
        if (v == SpriteView.Back) return;
        double glow = f == SpriteFrame.Windup ? 11 : f == SpriteFrame.Strike ? 14 : 7;
        p.Ellipse(32, cy, glow * (rx / 26), glow, C(170, 120, 255), false);
        p.Ellipse(32, cy, glow * 0.55 * (rx / 26), glow * 0.55, C(235, 220, 255), false);
        p.Ellipse(32, cy, glow * 0.22 * (rx / 26), glow * 0.22, C(255, 255, 255), false);
    }
}
