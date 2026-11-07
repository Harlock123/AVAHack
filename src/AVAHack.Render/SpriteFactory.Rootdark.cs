namespace AVAHack.Render;

/// <summary>
/// The Rootdark's creatures (spec 38): burrowing worms rising out of the floor, the spore-thralls' cap growths, and
/// the Mycel Crown with its nodes. The rest of the region's creatures reuse the bestiary's shapes in
/// <see cref="DrawMore"/>.
/// </summary>
public sealed partial class SpriteFactory
{
    /// <summary>
    /// A burrower reared up out of its hole: a ringed body curving up from a heap of turned earth to a round maw
    /// of teeth. <paramref name="s"/> sizes it; <paramref name="plates"/> gives it a back of chitin plates.
    /// </summary>
    private static void Worm(Paint p, SpriteView v, SpriteFrame f, uint body, uint maw, double s, bool plates, int teeth = 10)
    {
        uint earth = C(84, 64, 44), dark = Paint.Shade(body, -30);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 60, 22 * s, 4, earth);
            for (int i = 0; i < 6; i++) p.Ellipse(14 + i * 7 * s, 58 - i % 2, 4.5 * s, 3.4 * s, i % 2 == 0 ? body : dark);
            return;
        }
        double rise = f == SpriteFrame.Windup ? -4 : f == SpriteFrame.Strike ? 6 : 0;
        double lean = v == SpriteView.Side ? 8 : 0, lunge = f == SpriteFrame.Strike ? 5 : 0;
        // The heap of earth it came up through.
        p.Ellipse(32, 60, 20 * s, 4.5, earth);
        p.Ellipse(24, 57, 6 * s, 3, Paint.Shade(earth, 12));
        p.Ellipse(41, 58, 5 * s, 2.6, Paint.Shade(earth, -10));
        // Rings from the ground up, curving forward at the top.
        int rings = 8;
        double headX = 32 + lean + lunge, headY = 63 - 50 * s - rise;
        for (int i = 0; i < rings; i++)
        {
            double t = i / (double)(rings - 1);
            double x = 32 + (headX - 32) * t * t + Math.Sin(t * 3.1) * 4 * s;
            double y = 58 + (headY - 58) * t;
            double r = (9.5 - t * 2.5) * s;
            p.Mat = plates ? Material.Gloss : Material.Matte;
            p.Ellipse(x, y, r, r * 0.8, i % 2 == 0 ? body : Paint.Shade(body, -12));
            if (plates) p.Ellipse(x - r * 0.2, y - r * 0.35, r * 0.7, r * 0.35, Paint.Shade(body, 22));
            p.Mat = Material.Matte;
            p.Line(x - r * 0.9, y + r * 0.4, x + r * 0.9, y + r * 0.4, 0.6, dark);
        }
        // The maw: a ring of hooked teeth round a dark throat.
        double mr = 8.5 * s;
        if (v == SpriteView.Back)
        {
            p.Ellipse(headX, headY, mr, mr * 0.9, dark);
            return;
        }
        p.Ellipse(headX, headY, mr, mr * (v == SpriteView.Side ? 0.6 : 0.95), Paint.Shade(body, 10));
        p.Ellipse(headX, headY, mr * 0.7, mr * (v == SpriteView.Side ? 0.4 : 0.66), maw, false);
        p.Ellipse(headX, headY, mr * 0.35, mr * 0.3, C(20, 10, 12), false);
        p.Mat = Material.Bone;
        for (int i = 0; i < teeth; i++)
        {
            double a = i * Math.PI * 2 / teeth;
            double ox = Math.Cos(a) * mr * 0.72, oy = Math.Sin(a) * mr * 0.68 * (v == SpriteView.Side ? 0.6 : 1);
            p.Taper(headX + ox, headY + oy, 1.0 * s, headX + ox * 0.45, headY + oy * 0.45, 0.2, C(232, 222, 196));
        }
        p.Mat = Material.Matte;
    }

    /// <summary>The fungus growing out of a spore-thrall: a cluster of caps through the skull, threads down the face.</summary>
    private static void Capped(Paint p, Look k, uint cap, bool big)
    {
        double s = k.Height, top = 63 - 58 * s, headY = top + 6.5 * s;
        uint gill = Paint.Shade(cap, 40);
        for (int i = 0; i < 4; i++) p.Line(29 + i * 2, headY - 2, 28 + i * 2.6, headY + 7 * s, 0.4, C(226, 220, 196)); // threads
        p.Mat = Material.Matte;
        p.Taper(31, headY - 3 * s, 1.4, 30, headY - 8 * s, 1.2, C(220, 210, 180));
        p.Ellipse(30, headY - 9 * s, (big ? 9 : 6.5) * s, (big ? 3.4 : 2.6) * s, cap);
        p.Ellipse(30, headY - 8 * s, (big ? 7.5 : 5.5) * s, 0.8 * s, gill, false);
        p.Taper(36, headY - 2 * s, 0.9, 38, headY - 6 * s, 0.8, C(220, 210, 180));
        p.Ellipse(38.5, headY - 6.5 * s, 3.2 * s, 1.6 * s, Paint.Shade(cap, -14));
        foreach (var (x, y) in new[] { (27.0, -11.0), (33.0, -10.5), (30.0, -12.0) }) p.Dot(x, headY + y * s, Paint.Shade(cap, 50));
        if (big)
            for (int i = 0; i < 3; i++)
                p.Ellipse(24 + i * 8, top + 22 * s + i % 2 * 6, 2.2 * s, 1.2 * s, cap);                  // caps through the armour
    }

    /// <summary>A node of the Mycel Crown: a pulsing knot of root and fungus, rooted to the floor.</summary>
    private static void Node(Paint p, SpriteFrame f)
    {
        uint flesh = C(150, 120, 96), glow = C(240, 210, 110), root = C(90, 66, 44);
        for (int i = 0; i < 6; i++)
            p.Limb(32, 54, 32 + (i - 2.5) * 6, 60, 32 + (i - 2.5) * 11, 63, 2.4, 1.2, root);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 58, 14, 5, Paint.Shade(flesh, -40));
            for (int i = 0; i < 5; i++) p.Line(22 + i * 5, 58, 24 + i * 4, 52, 0.7, C(220, 214, 196)); // threads, still alive
            return;
        }
        double swell = f == SpriteFrame.Windup ? 3 : f == SpriteFrame.Strike ? -2 : 0;
        p.Mat = Material.Gloss;
        p.Ellipse(32, 42, 15 + swell, 15 + swell, flesh);
        p.Mat = Material.Matte;
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3;
            p.Line(32, 42, 32 + Math.Cos(a) * 14, 42 + Math.Sin(a) * 14, 0.8, Paint.Shade(flesh, -30));
        }
        p.Ellipse(32, 42, 5 + swell / 2, 5 + swell / 2, glow, false);
        p.Ellipse(32, 42, 2.2, 2.2, C(255, 250, 220), false);
        for (int i = 0; i < 3; i++) p.Ellipse(22 + i * 10, 28 - i % 2 * 3, 3.5, 1.6, C(200, 170, 90));
    }

    /// <summary>
    /// The Mycel Crown: a vast fungal body risen out of a mat of roots, a ring of caps round a hollow face, root
    /// arms reaching out, and the glow of spores in its gills.
    /// </summary>
    private static void Crown(Paint p, SpriteView v, SpriteFrame f)
    {
        uint body = C(176, 158, 120), cap = C(150, 96, 60), gill = C(236, 210, 140), root = C(84, 62, 42);
        p.Ellipse(32, 60, 30, 5, root);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 56, 24, 7, Paint.Shade(body, -40));
            for (int i = 0; i < 5; i++) p.Ellipse(12 + i * 10, 54 - i % 2 * 2, 5, 2, Paint.Shade(cap, -40));
            return;
        }
        double reach = f == SpriteFrame.Windup ? -6 : f == SpriteFrame.Strike ? 8 : 0;
        // Root arms.
        p.Limb(18, 40, 8 - reach / 2, 30, 4 - reach, 18 + reach, 3.4, 1.4, root);
        p.Limb(46, 40, 56 + reach / 2, 30, 60 + reach, 18 + reach, 3.4, 1.4, root);
        for (int i = 0; i < 7; i++) p.Limb(14 + i * 6, 62, 18 + i * 5, 54, 22 + i * 3.5, 44, 2.4, 1.8, i % 2 == 0 ? root : Paint.Shade(root, 14));
        // The trunk of the body.
        p.Poly(body, (16, 60), (19, 34), (24, 20), (40, 20), (45, 34), (48, 60));
        // The ring of caps: its crown.
        for (int i = 0; i < 7; i++)
        {
            double x = 14 + i * 6, y = 14 - Math.Abs(i - 3) * -1.5 - (i % 2) * 3;
            p.Mat = Material.Gloss;
            p.Ellipse(x, y, 6, 3, i % 2 == 0 ? cap : Paint.Shade(cap, 16));
            p.Mat = Material.Matte;
            p.Ellipse(x, y + 1.6, 5, 0.9, gill, false);
            p.Taper(x, y + 2, 1, 32 + (x - 32) * 0.4, 24, 1.6, Paint.Shade(body, 10));
        }
        if (v != SpriteView.Back)
        {
            // A hollow face of gills, lit from within.
            p.Ellipse(32, 32, 7, 9, C(60, 40, 30));
            for (int i = 0; i < 7; i++) p.Line(32, 24 + i * 0.4, 26 + i * 2, 40, 0.6, gill);
            p.Ellipse(29, 29, 1.6, 1.6, C(255, 240, 160), false);
            p.Ellipse(35, 29, 1.6, 1.6, C(255, 240, 160), false);
        }
        for (int i = 0; i < 6; i++) p.Dot(20 + i * 5, 46 + i % 3 * 3, C(240, 220, 140));
    }
}
