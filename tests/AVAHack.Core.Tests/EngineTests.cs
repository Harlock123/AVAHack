using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

public sealed class EngineTests
{
    private static GameEngine New() => new(LevelLoader.LoadBuiltIn("test-hall"));

    private static void Run(GameEngine g, int ticks)
    {
        for (int i = 0; i < ticks; i++) g.Tick();
    }

    [Fact]
    public void A_step_moves_one_cell_and_takes_a_quarter_second()
    {
        var g = New();
        g.Press(GameAction.StepForward);
        Assert.Equal(new Cell(3, 2), g.PartyCell); // the party holds the new cell from the start of the step
        Assert.True(g.IsMoving);
        Run(g, GameEngine.StepTicks - 1);
        Assert.True(g.IsMoving);
        Run(g, 1);
        Assert.False(g.IsMoving);
        Assert.Equal(15, GameEngine.StepTicks);
        Assert.Equal(new CameraPose(3.5, 2.5, 0), g.Camera());
    }

    [Fact]
    public void The_camera_glides_between_cells()
    {
        var g = New();
        g.HeadBob = false;
        g.Press(GameAction.StepForward);
        Run(g, 7);
        var mid = g.Camera(0.5);
        Assert.InRange(mid.X, 2.9, 3.1); // halfway from 2.5 to 3.5
        Assert.Equal(2.5, mid.Y);
        Assert.True(g.Camera(0).X < g.Camera(0.9).X);
    }

    [Fact]
    public void Head_bob_dips_the_eye_mid_step()
    {
        var g = New();
        g.Press(GameAction.StepForward);
        Run(g, 7);
        Assert.InRange(g.Camera(0.5).Bob, -GameEngine.BobHeight, -GameEngine.BobHeight * 0.9);
        Run(g, 8);
        Assert.Equal(0, g.Camera().Bob);
    }

    [Fact]
    public void Turning_takes_a_fifth_of_a_second_and_eases_the_yaw()
    {
        var g = New();
        g.Press(GameAction.TurnRight);
        Assert.Equal(Dir.South, g.Facing);
        Run(g, 6);
        double yaw = g.Camera().Yaw;
        Assert.InRange(yaw, 0.6, 1.0); // about halfway from 0 to π/2
        Run(g, 6);
        Assert.False(g.IsMoving);
        Assert.Equal(Math.PI / 2, g.Camera().Yaw, 9);
        g.Press(GameAction.TurnLeft);
        g.Press(GameAction.TurnLeft);
        Run(g, 24);
        Assert.Equal(Dir.North, g.Facing);
    }

    [Fact]
    public void Strafing_and_backing_keep_the_facing()
    {
        var g = New();
        g.Press(GameAction.StrafeRight); // facing east, so right is south
        Run(g, 15);
        Assert.Equal(new Cell(2, 3), g.PartyCell);
        g.Press(GameAction.StepBack);
        Run(g, 15);
        Assert.Equal(new Cell(1, 3), g.PartyCell);
        g.Press(GameAction.StrafeLeft);
        Run(g, 15);
        Assert.Equal(new Cell(1, 2), g.PartyCell);
        Assert.Equal(Dir.East, g.Facing);
    }

    [Fact]
    public void Walls_stop_the_party_with_a_bump_and_drop_what_was_queued()
    {
        var g = New();
        g.Stand(new Cell(1, 2), Dir.North);
        var events = new List<GameEvent>();
        g.Event += events.Add;
        g.Press(GameAction.StepForward); // to (1,1)
        g.Press(GameAction.StepForward); // into the wall at (1,0)
        g.Press(GameAction.TurnRight);   // queued behind the bump: dropped
        Run(g, GameEngine.StepTicks);
        Assert.Equal(new Cell(1, 1), g.PartyCell);
        Assert.Equal([GameEvent.Stepped, GameEvent.Bumped], events);
        Assert.Equal(0, g.Queued);
        Run(g, GameEngine.BumpTicks);
        Assert.False(g.IsMoving);
        Assert.Equal(Dir.North, g.Facing);
        Assert.Equal(new CameraPose(1.5, 1.5, Dir.North.Yaw()), g.Camera());
    }

    [Fact]
    public void Presses_queue_two_deep_while_moving()
    {
        var g = New();
        g.Press(GameAction.StepForward);
        g.Press(GameAction.TurnRight);
        g.Press(GameAction.StepForward);
        g.Press(GameAction.StepForward); // dropped: the queue is full
        Assert.Equal(GameEngine.QueueLimit, g.Queued);
        Run(g, 120);
        Assert.Equal(new Cell(3, 3), g.PartyCell);
        Assert.Equal(Dir.South, g.Facing);
    }

