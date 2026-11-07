using AVAHack.Characters;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;
using Attribute = AVAHack.Rules.Attribute;

namespace AVAHack.Game;

/// <summary>
/// The Ossuary Stacks' rules (spec 37), switched on by the region's settings in regions.json so any region can
/// take them up:
/// <list type="bullet">
/// <item><b>The hush.</b> Loud sounds the party makes (blows, doors, a dropped shield) wake unaware creatures within
/// <see cref="HushReach"/> steps along the passages; a shut door counts as three.</item>
/// <item><b>Candles.</b> Lit candles stand in the halls (stations of kind "candle"). Within two cells of one the
/// party can be seen from three cells further. Use snuffs a candle, or lights it again.</item>
/// <item><b>Shelves.</b> Bookshelves line some walls. Use searches one, once: a Lore check finds a book, a scroll
/// or a tome.</item>
/// <item><b>Heralds.</b> A herald (a candlewight, a bell, a keener) that notices the party rouses everything within
/// <see cref="HeraldReach"/> cells.</item>
/// <item><b>The Archivist</b> reads by the candles in his hall: each one lit turns aside a fifth of every blow. He
/// calls up pages, files heroes away (paralysed), and at half health reads aloud (frightened) while any candle burns.</item>
/// </list>
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>How far noise carries in the hush, in steps along the passages (a shut door costs <see cref="HushDoor"/>);
    /// beyond the 4 cells any blow carries anywhere (<see cref="NoiseRange"/>).</summary>
    public const int HushReach = 8, HushDoor = 3, HeraldReach = 8, CandleGlow = 2;

    /// <summary>The region the party is in (the handmade levels count as the first).</summary>
    public RegionDef Region => Content.Regions.FirstOrDefault(r => r.Id == Level.Theme) ?? Content.Regions.FirstOrDefault() ?? new RegionDef { Id = "cellar", Name = "", Families = [] };

    // ---- Candles ----

    public bool IsCandle(Cell c) => Level.Stations.TryGetValue(c, out var k) && k == "candle";
    public bool CandleLit(Cell c) => IsCandle(c) && !LevelFlags.ContainsKey($"snuffed:{c}");
    public IEnumerable<Cell> LitCandles => Level.Stations.Where(s => s.Value == "candle" && !LevelFlags.ContainsKey($"snuffed:{s.Key}")).Select(s => s.Key);

    /// <summary>Whether a lit candle near the party shows it up (spec 37).</summary>
    public bool Candlelit => LitCandles.Any(c => c.Distance(PartyCell) <= CandleGlow);

    private bool TouchCandle(Cell ahead)
    {
        if (!IsCandle(ahead)) return false;
        if (CandleLit(ahead))
        {
            LevelFlags[$"snuffed:{ahead}"] = "1";
            Say("You pinch out the candle. The dark closes in a little.");
            Cue("snuff", ahead);
        }
        else
        {
            LevelFlags.Remove($"snuffed:{ahead}");
            Say("You light the candle again.");
            Cue("torch", ahead, 0.6);
        }
        return true;
    }

    // ---- Shelves ----

    private bool SearchShelf(Cell wall)
    {
        if (!Level.Shelves.Contains(wall)) return false;
        if (LevelFlags.ContainsKey($"shelf:{wall}")) { Say("You have been through this shelf already."); return true; }
        LevelFlags[$"shelf:{wall}"] = "1";
        Cue("read", wall);
        var reader = Party.Standing.MaxBy(h => h.SkillLevel(SkillId.Lore) / 5 + h.Mod(Attribute.Int));
        if (reader is null) return true;
        int roll = reader.RollD20(Rng.Loot) + reader.SkillLevel(SkillId.Lore) / 5 + reader.Mod(Attribute.Int);
        reader.UseSkill(SkillId.Lore, Level.Depth, XpSource.Plain);
        if (roll < 14) { Say($"{reader.Name} leafs through rotten ledgers and crumbling bindings. Nothing worth keeping."); return true; }
        var found = new List<Items.Item>();
        for (int i = 0; i < 4 && found.Count == 0; i++)
            found = Loot.Roll("archive", Level.Depth, Rng.Loot, Magic).Where(it => it.BaseId != "gold").ToList();
        if (found.Count == 0) { Say($"{reader.Name} finds only a pressed flower in a hymnal."); return true; }
        foreach (var it in found) Level.Drop(PartyCell, it);
        Say($"{reader.Name} pulls something from the shelf: {string.Join(", ", found.Select(f => f.Name(Content)))} (G to pick up).");
        return true;
    }

    // ---- The hush ----

    private static readonly HashSet<string> Loud =
    [
        "swing", "swing.heavy", "shoot", "miss", "block", "bump", "hit.flesh", "hit.bone", "hit.metal", "hit.slime", "hit.crit",
        "door.open", "door.close", "portcullis", "lever", "chest", "hero.hurt", "hero.down", "cast.destruction",
        "trap.dart", "trap.alarm", "trap.fire", "trap.lightning", "trap.boulder", "trap.spikes", "trap.blade", "pit", "anvil",
    ];

    private double _hushSaid = -100;

    /// <summary>In a hushed region, a loud sound wakes the unaware within reach along the passages.</summary>
    private void Hear(string id)
    {
        if (!Region.Hush || !Loud.Contains(id)) return;
        int woken = 0;
        foreach (var m in _monsters.ToList())
        {
            if (!m.IsAlive || m.IsAware || m.Ally || m.CalmLeft > 0) continue;
            if (Path(m.Cell) is not var (steps, doors) || steps + doors * HushDoor > HushReach) continue;
            Alert(m);
            woken++;
        }
        if (woken > 0 && Time - _hushSaid > 8)
        {
            _hushSaid = Time;
            Say("The noise carries through the stacks. Something stirs.");
        }
    }

    // ---- Heralds ----

    private void Herald(Monster m)
    {
        if (!m.Def.Has("herald")) return;
        bool any = false;
        foreach (var o in _monsters)
            if (o != m && o.IsAlive && !o.IsAware && !o.Ally && o.Cell.Distance(m.Cell) <= HeraldReach)
            {
                o.State = MonsterState.Aware;
                o.LastSeen = Time;
                any = true;
            }
        if (any) Say($"{m.TheName} raises the alarm. The stacks wake around you.");
    }

    // ---- The Archivist ----

    /// <summary>The share of a blow the Archivist turns aside: a fifth for each lit candle in his hall, at most four.</summary>
    internal double ArchivistShield(Monster m) =>
        Math.Min(4, LitCandles.Count(c => c.Distance(m.Cell) <= 8)) * 0.2;

    private void ArchivistTick(Monster m, double share)
    {
        if (m.SpecialTimer >= 9 && m.SpecialTimer < 9 + TickSeconds) Summon(m, "pageSwarm", 3);
        if (m.SpecialTimer >= 16)
        {
            m.SpecialTimer = 0;
            // He files one hero away between the shelves.
            var standing = Party.Standing.Where(h => !h.Conditions.Has(ConditionId.Paralysed)).ToList();
            if (standing.Count > 0)
            {
                var h = standing[Rng.Ai.Next(standing.Count)];
                int dc = 14 + Level.Depth / 4;
                if (!h.Save(Rng.Combat, Attribute.Wis, dc) && h.ApplyCondition(ConditionId.Paralysed, dc, 5))
                {
                    Say($"The Archivist catalogues {h.Name} and files them away between the shelves!");
                    CueHere("archive");
                }
                else Say($"Pale hands reach for {h.Name}, who tears free.");
            }
        }
        if (m.Phase == 0 && share <= 0.5)
        {
            m.Phase = 1;
            if (LitCandles.Any(c => c.Distance(m.Cell) <= 8))
            {
                Say("The Archivist reads aloud from the ledger of the dead, by the light of his candles.");
                foreach (var h in Party.Standing.ToList())
                    if (!h.Save(Rng.Combat, Attribute.Wis, 15 + Level.Depth / 4)) h.ApplyCondition(ConditionId.Frightened, 15, 10);
            }
            else Say("The Archivist gropes for a page it can no longer read in the dark.");
        }
    }
}
