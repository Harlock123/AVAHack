using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Render;

/// <summary>
/// Draws the party's first-person view into a fixed-size framebuffer of 0xAARRGGBB pixels. The App scales that
/// buffer to the window. Another back end (OpenGL, say) can stand in for the software one behind this interface.
/// </summary>
public interface IViewRenderer
{
    int Width { get; }
    int Height { get; }
    uint[] Pixels { get; }

    /// <summary>Draws the level as seen from <paramref name="camera"/>. <paramref name="time"/> drives torch flicker.</summary>
    void Render(Level level, CameraPose camera, double time);

    /// <summary>What the last frame showed at framebuffer pixel (x, y): a wall face, a door, or nothing.</summary>
    WorldTarget? Pick(int x, int y);
}
