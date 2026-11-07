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
        PhysicalKey.G => GameAction.PickUp,
        PhysicalKey.X => GameAction.Sneak,
        PhysicalKey.R => GameAction.Rest,
        _ => null,
    };

    /// <summary>
    /// Keys 1–8 are the heroes' hands, held and released: 1 and 2 are the first hero's left and right, 3 and 4 the
    /// second's, and so on (spec 16.2).
    /// </summary>
    public static (int Hero, HandSide Side)? MapHand(PhysicalKey key) => key switch
    {
        PhysicalKey.Digit1 => (0, HandSide.Left),
        PhysicalKey.Digit2 => (0, HandSide.Right),
        PhysicalKey.Digit3 => (1, HandSide.Left),
        PhysicalKey.Digit4 => (1, HandSide.Right),
        PhysicalKey.Digit5 => (2, HandSide.Left),
        PhysicalKey.Digit6 => (2, HandSide.Right),
        PhysicalKey.Digit7 => (3, HandSide.Left),
        PhysicalKey.Digit8 => (3, HandSide.Right),
        _ => null,
    };

    /// <summary>Ctrl+1–4 moves the selected hero to that slot.</summary>
    public static Characters.Slot? MapSlot(PhysicalKey key) => key switch
    {
        PhysicalKey.Digit1 => Characters.Slot.FrontLeft,
        PhysicalKey.Digit2 => Characters.Slot.FrontRight,
        PhysicalKey.Digit3 => Characters.Slot.BackLeft,
        PhysicalKey.Digit4 => Characters.Slot.BackRight,
        _ => null,
    };
}
