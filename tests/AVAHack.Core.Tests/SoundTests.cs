using AVAHack.Game;
using AVAHack.World;

namespace AVAHack.Core.Tests;

/// <summary>Sound (spec 16.3): which cues the engine raises, and how they reach the party.</summary>
public sealed class SoundTests
{
    // A corridor running east from the party with a hand door at x = 6; below it a passage round to (3,3), and
    // past another wall a sealed room.
    private const string Map = """
        start: 2 1 east
        map:
        ############
        #.....+....#
        #.##########
        #...#......#
        ############
        """;

    private static GameEngine Make() => new(LevelLoader.Parse(Map), 1);

    private static SoundCue At(int x, int y) => new("bark.goblin", x + 0.5, y + 0.5);

    [Fact]
    public void A_sound_fades_with_the_steps_it_has_to_travel()
    {
        var g = Make();
        double near = g.Hearing(At(3, 1)).Gain, far = g.Hearing(At(5, 1)).Gain;
        Assert.True(near > far && far > 0, $"near {near}, far {far}");
        Assert.Equal(1, g.Hearing(At(2, 1)).Gain, 6);                                  // in the party's own cell
    }

    [Fact]
    public void A_shut_door_muffles_it_and_an_open_one_does_not()
    {
        var g = Make();
        double shut = g.Hearing(At(7, 1)).Gain;
        double open = shut / GameEngine.DoorMuffle;
        Assert.True(shut > 0);
        g.Level.DoorAt(new Cell(6, 1))!.Open = 1;
        Assert.Equal(open, g.Hearing(At(7, 1)).Gain, 6);
    }

    [Fact]
    public void Sound_goes_round_by_the_passages_not_through_rock()
    {
        var g = Make();
        // (3,3) is two cells away as the crow flies, but six steps round by the passage.
        double roundabout = g.Hearing(At(3, 3)).Gain;
        Assert.True(roundabout > 0 && roundabout < g.Hearing(At(4, 1)).Gain);
        Assert.Equal(0, g.Hearing(At(6, 3)).Gain);                                     // the sealed room: rock all round
        // Past the hearing range it is silent.
        var far = new GameEngine(LevelLoader.Parse("start: 1 1 east\nmap:\n" + new string('#', 22) + "\n#" + new string('.', 20) + "#\n" + new string('#', 22)), 1);
        Assert.Equal(0, far.Hearing(At(20, 1)).Gain);
    }

    [Fact]
    public void A_sound_is_panned_to_the_side_it_comes_from()
    {
        var g = Make();
        g.Stand(new Cell(1, 1), Dir.North);
        Assert.True(g.Hearing(At(3, 1)).Pan > 0.3);  // facing north, the corridor east is on the right
        g.Stand(new Cell(1, 1), Dir.South);
        Assert.True(g.Hearing(At(3, 1)).Pan < -0.3); // and on the left facing south
        g.Stand(new Cell(1, 1), Dir.East);
        Assert.Equal(0, g.Hearing(At(3, 1)).Pan, 6);  // straight ahead
        Assert.Equal(0, g.Hearing(new SoundCue("step", 0, 0, 0.7, Local: true)).Pan);
    }

    [Fact]
    public void Steps_blows_and_cries_raise_cues()
    {
        var g = Make();
        var heard = new List<string>();
        g.Sound += c => heard.Add(c.Id);
        g.Press(GameAction.StepForward);
        for (int i = 0; i < 60; i++) g.Tick();
        Assert.Contains("step", heard);
        var rat = g.AddMonster("cellarRat", new Cell(5, 1), Dir.West);
        for (int i = 0; i < 60 * 20 && !heard.Contains("hero.hurt"); i++)
        {
            if (g.RightHand.Ready) { g.HandDown(HandSide.Right); g.HandUp(HandSide.Right); }
            g.Tick();
        }
        Assert.Contains("bark.vermin", heard);
        Assert.Contains(heard, id => id is "hit.flesh" or "hit.crit" or "miss");
        Assert.Contains(heard, id => id is "swing" or "hero.hurt" or "die.flesh");
    }

    [Fact]
    public void Doors_and_levers_sound_where_they_are()
    {
        var g = Make();
        var cues = new List<SoundCue>();
        g.Sound += cues.Add;
        g.Stand(new Cell(5, 1), Dir.East);
        g.Press(GameAction.Use);
        for (int i = 0; i < 5; i++) g.Tick();
        var open = Assert.Single(cues, c => c.Id == "door.open");
        Assert.Equal((6.5, 1.5), (open.X, open.Y));
    }

    [Fact]
    public void Cues_change_nothing_in_the_game()
    {
        // The same presses with and without a listener give the same game.
        string Play(bool listen)
        {
            var g = new GameEngine(LevelLoader.LoadBuiltIn("the-cellars"), 7);
            if (listen) g.Sound += c => g.Hearing(c);
            for (int i = 0; i < 60 * 30; i++)
            {
                if (i % 40 == 0) g.Press(i % 120 == 0 ? GameAction.TurnRight : GameAction.StepForward);
                if (g.RightHand.Ready) { g.HandDown(HandSide.Right); g.HandUp(HandSide.Right); }
                g.Tick();
            }
            return $"{g.PartyCell} {g.Hero.Hp} {g.Kills} {string.Join(",", g.Monsters.Select(m => m.Hp))}";
        }
        Assert.Equal(Play(false), Play(true));
    }
}
