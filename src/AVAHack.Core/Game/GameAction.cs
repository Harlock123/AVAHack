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
    /// <summary>Pick up everything lying in the party's cell.</summary>
    PickUp,
    /// <summary>Start or stop sneaking (spec 11.1).</summary>
    Sneak,
    /// <summary>Rest until healed, or until something comes (spec 13.7).</summary>
    Rest,
}
