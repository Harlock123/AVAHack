using System.Globalization;
using AVAHack.Content;
using AVAHack.Rules;
using AVAHack.World;

namespace AVAHack.Game;

/// <summary>
/// The bestiary's special rules (spec 12): elites and champions with their affixes, summoners, splitters,
/// creatures that burst or raise the dead, and the bosses' scripts.
/// </summary>
public sealed partial class GameEngine
{
    /// <summary>The 16 champion affixes (spec 12.2).</summary>
    public static readonly string[] ChampionAffixList =
    [
        "vampiric", "frenzied", "stoneskin", "blinking", "mortar", "shielded", "splitting", "venomous", "frostbound",
        "burningAura", "thunderous", "mirrored", "warded", "summoner", "bloodthirsty", "unyielding",
    ];

    private static readonly DamageType[] WardSchools =
        [DamageType.Fire, DamageType.Cold, DamageType.Lightning, DamageType.Acid, DamageType.Necrotic, DamageType.Radiant, DamageType.Psychic, DamageType.Force];

    private static readonly string[] NameStart = ["Gr", "Vor", "Sk", "Mal", "Th", "Ur", "Kr", "Zel", "Dr", "Hask", "Br", "Ny"];
    private static readonly string[] NameMid = ["isk", "ag", "oth", "en", "ul", "ix", "ar", "eg", "ok", "und", "ash", "im"];

    /// <summary>Elite chance per creature on Standard (spec 12.2), times the Delve's champion factor.</summary>
    public const double EliteChance = 0.08;
    /// <summary>Champion chance per creature from level 3 down.</summary>
    public const double ChampionChance = 0.02;

    /// <summary>Every affix this run's champions have shown; the Unmaker borrows them in its second phase.</summary>
    public List<string> ChampionAffixes { get; } = new();

    /// <summary>What the Waystones keep for the party, shared by all of them.</summary>
    public List<Items.Item> Stash { get; } = new();

    /// <summary>The boss still guarding this level's stairs, if any.</summary>
    public Monster? LivingBoss => _monsters.FirstOrDefault(m => m.Boss is not null && m.IsAlive && !m.Ally && m.Def.Id == Level.Boss);

    /// <summary>A Delve's fresh creature may come out elite or a champion.</summary>
    private void MaybeChampion(Monster m)
    {
        if (m.Boss is not null || m.Ally || m.Def.Family is "summoned") return;
        double f = Difficulty.Champions;
        double roll = Rng.World.Next(1_000_000) / 1_000_000.0;
        if (Level.Depth >= 3 && roll < ChampionChance * f) MakeChampion(m, 1 + Rng.World.Next(3));
        else if (roll < (ChampionChance + EliteChance) * f) MakeElite(m);
    }

    /// <summary>Elite: +50% HP and +2 AC (spec 12.2); its loot rolls a tier up.</summary>
    public void MakeElite(Monster m)
    {
        if (m.Elite) return;
        m.Elite = true;
        m.MaxHp = Math.Max(1, m.MaxHp * 3 / 2);
        m.Hp = m.MaxHp;
        m.AcBonus += 2;
    }

    /// <summary>Champion: an elite with 1–3 affixes and a name.</summary>
    public void MakeChampion(Monster m, int affixes, params string[] chosen)
    {
        MakeElite(m);
        var pool = ChampionAffixList.Where(a => !m.Affixes.Contains(a)).ToList();
        var picks = chosen.Length > 0 ? chosen.ToList() : new List<string>();
        while (picks.Count < affixes && pool.Count > 0)
        {
            var a = pool[Rng.World.Next(pool.Count)];
            pool.Remove(a);
            picks.Add(a);
        }
        foreach (var a in picks) AddAffix(m, a);
        string name = NameStart[Rng.World.Next(NameStart.Length)] + NameMid[Rng.World.Next(NameMid.Length)];
        m.Title = $"{name} the {Label(picks[0])}";
        m.MaxHp = m.MaxHp * 5 / 4;
        m.Hp = m.MaxHp;
        foreach (var a in picks) if (!ChampionAffixes.Contains(a)) ChampionAffixes.Add(a);
    }

