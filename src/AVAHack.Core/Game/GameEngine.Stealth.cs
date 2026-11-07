using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

public sealed partial class GameEngine
{
    /// <summary>The party is sneaking: slower steps, a shaded light, and creatures notice it gradually (spec 11.1).</summary>
    public bool Sneaking => Party.Standing.Any() && Party.Standing.All(c => c.Sneaking);

    /// <summary>Rest takes over in survival; until then, nothing.</summary>
    internal Action? RestHook;

    public void ToggleSneak()
    {
        bool on = !Sneaking;
        foreach (var c in Party.Members) c.Sneaking = on;
        if (!on) foreach (var m in _monsters) m.Awareness = 0;
        Say(on ? "The party moves quietly, keeping to the shadows. (X to stop)" : "The party stops sneaking.");
    }

    /// <summary>How well the party hides: its worst member's Stealth / 5 + DEX, less 3 in heavy armour.</summary>
    public int PartyHide => Party.Standing.Select(c => c.SkillLevel(SkillId.Stealth) / 5 + c.Mod(Rules.Attribute.Dex)
        - (c.Body?.Kind == ArmourKind.Heavy ? 3 : 0)).DefaultIfEmpty(0).Min();

    /// <summary>
    /// The notice check while sneaking: each tick a creature that can see the party fills its awareness meter by
    /// (Perception + light − hide) / 10 a second, faster up close; at 1 it has noticed. Out of sight, the meter
    /// drains.
    /// </summary>
    private bool SneakNotice(Monster m)
    {
        double d = Math.Sqrt(Sq(m.Cell.X - PartyCell.X) + Sq(m.Cell.Y - PartyCell.Y));
        double range = m.Def.Has("ambush") ? AmbushRange : Math.Max(2, m.Def.Perception - 5);
        if (d > range + 0.01 || !HasLineOfSight(m.Cell, PartyCell))
        {
            m.Awareness = Math.Max(0, m.Awareness - 0.2 * TickSeconds);
            return false;
        }
        int light = LightLeft > 0 ? 3 : 0;
        double score = m.Def.Perception + light - PartyHide;
        double rate = Math.Max(0.05, score / 10) * (1.5 - d / Math.Max(1, range)) * (d <= 1.01 ? 3 : 1);
        m.Awareness = Math.Min(1, m.Awareness + rate * TickSeconds);
        return m.Awareness >= 1;
    }

    /// <summary>Each step taken sneaking past a creature that hasn't noticed teaches Stealth.</summary>
    private void SneakPractice()
    {
        if (!Sneaking) return;
        foreach (var m in _monsters.Where(m => m.IsAlive && !m.IsAware && !m.Ally && m.Cell.Distance(PartyCell) <= Math.Max(2, m.Def.Perception - 5)))
        {
            foreach (var c in Party.Standing) c.UseSkill(SkillId.Stealth, m.Def.Level, XpSource.Against(m.Def.Level));
            break;
        }
    }
}
