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
        _ => 0.92,
    };

    public static List<SpriteInstance> Build(GameEngine g, CameraPose cam, SpriteFactory art, double alpha = 0)
    {
        var list = new List<SpriteInstance>();
        double rx = -cam.DirY, ry = cam.DirX; // the camera's right
        foreach (var m in g.Monsters)
        {
            if (m.FellInPit) continue;
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

            double size = SizeOf(m.Def.Sprite);
            double glow = frame == SpriteFrame.Windup ? 0.25 + 0.25 * Math.Sin(m.ActionProgress * Math.PI * 4) : 0;
            list.Add(new SpriteInstance(x, y, 0, size, size, art.Get(m.Def.Sprite, view, frame, mirror),
                Tint: 0xFFFF2010, TintAmount: m.HurtFlash > 0 ? 0.6 : 0, Glow: glow));
        }
        foreach (var p in g.Projectiles)
            list.Add(p.Look == "stone"
                ? new SpriteInstance(p.X, p.Y, 0.42, 0.12, 0.12, art.Stone())
                : new SpriteInstance(p.X, p.Y, 0.36, 0.24, 0.24, art.Bolt(p.Look), SelfLit: true));
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
                list.Add(new SpriteInstance(cell.X + 0.5, cell.Y + 0.5, 0, 0.9, 0.9, art.Prop(cloud.Variant == "sleep" ? "gasSleep" : "gasPoison"), Glow: 0.3));
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

    public static string IconKind(GameEngine g, AVAHack.Items.Item item)
    {
        var b = g.Content.Base(item.BaseId);
        return b.Kind switch
        {
            Content.ItemKind.Weapon => "weapon",
            Content.ItemKind.Shield => "shield",
            Content.ItemKind.Armour => "armour",
            Content.ItemKind.Consumable when b.Consumable?.Heal is not null || b.Consumable?.Mana > 0 || b.Consumable?.Stamina > 0 => "potion",
            Content.ItemKind.Book or Content.ItemKind.Tome or Content.ItemKind.Scroll => "book",
            Content.ItemKind.Gold => "gold",
            _ => "other",
        };
    }
}
