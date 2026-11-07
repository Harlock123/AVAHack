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
            list.Add(new SpriteInstance(p.X, p.Y, 0.42, 0.12, 0.12, art.Stone()));
        return list;
    }
}