    private void AddAffix(Monster m, string a)
    {
        m.Affixes.Add(a);
        switch (a)
        {
            case "stoneskin": m.AcBonus += 3; m.Resistances.Resist(DamageType.Slashing, DamageType.Piercing); break;
            case "shielded": m.Ward = Math.Max(m.Ward, m.MaxHp / 5); break;
            case "warded":
            {
                var school = WardSchools[Rng.World.Next(WardSchools.Length)];
                m.Affixes.Add("ward:" + school);
                m.Resistances.Immune(school);
                break;
            }
        }
    }

    /// <summary>Affix effects that aren't kept in the save: resistances come back from the affixes.</summary>
    internal static void ReapplyAffixes(Monster m)
    {
        foreach (var a in m.Affixes)
        {
            if (a == "stoneskin") m.Resistances.Resist(DamageType.Slashing, DamageType.Piercing);
            if (a.StartsWith("ward:", StringComparison.Ordinal) && Enum.TryParse<DamageType>(a[5..], out var t)) m.Resistances.Immune(t);
        }
    }

    public static string Label(string affix) => affix switch
    {
        "burningAura" => "Burning",
        "mortar" => "Mortar",
        _ => char.ToUpperInvariant(affix[0]) + affix[1..],
    };

    /// <summary>A creature's special rules, each tick, before it moves or fights.</summary>
    private void BestiaryTick(Monster m)
    {
        if (m.Ally) return;
        // A boss wakes when the party comes into its arena.
        if (m.Boss is not null && !m.IsAware && m.CalmLeft <= 0 && m.Cell.Distance(PartyCell) <= 5 && HasLineOfSight(m.Cell, PartyCell))
        {
            Alert(m);
            Say($"{m.Name} turns to face you.");
        }
        if (m.Boss is not null) BossTick(m);
        if (!m.IsAware || !m.IsAlive) return;
        Burrow(m);
        int dist = m.Cell.Distance(PartyCell);

        // Summoners call up their kind every 12 s, three at a time at most.
        bool summoner = m.Def.Has("summoner") || m.Affixes.Contains("summoner");
        bool raiser = m.Def.Has("raises");
        if (summoner || raiser)
        {
            m.SpecialTimer += TickSeconds;
            if (m.SpecialTimer >= (raiser ? 15 : 12))
            {
                m.SpecialTimer = 0;
                if (summoner) Summon(m, m.Def.Summons ?? LesserKin(m), 3);
                if (raiser) RaiseDead(m);
            }
        }

        if (m.Affixes.Count == 0) return;
        m.AffixTimer += TickSeconds;
        double t = m.AffixTimer;
        bool every(double s) => (int)((t - TickSeconds) / s) != (int)(t / s);

        if (m.Affixes.Contains("shielded") && every(10)) m.Ward = Math.Max(m.Ward, m.MaxHp / 5);
        if (m.Affixes.Contains("burningAura") && dist <= 1 && every(2))
            foreach (var c in Party.Standing.Where(c => Party.IsFront(c)).ToList()) HurtHero(c, Dice.Parse("1d6").Roll(Rng.Combat), DamageType.Fire);
        if (m.Affixes.Contains("blinking") && dist >= 2 && every(6) && m.Action == MonsterAction.None)
        {
            var spots = Enum.GetValues<Dir>().Select(d => PartyCell.Step(d))
                            .Where(c => c.Distance(m.Cell) <= 3 && CanEnter(m, c, false) && Level.PitAt(c) is null).ToList();
            if (spots.Count > 0)
            {
                m.Cell = m.From = spots[Rng.Ai.Next(spots.Count)];
                m.Facing = Enum.GetValues<Dir>().First(d => m.Cell.Step(d) == PartyCell);
                Say($"{m.Name} blinks to your side!");
            }
        }
        if (m.Affixes.Contains("mortar") && dist is >= 2 and <= 5 && every(7) && HasLineOfSight(m.Cell, PartyCell))
        {
            var a = m.Def.Attacks[0];
            Say($"{m.Name} lobs something heavy at you!");
            foreach (var c in Party.Standing.ToList())
            {
                int dmg = (int)(Dice.Parse(a.Damage).Roll(Rng.Combat) * m.DamageFactor / 2);
                if (c.Save(Rng.Combat, Rules.Attribute.Dex, 12 + m.Def.Level / 2)) dmg /= 2;
                HurtHero(c, dmg, DamageType.Bludgeoning);
            }
            if (Party.AllDown) Die();
        }
        if (m.Affixes.Contains("mirrored") && m.Phase == 0)
        {
            m.Phase = 1;
            for (int i = 0; i < 2; i++)
                if (SpawnNear(m, m.Def, m.Cell) is { } copy)
                {
                    copy.MaxHp = copy.Hp = Math.Max(1, m.MaxHp / 4);
                    copy.Title = $"{m.Name}'s reflection";
                    copy.SplitHalf = true;
                }
            Say($"{m.Name} splits into three!");
        }
    }

