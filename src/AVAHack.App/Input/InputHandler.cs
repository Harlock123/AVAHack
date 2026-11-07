using Avalonia.Input;
using AVAHack.Game;

namespace AVAHack.App.Input;

/// <summary>
/// The one place keys become game actions. Keys are matched by <see cref="PhysicalKey"/> (where the key sits),
/// so WASD works on any keyboard layout, and a key that reports itself oddly on release can't confuse us.
/// </summary>
public static class InputHandler
{
    public static GameAction? Map(PhysicalKey key) => key switch
    {
        PhysicalKey.W or PhysicalKey.ArrowUp or PhysicalKey.NumPad8 => GameAction.StepForward,
        PhysicalKey.S or PhysicalKey.ArrowDown or PhysicalKey.NumPad2 => GameAction.StepBack,
        PhysicalKey.A or PhysicalKey.NumPad4 => GameAction.StrafeLeft,
        PhysicalKey.D or PhysicalKey.NumPad6 => GameAction.StrafeRight,
        PhysicalKey.Q or PhysicalKey.ArrowLeft or PhysicalKey.NumPad7 => GameAction.TurnLeft,
        PhysicalKey.E or PhysicalKey.ArrowRight or PhysicalKey.NumPad9 => GameAction.TurnRight,
        PhysicalKey.F or PhysicalKey.NumPad5 or PhysicalKey.Enter => GameAction.Use,
        _ => null,
    };
}
