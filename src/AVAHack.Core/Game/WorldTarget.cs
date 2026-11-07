using AVAHack.World;

namespace AVAHack.Game;

/// <summary>Something in the world the player clicked on in the view.</summary>
public abstract record WorldTarget;

/// <summary>The <paramref name="Face"/> wall of floor cell (<paramref name="X"/>, <paramref name="Y"/>).</summary>
public sealed record WallFaceTarget(int X, int Y, Dir Face) : WorldTarget;

/// <summary>The door in cell (<paramref name="X"/>, <paramref name="Y"/>).</summary>
public sealed record DoorTarget(int X, int Y) : WorldTarget;
