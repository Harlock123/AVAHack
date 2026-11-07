namespace AVAHack.Game;

/// <summary>What the player asks the party to do. Keys and buttons map to these in the App's InputHandler.</summary>
public enum GameAction
{
    StepForward,
    StepBack,
    StrafeLeft,
    StrafeRight,
    TurnLeft,
    TurnRight,
    /// <summary>Use what is in front: pull a lever on the wall ahead, or open or close the door ahead.</summary>
    Use,
}