    /// <summary>The weakest creature of a summoner's family, for summoning champions.</summary>
    private string LesserKin(Monster m) =>
        Content.Creatures.Where(c => c.Family == m.Def.Family && !c.Has("boss") && !c.Has("summoner")).OrderBy(c => c.Level).ThenBy(c => c.Id, StringComparer.Ordinal)
               .Select(c => c.Id).FirstOrDefault() ?? "cellarRat";

    /// <summary>Calls up a creature next to <paramref name="m"/>, if it has fewer than <paramref name="max"/> about.</summary>
    private Monster? Summon(Monster m, string id, int max)
    {
        if (_monsters.Count(o => o.IsAlive && o.SummonedBy == m.Id) >= max) return null;
        var def = Content.Creature(id);
        def = Bestiary.ScaleTo(def, Math.Max(1, m.Def.Level - 2));
        var s = SpawnNear(m, def, m.Cell);
        if (s is null) return null;
        s.SummonedBy = m.Id;
        Say($"{m.Name} calls, and a {def.Name} answers.");
        return s;
    }

    /// <summary>Places a creature near a cell, already aware of the party.</summary>
    private Monster? SpawnNear(Monster from, CreatureDef def, Cell near)
    {
        var free = new[] { near }.Concat(Enum.GetValues<Dir>().Select(d => near.Step(d)))
                                 .Concat(Enum.GetValues<Dir>().SelectMany(d => Enum.GetValues<Dir>().Select(e => near.Step(d).Step(e))))
                                 .Where(c => c != PartyCell && Level.IsPassable(c) && Level.PitAt(c) is null && Level.ChestAt(c) is null)
                                 .Distinct().ToList();
        var probe = new Monster(0, def, near, Dir.North, 1, Content);
        var cell = free.FirstOrDefault(c => c != near || def.Size == CreatureSize.Small ? CanEnter(probe, c, false) : false);
        if (cell == default) return null;
        var s = AddMonster(def, cell, from.Facing);
        ApplyDifficulty(s);
        s.State = MonsterState.Aware;
        s.LastSeen = Time;
        return s;
    }

    /// <summary>A Spore Shepherd sows a corpse within 5 cells, and it rises as a Shambler.</summary>
    private void RaiseDead(Monster m)
    {
        var corpse = _monsters.Where(o => o.State == MonsterState.Dead && !o.FellInPit && !o.Raised && !o.Ally && o.Boss is null && o.Cell.Distance(m.Cell) <= 5)
                              .OrderBy(o => o.Cell.Distance(m.Cell)).ThenBy(o => o.Id).FirstOrDefault();
        if (corpse is null) return;
        corpse.Raised = true;
        var def = Content.Creature("shambler");
        def = Bestiary.ScaleTo(def, Math.Max(1, m.Def.Level - 2));
        if (SpawnNear(m, def, corpse.Cell) is { } s)
        {
            s.SummonedBy = m.Id;
            Say($"Spores settle on the dead {corpse.Def.Name}. It gets up.");
        }
    }

