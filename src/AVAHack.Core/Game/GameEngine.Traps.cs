using AVAHack.Characters;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>How close a hidden pit must be for the hero's passive Perception to get a chance at it.</summary>
    public const int SpotRange = 2;

    /// <summary>How far the party sees to fill in the automap, in cells.</summary>
    public const int MapSight = 3;

    private readonly HashSet<Cell> _explored = new();

    public int Kills { get; private set; }

    /// <summary>Cells the party has seen, for the automap: those within <see cref="MapSight"/> with a clear view.</summary>
    public IReadOnlySet<Cell> Explored => _explored;

    private void Explore(Cell from)
    {
        for (int dy = -MapSight; dy <= MapSight; dy++)
        for (int dx = -MapSight; dx <= MapSight; dx++)
        {
            var c = new Cell(from.X + dx, from.Y + dy);
            if (!Level.InBounds(c.X, c.Y) || _explored.Contains(c)) continue;
            // A wall is seen if a seen floor cell touches it, so only test floor and doors for sight.
            if (Level.KindAt(c) == CellKind.Wall)
            {
                if (Enum.GetValues<Dir>().Any(d => HasLineOfSight(from, c.Step(d)) && Level.KindAt(c.Step(d)) != CellKind.Wall && c.Step(d).Distance(from) <= MapSight + 1))
                    _explored.Add(c);
                continue;
            }
            if (c == from || HasLineOfSight(from, c)) _explored.Add(c);
        }
    }

    /// <summary>The party has just taken a cell (a step starts): notes, and looking for hidden pits.</summary>
    private void EnterCell(Cell c)
    {
        Explore(c);
        if (Level.NoteAt(c) is { Read: false } note)
        {
            note.Read = true;
            Say(note.Text);
        }
        int perception = Hero.PassivePerception();
        foreach (var pit in Level.Pits)
        {
            if (pit.Spotted || pit.Cell.Distance(c) > SpotRange) continue;
            if (perception >= pit.HideDc)
            {
                Level.Spot(pit);
                Hero.UseSkill(SkillId.Survival, 0, XpSource.Plain);
                Say("You notice a loose, hollow-sounding flagstone. A pit!");
                Event?.Invoke(GameEvent.Spotted);
            }
        }
        UpdatePlates();
    }

    /// <summary>The step into a cell has finished: pits and the stairs out act now.</summary>
    private void ArriveInCell()
    {
        if (Level.PitAt(PartyCell) is { } pit)
        {
            Level.Spot(pit);
            int dmg = Dice.Parse("2d6").Roll(Rng.Combat);
            Say($"The floor gives way! You fall into the cellar below ({dmg} damage).");
            Hero.TakeDamage(dmg);
            FloatAtHero($"-{dmg}", TextKind.HeroHurt);
            Event?.Invoke(GameEvent.Fell);
            PartyCell = pit.Landing;
            _queue.Clear();
            EnterCell(PartyCell);
            if (Hero.IsDown) Die();
            return;
        }
        if (Level.IsStairs(PartyCell))
        {
            Mode = GameMode.Won;
            Say("You find the stairs down. The cellars are behind you.");
        }
    }

    /// <summary>Presses and releases plates: the party or any living creature standing on one presses it.</summary>
    private void UpdatePlates()
    {
        foreach (var plate in Level.Plates)
        {
            bool pressed = PartyCell == plate.Cell || Monsters.Any(m => m.IsAlive && m.Cell == plate.Cell);
            if (pressed == plate.Pressed) continue;
            plate.Pressed = pressed;
            foreach (var c in plate.Targets)
            {
                var door = Level.DoorAt(c)!;
                if (plate.Mode == PlateMode.Hold) door.Opening = pressed;
                else if (pressed) door.Opening = !door.Opening;
            }
            if (pressed) Say("Click. Something gives under your weight, and machinery grinds in the walls.");
        }
    }
}