    [Fact]
    public void Update_runs_whole_ticks_and_keeps_the_remainder()
    {
        var g = New();
        g.Update(0.1);
        Assert.Equal(6, g.TickCount);
        g.Update(1.0 / 120);
        Assert.Equal(6, g.TickCount);
        Assert.InRange(g.Alpha, 0.49, 0.51);
        g.Update(10); // a long stall runs at most a quarter second of ticks
        Assert.True(g.TickCount <= 6 + 16);
    }

    [Fact]
    public void A_hand_door_opens_in_a_second_and_lets_the_party_through()
    {
        var g = New();
        g.Stand(new Cell(4, 3), Dir.East);
        var door = g.Level.DoorAt(5, 3)!;
        g.Press(GameAction.StepForward);
        Assert.Equal(new Cell(4, 3), g.PartyCell); // shut: bump
        Run(g, GameEngine.BumpTicks);

        g.Press(GameAction.Use);
        Run(g, 30);
        Assert.InRange(door.Open, 0.45, 0.55);
        Assert.False(door.IsPassable);
        Run(g, 30);
        Assert.True(door.IsPassable);
        g.Press(GameAction.StepForward);
        g.Press(GameAction.StepForward);
        Run(g, 40);
        Assert.Equal(new Cell(6, 3), g.PartyCell);
    }

    [Fact]
    public void A_door_will_not_close_on_the_party()
    {
        var g = New();
        var door = g.Level.DoorAt(5, 3)!;
        g.Stand(new Cell(4, 3), Dir.East);
        g.Press(GameAction.Use);
        Run(g, 60);
        g.Press(GameAction.StepForward);
        Run(g, 15);
        Assert.Equal(door.Cell, g.PartyCell);

        // Clicking the door from inside it is refused while the party stands there.
        Assert.False(g.ClickWorld(new DoorTarget(5, 3)));
        Assert.True(door.Opening);

        g.Press(GameAction.StepForward);
        Run(g, 15);
        Assert.True(g.ClickWorld(new DoorTarget(5, 3)));
        Run(g, 60);
        Assert.True(door.IsShut);
    }

    [Fact]
    public void A_portcullis_ignores_hands_but_answers_its_lever()
    {
        var g = New();
        var gate = g.Level.DoorAt(7, 6)!;
        g.Stand(new Cell(6, 6), Dir.East);
        string? said = null;
        g.Message += m => said = m;
        g.Press(GameAction.Use);
        Run(g, 60);
        Assert.True(gate.IsShut);
        Assert.Contains("mechanism", said);

        g.Stand(new Cell(6, 4), Dir.West);
        g.Press(GameAction.Use);
        var lever = Assert.Single(g.Level.Levers);
        Assert.True(lever.Down);
        Run(g, 60);
        Assert.True(gate.IsPassable);

        // The lever is also worked by clicking it, but only from its own cell.
        Assert.True(g.ClickWorld(new WallFaceTarget(6, 4, Dir.West)));
        Assert.False(lever.Down);
        g.Stand(new Cell(6, 5), Dir.West);
        Assert.False(g.ClickWorld(new WallFaceTarget(6, 4, Dir.West)));
        Run(g, 60);
        Assert.True(gate.IsShut);
    }

    [Fact]
    public void Doors_out_of_reach_ignore_clicks()
    {
        var g = New();
        g.Stand(new Cell(3, 3), Dir.East); // two cells from the door
        Assert.False(g.ClickWorld(new DoorTarget(5, 3)));
        Assert.False(g.Level.DoorAt(5, 3)!.Opening);
    }

    [Fact]
    public void The_same_presses_give_the_same_run()
    {
        static string Play()
        {
            var g = New();
            var keys = new[] { GameAction.StepForward, GameAction.TurnRight, GameAction.StepForward, GameAction.StrafeLeft, GameAction.Use };
            var trace = new List<string>();
            for (int i = 0; i < 300; i++)
            {
                if (i % 13 == 0) g.Press(keys[i / 13 % keys.Length]);
                g.Tick();
                var c = g.Camera();
                trace.Add($"{c.X:F4},{c.Y:F4},{c.Yaw:F4},{c.Bob:F4}");
            }
            return string.Join(';', trace);
        }
        Assert.Equal(Play(), Play());
    }
}