    /// <summary>Damage that a boss's script turns aside (the Engine's valves, the Mother under the water).</summary>
    private int BossShield(Monster m, int dmg, DamageType type)
    {
        if (m.Boss is null || dmg <= 0) return dmg;
        if (m.Boss == "lens") return LensShield(m, dmg, type);
        if (m.Boss == "mycelCrown" && CrownFed)
        {
            if (!LevelFlags.TryGetValue("bossSaid", out var fed) || Time - double.Parse(fed, CultureInfo.InvariantCulture) > 6)
            {
                LevelFlags["bossSaid"] = Time.ToString("R", CultureInfo.InvariantCulture);
                Say("The blow sinks into the Crown and closes up. Its nodes feed it: find them, and burn them.");
            }
            return 0;
        }
        if (m.Boss == "archivist" && ArchivistShield(m) is > 0 and var cut)
        {
            if (!LevelFlags.TryGetValue("bossSaid", out var said) || Time - double.Parse(said, CultureInfo.InvariantCulture) > 6)
            {
                LevelFlags["bossSaid"] = Time.ToString("R", CultureInfo.InvariantCulture);
                Say("The Archivist reads on by candlelight, and the blow turns half aside. Snuff the candles!");
            }
            return Math.Max(1, (int)Math.Round(dmg * (1 - cut)));
        }
        string? why = null;
        if (m.Boss == "bellwright" && Level.Valves.Count > 0 && Level.Levers.Any(l => Level.Valves.Contains(l.Cell) && !l.Down))
            why = "The Engine shrugs it off. Steam still roars through its valves: shut them!";
        else if (m.Boss == "motherOfCoils" && m.Phase % 2 == 0)
            why = "She is under the water, out of reach. Wait for it to drop.";
        if (why is null) return dmg;
        if (!LevelFlags.TryGetValue("bossSaid", out var last) || Time - double.Parse(last, CultureInfo.InvariantCulture) > 4)
        {
            LevelFlags["bossSaid"] = Time.ToString("R", CultureInfo.InvariantCulture);
            Say(why);
        }
        return 0;
    }

    private void BossTick(Monster m)
    {
        if (!m.IsAware) return;
        double share = (double)m.Hp / Math.Max(1, m.MaxHp);
        m.SpecialTimer += TickSeconds;
        switch (m.Boss)
        {
            case "hollowKing":
                if (m.SpecialTimer >= 20) { m.SpecialTimer = 0; Summon(m, "boneSentry", 2); }
                if (m.Phase == 0 && share <= 0.5)
                {
                    m.Phase = 1;
                    Say("The Hollow King breaks his crown in two. Each half becomes a knight!");
                    for (int i = 0; i < 2; i++) if (SpawnNear(m, Scaled("ossuaryKnight", m.Def.Level - 5), m.Cell) is { } k) { k.SummonedBy = m.Id; k.MaxHp = k.Hp = Math.Max(1, m.MaxHp / 5); }
                }
                break;
            case "bellwright":
                // The bells ring every 10 s: lightning through the floor near the Engine.
                if (m.SpecialTimer >= 10)
                {
                    m.SpecialTimer = 0;
                    Cue("bell", m.Cell); // heard all over the level, nearer by its passages
                    if (m.Cell.Distance(PartyCell) <= 2)
                    {
                        Say("The great bell tolls. Lightning runs through the floor!");
                        foreach (var c in Party.Standing.ToList())
                        {
                            int dmg = Dice.Parse("3d6").Roll(Rng.Combat);
                            if (c.Save(Rng.Combat, Rules.Attribute.Dex, 15)) dmg /= 2;
                            HurtHero(c, dmg, DamageType.Lightning);
                        }
                        if (Party.AllDown) Die();
                    }
                }
                break;
            case "motherOfCoils":
                // Water rises (12 s, out of reach) and falls (8 s, open to blows); each rise brings a Coil Guard.
                if (m.SpecialTimer >= (m.Phase % 2 == 0 ? 12 : 8))
                {
                    m.SpecialTimer = 0;
                    m.Phase++;
                    if (m.Phase % 2 == 0) { Say("The water rises, and the Mother sinks beneath it."); Summon(m, "coilGuard", 3); }
                    else Say("The water drains away. The Mother is exposed!");
                }
                break;
            case "archivist":
                ArchivistTick(m, share);
                break;
            case "mycelCrown":
                CrownTick(m);
                break;
            case "lens":
                LensTick(m, share);
                break;
            case "unmaker":
                if (m.Phase == 0 && m.SpecialTimer >= 15) { m.SpecialTimer = 0; Summon(m, "unmadeHound", 2); }
                if (m.Phase == 0 && share <= 2.0 / 3)
                {
                    m.Phase = 1;
                    m.SpecialTimer = 0;
                    var borrowed = ChampionAffixes.Where(a => a is not ("mirrored" or "splitting" or "summoner")).Take(3).ToList();
                    if (borrowed.Count == 0) borrowed = ["vampiric", "shielded", "frenzied"];
                    foreach (var a in borrowed) AddAffix(m, a);
                    Say($"The Unmaker remembers every champion you killed. It is {string.Join(", ", borrowed.Select(Label))} now.");
                }
                if (m.Phase == 1 && share <= 1.0 / 3) { m.Phase = 2; m.SpecialTimer = 0; Say("The Knell tolls. The room begins to move.");
                    CueHere("bell"); }
                if (m.Phase == 2 && m.SpecialTimer >= 15)
                {
                    m.SpecialTimer = 0;
                    var spots = Around(m.Cell, 4).Where(c => c.Distance(m.Cell) >= 2 && CanPartyEnter(c) && !MonstersAt(c).Any() && Level.PitAt(c) is null).ToList();
                    if (spots.Count > 0)
                    {
                        PlaceParty(spots[Rng.Ai.Next(spots.Count)], Facing);
                        Say("The Knell tolls, and the room rearranges itself around you.");
                    CueHere("bell");
                    }
                }
                break;
        }
    }

