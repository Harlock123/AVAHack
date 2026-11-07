namespace AVAHack.Render;

/// <summary>
/// The creatures that aren't people (spec 12): rats, spiders and bugs, beasts on four legs, oozes, floating things,
/// fungi, machines, serpents, tentacles and the mimic. Each draws on the 64-unit grid with its feet on row 63, and
/// <see cref="Paint.Finish"/> shades it. Materials mark what should shine (shells, metal, slime) or show grain (fur,
/// stone); flat shapes are eyes, mouths and glows.
/// </summary>
public sealed partial class SpriteFactory
{
    private static readonly uint Socket = 0xFF1A1014, Maw = 0xFF3A1018, Tooth = 0xFFEEE8D8;

    // ---- Rats ----

    private static void Rat(Paint p, SpriteView v, SpriteFrame f)
    {
        uint fur = C(112, 94, 80), belly = C(150, 130, 112), pink = C(214, 144, 144), eye = C(255, 60, 40);
        p.Mat = Material.Fur;
        if (f == SpriteFrame.Dead)
        {
            p.Mat = Material.Matte;
            p.Limb(12, 59, 6, 54, 1, 56, 1.4, 0.6, pink);
            p.Mat = Material.Fur;
            p.Ellipse(30, 57, 17, 6.5, fur);
            p.Ellipse(30, 55, 10, 3.5, belly);
            p.Mat = Material.Matte;
            for (int i = 0; i < 4; i++) p.Taper(22 + i * 6, 52, 1.1, 20 + i * 6, 45 - i % 2 * 2, 0.8, pink);
            p.Mat = Material.Fur;
            p.Ellipse(49, 57, 7, 5, fur);
            p.Mat = Material.Matte;
            p.Taper(52, 58, 1.8, 58, 59, 0.8, fur);
            p.Ellipse(46, 53, 2.6, 2.6, pink);
            p.Taper(48, 56, 0.4, 50, 57, 0.4, Socket, false);
            return;
        }
        double rear = f == SpriteFrame.Windup ? -5 : 0, lunge = f == SpriteFrame.Strike ? 3 : 0;
        bool open = f != SpriteFrame.Idle;
        switch (v)
        {
            case SpriteView.Front:
                p.Mat = Material.Matte;
                p.Limb(46, 56, 56, 50, 60, 40, 1.4, 0.6, pink);                    // the tail, curling round
                p.Mat = Material.Fur;
                p.Ellipse(32, 51, 18, 12.5, fur);                                  // the body, hunched
                p.Ellipse(32, 54, 10, 7, belly);
                p.Ellipse(19, 37 + rear, 5.5, 5.5, fur);                           // ears
                p.Ellipse(45, 37 + rear, 5.5, 5.5, fur);
                p.Mat = Material.Matte;
                p.Ellipse(19, 37 + rear, 3.2, 3.2, pink, false);
                p.Ellipse(45, 37 + rear, 3.2, 3.2, pink, false);
                p.Mat = Material.Fur;
                p.Ellipse(32, 43 + rear, 10.5 + lunge, 9.5 + lunge, fur);         // the head
                p.Ellipse(32, 48 + rear, 5.5 + lunge / 2, 4.5, belly);           // the muzzle
                p.Mat = Material.Matte;
                p.Ellipse(32, 46.5 + rear, 2, 1.5, pink);                          // the nose
                p.Ellipse(27.5, 41 + rear, 1.6, 1.6, Socket, false); p.Dot(27.5, 41 + rear, eye);
                p.Ellipse(36.5, 41 + rear, 1.6, 1.6, Socket, false); p.Dot(36.5, 41 + rear, eye);
                if (open) { p.Ellipse(32, 51 + rear, 2.6, 2, Maw, false); p.Rect(31, 49.5 + rear, 33, 52 + rear, Tooth, false); }
                for (int i = -1; i <= 1; i += 2)
                {
                    p.Line(32 + i * 4, 47 + rear, 32 + i * 14, 45 + rear, 0.4, C(60, 50, 44));
                    p.Line(32 + i * 4, 48 + rear, 32 + i * 13, 49 + rear, 0.4, C(60, 50, 44));
                }
                p.Ellipse(24, 61, 3.5, 2, pink); p.Ellipse(40, 61, 3.5, 2, pink);   // the front paws
                break;
            case SpriteView.Side:
                p.Mat = Material.Matte;
                p.Limb(14, 54, 6, 50, 1, 38, 1.6, 0.6, pink);                     // the tail
                p.Limb(17, 52, 14, 58, 17, 62, 2.6, 1.4, fur);                    // a hind leg
                p.Mat = Material.Fur;
                p.Ellipse(29, 50, 17, 10.5, fur);
                p.Ellipse(30, 55, 11, 5, belly);
                p.Ellipse(47 + lunge, 46 + rear, 9.5, 7.5, fur);                  // the head
                p.Mat = Material.Matte;
                p.Taper(51 + lunge, 46 + rear, 4.5, 58 + lunge, 48 + rear, 1.6, fur); // the snout
                p.Ellipse(58.5 + lunge, 48 + rear, 1.5, 1.4, pink);
                p.Ellipse(43 + lunge, 39 + rear, 4, 4.5, fur);
                p.Ellipse(43 + lunge, 39 + rear, 2.4, 2.8, pink, false);
                p.Ellipse(50 + lunge, 44 + rear, 1.5, 1.4, Socket, false); p.Dot(50 + lunge, 44 + rear, eye);
                if (open) p.Taper(52 + lunge, 51 + rear, 0.6, 57 + lunge, 51 + rear, 0.6, Maw, false);
                p.Line(55 + lunge, 47 + rear, 61 + lunge, 44 + rear, 0.4, C(60, 50, 44));
                p.Limb(36, 54, 40, 58, 41 + lunge, 62, 2.2, 1.2, fur);            // a foreleg
                p.Ellipse(41 + lunge, 62, 2.4, 1.3, pink);
                p.Ellipse(19, 62, 2.4, 1.3, pink);
                break;
            default:
                p.Mat = Material.Fur;
                p.Ellipse(32, 51, 18, 12.5, fur);
                p.Ellipse(20, 40, 5.5, 5.5, fur); p.Ellipse(44, 40, 5.5, 5.5, fur);
                p.Ellipse(32, 42, 9, 7, fur);
                p.Mat = Material.Matte;
                p.Limb(32, 60, 34, 63, 38, 63, 1.6, 0.8, pink);
                break;
        }
        p.Mat = Material.Matte;
    }

    // ---- Spiders and bugs ----

    private static void Spider(Paint p, SpriteView v, SpriteFrame f) =>
        Bug(p, v, f, C(64, 50, 42), C(48, 38, 32), 1.0, 8, C(170, 70, 44), hairy: true);

