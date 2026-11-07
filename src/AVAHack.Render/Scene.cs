using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Render;

/// <summary>Turns the engine's creatures and missiles into sprites for the view.</summary>
public static class Scene
{
    /// <summary>World height of each creature's picture (the picture is square).</summary>
    public static double SizeOf(string sprite) => sprite switch
    {
        "rat" => 0.42,
        "spider" => 0.5,
        "swarm" or "mushroom" or "wisp" or "crawler" or "slimeGrey" or "bat" => 0.55,
        "moths" or "imp" or "beetle" => 0.7,
        _ => 0.92,
    };

    public static List<SpriteInstance> Build(GameEngine g, CameraPose cam, SpriteFactory art, double alpha = 0)
    {
        var list = new List<SpriteInstance>();
        double rx = -cam.DirY, ry = cam.DirX; // the camera's right
        foreach (var m in g.Monsters)
        {
            if (m.FellInPit || m.HiddenFrom(g.PartyCell)) continue;
            var (x, y) = m.Position(alpha);
            var frame = !m.IsAlive ? SpriteFrame.Dead
                : m.Action == MonsterAction.WindingUp ? SpriteFrame.Windup
                : m.StrikeTime > 0 ? SpriteFrame.Strike
                : SpriteFrame.Idle;

            // Which side of the creature the camera sees.
            double vx = cam.X - x, vy = cam.Y - y, len = Math.Sqrt(vx * vx + vy * vy);
            if (len < 1e-6) continue;
            vx /= len; vy /= len;
            double fx = m.Facing.Dx(), fy = m.Facing.Dy();
            double dot = fx * vx + fy * vy;
            var view = dot > 0.38 ? SpriteView.Front : dot < -0.38 ? SpriteView.Back : SpriteView.Side;
            bool mirror = view == SpriteView.Side && fx * rx + fy * ry < 0;
            if (frame == SpriteFrame.Dead) mirror = m.Id % 2 == 0;

            double size = SizeOf(m.Def.Sprite) * m.Def.Size switch { Content.CreatureSize.Large => 1.18, _ => 1 };
            double glow = frame == SpriteFrame.Windup ? 0.25 + 0.25 * Math.Sin(m.ActionProgress * Math.PI * 4) : 0;
            // Elites are tinted gold, champions violet and glowing (spec 12.2); a hit flashes red over either.
            (uint tint, double amount) = m.HurtFlash > 0 ? (0xFFFF2010u, 0.6)
                : !m.IsAlive ? (0u, 0)
                : m.Title is not null && m.Affixes.Count > 0 ? (0xFFB060FFu, 0.28)
                : m.Elite ? (0xFFFFC840u, 0.22) : (0u, 0);
            if (m.IsAlive && m.Affixes.Count > 0 && m.Title is not null) glow = Math.Max(glow, 0.15);
            list.Add(new SpriteInstance(x, y, 0, size, size, art.Get(m.Def.Sprite, view, frame, mirror),
                Tint: tint, TintAmount: amount, Glow: glow));
        }
        foreach (var p in g.Projectiles)
            list.Add(p.Look == "stone"
                ? new SpriteInstance(p.X, p.Y, 0.42, 0.12, 0.12, art.Stone())
                : new SpriteInstance(p.X, p.Y, 0.36, 0.24, 0.24, art.Bolt(p.Look), SelfLit: true));
        foreach (var (cell, kind) in g.Level.Stations)
        {
            if (kind == "candle")
            {
                bool lit = g.CandleLit(cell);
                list.Add(new SpriteInstance(cell.X + 0.5, cell.Y + 0.5, 0, 0.7, 0.7, art.Prop(lit ? "candle" : "candle-out"), Glow: lit ? 0.5 : 0, SelfLit: lit));
                continue;
            }
            if (kind is "pod" or "roots")
            {
                string look = kind == "pod" ? (g.PodWhole(cell) ? "pod" : "pod-burst") : g.RootsStand(cell) ? "roots" : "roots-cut";
                double size = kind == "roots" ? 1.0 : 0.75;
                list.Add(new SpriteInstance(cell.X + 0.5, cell.Y + 0.5, 0, size, size, art.Prop(look), Glow: look == "pod" ? 0.1 : 0));
                continue;
            }
            list.Add(new SpriteInstance(cell.X + 0.5, cell.Y + 0.5, 0, 0.8, 0.8, art.Prop(kind == "anvil" ? "anvil" : "altar-enchant"), Glow: 0.15));
        }
        if (g.Level.Waystone is { } ws)
            list.Add(new SpriteInstance(ws.X + 0.5, ws.Y + 0.5, 0, 0.95, 0.95, art.Prop("waystone"), Glow: 0.2));
        foreach (var t in g.Level.Traps)
        {
            if (t.Kind == World.TrapKind.CursedAltar)
                list.Add(new SpriteInstance(t.Cell.X + 0.5, t.Cell.Y + 0.5, 0, 0.7, 0.7, art.Prop("altar")));
            else if (t.Kind == World.TrapKind.SwingingBlade && t.Armed && t.Active)
                list.Add(new SpriteInstance(t.Cell.X + 0.5, t.Cell.Y + 0.5, 0.25, 0.9, 0.9, art.Prop("blade")));
        }
        foreach (var b in g.Boulders)
            list.Add(new SpriteInstance(b.X, b.Y, 0, 0.85, 0.85, art.Prop("boulder")));
        foreach (var cloud in g.Gas)
            foreach (var cell in cloud.Cells.Where(c => !g.Level.LooksLikeWall(c.X, c.Y)))
                list.Add(new SpriteInstance(cell.X + 0.5, cell.Y + 0.5, 0, 0.9, 0.9, art.Prop(cloud.Variant switch { "sleep" => "gasSleep", "spores" => "gasSpores", _ => "gasPoison" }), Glow: 0.3));
        foreach (var h in g.Hazards)
            list.Add(new SpriteInstance(h.Cell.X + 0.5, h.Cell.Y + 0.5, 0, 0.5, 0.5, art.Bolt(h.Type == Rules.DamageType.Acid ? "acid" : "fire"), SelfLit: true));
        foreach (var c in g.Level.Chests)
            list.Add(new SpriteInstance(c.Cell.X + 0.5, c.Cell.Y + 0.5, 0, 0.62, 0.62, art.Chest(c.Opened)));
        foreach (var (cell, items) in g.Level.Piles)
        {
            for (int i = 0; i < Math.Min(items.Count, 4); i++)
            {
                double ox = (i & 1) == 0 ? -0.15 : 0.15, oy = (i & 2) == 0 ? -0.12 : 0.15;
                list.Add(new SpriteInstance(cell.X + 0.5 + ox, cell.Y + 0.5 + oy, 0, 0.2, 0.2, art.ItemIcon(IconKind(g, items[i]))));
            }
        }
        return list;
    }

    /// <summary>The icon key for an item (<see cref="SpriteFactory.IconFor"/>).</summary>
    public static string IconKind(GameEngine g, AVAHack.Items.Item item) => SpriteFactory.IconFor(g.Content, item);
}