    private static IEnumerable<Cell> Around(Cell c, int r)
    {
        for (int y = c.Y - r; y <= c.Y + r; y++)
            for (int x = c.X - r; x <= c.X + r; x++)
                yield return new Cell(x, y);
    }

    private CreatureDef Scaled(string id, int level)
    {
        var d = Content.Creature(id);
        return Bestiary.ScaleTo(d, level);
    }

    /// <summary>What happens when a creature dies: splitting, bursting, the Choir, the Unmaker.</summary>
    private void BestiaryDeath(Monster m)
    {
        if ((m.Def.Has("splits") || m.Affixes.Contains("splitting")) && !m.SplitHalf && m.MaxHp >= 8)
        {
            int made = 0;
            for (int i = 0; i < 2; i++)
                if (SpawnNear(m, m.Def, m.Cell) is { } half)
                {
                    half.MaxHp = half.Hp = Math.Max(1, m.MaxHp / 2);
                    half.SplitHalf = true;
                    foreach (var a in m.Affixes.Where(a => a != "splitting")) half.Affixes.Add(a);
                    made++;
                }
            if (made > 0) Say($"{m.TheKind} splits in two!");
        }
        if (m.Def.Has("explodes") && m.Cell.Distance(PartyCell) <= 1)
        {
            var a = m.Def.Attacks[0];
            Say($"{m.TheKind} bursts in a choking cloud!");
            foreach (var c in Party.Standing.ToList())
            {
                int dmg = (int)(Dice.Parse(a.Damage).Roll(Rng.Combat) * m.DamageFactor);
                bool saved = c.Save(Rng.Combat, Rules.Attribute.Con, 11 + m.Def.Level / 2);
                if (saved) dmg /= 2;
                else c.ApplyCondition(ConditionId.Poisoned, 11 + m.Def.Level / 2, 8);
                HurtHero(c, dmg, a.Type);
            }
            if (Party.AllDown) Die();
        }
        if (m.Boss == "crownNode") NodeDied(m);
        if (m.Boss == "choir")
        {
            // A fallen singer returns in 10 s while another still sings.
            m.SpecialTimer = 10;
        }
        if (m.Boss == "unmaker") Win("The Unmaker comes apart into silence. The Knell stops. Harrowgate will wake.");
        else if (m.Boss is not null && m.Def.Id == Level.Boss) Say($"{m.Name} has fallen. The way down is open.");
    }

    /// <summary>Things that happen across the level: the Choir's singers return.</summary>
    private void BestiaryLevelTick()
    {
        foreach (var s in _monsters)
        {
            if (s.Boss != "choir" || s.State != MonsterState.Dead || s.SpecialTimer <= 0) continue;
            s.SpecialTimer -= TickSeconds;
            if (s.SpecialTimer > 0) continue;
            s.SpecialTimer = 0;
            if (!_monsters.Any(o => o.Boss == "choir" && o.IsAlive)) continue; // all five fell together: silence
            s.State = MonsterState.Aware;
            s.Hp = s.MaxHp / 2;
            s.LastSeen = Time;
            s.Raised = true; // no second helping of loot
            Kills--;
            Say("Another voice takes up the song, and a fallen singer rises.");
        }
    }
}