    /// <summary>
    /// Anything with a shell and jointed legs. Spiders (eight legs) carry a big abdomen behind a small head with a
    /// cluster of eyes and fangs; beetles and crawlers (six) a domed, split shell.
    /// </summary>
    private static void Bug(Paint p, SpriteView v, SpriteFrame f, uint shell, uint leg, double s, int legs, uint? mark = null, bool hairy = false)
    {
        bool spider = legs == 8;
        uint eye = C(255, 70, 40);
        var shellMat = hairy ? Material.Fur : Material.Gloss;
        if (f == SpriteFrame.Dead)
        {
            // On its back, legs curled up.
            p.Mat = shellMat;
            p.Ellipse(32, 57, 16 * s, 6.5, shell);
            p.Mat = Material.Matte;
            for (int i = 0; i < legs / 2; i++)
            {
                double bx = 22 + i * (20.0 / (legs / 2 - 1)) * s;
                p.Limb(bx, 53, bx - 3 + i, 44 - i % 2 * 3, bx + 1, 41, 1.3, 0.7, leg);
            }
            return;
        }
        double raise = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? -3 : 0;
        int pairs = legs / 2;
        if (v == SpriteView.Side)
        {
            // Legs on the far side first, then the body, then the near legs.
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    p.Mat = shellMat;
                    if (spider)
                    {
                        p.Ellipse(20, 42, 16 * s, 12.5 * s, shell);                              // the abdomen
                        if (mark is { } m) { p.Mat = Material.Matte; p.Ellipse(18, 39, 6, 4, m); p.Mat = shellMat; }
                        p.Ellipse(40, 48, 9 * s, 7 * s, shell);                                 // the head
                    }
                    else
                    {
                        p.Ellipse(30, 47, 19 * s, 11 * s, shell);
                        p.Mat = Material.Matte;
                        p.Taper(14, 47, 0.6, 46, 47, 0.6, Paint.Shade(shell, -40), false);   // the seam of the wing cases
                        p.Mat = shellMat;
                        p.Ellipse(49, 50, 7 * s, 6 * s, Paint.Shade(shell, -16));
                    }
                    p.Mat = Material.Matte;
                    double hx = spider ? 47 : 54;
                    p.Ellipse(hx - 1, 46, 1.4, 1.4, eye, false);
                    if (spider) p.Ellipse(hx - 3, 44.5, 1, 1, eye, false);
                    p.Taper(hx, 51, 1.4, hx + 4, 56 + raise / 3, 0.5, Paint.Shade(leg, -10)); // fang or mandible
                    continue;
                }
                for (int i = 0; i < pairs; i++)
                {
                    double bx = 26 + i * 5 * s, knee = 32 - i % 2 * 3 + (i == pairs - 1 ? raise : 0);
                    p.Limb(bx, 48, bx + 6 + i * 2, knee, bx + 9 + i * 3, 63, 1.7, 0.8, leg);
                }
            }
            return;
        }
        // Front and back: legs fanned out to either side.
        for (int i = 0; i < pairs; i++)
        {
            double knee = (spider ? 28 : 36) - i * 3 + (i == 0 ? raise : 0);
            double reach = (spider ? 20 : 15) * s + i * 2;
            p.Mat = hairy ? Material.Fur : Material.Matte;
            p.Limb(28, 49, 32 - reach, knee, 32 - reach + 6 + i * 3, 63, 1.9, 0.8, leg);
            p.Limb(36, 49, 32 + reach, knee, 32 + reach - 6 - i * 3, 63, 1.9, 0.8, leg);
        }
        p.Mat = shellMat;
        if (spider)
        {
            p.Ellipse(32, 38, 15 * s, 12 * s, shell);                                       // the abdomen, behind
            if (v == SpriteView.Back) { if (mark is { } mk) { p.Mat = Material.Matte; p.Ellipse(32, 36, 6, 6.5, mk); p.Ellipse(32, 36, 2.5, 3, Paint.Shade(mk, 40)); } p.Mat = Material.Matte; return; }
            p.Ellipse(32, 50, 10 * s, 8 * s, Paint.Shade(shell, 8));                       // the head
            p.Mat = Material.Matte;
            foreach (var (x, y, r) in new[] { (28.5, 47.0, 1.5), (35.5, 47.0, 1.5), (30.5, 45.0, 1.0), (33.5, 45.0, 1.0), (26.5, 49.0, 1.0), (37.5, 49.0, 1.0) })
                p.Ellipse(x, y, r, r, eye, false);
            p.Taper(29, 54, 1.8, 27.5, 60 + raise / 4, 0.6, Paint.Shade(leg, -14));      // the fangs
            p.Taper(35, 54, 1.8, 36.5, 60 + raise / 4, 0.6, Paint.Shade(leg, -14));
            return;
        }
        p.Ellipse(32, 46, 18 * s, 12 * s, shell);
        p.Mat = Material.Matte;
        p.Taper(32, 46 - 12 * s, 0.5, 32, 46 + 11 * s, 0.5, Paint.Shade(shell, -46), false);
        if (v == SpriteView.Back) return;
        p.Mat = shellMat;
        p.Ellipse(32, 56, 8.5 * s, 6, Paint.Shade(shell, -18));                             // the head
        p.Mat = Material.Matte;
        p.Ellipse(28.5, 54.5, 1.4, 1.4, eye, false); p.Ellipse(35.5, 54.5, 1.4, 1.4, eye, false);
        p.Limb(29, 59, 25, 62, 27 + raise / 5, 64, 1.2, 0.5, leg);                          // mandibles
        p.Limb(35, 59, 39, 62, 37 - raise / 5, 64, 1.2, 0.5, leg);
        if (s < 0.9) for (int i = 0; i < 3; i++) p.Ellipse(24 + i * 8, 44, 2, 2, C(255, 200, 80), false); // a crawler's glowing vents
    }

    // ---- Beasts on four legs ----

    private static void Beast(Paint p, SpriteView v, SpriteFrame f, uint fur, uint eye, bool bony, bool rider = false, bool lean = false)
    {
        uint dark = Paint.Shade(fur, -30), pale = Paint.Shade(fur, 24), nose = C(30, 22, 22);
        uint bone = C(226, 216, 196);
        if (f == SpriteFrame.Dead)
        {
            p.Mat = Material.Fur;
            p.Ellipse(30, 57, 20, 6, fur);
            p.Ellipse(52, 55, 7, 5.5, fur);
            p.Mat = Material.Matte;
            p.Taper(57, 56, 3, 62, 57, 1.5, fur);
            for (int i = 0; i < 4; i++) p.Limb(16 + i * 9, 56, 14 + i * 9, 51, 17 + i * 9, 48, 1.8, 1, dark);
            p.Limb(10, 58, 5, 56, 1, 59, 1.8, 0.8, fur);
            p.Dot(52, 54, Socket);
            return;
        }
        double rear = f == SpriteFrame.Windup ? -5 : 0, lunge = f == SpriteFrame.Strike ? 4 : 0;
        bool snarl = f != SpriteFrame.Idle;
        double body = lean ? 7.5 : 10;
        if (v == SpriteView.Side)
        {
            p.Mat = Material.Fur;
            p.Limb(12, 40 + rear / 2, 5, 34 + rear, 3, 26 + rear, 2.6, 1, fur);              // the tail
            // Far legs, darker.
            p.Mat = Material.Matte;
            p.Limb(18, 46, 15, 54, 19, 62, 2.6, 1.6, dark);
            p.Limb(42, 46, 44, 54, 43 + lunge / 2, 62, 2.3, 1.5, dark);
            p.Mat = Material.Fur;
            p.Ellipse(29, 42 + rear / 2, 19, body, fur);                                      // the body
            p.Ellipse(42, 40 + rear / 2, 9, body + 1.5, fur);                                 // the chest and shoulders
            if (bony) { p.Mat = Material.Bone; for (int i = 0; i < 4; i++) p.Taper(22 + i * 5, 36, 1, 21 + i * 5, 48, 0.8, bone); p.Mat = Material.Fur; }
            // Near legs.
            p.Limb(22, 48, 18, 55, 22, 62, 3, 1.8, fur);
            p.Limb(40, 48, 41 + lunge / 2, 55, 40 + lunge, 62, 2.7, 1.8, fur);
            p.Mat = Material.Matte;
            p.Ellipse(23, 62, 2.8, 1.4, dark); p.Ellipse(41 + lunge, 62, 2.8, 1.4, dark);
            // The head: skull, long muzzle, ears.
            p.Mat = Material.Fur;
            p.Ellipse(50 + lunge, 33 + rear, 7.5, 6.5, fur);
            p.Taper(53 + lunge, 35 + rear, 4.2, 61 + lunge, 37 + rear, 2.6, fur);
            p.Mat = Material.Matte;
            p.Ellipse(61.5 + lunge, 36 + rear, 1.6, 1.4, nose);
            p.Triangle(46 + lunge, 29 + rear, 47 + lunge, 20 + rear, 51 + lunge, 28 + rear, dark);
            p.Ellipse(52 + lunge, 31 + rear, 1.4, 1.2, Socket, false); p.Dot(52.5 + lunge, 31 + rear, eye);
            if (snarl)
            {
                p.Taper(54 + lunge, 39.5 + rear, 0.8, 61 + lunge, 39 + rear, 0.6, Maw, false);
                for (int i = 0; i < 3; i++) p.Dot(55 + i * 2 + lunge, 38.5 + rear, Tooth);
            }
            if (rider) Rider(p, 30, rear / 2, true);
            p.Mat = Material.Matte;
            return;
        }
        if (v == SpriteView.Back)
        {
            p.Mat = Material.Matte;
            p.Limb(24, 48, 22, 55, 23, 62, 3.2, 2, dark); p.Limb(40, 48, 42, 55, 41, 62, 3.2, 2, dark);
            p.Mat = Material.Fur;
            p.Ellipse(32, 44, 15, body + 3, fur);
            p.Limb(32, 46, 34, 54, 31, 61, 2.6, 1.2, fur);                                     // the tail hanging
            p.Ellipse(23, 30, 3, 5, dark); p.Ellipse(41, 30, 3, 5, dark);
            p.Ellipse(32, 34, 8, 6.5, fur);
            if (rider) Rider(p, 32, 0, false, back: true);
            p.Mat = Material.Matte;
            return;
        }
        // Front: forelegs, chest and ruff, then the head coming at you.
        p.Mat = Material.Fur;
        p.Limb(24, 46, 23, 54, 24, 61, 3.2, 2.2, fur); p.Limb(40, 46, 41, 54, 40, 61, 3.2, 2.2, fur);
        p.Mat = Material.Matte;
        p.Ellipse(24, 61.5, 3.4, 1.8, dark); p.Ellipse(40, 61.5, 3.4, 1.8, dark);
        for (int i = 0; i < 3; i++) { p.Dot(22.5 + i * 1.4, 62.5, Tooth); p.Dot(38.5 + i * 1.4, 62.5, Tooth); } // claws
        p.Mat = Material.Fur;
        p.Ellipse(32, 44, 13.5, body + 2.5, fur);
        p.Ellipse(32, 42, 9, body, pale);                                                         // the chest ruff
        if (bony) { p.Mat = Material.Bone; for (int i = 0; i < 3; i++) { p.Taper(32, 38 + i * 4, 1, 24, 42 + i * 4, 0.7, bone); p.Taper(32, 38 + i * 4, 1, 40, 42 + i * 4, 0.7, bone); } }
        p.Mat = Material.Matte;
        p.Triangle(24, 27 + rear, 21, 15 + rear, 29, 23 + rear, dark);                              // ears
        p.Triangle(40, 27 + rear, 43, 15 + rear, 35, 23 + rear, dark);
        p.Mat = Material.Fur;
        p.Ellipse(32, 29 + rear, 9.5 + lunge / 2, 8.5, fur);                                        // the head
        p.Ellipse(32, 35 + rear, 5.5 + lunge / 2, 5, pale);                                         // the muzzle
        p.Mat = Material.Matte;
        p.Ellipse(32, 33 + rear, 2.4, 1.7, nose);
        p.Ellipse(27.5, 28 + rear, 1.6, 1.3, Socket, false); p.Dot(27.5, 28 + rear, eye);
        p.Ellipse(36.5, 28 + rear, 1.6, 1.3, Socket, false); p.Dot(36.5, 28 + rear, eye);
        p.Taper(25, 25.5 + rear, 0.6, 30, 26.5 + rear, 0.5, dark); p.Taper(39, 25.5 + rear, 0.6, 34, 26.5 + rear, 0.5, dark);
        if (snarl)
        {
            p.Ellipse(32, 38.5 + rear, 4.2, 2.2, Maw, false);
            p.Triangle(28.5, 37 + rear, 30, 37 + rear, 29.2, 39.5 + rear, Tooth); p.Triangle(34, 37 + rear, 35.5, 37 + rear, 34.8, 39.5 + rear, Tooth);
        }
        if (rider) Rider(p, 32, rear, false);
        p.Mat = Material.Matte;
    }

    /// <summary>A goblin riding a warg, with a spear.</summary>
    private static void Rider(Paint p, double cx, double dy, bool side, bool back = false)
    {
        uint skin = C(98, 142, 62), cloth = C(110, 76, 44);
        double y = side ? 4 : -2;
        p.Mat = Material.Cloth;
        p.Poly(cloth, (cx - 6, 22 + y + dy), (cx + 6, 22 + y + dy), (cx + 7, 34 + y + dy), (cx - 7, 34 + y + dy));
        p.Mat = Material.Matte;
        p.Limb(cx + 5, 24 + y + dy, cx + 9, 28 + y + dy, cx + 11, 22 + y + dy, 1.6, 1.3, skin);
        p.Taper(cx + 11, 34 + y + dy, 0.8, cx + 14, 2 + y + dy, 0.8, C(110, 80, 48));                  // the spear
        p.Mat = Material.Metal;
        p.Taper(cx + 14, 4 + y + dy, 1.6, cx + 14.4, -2 + y + dy, 0.3, C(180, 184, 194));
        p.Mat = Material.Matte;
        if (!back)
        {
            p.Taper(cx - 4, 15 + y + dy, 1.8, cx - 11, 11 + y + dy, 0.5, skin);
            p.Taper(cx + 4, 15 + y + dy, 1.8, cx + 11, 11 + y + dy, 0.5, skin);
        }
        p.Ellipse(cx, 16 + y + dy, 5, 5.5, skin);
        if (!back) { p.Dot(cx - 2, 16 + y + dy, C(250, 220, 60)); p.Dot(cx + 2, 16 + y + dy, C(250, 220, 60)); }
    }

    // ---- Oozes ----

    private static void Blob(Paint p, SpriteFrame f, uint body, uint shine, double rx, double ry, bool eyes)
    {
        uint deep = Paint.Shade(body, -34);
        if (f == SpriteFrame.Dead)
        {
            p.Mat = Material.Gloss;
            p.Ellipse(32, 60, rx + 6, 4, deep);
            p.Ellipse(20, 61, 5, 2, deep);
            p.Ellipse(46, 61, 6, 2, deep);
            p.Mat = Material.Matte;
            return;
        }
        double squash = f == SpriteFrame.Windup ? -4 : f == SpriteFrame.Strike ? 5 : 0;
        double h = ry - squash / 2, w = rx + squash;
        p.Mat = Material.Gloss;
        p.Ellipse(32, 62, w + 3, 2.5, deep);                                                     // the spreading foot
        p.Poly(body, (32 - w, 62), (32 - w * 0.9, 62 - h * 0.9), (32 - w * 0.5, 62 - h * 1.7), (32, 62 - h * 1.95), (32 + w * 0.5, 62 - h * 1.7), (32 + w * 0.9, 62 - h * 0.9), (32 + w, 62));
        p.Ellipse(32, 62 - h * 0.9, w, h, body);
        p.Mat = Material.Matte;
        // Things suspended inside, and bubbles.
        p.Ellipse(24, 58 - h * 0.5, w / 6, h / 6, Paint.Shade(body, 22), false);
        p.Ellipse(40, 59 - h * 0.3, w / 8, h / 8, Paint.Shade(body, 26), false);
        p.Ellipse(36, 60 - h * 1.1, w / 10, h / 10, Paint.Shade(body, 30), false);
        p.Ellipse(32 - w / 2.6, 62 - h * 1.5, w / 7, h / 10, Paint.Shade(body, 60), false);    // the sheen
        p.Ellipse(32 - w / 2.4, 62 - h * 1.55, w / 16, h / 18, shine, false);
        // Drips running down.
        p.Mat = Material.Gloss;
        p.Taper(32 + w * 0.7, 62 - h * 0.8, 1.6, 32 + w * 0.75, 60, 1, body);
        p.Taper(32 - w * 0.4, 62 - h * 0.3, 1.4, 32 - w * 0.42, 63, 0.9, body);
        p.Mat = Material.Matte;
        if (eyes)
        {
            uint white = C(250, 250, 230);
            p.Ellipse(26, 60 - h, 3.2, 3.2, white, false); p.Ellipse(26.5, 60.5 - h, 1.4, 1.6, Socket, false);
            p.Ellipse(38, 58 - h, 2.6, 2.6, white, false); p.Ellipse(38.5, 58.5 - h, 1.2, 1.4, Socket, false);
            p.Ellipse(32, 66 - h * 0.6, w / 2.4, f == SpriteFrame.Idle ? 1.6 : 3.5, Maw, false);
        }
    }

    private static void Cube(Paint p, SpriteFrame f)
    {
        uint glass = C(140, 196, 186), edge = C(206, 244, 234);
        if (f == SpriteFrame.Dead) { p.Mat = Material.Gloss; p.Ellipse(32, 60, 26, 4, glass); p.Mat = Material.Matte; return; }
        double grow = f == SpriteFrame.Strike ? 3 : f == SpriteFrame.Windup ? -2 : 0;
        p.Mat = Material.Gloss;
        p.Poly(glass, (6 - grow, 12 - grow), (12 - grow, 6 - grow), (60 + grow, 6 - grow), (60 + grow, 58), (54 + grow, 63), (6 - grow, 63));
        p.Mat = Material.Matte;
        p.Poly(Paint.Shade(glass, 16), false, (6 - grow, 12 - grow), (12 - grow, 6 - grow), (60 + grow, 6 - grow), (54 + grow, 12 - grow)); // the top face
        p.Poly(Paint.Shade(glass, -18), false, (54 + grow, 12 - grow), (60 + grow, 6 - grow), (60 + grow, 58), (54 + grow, 63));          // the right face
        p.Line(6 - grow, 12 - grow, 54 + grow, 12 - grow, 0.6, edge);
        p.Line(54 + grow, 12 - grow, 54 + grow, 63, 0.6, edge);
        // What it has swallowed.
        p.Mat = Material.Bone;
        p.Ellipse(22, 40, 4.5, 5, C(212, 206, 186));
        p.Mat = Material.Matte;
        p.Ellipse(21, 40, 1, 1.3, C(80, 110, 104), false); p.Ellipse(24, 40, 1, 1.3, C(80, 110, 104), false);
        p.Mat = Material.Metal;
        p.Taper(34, 50, 0.9, 46, 32, 0.5, C(150, 170, 170));
        p.Mat = Material.Matte;
        p.Ellipse(40, 56, 3, 2, C(160, 140, 90));
        for (int i = 0; i < 5; i++) p.Ellipse(14 + i * 9, 20 + i * 7 % 30, 1.2, 1.2, edge, false);
    }

    // ---- Things that float (each trails a wisp down to the floor) ----

    private static void Floater(Paint p, SpriteFrame f, uint body, uint glow, string kind)
    {
        double bob = f == SpriteFrame.Windup ? -4 : f == SpriteFrame.Strike ? 3 : 0;
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 60, 14, 4, Paint.Shade(body, -20));
            p.Ellipse(32, 58, 5, 2, glow, false);
            for (int i = 0; i < 3; i++) p.Ellipse(22 + i * 10, 55 - i % 2 * 3, 2, 2, Paint.Shade(body, -40), false);
            return;
        }
        // The trail to the floor.
        if (kind is "wings" or "moth" or "pages" or "bell") p.Ellipse(32, 61.5, 7, 1.4, C(24, 20, 18), false); // its shadow on the floor
        else
            for (int i = 0; i < 5; i++) p.Ellipse(32 + (i % 2 == 0 ? -2 : 2), 47 + i * 3.6, 6 - i, 2.6, Paint.Shade(body, -14 - i * 9), false);
        switch (kind)
        {
            case "flame":
            {
                // Tongues of flame round a white-hot core.
                uint outer = Paint.Shade(body, -30), mid = body, hot = glow;
                p.Poly(outer, false, (20, 44 + bob), (18, 30 + bob), (24, 22 + bob), (25, 30 + bob), (30, 10 + bob), (35, 24 + bob), (40, 14 + bob), (42, 28 + bob), (46, 30 + bob), (44, 44 + bob), (32, 50 + bob));
                p.Poly(mid, false, (23, 43 + bob), (23, 32 + bob), (28, 27 + bob), (31, 16 + bob), (34, 28 + bob), (39, 22 + bob), (41, 34 + bob), (41, 43 + bob), (32, 48 + bob));
                p.Ellipse(32, 38 + bob, 6, 8, hot, false);
                p.Ellipse(32, 40 + bob, 3, 4, C(255, 255, 240), false);
                p.Dot(29, 35 + bob, Socket); p.Dot(35, 35 + bob, Socket);
                for (int i = 0; i < 4; i++) p.Dot(18 + i * 9, 8 + bob + i % 2 * 6, hot);
                break;
            }
            case "imp":
            {
                uint dark = Paint.Shade(body, -40), horn = C(46, 30, 26);
                // Membrane wings behind, a tail, then the little devil.
                p.Poly(dark, (25, 30 + bob), (8, 16 + bob), (10, 24 + bob), (4, 28 + bob), (10, 32 + bob), (6, 38 + bob), (22, 38 + bob));
                p.Poly(dark, (39, 30 + bob), (56, 16 + bob), (54, 24 + bob), (60, 28 + bob), (54, 32 + bob), (58, 38 + bob), (42, 38 + bob));
                p.Taper(24, 30 + bob, 0.6, 8, 16 + bob, 0.4, horn); p.Taper(40, 30 + bob, 0.6, 56, 16 + bob, 0.4, horn);
                p.Limb(36, 44 + bob, 46, 48 + bob, 50, 40 + bob, 1.4, 0.6, body);
                p.Triangle(48, 39 + bob, 53, 38 + bob, 51, 42 + bob, body);
                p.Ellipse(32, 37 + bob, 8, 9.5, body);
                p.Limb(26, 33 + bob, 21, 39 + bob, 23, 44 + bob, 1.8, 1.2, body);
                p.Limb(38, 33 + bob, 43, f == SpriteFrame.Windup ? 26 + bob : 39 + bob, 42, f == SpriteFrame.Windup ? 20 + bob : 44 + bob, 1.8, 1.2, body);
                p.Limb(28, 44 + bob, 27, 49 + bob, 28, 53 + bob, 2, 1.2, body); p.Limb(36, 44 + bob, 37, 49 + bob, 36, 53 + bob, 2, 1.2, body);
                p.Ellipse(32, 23 + bob, 7, 6.5, body);
                p.Taper(27, 19 + bob, 1.6, 23, 10 + bob, 0.4, horn); p.Taper(37, 19 + bob, 1.6, 41, 10 + bob, 0.4, horn);
                p.Ellipse(29, 22 + bob, 1.6, 1.2, glow, false); p.Ellipse(35, 22 + bob, 1.6, 1.2, glow, false);
                p.Taper(29, 26.5 + bob, 0.5, 35, 26.5 + bob, 0.5, Maw, false);
                if (f == SpriteFrame.Windup) { p.Ellipse(42, 18 + bob, 4.5, 4.5, Paint.Shade(glow, -40), false); p.Ellipse(42, 18 + bob, 2.6, 2.6, C(255, 250, 220), false); }
                break;
            }
            case "ghost":
            {
                // A tattered shroud with hollow eyes and reaching arms.
                p.Mat = Material.Cloth;
                p.Poly(body, (20, 24 + bob), (26, 12 + bob), (32, 9 + bob), (38, 12 + bob), (44, 24 + bob), (47, 42 + bob), (43, 50 + bob), (40, 45 + bob), (36, 52 + bob), (32, 46 + bob), (28, 53 + bob), (24, 45 + bob), (21, 50 + bob), (17, 42 + bob));
                p.Mat = Material.Matte;
                double reach = f == SpriteFrame.Strike ? 6 : f == SpriteFrame.Windup ? -4 : 0;
                p.Limb(22, 28 + bob, 14 - reach / 2, 34 + bob, 10 - reach, 30 + bob - reach, 2.4, 1.2, Paint.Shade(body, -10));
                p.Limb(42, 28 + bob, 50 + reach / 2, 34 + bob, 54 + reach, 30 + bob - reach, 2.4, 1.2, Paint.Shade(body, -10));
                p.Ellipse(27, 22 + bob, 3, 4, Socket, false); p.Ellipse(37, 22 + bob, 3, 4, Socket, false);
                p.Ellipse(27, 22.5 + bob, 1.3, 1.6, glow, false); p.Ellipse(37, 22.5 + bob, 1.3, 1.6, glow, false);
                p.Ellipse(32, 31 + bob, 2.6, f == SpriteFrame.Idle ? 2 : 5, Socket, false);
                break;
            }
            case "wings":
            {
                // A bat: membrane wings on finger bones, furry body, big ears.
                double flap = f == SpriteFrame.Windup ? -9 : f == SpriteFrame.Strike ? 5 : 0;
                uint skin = Paint.Shade(body, 14), bone = Paint.Shade(body, -26);
                p.Poly(skin, (28, 30 + bob), (14, 14 + bob + flap), (3, 22 + bob + flap), (6, 30 + bob + flap / 2), (10, 28 + bob + flap / 2), (13, 36 + bob), (19, 33 + bob), (24, 38 + bob));
                p.Poly(skin, (36, 30 + bob), (50, 14 + bob + flap), (61, 22 + bob + flap), (58, 30 + bob + flap / 2), (54, 28 + bob + flap / 2), (51, 36 + bob), (45, 33 + bob), (40, 38 + bob));
                p.Taper(28, 30 + bob, 0.8, 14, 14 + bob + flap, 0.5, bone); p.Taper(14, 14 + bob + flap, 0.5, 13, 36 + bob, 0.4, bone);
                p.Taper(36, 30 + bob, 0.8, 50, 14 + bob + flap, 0.5, bone); p.Taper(50, 14 + bob + flap, 0.5, 51, 36 + bob, 0.4, bone);
                p.Mat = Material.Fur;
                p.Ellipse(32, 34 + bob, 6, 8, body);
                p.Ellipse(32, 26 + bob, 5, 4.5, body);
                p.Mat = Material.Matte;
                p.Triangle(27.5, 25 + bob, 26.5, 17 + bob, 30.5, 23 + bob, body); p.Triangle(36.5, 25 + bob, 37.5, 17 + bob, 33.5, 23 + bob, body);
                p.Ellipse(30, 25.5 + bob, 1.1, 1.1, glow, false); p.Ellipse(34, 25.5 + bob, 1.1, 1.1, glow, false);
                p.Dot(31, 28.5 + bob, Tooth); p.Dot(33, 28.5 + bob, Tooth);
                p.Limb(30, 41 + bob, 29, 45 + bob, 30, 47 + bob, 0.8, 0.5, bone); p.Limb(34, 41 + bob, 35, 45 + bob, 34, 47 + bob, 0.8, 0.5, bone);
                break;
            }
            case "moth":
            {
                double flap = f == SpriteFrame.Windup ? -3 : 0;
                p.Poly(body, (31, 28 + bob), (16, 18 + bob + flap), (10, 26 + bob), (16, 34 + bob), (30, 33 + bob));
                p.Poly(body, (33, 28 + bob), (48, 18 + bob + flap), (54, 26 + bob), (48, 34 + bob), (34, 33 + bob));
                p.Poly(Paint.Shade(body, -26), (30, 32 + bob), (20, 36 + bob), (22, 42 + bob), (30, 37 + bob));
                p.Poly(Paint.Shade(body, -26), (34, 32 + bob), (44, 36 + bob), (42, 42 + bob), (34, 37 + bob));
                p.Ellipse(19, 25 + bob, 3.4, 3.4, Socket, false); p.Ellipse(19, 25 + bob, 2.2, 2.2, glow, false);
                p.Ellipse(45, 25 + bob, 3.4, 3.4, Socket, false); p.Ellipse(45, 25 + bob, 2.2, 2.2, glow, false);
                p.Mat = Material.Fur;
                p.Taper(32, 24 + bob, 2.2, 32, 40 + bob, 1.2, C(70, 58, 46));
                p.Mat = Material.Matte;
                p.Taper(31, 24 + bob, 0.4, 27, 18 + bob, 0.3, C(70, 58, 46)); p.Taper(33, 24 + bob, 0.4, 37, 18 + bob, 0.3, C(70, 58, 46));
                break;
            }
            case "brain":
            {
                // A floating brain trailing nerves.
                uint fold = Paint.Shade(body, -34);
                for (int i = 0; i < 4; i++) p.Limb(26 + i * 4, 40 + bob, 22 + i * 7, 50 + bob, 18 + i * 9, 62, 1.4, 0.4, Paint.Shade(body, -46));
                p.Mat = Material.Gloss;
                p.Ellipse(32, 30 + bob, 17, 13.5, body);
                p.Mat = Material.Matte;
                p.Taper(32, 17 + bob, 0.6, 32, 43 + bob, 0.6, fold, false);
                for (int i = 0; i < 5; i++)
                {
                    p.Limb(19 + i * 1.5, 22 + i * 4 + bob, 24 + i % 2 * 2, 24 + i * 4 + bob, 28, 21 + i * 4 + bob, 0.6, 0.5, fold);
                    p.Limb(45 - i * 1.5, 22 + i * 4 + bob, 40 - i % 2 * 2, 24 + i * 4 + bob, 36, 21 + i * 4 + bob, 0.6, 0.5, fold);
                }
                p.Ellipse(32, 38 + bob, 3, 2.5, glow, false);                                           // the third eye
                if (f == SpriteFrame.Windup) p.Ellipse(32, 30 + bob, 20, 16, Paint.Shade(glow, -50), false);
                if (f == SpriteFrame.Windup) p.Ellipse(32, 30 + bob, 17, 13.5, body);
                break;
            }
            case "pages":
            {
                // A storm of loose pages, edges catching the light.
                double spin = f == SpriteFrame.Windup ? 0.5 : f == SpriteFrame.Strike ? -0.4 : 0;
                for (int i = 0; i < 11; i++)
                {
                    double a = i * 2.39996 + spin, r = 6 + i * 1.7;
                    double cx = 32 + Math.Cos(a) * r, cy = 30 + bob + Math.Sin(a) * r * 0.75;
                    double tilt = (i % 3 - 1) * 2.2;
                    p.Poly(i % 2 == 0 ? body : Paint.Shade(body, -18), (cx - 4, cy - 3 + tilt), (cx + 4, cy - 3 - tilt), (cx + 4, cy + 3 - tilt), (cx - 4, cy + 3 + tilt));
                    for (int l = 0; l < 3; l++) p.Taper(cx - 2.6, cy - 1.4 + l * 1.4, 0.2, cx + 2.6, cy - 1.4 + l * 1.4 - tilt / 3, 0.2, glow, false); // the writing
                }
                break;
            }
            case "errata":
            {
                // Struck-out writing given a shape: lines of script, each crossed through in red, round a dark core.
                p.Ellipse(32, 30 + bob, 11, 15, body);
                for (int i = 0; i < 9; i++)
                {
                    double y = 12 + i * 4.2 + bob, half = 15 - Math.Abs(i - 4) * 1.8;
                    p.Taper(32 - half, y, 0.6, 32 + half, y + (i % 2 == 0 ? 1 : -1), 0.6, C(220, 210, 190), false);
                    p.Taper(32 - half - 1, y + 0.6, 0.4, 32 + half + 1, y - 0.6, 0.4, glow, false);
                }
                p.Ellipse(28, 24 + bob, 1.6, 1.6, glow, false); p.Ellipse(36, 24 + bob, 1.6, 1.6, glow, false);
                if (f != SpriteFrame.Idle) p.Limb(40, 30 + bob, 50, 26 + bob, 56, f == SpriteFrame.Windup ? 16 + bob : 30 + bob, 1.6, 0.6, C(220, 210, 190));
                break;
            }
            case "bell":
            {
                // A drifting bronze bell weeping verdigris, its clapper swinging with the toll.
                p.Mat = Material.Metal;
                p.Poly(body, (22, 40 + bob), (24, 22 + bob), (28, 14 + bob), (36, 14 + bob), (40, 22 + bob), (42, 40 + bob));
                p.Rect(19, 39 + bob, 45, 43 + bob, Paint.Shade(body, 16));
                p.Rect(30, 9 + bob, 34, 14 + bob, body);
                p.Mat = Material.Matte;
                double swing = f == SpriteFrame.Windup ? -5 : f == SpriteFrame.Strike ? 5 : 0;
                p.Ellipse(32 + swing, 44 + bob, 3, 3, Paint.Shade(body, -50));
                for (int i = 0; i < 4; i++) p.Taper(25 + i * 4.6, 18 + bob + i % 2 * 4, 0.8, 25.5 + i * 4.6, 32 + bob + i % 2 * 5, 0.5, glow, false); // verdigris tears
                if (f == SpriteFrame.Strike) // the toll, as rings of struck air either side
                    for (int i = 0; i < 3; i++) { p.Taper(16 - i * 4, 30 + bob - i * 3, 0.5, 14 - i * 4, 44 + bob + i * 3, 0.5, Paint.Shade(glow, -i * 30), false); p.Taper(48 + i * 4, 30 + bob - i * 3, 0.5, 50 + i * 4, 44 + bob + i * 3, 0.5, Paint.Shade(glow, -i * 30), false); }
                break;
            }
            case "veil":
            case "keener":
            case "hush":
            {
                // A tall shrouded mourner. The veil hides its face; the keener's face is all mouth; the Hush has no
                // face at all, only a finger at its lips.
                p.Mat = Material.Cloth;
                p.Poly(body, (24, 14 + bob), (32, 6 + bob), (40, 14 + bob), (44, 50 + bob), (39, 46 + bob), (35, 52 + bob), (32, 47 + bob), (28, 52 + bob), (25, 46 + bob), (20, 50 + bob));
                p.Mat = Material.Matte;
                if (kind == "veil")
                {
                    p.Poly(Paint.Shade(body, 30), false, (25, 12 + bob), (32, 7 + bob), (39, 12 + bob), (41, 30 + bob), (23, 30 + bob));
                    p.Ellipse(29, 18 + bob, 1.2, 1.2, glow, false); p.Ellipse(35, 18 + bob, 1.2, 1.2, glow, false);
                }
                else if (kind == "keener")
                {
                    if (f != SpriteFrame.Idle) // the keen, as lines spreading from its mouth
                        for (int i = 0; i < 3; i++) { p.Taper(26 - i * 4, 18 + bob - i * 2, 0.5, 24 - i * 4, 24 + bob + i * 2, 0.5, Paint.Shade(glow, -i * 30), false); p.Taper(38 + i * 4, 18 + bob - i * 2, 0.5, 40 + i * 4, 24 + bob + i * 2, 0.5, Paint.Shade(glow, -i * 30), false); }
                    p.Ellipse(32, 16 + bob, 6, 7, Paint.Shade(body, 10));
                    p.Ellipse(29, 14 + bob, 1.6, 2, Socket, false); p.Ellipse(35, 14 + bob, 1.6, 2, Socket, false);
                    p.Ellipse(32, 20 + bob, 2.6, f == SpriteFrame.Idle ? 3 : 5, Socket, false);
                }
                else
                {
                    p.Ellipse(32, 15 + bob, 5.5, 7, Paint.Shade(body, 14));                                   // a blank face
                    p.Limb(38, 26 + bob, 36, 21 + bob, 32.5, 18 + bob, 1.3, 0.7, Paint.Shade(body, 20)); // the finger at its lips
                }
                double reach = f == SpriteFrame.Strike ? 7 : 0;
                p.Limb(24, 24 + bob, 18 - reach / 2, 32 + bob, 15 - reach, 28 + bob - reach, 1.8, 1, Paint.Shade(body, 8));
                if (kind != "hush") p.Limb(40, 24 + bob, 46 + reach / 2, 32 + bob, 49 + reach, 28 + bob - reach, 1.8, 1, Paint.Shade(body, 8));
                break;
            }
            case "unmaker":
            {
                // A hole in the world, ringed with eyes, shards of what it has unmade turning round it.
                p.Mat = Material.Stone;
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 + (f == SpriteFrame.Windup ? 0.3 : 0), r = 26;
                    double sx = 32 + Math.Cos(a) * r, sy = 30 + bob + Math.Sin(a) * r * 0.8;
                    p.Poly(C(110, 104, 120), (sx - 3, sy), (sx, sy - 4), (sx + 3, sy + 1), (sx - 1, sy + 3));
                }
                p.Mat = Material.Matte;
                p.Ellipse(32, 30 + bob, 21, 24, body);
                p.Ellipse(32, 30 + bob, 15, 17, C(4, 2, 8), false);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    double ex = 32 + Math.Cos(a) * 18, ey = 30 + bob + Math.Sin(a) * 20.5;
                    p.Ellipse(ex, ey, 2, 1.5, C(240, 230, 250), false); p.Dot(ex, ey, glow);
                }
                p.Ellipse(32, 30 + bob, 4, f == SpriteFrame.Windup ? 11 : 6, glow, false);
                p.Ellipse(32, 30 + bob, 1.6, f == SpriteFrame.Windup ? 8 : 4, C(255, 255, 255), false);
                break;
            }
        }
    }

    // ---- Fungi ----

    private static void Fungus(Paint p, SpriteFrame f, uint cap, uint stalk, double s, bool heap = false)
    {
        uint gill = Paint.Shade(stalk, -36), spot = C(240, 232, 214);
        if (f == SpriteFrame.Dead)
        {
            p.Ellipse(32, 58, 22, 6, cap);
            p.Ellipse(22, 59, 7, 3.5, stalk);
            for (int i = 0; i < 4; i++) p.Ellipse(20 + i * 8, 54 + i % 2 * 2, 1.6, 1.6, spot, false);
            return;
        }
        double sway = f == SpriteFrame.Windup ? -6 : f == SpriteFrame.Strike ? 6 : 0;
        if (heap)
        {
            // A rotting colossus: a heap of fungus-eaten flesh with caps sprouting from it.
            p.Limb(18, 50, 14, 56, 16, 63, 5, 4, Paint.Shade(cap, -20)); p.Limb(46, 50, 50, 56, 48, 63, 5, 4, Paint.Shade(cap, -20));
            p.Mat = Material.Stone;
            p.Ellipse(32, 42, 27, 19, cap);
            p.Mat = Material.Matte;
            p.Limb(10, 36, 3 + sway, 30, 4 + sway, 18, 5, 3, cap); p.Limb(54, 36, 61 + sway, 30, 60 + sway, 18, 5, 3, cap);
            for (int i = 0; i < 6; i++)
            {
                double x = 12 + i * 8, y = 27 + i % 3 * 5;
                p.Taper(x, y + 6, 1.4, x, y, 1, stalk);
                p.Ellipse(x, y, 4.5 - i % 2, 3, Paint.Shade(stalk, -10 + i * 6));
            }
            p.Ellipse(24, 42, 2.4, 2, C(220, 230, 120), false); p.Ellipse(38, 40, 2.4, 2, C(220, 230, 120), false);
            p.Ellipse(31, 50, 7, f == SpriteFrame.Idle ? 2 : 4.5, Maw, false);
            return;
        }
        double top = 64 - 58 * s;
        // Root-legs, a fibrous stalk, arms for the bigger ones, and the cap with its gills and spots.
        double legTop = top + 13 + 21 * s, knee = (legTop + 63) / 2;
        p.Limb(29, legTop, 26, knee, 24, 63, 3.6 * s + 0.8, 2.4, stalk);
        p.Limb(35, legTop, 38, knee, 40, 63, 3.6 * s + 0.8, 2.4, stalk);
        p.Mat = Material.Fur;
        p.Taper(32, 63 - 26 * s, 6.5 * s + 1, 32 + sway / 3, top + 13, 5 * s + 1, stalk);
        p.Mat = Material.Matte;
        if (s > 0.7)
        {
            p.Limb(28, top + 22, 18 + sway, top + 30, 14 + sway, top + 40, 2.4, 1.2, stalk);
            p.Limb(36, top + 22, 46 + sway, top + 30, 50 + sway, top + 40, 2.4, 1.2, stalk);
            for (int i = 0; i < 3; i++) { p.Taper(14 + sway, top + 40, 0.6, 11 + i * 3 + sway, top + 44, 0.3, stalk); p.Taper(50 + sway, top + 40, 0.6, 47 + i * 3 + sway, top + 44, 0.3, stalk); }
        }
        double capX = 32 + sway / 2, capY = top + 10;
        p.Ellipse(capX, capY + 3 * s + 1, 18 * s + 3, 4 * s + 1.5, gill);                                // the gills underneath
        for (int i = -3; i <= 3; i++) p.Taper(capX + i * 4 * s, capY + 3, 0.4, capX + i * 5 * s, capY + 5 * s + 2, 0.4, Paint.Shade(gill, -20), false);
        p.Poly(cap, (capX - 20 * s - 4, capY + 3 * s + 1), (capX - 15 * s - 3, capY - 6 * s), (capX - 6 * s, capY - 10 * s - 2), (capX + 6 * s, capY - 10 * s - 2), (capX + 15 * s + 3, capY - 6 * s), (capX + 20 * s + 4, capY + 3 * s + 1));
        for (int i = 0; i < 5; i++) p.Ellipse(capX - 13 * s + i * 6.5 * s, capY - 3 * s - i % 2 * 3 * s, 2.4 * s + 0.6, 1.8 * s + 0.6, spot, false);
        // A face in the stalk.
        p.Ellipse(29.5 + sway / 4, top + 20, 1.6, 1.4, C(230, 240, 140), false); p.Ellipse(34.5 + sway / 4, top + 20, 1.6, 1.4, C(230, 240, 140), false);
        if (f != SpriteFrame.Idle) p.Ellipse(32 + sway / 4, top + 25, 2.2, 1.6, Maw, false);
        if (s < 0.7 && f == SpriteFrame.Windup) for (int i = 0; i < 8; i++) p.Ellipse(14 + i * 5, top + 2 + i % 3 * 3, 1.6, 1.6, C(230, 220, 140), false); // spores
    }

    // ---- Machines ----

    private static void Machine(Paint p, SpriteView v, SpriteFrame f, uint metal, uint eye, double s, bool heavy, bool cracks = false, bool wick = false)
    {
        uint dark = Paint.Shade(metal, -34), rivet = Paint.Shade(metal, 40);
        var mat = cracks ? Material.Stone : Material.Metal;
        if (f == SpriteFrame.Dead)
        {
            p.Mat = mat;
            p.Rect(10, 53, 54, 63, metal);
            p.Ellipse(44, 51, 8, 6, dark);
            p.Mat = Material.Matte;
            for (int i = 0; i < 4; i++) p.Rect(12 + i * 11, 49, 16 + i * 11, 53, dark);
            p.Ellipse(44, 51, 2, 1.5, Paint.Shade(eye, -80), false);
            return;
        }
        double w = heavy ? 15 : 11, top = 64 - 60 * s;
        double arm = f == SpriteFrame.Windup ? -12 : f == SpriteFrame.Strike ? 6 : 0;
        bool side = v == SpriteView.Side, back = v == SpriteView.Back;
        if (side) w *= 0.7;
        p.Mat = mat;
        // Legs: thick pistons with knee joints and flat feet.
        p.Limb(32 - w / 2 - 1, top + 44, 32 - w / 2 - 2, top + 52, 32 - w / 2 - 1, 61, heavy ? 4.5 : 3.2, heavy ? 4 : 2.6, dark);
        p.Limb(32 + w / 2 + 1, top + 44, 32 + w / 2 + 2, top + 52, 32 + w / 2 + 1, 61, heavy ? 4.5 : 3.2, heavy ? 4 : 2.6, dark);
        p.Rect(32 - w / 2 - 7, 60, 32 - w / 2 + 4, 63, metal); p.Rect(32 + w / 2 - 4, 60, 32 + w / 2 + 7, 63, metal);
        // The body: a chest plate over a narrower waist.
        p.Poly(metal, (32 - w, top + 14), (32 + w, top + 14), (32 + w - 2, top + 36), (32 + w / 2, top + 46), (32 - w / 2, top + 46), (32 - w + 2, top + 36));
        p.Mat = Material.Matte;
        if (cracks) for (int i = 0; i < 5; i++) p.Limb(32 - w + 4 + i * 5.5, top + 16 + i % 2 * 3, 32 - w + 6 + i * 5, top + 27, 32 - w + 3 + i * 6, top + 40, 0.8, 0.5, eye);
        else
        {
            for (int i = 0; i < 4; i++) { p.Dot(32 - w + 2.5, top + 17 + i * 6, rivet); p.Dot(32 + w - 3.5, top + 17 + i * 6, rivet); }
            p.Taper(32 - w + 1, top + 36, 0.5, 32 + w - 1, top + 36, 0.5, dark, false);                       // the plate's seam
        }
        if (!back && !side)
        {
            // The heart of it: a furnace grill or a gauge.
            if (heavy)
            {
                p.Ellipse(32, top + 27, 6.5, 6.5, dark);
                p.Ellipse(32, top + 27, 5, 5, eye, false);
                for (int i = -1; i <= 1; i++) p.Rect(27, top + 26 + i * 2.6, 37, top + 26.8 + i * 2.6, Paint.Shade(eye, -90), false);
            }
            else
            {
                p.Ellipse(32, top + 25, 5, 5, C(220, 214, 190));
                p.Taper(32, top + 25, 0.5, 34.5, top + 22, 0.4, C(40, 30, 30), false);
                p.Ellipse(32, top + 25, 5, 5, Paint.Shade(metal, -10));
                p.Ellipse(32, top + 25, 3.8, 3.8, C(230, 224, 200), false);
                p.Taper(32, top + 25, 0.5, 34.5, top + 22.5, 0.4, C(40, 30, 30), false);
            }
        }
        p.Mat = mat;
        // Arms: shoulder balls, piston arms, and fists or hammers.
        void Arm(double sx, double dir)
        {
            p.Ellipse(sx, top + 16, heavy ? 5.5 : 4, heavy ? 5 : 3.6, metal);
            double ex = sx + dir * (heavy ? 5 : 3), ey = top + 28 + arm / 2, hx = sx + dir * (heavy ? 4 : 3), hy = top + 40 + arm;
            p.Limb(sx, top + 17, ex, ey, hx, hy, heavy ? 3.4 : 2.3, heavy ? 3 : 2, dark);
            p.Ellipse(hx, hy + 1, heavy ? 5 : 3.2, heavy ? 4.5 : 3, metal);
        }
        if (!side) Arm(32 - w - 2, -1);
        Arm(32 + (side ? 2 : w + 2), 1);
        // The head: a riveted helm with a glowing slit.
        p.Rect(25, top + 1, 39, top + 14, metal);
        p.Ellipse(32, top + 2, 7, 3, metal);
        p.Mat = Material.Matte;
        if (back) return;
        p.Rect(side ? 33 : 27, top + 6, 37, top + 9, Socket, false);
        p.Rect(side ? 34 : 28, top + 6.8, 36, top + 8.4, eye, false);
        if (!side) { p.Dot(26.5, top + 3, rivet); p.Dot(37, top + 3, rivet); p.Dot(26.5, top + 12, rivet); p.Dot(37, top + 12, rivet); }
        if (heavy && f == SpriteFrame.Windup && !wick) for (int i = 0; i < 3; i++) p.Ellipse(24 + i * 8, top - 3 - i % 2 * 2, 2.6, 2, C(200, 200, 200), false); // steam
        if (wick)
        {
            // A wax warden: drips down its body, and a lit wick on its head.
            for (int i = 0; i < 5; i++) p.Taper(32 - w + 3 + i * (w * 2 - 6) / 4, top + 14, 1.6, 32 - w + 3 + i * (w * 2 - 6) / 4, top + 20 + i % 3 * 5, 1.1, Paint.Shade(metal, 20));
            p.Taper(32, top + 1, 0.6, 32, top - 3, 0.5, C(40, 30, 24));
            p.Ellipse(32, top - 6, 2.2, 3.4, eye, false);
            p.Ellipse(32, top - 5.4, 1, 1.8, C(255, 255, 230), false);
        }
    }

    private static void Cart(Paint p, SpriteFrame f)
    {
        uint iron = C(84, 80, 76), rust = C(134, 82, 50), eye = C(255, 190, 70), tyre = C(40, 38, 36);
        if (f == SpriteFrame.Dead) { p.Mat = Material.Metal; p.Rect(6, 50, 58, 63, iron); p.Mat = Material.Matte; p.Ellipse(14, 60, 6, 4, rust); return; }
        double lunge = f == SpriteFrame.Strike ? 4 : 0;
        p.Mat = Material.Metal;
        p.Rect(30, 2, 36, 18, C(60, 56, 54));                                                              // the stack
        p.Poly(iron, (5 - lunge, 24), (59 + lunge, 24), (56 + lunge, 54), (8 - lunge, 54));
        p.Rect(4 - lunge, 18, 60 + lunge, 25, rust);
        p.Mat = Material.Matte;
        for (int i = 0; i < 7; i++) { p.Dot(8 + i * 8, 21, C(200, 150, 110)); p.Dot(10 + i * 7, 51, C(150, 146, 140)); }
        p.Rect(20, 30, 44, 42, Socket, false);                                                             // the grille
        for (int i = 0; i < 5; i++) p.Rect(21 + i * 5, 31, 23 + i * 5, 41, C(60, 56, 54), false);
        p.Ellipse(14, 34, 4.5, 4.5, C(60, 56, 54)); p.Ellipse(14, 34, 3.2, 3.2, eye, false);                 // headlamps
        p.Ellipse(50, 34, 4.5, 4.5, C(60, 56, 54)); p.Ellipse(50, 34, 3.2, 3.2, eye, false);
        p.Mat = Material.Metal;
        foreach (int wx in new[] { 15, 49 })
        {
            p.Ellipse(wx, 56, 7.5, 7.5, tyre);
            p.Ellipse(wx, 56, 4, 4, C(120, 116, 110));
        }
        p.Mat = Material.Matte;
        p.Poly(C(150, 150, 156), (4 - lunge, 54), (12 - lunge, 46), (12 - lunge, 63));                         // the ram's prow
        p.Poly(C(150, 150, 156), (60 + lunge, 54), (52 + lunge, 46), (52 + lunge, 63));
        if (f == SpriteFrame.Windup) for (int i = 0; i < 3; i++) p.Ellipse(33 + i * 4, -1 - i * 3, 4 + i, 3 + i, C(200, 200, 200), false);
    }

    private static void Engine(Paint p, SpriteFrame f)
    {
        uint brass = C(178, 136, 62), dark = C(92, 66, 34), spark = C(200, 224, 255), pipe = C(120, 110, 96);
        if (f == SpriteFrame.Dead) { p.Mat = Material.Metal; p.Rect(4, 46, 60, 63, dark); p.Ellipse(32, 44, 16, 8, brass); p.Mat = Material.Matte; return; }
        double swing = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? 10 : 0;
        p.Mat = Material.Metal;
        // Pipes, the boiler house, the great bell on top, and hammer arms.
        p.Taper(6, 60, 2, 6, 28, 2, pipe); p.Taper(58, 60, 2, 58, 28, 2, pipe);
        p.Rect(9, 32, 55, 63, dark);
        for (int i = 0; i < 3; i++) p.Rect(9, 38 + i * 8, 55, 39.5 + i * 8, brass);
        p.Poly(brass, (17, 31), (21, 14), (26, 7), (38, 7), (43, 14), (47, 31));                           // the bell
        p.Rect(14, 29, 50, 33, brass);
        p.Rect(29, 1, 35, 8, brass);
        p.Mat = Material.Matte;
        p.Ellipse(32, 31, 3.2, 3, dark);                                                                    // its clapper
        p.Mat = Material.Metal;
        p.Limb(10, 36, 2, 26 + swing, 6, 14 + swing, 2.6, 2.2, pipe); p.Limb(54, 36, 62, 26 + swing, 58, 14 + swing, 2.6, 2.2, pipe);
        p.Rect(1, 10 + swing, 11, 16 + swing, C(90, 90, 96)); p.Rect(53, 10 + swing, 63, 16 + swing, C(90, 90, 96));
        p.Mat = Material.Matte;
        // The furnace door, and two gauge-eyes.
        p.Rect(22, 44, 42, 58, Socket, false);
        p.Rect(24, 46, 40, 56, f == SpriteFrame.Idle ? C(240, 120, 40) : C(255, 200, 90), false);
        for (int i = 0; i < 4; i++) p.Rect(24, 47 + i * 2.6, 40, 47.8 + i * 2.6, C(120, 50, 20), false);
        p.Ellipse(20, 38, 3, 3, C(230, 224, 200), false); p.Ellipse(44, 38, 3, 3, C(230, 224, 200), false);
        p.Dot(20, 38, Socket); p.Dot(44, 38, Socket);
        if (f != SpriteFrame.Idle) for (int i = 0; i < 4; i++) p.Line(14 + i * 12, 60, 18 + i * 12, 63, 0.6, spark);
    }

    // ---- Serpents and tentacles ----

    private static void Serpent(Paint p, SpriteView v, SpriteFrame f, uint scale, uint trim, double s, bool crown = false)
    {
        uint belly = Paint.Shade(scale, 46), dark = Paint.Shade(scale, -30), skin = Paint.Shade(scale, 26);
        if (f == SpriteFrame.Dead)
        {
            p.Mat = Material.Gloss;
            p.Limb(8, 58, 30, 61, 52, 57, 4, 3, scale);
            p.Mat = Material.Matte;
            p.Ellipse(54, 55, 6, 4.5, scale);
            p.Dot(56, 54, Socket);
            return;
        }
        double rear = f == SpriteFrame.Windup ? -6 : f == SpriteFrame.Strike ? 3 : 0;
        bool back = v == SpriteView.Back;
        // The coils on the floor, then the rising body.
        p.Mat = Material.Gloss;
        p.Ellipse(32, 57, 25, 6.5, scale);
        p.Ellipse(19, 53, 11, 5, dark);
        p.Limb(52, 58, 60, 55, 62, 49, 3, 0.6, scale);                                                     // the tail tip
        p.Taper(32, 56, 6.5 * s, 32, 30 + rear, 5.5 * s, scale);
        p.Mat = Material.Matte;
        if (!back) for (int i = 0; i < 6; i++) p.Rect(29, 32 + rear + i * 4, 35, 34.5 + rear + i * 4, belly);  // belly scales
        // A torso with arms, cloth over it.
        p.Mat = Material.Cloth;
        p.Poly(trim == C(220, 200, 120) ? C(70, 50, 90) : C(40, 60, 80), (32 - 8 * s, 20 + rear), (32 + 8 * s, 20 + rear), (32 + 6 * s, 34 + rear), (32 - 6 * s, 34 + rear));
        p.Mat = Material.Matte;
        p.Limb(32 - 8 * s, 22 + rear, 22, 30 + rear, 18, 24 + rear - (f == SpriteFrame.Windup ? 6 : 0), 2.2, 1.6, skin);
        p.Limb(32 + 8 * s, 22 + rear, 42, 30 + rear, 46, 24 + rear - (f == SpriteFrame.Windup ? 6 : 0), 2.2, 1.6, skin);
        if (f == SpriteFrame.Windup) { p.Ellipse(18, 22 + rear - 6, 3, 3, Paint.Shade(trim, -60), false); p.Ellipse(18, 22 + rear - 6, 1.8, 1.8, C(200, 255, 150), false); }
        // The hooded head.
        p.Mat = Material.Gloss;
        p.Ellipse(32, 14 + rear, 12 * s, 9, dark);                                                         // the hood
        p.Ellipse(32, 14 + rear, 6.5 * s, 7, scale);
        p.Mat = Material.Matte;
        if (!back)
        {
            for (int i = -1; i <= 1; i += 2) p.Ellipse(32 + i * 8 * s, 14 + rear, 2.2, 3.4, trim, false);      // the hood's markings
            p.Ellipse(29, 13 + rear, 1.6, 1.1, trim, false); p.Ellipse(35, 13 + rear, 1.6, 1.1, trim, false);
            p.Dot(29, 13 + rear, Socket); p.Dot(35, 13 + rear, Socket);
            if (f != SpriteFrame.Idle) { p.Taper(32, 18 + rear, 0.5, 32, 22 + rear, 0.5, C(210, 40, 50)); p.Taper(32, 22 + rear, 0.4, 30, 24 + rear, 0.3, C(210, 40, 50)); p.Taper(32, 22 + rear, 0.4, 34, 24 + rear, 0.3, C(210, 40, 50)); }
        }
        if (crown)
        {
            p.Mat = Material.Metal;
            p.Rect(25, 4 + rear, 39, 7 + rear, trim);
            p.Triangle(25, 4 + rear, 27, -1 + rear, 29, 4 + rear, trim); p.Triangle(30, 4 + rear, 32, -3 + rear, 34, 4 + rear, trim); p.Triangle(35, 4 + rear, 37, -1 + rear, 39, 4 + rear, trim);
            p.Mat = Material.Matte;
            p.Dot(32, 5, C(120, 220, 255));
        }
    }

    private static void Tentacles(Paint p, SpriteFrame f)
    {
        uint skin = C(116, 112, 128), dark = C(64, 58, 76), mouth = C(150, 40, 60), sucker = C(200, 170, 180);
        if (f == SpriteFrame.Dead)
        {
            p.Mat = Material.Gloss;
            p.Ellipse(32, 58, 22, 6, skin);
            p.Mat = Material.Matte;
            for (int i = 0; i < 4; i++) p.Limb(14 + i * 12, 59, 8 + i * 14, 55, 4 + i * 16, 59, 2.4, 0.8, dark);
            return;
        }
        double lift = f == SpriteFrame.Windup ? -10 : f == SpriteFrame.Strike ? 6 : 0;
        p.Mat = Material.Gloss;
        for (int i = 0; i < 6; i++)
        {
            double a = i / 5.0, tipX = 4 + a * 56, tipY = 8 + lift + (i % 2) * 10;
            double midX = 32 + (tipX - 32) * 0.55 + (i % 2 == 0 ? -5 : 5), midY = 36 + lift / 2;
            p.Limb(32, 46, midX, midY, tipX, tipY, 3.8, 0.7, i % 2 == 0 ? skin : Paint.Shade(skin, -14));
            p.Mat = Material.Matte;
            for (int k = 1; k < 4; k++)
            {
                double t = k / 4.0;
                p.Dot(midX + (tipX - midX) * t + 1, midY + (tipY - midY) * t, sucker);
            }
            p.Mat = Material.Gloss;
        }
        p.Ellipse(32, 50, 17, 14, skin);
        p.Mat = Material.Matte;
        p.Ellipse(32, 50, 9, f == SpriteFrame.Idle ? 4.5 : 7.5, mouth, false);
        p.Ellipse(32, 50, 6, f == SpriteFrame.Idle ? 2.5 : 5, Socket, false);
        for (int i = 0; i < 6; i++) p.Triangle(25 + i * 2.6, 46 - (f == SpriteFrame.Idle ? 0 : 3), 27.6 + i * 2.6, 46 - (f == SpriteFrame.Idle ? 0 : 3), 26.3 + i * 2.6, 49, Tooth);
        p.Ellipse(22, 42, 2.2, 2.2, C(255, 230, 120), false); p.Ellipse(42, 42, 2.2, 2.2, C(255, 230, 120), false);
        p.Dot(22, 42, Socket); p.Dot(42, 42, Socket);
    }

    private static void Mimic(Paint p, SpriteFrame f)
    {
        uint wood = C(124, 86, 50), iron = C(84, 84, 90), gold = C(200, 166, 70);
        if (f == SpriteFrame.Dead) { p.Mat = Material.Stone; p.Rect(10, 46, 54, 63, wood); p.Mat = Material.Metal; p.Rect(10, 44, 54, 48, iron); p.Mat = Material.Matte; return; }
        double open = f == SpriteFrame.Windup ? 14 : f == SpriteFrame.Strike ? 5 : 0;
        p.Mat = Material.Stone;
        p.Rect(10, 40, 54, 63, wood);
        p.Mat = Material.Matte;
        for (int i = 0; i < 4; i++) p.Taper(12, 45 + i * 5, 0.3, 52, 45 + i * 5, 0.3, Paint.Shade(wood, -26), false); // planks
        p.Mat = Material.Metal;
        p.Rect(10, 40, 54, 43, iron); p.Rect(14, 40, 18, 63, iron); p.Rect(46, 40, 50, 63, iron);
        p.Mat = Material.Stone;
        p.Poly(wood, (8, 40 - open), (8, 30 - open), (12, 24 - open), (52, 24 - open), (56, 30 - open), (56, 40 - open));
        p.Mat = Material.Metal;
        p.Rect(8, 24 - open, 56, 27 - open, iron);
        p.Mat = Material.Matte;
        if (open > 0)
        {
            p.Rect(12, 40 - open, 52, 40, C(120, 20, 30), false);
            for (int i = 0; i < 7; i++)
            {
                p.Triangle(12 + i * 6, 40 - open, 15 + i * 6, 46 - open, 18 + i * 6, 40 - open, Tooth);
                p.Triangle(12 + i * 6, 40, 15 + i * 6, 35, 18 + i * 6, 40, Tooth);
            }
            p.Limb(32, 40, 36 + open / 2, 44, 32 + open, 47 + open / 2, 2.6, 1.6, C(200, 80, 100));          // the tongue
            p.Ellipse(20, 30 - open, 2.6, 2, C(255, 230, 120), false); p.Ellipse(44, 30 - open, 2.6, 2, C(255, 230, 120), false);
            p.Dot(20, 30 - open, Socket); p.Dot(44, 30 - open, Socket);
        }
        p.Mat = Material.Metal;
        p.Rect(29, 43, 35, 50, gold);
        p.Mat = Material.Matte;
        p.Ellipse(32, 46.5, 1, 1.4, Socket, false);
    }

    /// <summary>What sits on a person's head or around them: crowns, haloes, helms.</summary>
    private static void Extras(Paint p, string sprite, Look k)
    {
        double s = k.Height, top = 63 - 58 * s, headY = top + 6.5 * s;
        switch (sprite)
        {
            case "king":
                p.Mat = Material.Metal;
                p.Rect(32 - 6.5 * s, headY - 7.5 * s, 32 + 6.5 * s, headY - 4.5 * s, k.Trim);
                for (int i = 0; i < 3; i++) p.Triangle(32 - 6.5 * s + i * 4.8 * s, headY - 7.5 * s, 32 - 4.2 * s + i * 4.8 * s, headY - 12 * s, 32 - 1.9 * s + i * 4.8 * s, headY - 7.5 * s, k.Trim);
                p.Mat = Material.Matte;
                p.Dot(32, headY - 6.5 * s, C(200, 40, 60));
                break;
            case "saint":
            case "singer":
                p.Ellipse(32, headY - 10 * s, 8 * s, 2 * s, C(250, 230, 140), false);
                p.Ellipse(32, headY - 10 * s, 5.5 * s, 0.9 * s, Paint.Shade(k.Clothes, -60), false);
                break;
            case "chief":
                p.Mat = Material.Metal;
                p.Rect(32 - 6 * s, headY - 6.5 * s, 32 + 6 * s, headY - 4 * s, k.Trim);
                p.Triangle(32 - 3 * s, headY - 6.5 * s, 32, headY - 11 * s, 32 + 3 * s, headY - 6.5 * s, k.Trim);
                p.Mat = Material.Matte;
                p.Taper(32 + 5 * s, headY - 6 * s, 1, 32 + 9 * s, headY - 14 * s, 0.5, C(200, 60, 40)); // a feather
                break;
            case "archivist":
            {
                // Four more arms fanning out behind it, each with a book, a quill, a seal or a ledger.
                for (int i = 0; i < 4; i++)
                {
                    double side = i % 2 == 0 ? -1 : 1, lift = i < 2 ? -10 : 0;
                    double sx = 32 + side * 7 * s, sy = top + 20 * s + i / 2 * 4;
                    double hx = 32 + side * (20 + i / 2 * 4) * s, hy = top + (14 + lift) * s;
                    p.Limb(sx, sy, 32 + side * 15 * s, top + (24 + lift / 2) * s, hx, hy, 2 * s, 1.4 * s, k.Skin);
                    p.Ellipse(hx, hy, 2 * s, 2 * s, k.Skin);
                    if (i == 0) { p.Rect(hx - 4, hy - 6, hx + 4, hy, C(80, 30, 30)); p.Rect(hx - 3, hy - 5, hx + 3, hy - 1, C(230, 220, 190)); }
                    else if (i == 1) p.Taper(hx, hy, 0.6, hx + 4, hy - 8, 0.3, C(240, 240, 230));
                    else if (i == 2) p.Ellipse(hx, hy - 2, 3, 3, C(190, 40, 40));
                    else p.Rect(hx - 3, hy - 7, hx + 3, hy, C(60, 70, 110));
                }
                // Spectacles, and a candle stuck in its cowl.
                p.Ellipse(29, headY + 0.5, 2.2, 2.2, C(200, 230, 255), false); p.Ellipse(35, headY + 0.5, 2.2, 2.2, C(200, 230, 255), false);
                p.Rect(30.4, headY - 14 * s, 33.6, headY - 7 * s, C(230, 220, 190));
                p.Ellipse(32, headY - 16 * s, 1.6, 2.6, C(255, 200, 90), false);
                break;
            }
            case "knight":
                p.Mat = Material.Metal;
                p.Ellipse(32, headY - 0.5 * s, 7 * s, 7.6 * s, k.Clothes);
                p.Rect(32 - 7 * s, headY - 0.5 * s, 32 + 7 * s, headY + 7 * s, k.Clothes);
                p.Mat = Material.Matte;
                p.Rect(32 - 5 * s, headY - 1.2 * s, 32 + 5 * s, headY + 0.6 * s, Socket, false);                     // the visor's slit
                p.Dot(32 - 2.5 * s, headY - 0.6 * s, k.Eye); p.Dot(32 + 2.5 * s, headY - 0.6 * s, k.Eye);
                p.Taper(32, headY + 1.5 * s, 0.5, 32, headY + 6 * s, 0.5, Paint.Shade(k.Clothes, -40), false);
                p.Taper(32, headY - 8 * s, 1.6, 32, headY - 14 * s, 0.8, C(150, 30, 30));                            // the plume
                break;
        }
    }
}
