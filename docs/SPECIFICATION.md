# AVAHack — Game Design Specification

**Version:** 0.1 (draft, 2026-11-06)
**Stack:** C# on .NET 10, Avalonia 12 (12.1.4), SkiaSharp, OpenAL Soft (Silk.NET), xUnit v3
**Repo:** `AVAHack`, branch `main`
**Reader:** the implementer (human or AI agent). Every formula here should get a unit test.

---

## Contents

1. Overview
2. Inspirations and boundaries
3. World and story
4. The Delve: run structure and options
5. Character creation
6. Attributes and the d20 core
7. Skills and progression
8. Perks
9. Combat
10. Magic
11. Stealth, locks and traps
12. Monsters
13. Items, loot and crafting
14. Dungeon generation
15. The first-person view
16. UI, controls and audio
17. Saves and difficulty
18. Technical architecture
19. Testing
20. Milestones
21. Open questions
22. Implementation decisions (M0)
23. Implementation decisions (M1)
24. Implementation decisions (M2)
25. Implementation decisions (M3)
26. Implementation decisions (M4)
27. Implementation decisions (M5)
28. Implementation decisions (M6)
29. Implementation decisions (M7)
30. Implementation decisions (M8)
31. Implementation decisions (the balance pass)
32. Implementation decisions (M9: textures from an art pack)
33. Implementation decisions (M9: the creatures redrawn)
34. Implementation decisions (M9: item icons)
35. Implementation decisions (M9: hero portraits)
36. Implementation decisions (M9: audio)
37. Implementation decisions (a deeper Delve: six regions, and the Ossuary Stacks)

---

## 1. Overview

AVAHack is a real-time, grid-based, first-person dungeon crawler. A party of up to four heroes, made from a wide
mix of ancestries and callings, descends through a dungeon. Each run's dungeon is generated from a seed, with
handmade puzzle vaults stitched in. The party fights a large bestiary and gets past traps, locks and puzzles.

The rules are a hybrid:

- **Character building is like a tabletop d20 game.** There are six attributes, ancestries, starting classes
  ("callings"), hit dice, armour class, saving throws and d20 attack rolls.
- **Growth is like a modern open-world RPG.** Skills rise by being used. The character level comes from skill
  gains, not from kills. Perk trees hang off each skill, and nobody is locked out of anything by their calling.

### Design pillars

1. **Step, turn, strike.** The dungeon is a square grid. Moving takes one step and turning takes 90°, both
   animated smoothly. Fights are about positioning: you sidestep, back away, and dance around a monster on a
   2×2 room ("the square dance") while weapons recharge.
2. **Every room is a question.** Pressure plates, levers, secret buttons, pits, teleporters, spinners, timed doors
   and riddles. The handmade vaults carry the puzzles, and the generator makes sure each one is solvable.
3. **You become what you do.** A wizard who picks up an axe and keeps swinging it becomes good with axes. Callings
   set where you start, never where you can go.
4. **A plethora of things to meet.** About 60 monsters in 10 families, champion variants with affixes, and about
   20 kinds of trap.
5. **Replayable.** Seeded dungeons, a Delve options screen, Ironman mode and a shareable seed code.

### Target scope (v1.0)

| Item | Count |
| --- | --- |
| Dungeon depth | 12 levels by default (options: 6, 12, 18, 24), plus a final level |
| Ancestries | 8 |
| Callings | 10 |
| Skills | 22 |
| Perks | about 160 (6–10 per skill) |
| Spells | 48 across 5 schools |
| Monsters | about 60 base types, plus elites, champions and 5 bosses |
| Trap kinds | 20 |
| Handmade vaults | about 40 |
| Item bases | about 120, plus affixes, uniques and sets |

---

## 2. Inspirations and boundaries

AVAHack belongs to the genre of 1990s and 2010s grid crawlers. It borrows **mechanics that are common to the
genre**: grid movement, real-time cooldown combat, a 2×2 party formation, procedural levels with options, use-based
skills and perk trees. It does **not** recreate any particular game.

The rules for this are:

- **No names, characters, places, maps, art, music, text or UI layout from any existing game.** That includes
  Legend of Grimrock, Dungeon Hack and The Elder Scrolls.
- **No trademarked tabletop content.** Use no product-identity monsters or names. The d20 mechanics (six
  attributes, modifiers, AC, saving throws, advantage) are generic, and the SRD 5.1 is under CC-BY-4.0, so these
  are safe. If any SRD text is quoted, credit it in `CREDITS.md`. Write all monster names, spell names and lore
  fresh.
- **Original look.** Don't copy another game's screen layout. The UI is designed in section 16.

---

## 3. World and story

### Premise

The town of **Harrowgate** stands over **the Hollow**, an old dwarven delving that went wrong. Below the third
cellar, the stone *moves*. Every time the town's iron bell, the **Knell**, tolls, the Hollow's passages rearrange
themselves. (This is why each run is a new seed.)

Something at the bottom, the **Unmaker**, is waking. With each toll the Hollow reaches closer to the surface.
The town sends delvers down to silence it.

### Structure

- **The Upper Hollow (levels 1–4).** Cellars, crypts and flooded cisterns. Vermin, the restless dead and
  goblin-kin.
- **The Deep Works (levels 5–8).** Dwarven forges, mine rails and steam pipes. Constructs, oozes, deep folk and
  fire.
- **The Drowned Choir (levels 9–12).** A sunken temple and fungal caverns. Spore-kin, serpent cults and
  aberrations.
- **The Ossuary Stacks (levels 13–16).** The buried archive and tomb-library of Harrowgate's dead. The Inkbound,
  the Mourners and the restless dead. Noise wakes them, candles show you to them, and the shelves hold books. Its
  boss is the Archivist. (Section 37.)
- **The Rootdark (levels 17–20).** Giant roots and fungus beneath the Works. The Burrowers, the spore-thralls and
  the spore-kin. Spore pods burst in choking clouds that fire sets alight, root tangles choke the passages, and
  burrowers come up out of the earth behind you. Its boss is the Mycel Crown. (Section 38.)
- **The Glass Roads (levels 21–24).** Halls of fused black glass where the Unmade first broke through. The
  Shardborn, the Hollowed and the Unmade. Broken glass crunches underfoot and wakes the halls, rifts let the Unmade
  through until you seal them, and its boss, the Lens, learns every blow you strike. (Section 39.)
- **The Unmade Heart (final level).** Below the last level of any Delve of 12 levels or more. It leads to the
  Unmaker.

### Bosses

There is one boss at the bottom of each region, on a handmade arena level. They are covered in section 12.
The regions come in the order of `regions.json`, four levels each; a Delve longer than all of them stays in the
deepest. (Section 37.)

### Story delivery

- Scraps of a lost delver's journal, found on levels 1–12. Collecting them gives a lore bonus (section 7).
- Wall carvings, read with the Lore skill.
- Three **Waystones**, after levels 4, 8 and 12. Each is a safe room with a merchant, a trainer, a shrine and a
  stash. The ghost of the journal's author waits there.

---

## 4. The Delve: run structure and options

A run is called a **Delve**. Before the first level, the Delve options screen sets:

| Option | Values | Default |
| --- | --- | --- |
| Seed | Any text; random if blank. Shown as an 8-character share code | Random |
| Depth | 6 / 12 / 18 / 24 levels | 12 |
| Party | Party of 4 / Pair / Lone Delver | Party of 4 |
| Monster strength | Gentle / Standard / Cruel / Brutal | Standard |
| Monster density | Sparse / Normal / Swarming | Normal |
| Traps | Few / Normal / Many | Normal |
| Food and water | Off / On | On |
| Ironman (one save, deleted on death) | Off / On | Off |
| Pause in combat | Off / On (Space pauses; orders can be queued) | On |
| Magic items | Rare / Normal / Plentiful | Normal |

**Lone Delver** gives the hero +50% HP, +2 to all saves, and twice the skill XP. This keeps a party of one
viable, in the style of single-hero crawlers.

**The score**, shown at the end of a Delve, counts the depth reached, kills, secrets, vaults solved and time. The
options apply a multiplier. Scores are kept locally in a hall of fame, one table per option set.

---

## 5. Character creation

The party has up to four members, standing in a 2×2 formation: two front and two back. Each member is built in
five steps.

### 5.1 Ancestry

| Ancestry | Attribute bonuses | Traits |
| --- | --- | --- |
| Human | +1 to all | Versatile: +10% skill XP; one extra perk point at level 1 |
| Dwarf | CON +2, STR +1 | Darkvision 2 cells; poison resistance; +10 Smithing |
| Elf | DEX +2, INT +1 | Keen senses: advantage on Perception for secret doors; sleep immunity |
| Halfling | DEX +2, CHA +1 | Lucky: reroll a natural 1 once per fight; +10 Stealth |
| Gnome | INT +2, CON +1 | Tinker: +10 Lockpicking and Traps; advantage on saves against illusion |
| Orc-blooded | STR +2, CON +1 | Relentless: at 0 HP, drop to 1 HP once per rest; +10 Two-handed |
| Saurian | CON +2, WIS +1 | Scaled hide: natural AC 12 + DEX; can hold breath 3× longer; eats raw meat safely |
| Emberkin | CHA +2, INT +1 | Fire resistance; one free Spark cantrip; +10 Destruction |

### 5.2 Calling

A calling sets the hit die, two save proficiencies, the starting skills (six majors at +15 and three minors at
+5; the minors are listed in `callings.json`), starting
gear and one **signature ability**. It does **not** restrict what the character can learn later.

| Calling | Hit die | Saves | Major skills (+15) | Signature |
| --- | --- | --- | --- | --- |
| Fighter | d10 | STR, CON | Blade, Blunt, Heavy Armour, Block, Athletics, Smithing | Second Wind: heal 1d10 + level, once per rest |
| Barbarian | d12 | STR, CON | Two-handed, Blunt, Light Armour, Athletics, Unarmed, Survival | Rage: +2 damage and half physical damage for 15 s |
| Ranger | d10 | STR, DEX | Archery, Blade, Light Armour, Survival, Stealth, Alchemy | Marked Prey: +1d6 damage to one marked target |
| Rogue | d8 | DEX, INT | Blade, Archery, Light Armour, Stealth, Lockpicking, Traps | Sneak Attack: ×3 damage from stealth (×5 with a dagger) |
| Cleric | d8 | WIS, CHA | Restoration, Blunt, Heavy Armour, Block, Warding, Lore | Turn Undead: undead within 2 cells save or flee |
| Wizard | d6 | INT, WIS | Destruction, Warding, Conjuration, Illusion, Enchanting, Lore | Arcane Recovery: restore 25% mana once per rest |
| Paladin | d10 | WIS, CHA | Blade, Heavy Armour, Block, Restoration, Warding, Athletics | Smite: spend mana for +2d8 radiant on a hit |
| Druid | d8 | INT, WIS | Restoration, Conjuration, Alchemy, Survival, Blunt, Light Armour | Wild Shape: become a bear for 20 s (own HP pool) |
| Monk | d8 | STR, DEX | Unarmed, Light Armour, Athletics, Stealth, Restoration, Block | Flurry: two extra unarmed strikes, 12 s cooldown |
| Spellblade | d8 | CON, INT | Blade, Destruction, Light Armour, Warding, Enchanting, Block | Spellstrike: cast a touch spell through a weapon hit |

### 5.3 Attributes

Attributes are set by **point buy (27 points, scores 8–15)** or by **4d6, dropping the lowest**. Each hero rolls
once, either way: the roll is kept for good, so going to point buy and back returns the same scores, and the
other kind of roll is closed. Any two rolled scores can swap places (click Swap on one, then on the other). Ancestry bonuses are added afterwards.

**Heroic attributes** are the third way: 4d6 dropping the lowest, with any 1 rolled again, and a score under 10
rolled again whole. Scores run 10–18 and average about 14 (4d6 averages about 12). They are laid out for the
calling, best first, as the standard array is. The price is slower levels: a heroic hero needs a quarter more
character XP for each level (section 7.3, `Progression.HeroicXp`). It is a fair trade, not an easy mode: the
bot wins about as often either way (section 41).

### 5.4 Background

There are 8 backgrounds. Each gives +10 to one skill, a small item and one line of flavour.

| Background | Skill | Item |
| --- | --- | --- |
| Gravedigger | Athletics | Iron shovel, which can dig up buried caches |
| Cutpurse | Lockpicking | 5 lockpicks |
| Acolyte | Lore | Holy symbol (+1 Restoration spell power) |
| Mercenary | Block | Battered shield |
| Apothecary | Alchemy | Mortar and 3 reagents |
| Hedge-witch | Illusion | Charm against the evil eye (+1 save against curses) |
| Miner | Smithing | Pick, which can break cracked walls |
| Cartographer | Survival | Map case: automap notes show traps that have been spotted |

### 5.5 Portrait, name and voice

There are 40 portraits (5 per ancestry), 4 voice sets for grunts and barks, a name, and a colour tag for the
party bar.

**Quick start:** a preset party of Fighter, Rogue, Cleric and Wizard, made of mixed ancestries, so a player can
be walking within one click.

---

## 6. Attributes and the d20 core

### 6.1 Attributes

The **modifier** is `floor((score − 10) / 2)`. Scores are capped at 20, or 24 with items.

| Attribute | Drives |
| --- | --- |
| STR | Melee to-hit (non-finesse) and damage; carrying capacity (15 × STR kg); breaking doors; Athletics |
| DEX | Ranged and finesse to-hit; AC (capped by armour); weapon recovery speed (−3% per modifier point); Stealth |
| CON | HP per level; Stamina pool and regeneration; poison and disease saves |
| INT | Mana pool; arcane spell power (Destruction, Conjuration, Illusion); Lore; Enchanting |
| WIS | Divine spell power (Restoration, Warding); mana regeneration; passive Perception (finding traps and secrets) |
| CHA | Merchant prices (±5% per modifier point); the chance that wounded monsters flee; Illusion's mind effects; Command words |

### 6.2 Derived statistics

| Statistic | Formula |
| --- | --- |
| Max HP | Level 1: hit-die maximum + CON mod. Each later level: average hit die (rounded up) + CON mod, plus the level-up choice (section 7.4) |
| Max Stamina | 50 + 5 × CON mod + level-up choices |
| Max Mana | 20 + 5 × INT mod (arcane) or WIS mod (divine), whichever is higher, + level-up choices |
| AC | 10 + armour + min(DEX mod, armour's cap) + shield + other bonuses |
| Passive Perception | 10 + WIS mod + floor(Survival / 10) |
| Saves | d20 + attribute mod + (proficient ? 2 + floor(level / 4) : 0) |
| Initiative (fight start only) | d20 + DEX mod; it sets who has their first swing ready |

Regeneration per second, out of combat: HP 1% of max, Stamina 10%, Mana 3% + WIS mod × 0.5%. In combat these are
quartered. HP doesn't regenerate in combat unless a perk or spell says so.

### 6.3 Attack resolution

```
to-hit  = d20 + attribute mod + floor(weapon skill / 10) + item and perk bonuses
hit     if to-hit ≥ target AC; a natural 20 always hits and crits; a natural 1 always misses
damage  = (weapon dice + attribute mod) × (1 + weapon skill / 200) × perk multipliers
crit    = weapon dice rolled twice (plus any crit perks)
```

- **Advantage or disadvantage** means roll 2d20 and keep the higher or lower. Advantage comes from attacking from
  a flank or from behind, from a target that is stunned or asleep, or from stealth. Disadvantage comes from
  darkness without darkvision, from being blinded, or from attacking from the back row with a short weapon (that
  isn't allowed at all; see section 9).
- **Spells** use spell attack = d20 + casting mod + floor(school skill / 10), or a save DC =
  8 + casting mod + floor(school skill / 10).
- **Resistance** halves damage, **vulnerability** doubles it and **immunity** zeroes it. They apply after
  multipliers.
- **Damage types:** slashing, piercing, bludgeoning, fire, cold, lightning, acid, poison, radiant, necrotic,
  psychic, force.

### 6.4 Conditions

There are 14 conditions: Poisoned, Diseased, Bleeding, Burning, Chilled (slower recovery), Frozen, Shocked
(spell failure 25%), Stunned, Asleep, Blinded, Frightened, Paralysed, Cursed and Weakened.

Each has a duration, a save to end it (rolled every 3 s), a portrait icon and a cure (a spell, potion or rest).

---

## 7. Skills and progression

### 7.1 The 22 skills

Each skill runs from 0 to 100. It starts at 5, plus bonuses from ancestry, calling and background (capped at 35
at creation).

| Group | Skill | Raised by |
| --- | --- | --- |
| Combat | Blade | Hits with swords and daggers |
| | Blunt | Hits with maces, hammers and flails |
| | Two-handed | Hits with greatswords, greataxes and polearms |
| | Archery | Hits with bows and crossbows |
| | Unarmed | Unarmed hits |
| | Block | Blocking damage with a shield or weapon |
| | Heavy Armour | Being hit while wearing mostly heavy armour |
| | Light Armour | Being hit while wearing mostly light armour |
| | Athletics | Steps taken while over-encumbered, climbing, swimming, forcing doors, power attacks |
| Magic | Destruction | Damage dealt with Destruction spells |
| | Restoration | HP healed and conditions cured |
| | Warding | Damage absorbed by wards; dispelling |
| | Conjuration | Time that summons are active; binding weapons |
| | Illusion | Mind effects that land (fear, calm, invisibility) |
| | Enchanting | Enchanting and disenchanting items; recharging |
| Craft and stealth | Stealth | Moving unseen near monsters that would otherwise notice you; sneak attacks |
| | Lockpicking | Locks opened (by difficulty) |
| | Traps | Traps disarmed or reset; traps built (by difficulty) |
| | Alchemy | Potions brewed; reagents tasted |
| | Smithing | Items improved or forged |
| | Survival | Food cooked; tracks read; secret doors and traps found |
| | Lore | Items identified; carvings and scrolls read |

### 7.2 Skill XP

Each use gives **use XP**. The XP for the next rank is:

```
xpToNext(rank) = (4 + 0.08 × rank^1.95) / 3  (rank 15 → about 6.6; rank 50 → about 56; rank 90 → about 174)
```

The divisor of 3 came with the balance pass (section 31); M1–M8 used the curve without it.

Use XP per action is defined in `skills.json`. Examples:

- Blade: 1 + 0.1 × damage dealt.
- Destruction: 2 + 0.15 × damage dealt (it was 0.5 + 0.1 until the balance pass).
- Lockpicking: (lock DC − 5) / 2.
- Restoration: 1 + HP healed / 4 (it was HP healed / 4 until the balance pass).

**Anti-grind rules:**

- Combat skills earn only 10% XP against a target whose level is more than 5 below the character's, and nothing
  against objects or allies.
- Magic skills earn XP only when the spell has an effect: it deals damage, heals missing HP, or a ward absorbs
  something.
- Each skill can rise at most 3 ranks in a single fight.

**Faster learning:**

- Skill books. Each title gives +1 to its skill once per character.
- Trainers at the Waystones charge `rank² × 2` gold, with at most 5 lessons per character level.
- The lore bonus: each 3 journal pages collected give +5% skill XP for the rest of the Delve.

### 7.3 Character level

Every time any skill gains a rank, the character gains **character XP equal to a third of the new rank, rounded
up** (changed in M4 from the whole rank; see section 26). The XP needed for the next level is:

```
levelXp(L) = (L + 3) × 25        (L1→2: 100, L10→11: 325, L30→31: 825)
```

A heroic hero (section 5.3) needs 1.25 × that, rounded up (L1→2: 125).

Raising skills that are already high levels the character faster. That is intended: specialists level quickly.

**Kills give character XP too** (added in the balance pass, section 31). Each hero still standing when the party
kills a creature gains **3 × the creature's level**:

- × 2 for an elite, × 3 for a named champion, × 10 for a boss, and half for a split-off half;
- a tenth for a creature more than 5 levels below the hero (the same gap as the skill anti-grind rule);
- nothing for what a creature called up (summons, the risen dead), so a summoner can't be farmed.

Skills still decide what a hero is good at; kills keep the level in step with the depth. There is no level cap, but
the content is tuned for heroes who end a 12-level Delve in their mid-twenties.

### 7.4 What a level-up gives

1. **HP.** Average hit die + CON mod, automatically.
2. **A choice.** +5 HP, +10 Stamina or +10 Mana.
3. **One perk point** (section 8).
4. **On every 4th level (4, 8, 12, …).** +2 attribute points, which can go +2 to one attribute or +1 to two.
   This comes as well as the perk point, not instead of it.
5. **On levels 5, 11 and 17.** An extra attack-recovery speed-up of 10% for the character's best weapon skill.
   This stands in for the tabletop "extra attack".

Save proficiency bonus rises with level (section 6.2). The calling's hit die is fixed for life.

Character XP comes from skill ranks and from kills (section 7.3).

### 7.5 Skill ceilings

A skill can't rise above **35 + 5 × character level**, capped at 100. This stops a level-2 hero from having
80 Lockpicking.

---

## 8. Perks

Each skill has a **perk tree** of 6–10 perks. Each perk needs a skill rank, and some need an earlier perk in the
same tree.

- Perks are bought with perk points (one per level, plus Human bonus).
- They can be reset at a Waystone shrine for gold. Each reset costs more than the last.

The screen draws each tree as a branching chart. The perks you can buy are lit, and their required ranks are
marked.

### Sample trees

**Blade**

| Perk | Rank | Effect |
| --- | --- | --- |
| Edge Discipline (5 ranks) | 0/20/40/60/80 | +20% blade damage per rank |
| Quick Draw | 20 | Blade recovery −15% |
| Bleeder | 30 | Crits apply Bleeding (1d4/s for 6 s) |
| Riposte | 50 | After a successful Block, the next blade hit within 2 s auto-crits |
| Twin Fangs | 50 | Dual-wielding blades: both hands share one cooldown |
| Whirl | 70 | Power attack hits every adjacent cell |
| Executioner | 90 | Hits on targets under 25% HP have advantage and +50% damage |

**Destruction**

| Perk | Rank | Effect |
| --- | --- | --- |
| Adept Flame / Frost / Storm | 20 | That element costs 25% less mana and deals +25% damage |
| Lingering Flame | 30 | Fire spells leave a burning cell for 4 s |
| Deep Freeze | 40 | Frost spells Freeze a Chilled target (CON save) |
| Arc | 50 | Lightning spells chain to a second target in an adjacent cell |
| Overcharge | 60 | Hold the cast to spend 2× mana for 2.5× damage |
| Elemental Mastery | 80 | Ignore resistance (not immunity) |
| Cataclysm | 100 | Once per rest: the next Destruction spell is a 3×3 blast |

**Stealth**

| Perk | Rank | Effect |
| --- | --- | --- |
| Hush | 0 | Noise from steps −20% |
| Backstab | 30 | Sneak attack with any weapon ×3, ×6 with daggers |
| Light Feet | 40 | Doesn't set off pressure plates while sneaking (characters under 60 kg) |
| Shadow Step | 60 | Once per fight: step 2 cells unseen, ignoring a blocker |
| Vanish | 80 | Dropping into sneak during a fight breaks lock-on 50% of the time |

The full set of perks lives in `perks.json`. **Every skill gets at least one perk that changes how you play** (a
new action or a new interaction with the world), not only bigger numbers. Examples:

- Survival: **Forager**, which finds reagents in rubble.
- Traps: **Rig**, which picks up a disarmed trap and places it elsewhere.
- Athletics: **Vault**, which jumps a 1-cell pit.
- Lore: **Tongue of Stone**, which lets a carving answer one question.

---

## 9. Combat

### 9.1 Time

The game runs on a fixed simulation tick of 60 Hz. Everything is real-time:

- A step takes 0.25 s and a turn takes 0.2 s, both animated (section 15).
- Each hand of each hero has a **recovery timer** after use, typically 1.5–4 s, depending on the weapon and DEX.
- Monsters have the same: an attack wind-up (shown as a tell) and then recovery.
- With "Pause in combat" on, Space freezes the tick. While paused, the player can queue one action per hand.

### 9.2 Formation and reach

- The party fills one cell. Inside it are four slots: **front-left, front-right, back-left and back-right**.
- Monsters attack the **front row** by default.
  - A monster to the side attacks that side's column.
  - A monster behind attacks the back row.
  - Area attacks hit everyone.
  - If both front heroes are down, the back row is exposed.
- **Melee weapons only reach from the front row.** The exceptions are polearms, whips and Reach weapons, which
  also work from the back row.
- Bows, thrown weapons and spells work from anywhere.
- Swap two heroes by dragging portraits, or with Ctrl+1–4. A swap takes 0.5 s, during which neither can act.

### 9.3 Actions per hand

- **Click a hand** (or press its key) to use the item in it: attack, cast from a staff or wand, throw, block with
  a shield, drink, or read.
- **Hold and release** for a **power attack**. It costs Stamina, deals +50% damage and has a chance to stagger
  the target, but has 30% longer recovery.
- **Block.** Hold the shield hand. Damage from the front is reduced by (shield value + Block / 4)%, which costs
  Stamina. A perfect block (within 0.2 s of the impact) negates the hit and staggers the attacker.
- **Dual wield.** Each hand has its own cooldown, at −2 to-hit on the off-hand unless the character has the perk.
- **Unarmed.** Monks scale damage with the Unarmed skill: 1d4 + floor(Unarmed / 20) × 1d4.

### 9.4 Positioning

- **Flanking.** If the party attacks a monster from the side or behind, it gets advantage. Monsters turn to face
  the party, taking 0.5–1.5 s depending on the type.
- **Backing away.** Leaving a monster's reach doesn't trigger a free attack, which keeps the square dance
  viable. But a fast monster pursues and attacks first.
- **Crowds.** Small monsters (rats and the like) can pack up to 4 into a cell. Each attacks separately and they
  share area damage.
- **Doors.** A closing door interrupts any monster in its cell for 2 s, and deals 1d6 to it if the door is heavy.

### 9.5 Death and unconsciousness

- At 0 HP a hero is **down**. The hero is unconscious and their portrait goes grey.
- The hero gets up with 1 HP after the fight if anyone in the party is still standing. Ironman and the Cruel and
  Brutal settings change this to: they die after 3 failed death saves (one every 3 s, DC 10), unless stabilised
  with a Restoration spell, a bandage or a potion.
- **The dead** can be raised at a Waystone shrine for gold, or with the Restoration 90 perk spell Recall Soul.
  Their bones and gear stay where they fell.
- **The whole party down** ends the Delve: on Ironman, or on any setting with no save to load.

### 9.6 Encumbrance

- Up to 100% of capacity: normal.
- Up to 150%: steps take ×1.5 as long, and the hero earns Athletics XP.
- Over 150%: the hero can't move.

---

## 10. Magic

### 10.1 Learning and casting

- Spells are learned from **tomes** found in the dungeon or bought at Waystones. Reading one needs the school at
  the listed rank.
- A spell can be assigned to a hand (a "spell in hand") or to a quick slot (1–8 for the selected hero).
- **Cast time** is 0.3–1.5 s. Getting hit while casting forces a CON save, DC 10 or half the damage (whichever is
  higher), or the cast is lost.
- **Mana cost** is reduced by school skill (up to −40% at 100) and by perks.
- **Wands and staves** hold charges of a spell and recharge through Enchanting.
- **Scrolls** cast once, at fixed power, with no school needed.

### 10.2 Schools

There are 5 schools of 8 spells, plus 8 cantrips (48 in all). The names are original.

| School | Sample spells (rank required) |
| --- | --- |
| Destruction (INT) | Spark (0, cantrip), Firedart (0), Frostlance (15), Thunderclap (25, all cells adjacent), Cinder Wall (40, a wall of fire across the corridor), Acid Rain (55, a 3×3 area over time), Chain Storm (70), Starfall (90) |
| Restoration (WIS) | Mend (0), Cleanse (15, cures one condition), Rally (25, heals the party), Bastion of Light (40, regen aura), Turn the Dead (40), Restore Limb (55, cures Weakened and Cursed), Second Dawn (70, revives down heroes at 25%), Recall Soul (90, raises the dead) |
| Warding (WIS) | Ward (0, absorbs 20 + skill), Resist Element (15), Dispel (25), Spell Turning (40), Sanctuary Line (55, monsters can't enter one cell for 10 s), Anti-magic Field (70), Aegis of Ages (90) |
| Conjuration (INT) | Bound Blade (0), Summon Familiar (15, a scout that walks ahead), Raise Bones (25), Summon Flame Spirit (40), Banish (55), Open the Way (70, a two-way portal on the same level), Gate of Teeth (90, summons a horror) |
| Illusion (INT/CHA) | Light (0, cantrip), Fear (15), Calm (25), Muffle (25, silent steps), Invisibility (40), Mirror Selves (55, decoys draw 3 attacks), Frenzy (70, monsters fight each other), Unmind (90, stuns a 3×3 area) |

The **cantrips** are Spark, Light, Mend (minor), Mage Hand (pull a lever 2 cells away), Detect Traps, Unlock
Trivial (DC 10 locks), Frost Touch and Guidance.

### 10.3 Light

- The dungeon is dark. The party carries light: torches (burn time 10 min, fit in a hand), lanterns (oil, a belt
  slot) or the Light spell.
- Monsters notice lit parties from farther away.
- Darkvision (Dwarf, or a potion) shows a grey, short-range view.

---

## 11. Stealth, locks and traps

### 11.1 Stealth

- Toggle **Sneak** with X (C is the character sheet). Steps take 0.4 s, light radius is halved and step noise is −50%.
- Each tick, every monster that has line of sight runs a **notice check**:

  ```
  notice = monster Perception + light bonus + noise − (Stealth / 5 + DEX mod)
  ```

  The check fills an awareness meter, shown as an eye icon over the monster. When the meter is full the monster
  is alerted.
- A whole party sneaks at the **worst** member's Stealth.
- Attacking an unaware monster is a **sneak attack**. It deals ×2 damage for anyone, ×3 (×5 with a dagger) for a
  Rogue (section 5.2), and more with the Stealth perks.

### 11.2 Locks

- Locks have tiers: Trivial (DC 10), Simple (15), Tricky (20), Masterwork (25) and Arcane (30, which also needs
  Dispel or a key).
- **Lockpicking** is a short timing mini-game. Three pins rise and fall; click each one as it reaches the shear
  line.
  - The window width scales with (Lockpicking − DC × 2) and DEX.
  - A failed pin has a chance to break the pick.
  - Players who don't want the mini-game can pick "Auto": roll d20 + DEX mod + Lockpicking / 5 against the DC,
    taking 5 s and risking the pick.
- **Other ways in:** smash it (STR, Athletics; noisy, and may break what's inside), use a key, cast Unlock, or use
  a key found elsewhere.

### 11.3 Traps

**Detection.**

- Every hero rolls passive Perception against the trap's hide DC each time the party enters a cell within 2 cells
  of the trap.
- Elves and Cartographers get bonuses.
- The Detect Traps cantrip reveals traps for 30 s.
- A trap that has been spotted is outlined in the view and marked on the automap.

**Disarming.**

- A hero in the front row uses a hand on the trap, or on its mechanism (a plate, a tripwire or a nozzle on a
  wall).
- Roll d20 + DEX mod + Traps / 5 against the disarm DC.
- Failing by 5 or more sets it off.
- With the Traps perk **Rig**, the hero can pick up the trap's parts and set it again later.

**The 20 trap kinds:**

| # | Trap | Trigger | Effect |
| --- | --- | --- | --- |
| 1 | Pressure plate (generic) | Weight in the cell | Fires a linked device: door, dart wall, fireball spout or summoning |
| 2 | Tripwire | Moving through the cell edge | Dart volley or bell alarm |
| 3 | Pit | Stepping on it (hidden or open) | Fall to the level below, 2d6 damage, scattered party, or a spiked pit for 4d6 |
| 4 | Spike floor | Cyclic, or a plate | 3d6 piercing; the timing is visible as the spikes retract |
| 5 | Dart wall | Plate or tripwire | 1d4 piercing ×3, poisoned on a failed CON save |
| 6 | Swinging blade | Cyclic across the corridor | 3d8 slashing; time the passage |
| 7 | Fire spout | Plate or rune | Fire on a line of 3 cells for 3d6; leaves a burning cell |
| 8 | Frost glyph | Stepping on it | Freezes the hero who failed a DEX save for 4 s |
| 9 | Lightning rune | Metal weight (heavy armour) in the cell | 4d6 lightning; arcs to adjacent party members |
| 10 | Gas vent | Plate | A poison or sleep cloud that spreads 2 cells and lingers 10 s |
| 11 | Collapsing ceiling | Loud noise or a plate | 4d10 bludgeoning, unless a DEX save pushes the party back; the cell becomes rubble |
| 12 | Rolling boulder | Lever or plate | Rolls down a corridor; it crushes whatever it meets unless the party steps aside or a door stops it |
| 13 | Teleporter | Stepping on it | Moves the party to a linked cell, sometimes one-way |
| 14 | Spinner | Stepping on it | Turns the party to a random facing, silently (the compass betrays it) |
| 15 | Illusory wall | — | Looks like a wall but can be walked through; may hide a pit |
| 16 | Alarm plate | Plate | Wakes and draws monsters within 12 cells |
| 17 | Mimic | Touching the chest or door | It's a monster; it bites and grapples |
| 18 | Cursed altar | Taking the offering | A Cursed condition until cured, or a guardian spawns |
| 19 | Flooding room | A door shuts behind | Water rises over 30 s; find the drain lever, or swim (Athletics) |
| 20 | Anti-magic zone | Entering it | No spells or wands; summons vanish |

**Traps are tools too.** Monsters set off plates, fall into pits and get crushed by boulders. Luring a monster
onto a plate is a valid tactic.

---

## 12. Monsters

### 12.1 Structure

Every monster has these, held in `creatures/*.json`:

- level, HP (hit dice), AC, saves, Perception, speed (seconds per step), turn time
- attacks (wind-up, damage, reach, the slot targeted, on-hit conditions)
- resistances
- behaviour (section 12.4)
- loot table
- sprite set

Monsters are generated in four **tiers** by level band: **Lesser (L1–8), Common (L6–16), Greater (L14–26) and
Dread (L24+)**. A family's members spread across the tiers.

### 12.2 Elite and champion variants

| Variant | Spawn chance (Standard) | Effect |
| --- | --- | --- |
| Elite | 8% | +50% HP, +2 AC, +1 tier of loot, a tinted sprite |
| Champion | 2% from level 3 down | Elite, plus 1–3 **affixes**, a name, a glowing outline and guaranteed magic loot |

The **affixes** (16) are: Vampiric, Frenzied (fast recovery), Stoneskin, Blinking (teleports 2 cells),
Mortar (lobs area attacks from a distance), Shielded, Splitting (into 2 on death), Venomous, Frostbound, Burning
Aura, Thunderous, Mirrored (summons 2 copies), Warded (immune to one school), Summoner, Bloodthirsty (heals on
hit) and Unyielding (can't be staggered).

### 12.3 The bestiary (v1.0, about 60)

| Family | Members (tier) | Signature |
| --- | --- | --- |
| **Vermin** | Cellar Rat (L), Bristle Rat swarm (L), Tunnel Spider (L), Carrion Beetle (C), Weeping Spider (C), Brood Mother (G) | Packs of 4 small creatures in a cell; poison; webs block a cell until cut or burned |
| **Restless Dead** | Shambler (L), Bone Sentry (L), Barrow Archer (C), Grave Hound (C), Lantern Wight (G), Ossuary Knight (G), the Hollow King (boss) | Undead: immune to poison, vulnerable to radiant; Bone Sentries reassemble unless crushed (bludgeoning) |
| **Goblin-kin** | Gutter Goblin (L), Goblin Slinger (L), Goblin Hexer (C), Warg Rider (C), Bugbear Bruiser (C), Goblin Chieftain (G) | Gangs; flee at low morale (CHA matters); hexers buff; they set off their own traps knowingly |
| **Oozes** | Grey Slick (L), Acid Jelly (C), Mirror Ooze (C, looks like the floor), Glutton Mass (G) | Corrode weapons and armour (durability); split when cut; slow |
| **Deep Folk** | Delver Thrall (L), Deep Miner (C), Pale Overseer (C), Stone Speaker (G) | Dwarven ghosts made flesh; use the Deep Works' mine rails and steam valves against the party |
| **Constructs** | Brass Sentinel (C), Pipe Crawler (C), Forge Golem (G), Rail Juggernaut (G), the Bellwright Engine (boss) | Immune to poison and mind; lightning stuns them; some are powered by levers you can cut |
| **Elementals** | Ember Wisp (L), Cinder Imp (C), Steam Wraith (C), Magma Brute (G), Frost Remnant (G) | Immune to their own element; vulnerable to the opposite; leave hazards behind |
| **Spore-kin** | Puffcap (L, explodes in a cloud), Mycelid Walker (C), Spore Shepherd (C), Rot Colossus (G) | Infect the dead to raise more; Confusion clouds; burn well |
| **Serpent Cult** | Scaled Acolyte (C), Coil Guard (C), Naga Oracle (G), Drowned Bishop (G), Mother of Coils (boss) | Swim through flooded cells; cast curses; cultists can't be talked to |
| **Aberrations** | Gloomstalker (C, invisible until close), Eye Moth swarm (C), Tendril Horror (G), Thought Eater (D), Unborn (D) | Psychic damage; Fear; can teleport; drawn to Illusion casters |
| **The Unmade** | Unmade Hound (D), Hollow Saint (D), the Unmaker (final boss, 3 phases) | Each one uses an affix from the run's champions; the final level only |

Plus wanderers, which appear in any region: Mimic (in chests and doors), Gelid Cube (a square ooze that fills a
corridor and swallows a hero), and Lost Delver (a hostile human party member, who drops good loot).

### 12.4 Behaviour (AI)

Each monster has a behaviour profile made of these blocks:

| Block | Meaning |
| --- | --- |
| Wander / Guard / Ambush / Patrol | Idle behaviour |
| Chase | Path to the party on the grid (A*, avoids known traps unless Mindless) |
| Melee / Ranged / Caster / Support | Combat role. Ranged keeps a distance of 2–4 cells and retreats if the party closes |
| Flank | Two or more monsters try to approach from different sides |
| Flee | At HP < morale threshold (with a CHA modifier), unless Mindless or Undead |
| Door-user / Opener | Can open doors; some can pick locks |
| Swimmer / Flier / Phaser | Ignore water, pits or walls respectively |
| Pack | Shares alert state with all of its kind within 8 cells |

**Telegraphs.** Every attack has a visible wind-up (the sprite rears up, glows or flashes), between 0.4 s and
1.2 s. Big attacks have longer tells.

### 12.5 Bosses

| Boss | Level | Arena | Hook |
| --- | --- | --- | --- |
| The Hollow King | after level 4 | A crypt with 4 sarcophagi | Raises a Bone Sentry from a sarcophagus every 20 s. Smash the sarcophagi (Blunt) to stop it. At 50% HP he splits his crown into two Ossuary Knights |
| The Bellwright Engine | after level 8 | A bell foundry with conveyor floors | The floor moves the party one cell every 4 s. The Engine is invulnerable until its 3 pressure valves are shut |
| Mother of Coils | after level 12 | A flooded temple | Water rises and falls on a cycle. She is only vulnerable when the water drops. She summons Coil Guards from the pools |
| The Hollow Choir (optional) | a secret level, after 6 | A round hall | 5 singers each cast a school. Silence (Warding) or kill them in the right order or they resurrect each other |
| The Unmaker | the final level | The Unmade Heart | Phase 1: it unmakes walls, so the arena grows. Phase 2: it uses the run's champion affixes. Phase 3: the Knell tolls and the room rearranges every 15 s |

---

## 13. Items, loot and crafting

### 13.1 Slots

Each hero has: head, neck, chest, cloak, hands, belt (with 2 belt slots for quick items), legs, feet, two rings,
left hand and right hand.

The backpack is a grid of 24 cells. Big items take 2 cells. The party also shares a ground pile for each cell.

### 13.2 Bases

There are about 120 item bases:

| Kind | Examples |
| --- | --- |
| Light blades | Dagger (d4, finesse, throwable), shortsword (d6, finesse), sabre (d6, crit 19–20) |
| Blades | Longsword (d8), bastard sword (d10 two-handed) |
| Blunt | Club (d4), mace (d6, ignores 1 AC of heavy armour), warhammer (d8), flail (d8, ignores shield block) |
| Two-handed | Greatsword (2d6), greataxe (d12), maul (2d6, stagger), halberd (d10, reach) |
| Archery | Shortbow (d6), longbow (d8, STR to damage), crossbow (d10, slow reload, ignores 2 AC) |
| Thrown | Daggers, javelins (d6), bombs (alchemy) |
| Staves and wands | +spell power; charges |
| Armour | Light: padded, leather, studded (AC 11–12 + DEX). Medium: hide, scale (AC 13–14 + DEX up to 2). Heavy: chain, splint, plate (AC 16–18, no DEX, Stealth disadvantage) |
| Shields | Buckler (+1, fast block), round (+2), tower (+3, −10% recovery speed) |
| Consumables | Potions, scrolls, food, water, torches, oil, lockpicks, bandages, ropes, reagents |

### 13.3 Rarity

| Rarity | Colour | Affixes | Notes |
| --- | --- | --- | --- |
| Common | White | 0 | — |
| Fine | Green | 1 | — |
| Enchanted | Blue | 2 | Unidentified until Lore or Identify |
| Masterwork | Gold | 3–4 | Unidentified; some are cursed |
| Relic | Orange | Fixed | About 30 named items, each with a unique effect |
| Set | Teal | Fixed | 4 sets of 3–4 pieces, each with set bonuses |

**Item level** follows the dungeon level. Loot tables are weighted by region and by the monster family.

**Identification.** Lore 0 identifies Fine items. Lore 25 identifies Enchanted items, and Lore 50 identifies
Masterwork items, or a scroll of Identify can be used.

**Curses.** A cursed item can't be removed until Dispel or a shrine lifts the curse. Each curse has an upside, so
some players keep them on purpose.

### 13.4 Durability

Weapons and armour have durability. Oozes and acid wear it down faster. At 0 durability a weapon deals half
damage and armour gives half its AC. Smithing repairs it at an anvil, or with a whetstone or a patch kit
anywhere.

### 13.5 Crafting

| Craft | Where | How |
| --- | --- | --- |
| **Alchemy** | Anywhere, with a mortar | Combine 2–3 reagents. Each reagent has 4 hidden effects. Tasting one reveals the first effect, and Alchemy perks reveal more. Effects shared by the reagents make the potion. Potion strength scales with Alchemy. About 40 reagents, about 30 effects |
| **Smithing** | Anvils (Waystones, and the Deep Works' forges) | Improve an item's quality (+1 to +5 damage or AC, capped by skill). Forge new bases from ore and bars. Melt down items for materials |
| **Enchanting** | Enchanting altars (Waystones, and rare ones in vaults) | Disenchant an Enchanted or Masterwork item to learn one of its affixes (destroying the item). Apply learned affixes using Spirit Shards dropped by monsters. Strength scales with Enchanting. Recharge wands |
| **Cooking** (Survival) | At a fire (campfire kit) | Combine ingredients into meals that give buffs and restore food and water |

### 13.6 Food and water

These rules apply when the option is on:

- Each hero has Food and Water meters, which drain over an in-game day (about 40 minutes of play).
- Hungry and thirsty heroes regenerate slower and get disadvantage on CON saves.
- At 0, they lose HP.
- Food is found in the dungeon, cooked from monster parts with Survival, or bought.

### 13.7 Resting

- **Rest (R).** Time passes until the party is healed or the player cancels. Spells are relearned for free, and
  signatures that are "once per rest" recharge.
- Each hour of rest rolls for a wandering monster encounter (base 15%, scaled by density and region). On an
  encounter, the monster arrives and the party wakes. Resting behind a closed door halves the chance.
- Resting in a cleared, sealed room, or at a Waystone, is safe.

---

## 14. Dungeon generation

### 14.1 Level shape

- Each level is a **32×32 grid** of cells.
- A cell is: floor, wall, door (with sides), water (shallow or deep), pit, stairs up or down, rubble, or a wall
  with a feature on one face (lever, button, alcove, niche, carving, torch holder, keyhole or secret button).
- Each level has a **theme**, which sets the textures, lighting, ambience, the monster spawn table and the
  vault pool.

### 14.2 The algorithm

1. **Seed.** `levelSeed = hash(runSeed, depth)`. A stable PCG32 generator is used for each step.
2. **Rooms.** Place 8–14 rooms, from 2×2 to 7×7, by rejection sampling. Up to 3 **vaults** are placed first
   (section 14.3).
3. **Connect.** Build the minimum spanning tree of the room centres, then add 20% extra edges for loops. Carve the
   corridors with a corridor-width chance of 15% for 2-wide corridors (the square dance needs room).
4. **Lock and key.** Choose 1–3 lock barriers along the critical path, from the start to the down stairs. For
   each barrier, place its key (a key, a lever, a plate puzzle or a pushable crate) in the part of the level that
   is reachable **before** the barrier. The generator checks this with a flood fill.
5. **Secrets.** 2–4 secret doors leading to dead-end treasure rooms, and 1 illusory wall.
6. **Traps.** Budget = level × trap option. Place traps on corridor chokepoints and in vault entrances. Never
   place more than 2 deadly traps on the critical path without a detection hint (scorch marks, bones, a draught).
7. **Monsters.** Budget = depth × density. Draw from the theme's spawn table as groups: packs, a leader with
   followers, or lone ambushers. Keep 2 cells clear of the start.
8. **Loot.** Chests, alcoves and corpses (dead delvers have their own small loot tables), with 1–2 guaranteed
   items of the current level's tier.
9. **Validate.** Check every cell is reachable (with keys), check the stairs, and check the trap and secret
   budgets. If validation fails, reseed the step and try again (at most 20 times, then use a fallback layout).

### 14.3 Vaults

About 40 handmade rooms, written as text maps with metadata in `vaults/*.txt`. Each one has:

- size
- connectors (where it can join a corridor)
- tags (puzzle, ambush, treasure or shrine)
- which themes it fits
- its triggers and links (plate → door)

**Example puzzle vaults:**

- **The Weighing Room.** Four plates. Drop items to balance them and open the gate.
- **Mirror Corridor.** Teleporters loop the party until they spot the spinner that is out of place.
- **The Choir Bells.** Ring 4 bells in the order carved on the wall. Lore reads the carving.
- **The Rolling Deep.** Outrun a boulder to a side alcove.
- **Lantern Gate.** Carry lit torches into 3 sconces at once (a party of 4 helps; Mage Hand helps the lone hero).

**The format:**

```
# vault: weighing-room
# size: 7x7  tags: puzzle  themes: cellar,works  connectors: N3,S3
#######
#P...P#
#.....#
#..G..#      G = gate (linked to all P plates; opens when total weight 20–25 kg on each)
#.....#
#P...P#
###D###
```

### 14.4 Stairs and levels

- Going down the stairs saves an autosave (except in Ironman, where the single save is overwritten).
- A level that has been left stays as it was. Monsters don't respawn, apart from wanderers that arrive during
  long rests.
- **Pits drop the party one level down** into a "landing" cell that the generator always places on the level
  below. The stairs down must be reachable from the landing as well as from the start, working the level's locks
  from there. From the landing, the way must need no hidden door and must not cross the stairs up.
- Below the first level the start holds the stairs up, and stepping on them climbs. The way from the start to the
  stairs down must not run back over that cell.

---

## 15. The first-person view

### 15.1 Renderer choice

**Decision: use a software renderer, written in C#, that draws textured, lit 3D cells into a fixed-size
framebuffer. Skia then scales the framebuffer up to the window.**

The reasons:

- It's deterministic and needs no GPU, so headless tests can render real frames and compare them against golden
  images.
- It avoids the OpenGL control-sizing problems seen on Hyprland (AVALightWave), and it runs the same on Windows,
  macOS, Linux and ARM.
- A grid dungeon is cheap to draw: at most about 7 cells deep and 9 wide are visible.

Draw it behind an `IViewRenderer` interface, so an OpenGL or Vulkan back end can be added later without touching
the game.

### 15.2 Renderer specification

- **Internal resolution.** 640×400 by default (16:10). It can be set to 480×300 or 960×600. Scaling uses nearest
  neighbour or bilinear (an option).
- **Ray casting.** Each screen column casts one ray through the grid (a DDA walk) and draws the first wall it
  meets as a textured strip. The floor and ceiling either side are cast row by row. This is exact for a grid
  with level floors and full-height walls, and it is far cheaper than general triangles.
- **Doors** stand in the middle of their cell and slide up into the ceiling. A ray records each door panel it
  crosses (up to 4) and carries on, and the doors are drawn over the far wall, nearest last. A shut wooden door
  ends the ray. A portcullis's gaps are transparent, so you see through it.
- **Wall features.** A lever is a wall texture of its own, chosen by the floor cell and the face. The sides of a
  doorway use a dressed-stone frame texture.
- **The eye** sits 0.4 of a cell behind the party's position, along the facing, so the wall ahead is about a
  cell away. Less than 0.5 keeps the eye inside the party's own cell at every yaw.
- **Textures** are 64×64, drawn in code by `TextureFactory` using integer hashing only, so the same seed gives
  the same pixels everywhere. A 256-colour palette per theme and 128×128 textures remain options.
- **Picking.** The renderer keeps, for each column, which wall face and door it drew and over which rows, so a
  click in the view maps to a `WorldTarget`.
- **Lighting.**
  - Per-vertex light from the party's light source (falloff by distance, with a flicker for torches), plus static
    light sources in the level (braziers, glowing fungus, lava). The light is interpolated across each span.
  - Fog colour per theme.
- **Sprites (from M2).** Monsters and items on the floor are camera-facing billboards.
  - Monsters have frames for front, side and back, plus wind-up, attack, hit and death frames, chosen by the
    angle between the monster's facing and the camera.
  - Small creatures in a crowd are offset within their cell.
- **Motion.**
  - A step tweens the camera position over 0.25 s with ease-in-out and a small head bob (which can be turned
    off).
  - A turn tweens the yaw over 0.2 s.
  - Input is buffered: one step and one turn queue.
- **Effects.** Projectiles are sprites or particles with their own light. Screen shake on big hits. A red
  vignette at low HP. Particles (sparks, gas, spores, dust) are drawn as point sprites.
- **Budget.** At most 8 ms of CPU per frame at 640×400 on a mid-range ARM laptop, so the game holds 60 fps with
  the simulation tick.

### 15.3 Art

- **Textures and sprites.** First choice: CC0 or CC-BY packs (OpenGameArt and similar). Each one is listed in
  `assets.manifest.json` with its source, licence, author and any changes, and `CREDITS.md` is generated from
  the manifest (the same process as AVAAshenReliquary).
- **Gaps.** Where no pack fits, art is drawn in code or made procedurally by `tools/` scripts. Procedural brick,
  stone, dirt and moss textures are made with noise.
- **Placeholders (M0–M2).** Flat-coloured procedural textures and silhouette sprites, so the game can be played
  before any art is chosen.
- **First pack (M9).** Walls, floors and ceilings come from Screaming Brain Studios' CC0 pack (section 32).
- **Creatures (M9).** No free pack covers the bestiary, so the creatures are drawn in code as shaded pixel art
  (section 33).

---

## 16. UI, controls and audio

### 16.1 Screen layout (at 1280×800 and up)

```
┌────────────────────────────────────────────┬───────────────┐
│                                            │  COMPASS  N   │
│                                            │  ┌─────────┐  │
│            FIRST-PERSON VIEW               │  │ minimap │  │
│              (640×400 scaled)              │  │ 9×9     │  │
│                                            │  └─────────┘  │
│                                            │  Effects/buffs│
│                                            │  Message log  │
├──────────┬──────────┬──────────┬──────────┼───────────────┤
│ Hero 1   │ Hero 2   │ Hero 3   │ Hero 4   │ Quick slots   │
│ portrait │ portrait │ portrait │ portrait │  1 2 3 4      │
│ HP ST MP │ HP ST MP │ HP ST MP │ HP ST MP │  5 6 7 8      │
│ [L] [R]  │ [L] [R]  │ [L] [R]  │ [L] [R]  │ Rest Map Menu │
└──────────┴──────────┴──────────┴──────────┴───────────────┘
   front-left front-right back-left  back-right
```

- **Hands [L] and [R]** show the item in that hand with a radial cooldown sweep. Click one to use it.
- **Portraits** flash when the hero is hit, show condition icons and turn grey when the hero is down. Drag one
  portrait onto another to swap them.
- **Overlays** sit over the view, and the game keeps running underneath unless paused:
  - Inventory (I), with the backpack grid, paper doll and character sheet tabs.
  - Skills and perks (K).
  - Spellbook (B).
  - Full map (M), with notes.
  - Journal (J).
- **Interacting with the world:** click in the view on a lever, a button, an item on the floor or a door. Items
  on the floor can be dragged into the backpack, and items can be thrown from the hand into the view.
- **Narrow windows.** Below 1000 px wide, the side panel moves under the party bar.

### 16.2 Controls

| Action | Keys |
| --- | --- |
| Step forward / back / strafe left / strafe right | W S A D (also keypad 8 2 4 6; the Up and Down arrows step) |
| Turn left / right | Q E (also the Left and Right arrows, and keypad 7 9) |
| Use what is ahead: pull the lever, open or close the door | F, Enter or keypad 5, or click it in the view |
| Use hand: hero 1–4, left / right | Mouse on the hand, or keys 1–8 (1/2 = hero 1 L/R …) |
| Quick slots | F1–F8 for the selected hero |
| Spellbook | B |
| Select hero | Tab cycles; Ctrl+1–4 swaps positions |
| Sneak | C |
| Pause / queue (if enabled) | Space |
| Rest | R |
| Inventory, Skills, Book, Map, Journal | I, K, B, M, J |
| Menu | Esc |

**Gamepad** support is a v1.1 stretch goal.

Keys are remappable. Movement goes through one `InputHandler`, with the same direction-sign convention as the
engine. Test the keys through `InputHandler`, not the engine, to catch any mismatch in the up/down sign. Track
held keys by `PhysicalKey`, because on this desktop Shift's KeyUp arrives as CapsLock.

### 16.3 Audio

- OpenAL Soft through Silk.NET, the same audio layer as the other AVA games. Effects are positional by cell
  (left and right, and attenuated by distance and walls).
- Each region has ambient beds: drips, wind, machinery or choir hum. The bosses have music stings.
- **Footsteps and monster barks are cues.** You hear monsters before you see them. The sound travels through open
  doors and is muffled by closed ones.
- Sound effects are synthesised by a standard-library Python tool, `tools/make_sounds.py`, or taken from CC0
  packs listed in the manifest.
- Use the 2048-frame OpenAL period through ALSOFT_CONF, as the other AVA games do, to stop choppy audio in the
  VM. (As built: 4096 frames in a VM by default; section 36.)

---

## 17. Saves and difficulty

### Saves

- Saves are JSON, holding:
  - the run seed and Delve options
  - the party
  - for each level visited, the **delta from the generated level**: opened doors, picked-up items, dead
    monsters, sprung traps and map notes
  - the time, and the version
- Saves are small because the level itself is regenerated from the seed.
- Saving: 10 manual slots, 3 rotating autosaves (on stairs and Waystones, and every 10 minutes) and a quicksave
  (F5 / F9).
- **Ironman** keeps one save, written on stairs, Waystones and exit. It is deleted when the party dies.
- The save folder is the OS's application data folder. An `AVAHACK_SAVE_DIR` environment variable overrides it,
  for tests. Tests never touch the user's real folders.

### Difficulty

**Monster strength** scales these:

| Setting | Monster HP | Monster damage | Monster to-hit | Down rules |
| --- | --- | --- | --- | --- |
| Gentle | ×0.75 | ×0.75 | −2 | Get up after the fight |
| Standard | ×1.0 | ×1.0 | — | Get up after the fight |
| Cruel | ×1.25 | ×1.25 | +1 | Death saves apply |
| Brutal | ×1.5 | ×1.5 | +2 | Death saves apply; champions 2× as often |

---

## 18. Technical architecture

### 18.1 Solution layout

The same split as AVAAshenReliquary: a pure rules and world core, and an Avalonia front end.

| Project | Responsibility |
| --- | --- |
| `src/AVAHack.Core` | Rules (dice, checks, conditions), characters and creation, skills and perks, items, magic, monsters and AI, the world (grid, cells, triggers), the generator, the save model, and the content database (embedded JSON in `Content/Data/`). It has no UI references and is deterministic for a given seed |
| `src/AVAHack.Render` | The software 3D view (`IViewRenderer`, `SoftwareViewRenderer`), texture and palette loading, light tables, the sprite atlas. It depends only on Core and `System.Numerics`, and outputs a `uint[]` framebuffer |
| `src/AVAHack.App` | The Avalonia window, the view control (copies the framebuffer to a `WriteableBitmap` and draws it with Skia), the HUD, overlays, input, audio and settings |
| `tests/AVAHack.Core.Tests` | xUnit v3: formulas, rules, generator invariants and AI |
| `tests/AVAHack.Render.Tests` | Golden-image tests for the view (rendered headless) |
| `tests/AVAHack.App.Tests` | Avalonia.Headless window tests and screenshots (`AVAHACK_SHOTS=<folder>`) |
| `tools/` | `make_sounds.py`, `make_textures.py` (procedural textures), `vault_lint.py`, `build_credits.py` |

### 18.2 Runtime model

- **The simulation tick** is a fixed 60 Hz in Core (`GameEngine.Tick`). The view interpolates the camera between
  ticks.
- **The engine is first, the drawing second.** All gameplay is in Core. The App reads the state and sends
  commands (`Press(action)`, `UseHand(hero, hand)`, `ClickWorld(cell, face, uv)`). This keeps everything testable
  without a window.
- **Events.** Core raises typed events (Hit, Miss, ConditionApplied, SkillRankUp, LevelUp, TrapSprung, DoorOpened
  and so on). The App turns them into floating text, sounds and log lines.
- **RNG.** PCG32, seeded per subsystem (`world`, `loot`, `combat` and `ai` streams derived from the run seed), so
  a change to the loot code doesn't change level layouts. The debug overlay (F3) shows the rolls.
- **Content.** JSON in `Content/Data/`: `ancestries.json`, `callings.json`, `backgrounds.json`, `skills.json`,
  `perks.json`, `spells.json`, `items/*.json`, `affixes.json`, `creatures/*.json`, `traps.json`, `themes/*.json`
  and `vaults/*.txt`. Every file is checked against a JSON schema by a test.

### 18.3 Example content (`creatures/restless-dead.json`)

```json
{
  "id": "creature.bone_sentry",
  "name": "Bone Sentry",
  "family": "restless_dead",
  "tier": "lesser",
  "level": 3,
  "hitDice": "3d8",
  "ac": 13,
  "saves": { "str": 1, "dex": 1, "con": 2, "int": -3, "wis": 0, "cha": -3 },
  "perception": 9,
  "stepSeconds": 0.9,
  "turnSeconds": 0.8,
  "resist": ["piercing"],
  "vulnerable": ["bludgeoning", "radiant"],
  "immune": ["poison", "mind"],
  "attacks": [
    { "id": "rusty_blade", "windup": 0.7, "recovery": 2.0, "toHit": 4, "damage": "1d8+1", "type": "slashing", "targets": "facing_row" }
  ],
  "behaviour": ["guard", "chase", "melee", "mindless"],
  "special": [{ "kind": "reassemble", "seconds": 8, "unlessKilledBy": "bludgeoning" }],
  "loot": "loot.undead.lesser",
  "sprite": "sprites/bone_sentry"
}
```

### 18.4 Build and conventions

- `global.json` pins SDK 10.0.400 or later (`rollForward: latestFeature`).
- `Directory.Build.props`: `TreatWarningsAsErrors`, nullable, implicit usings, invariant globalization.
- `Directory.Packages.props` for central package versions: Avalonia 12.1.4, SkiaSharp 3.119.4, Silk.NET.OpenAL
  2.23.0, xunit.v3 3.2.2.
- No trimming: `PublishTrimmed=false`, because Avalonia uses reflection for XAML. Publish single-file and
  self-contained.

### 18.5 CI and releases

`.github/workflows/build.yml` runs on every push:

1. Build and test.
2. Publish 8 builds: win-x64, win-x86, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64 and linux-arm.
3. A push to `main` refreshes the rolling `latest` pre-release. A `v*` tag makes a proper release.
4. After a release, ask the portfolio site to rebuild. Add the card only after the release has published.

---

## 19. Testing

| Area | Tests |
| --- | --- |
| Rules | Every formula in sections 6–9: modifiers, to-hit, damage scaling, advantage, saves, resistance order, conditions |
| Progression | XP curves; use XP and anti-grind rules; skill ceilings; level-up choices; perks' requirements and effects; the respec cost |
| Combat | Recovery timers; reach by formation slot; flanking advantage; blocking and perfect blocks; death saves |
| Generator | For 1,000 seeds per theme: everything reachable, keys before locks, stairs connected, trap and secret budgets, deterministic output (the same seed gives the same level, byte for byte) |
| Vaults | `vault_lint.py` and a test: every vault parses, connectors line up, and triggers link to devices that exist |
| AI | Pathing around traps; flee thresholds; pack alerts; door use |
| Traps | Each of the 20 kinds: trigger, detection, disarm, effect, monster interaction |
| Saves | Round trip (save, load, compare) at many points; old-version saves load |
| Renderer | Golden images for fixed scenes (corridor, door, lit room, sprites at 3 distances); a frame-time benchmark |
| Balance | Bot parties of each calling mix play simulated Delves (`AVAHACK_BALANCE_RUNS`) and report depth reached, deaths, level by depth and skill spread. The targets are in section 20 |
| App | Headless window tests: creation flow, HUD, overlays, input mapping. These also render the README's screenshots |

---

## 20. Milestones

Each milestone is finished when its exit test passes.

| # | Milestone | Contents | Exit test |
| --- | --- | --- | --- |
| **M0** | Skeleton and view. *Done (2026-11-06)* | Solution, CI, the software renderer, a handmade test level, step and turn tweens, doors and a lever | Walk a 16×16 test level at 60 fps; golden images pass. *Result:* a frame takes about 2.3 ms at 640×400 on an 8-core ARM machine; 7 golden images pass |
| **M1** | Rules core. *Done (2026-11-06)* | Attributes, d20 checks, conditions, the skill XP and level formulas, perks framework, content loader and schemas | Unit tests cover sections 6–8. *Result:* 94 new Core tests (120 in all) |
| **M2** | One hero, one fight. *Built (2026-11-06); the exit test waits on the user's play* | A Fighter, melee with recovery timers, power attacks and blocking, 6 Upper Hollow monsters, AI blocks, pressure plates and pits | A 15-minute handmade level is playable and fair: the user's own play. *So far:* the balance bot wins The Cellars 10 times in 12, in about 2.6 minutes of game time |
| **M3** | Party and creation. *Done (2026-11-06)* | 4-hero formation, the creation screens (all ancestries, callings and backgrounds), inventory and paper doll, loot and rarity, saving | Create a party and finish the M2 level with it. *Result:* the quick-start party (bot) gets through The Cellars about 9 times in 12; the creation screens make any party of one to four |
| **M4** | Progression. *Done (2026-11-06)* | Use-based skills for all 22, levelling, all perk trees (placeholders are allowed for later perks), trainers, books | A bot run shows 3+ skills over 40 by level 8 for each calling. *Revised:* that target didn't fit the level curve; a bot run showed every calling at level 8 with its best skill at 45 or more and its third-best at 35 or more (section 26). *Revised again in the balance pass:* kills now give character XP, so level 8 comes after 40–70 fights; each calling's best skill is then 35 or more and its second 25 or more (section 31) |
| **M5** | Magic. *Done (2026-11-06)* | 5 schools, cantrips, tomes, wands, scrolls, light | Each calling's opening spells work; Spellblade and Paladin hybrids play. *Result:* a test casts every calling's starting spells and checks each one does something; in the progression report the spellblade raises Destruction and the paladin Warding beside their weapons |
| **M6** | Generator. *Done (2026-11-06)* | 32×32 generation, lock and key, secrets, 20 traps, 15 vaults, Delve options, seed codes | The 1,000-seed invariant suite passes; a 4-level Delve is playable. *Result:* 1,000 seeds at each of depths 1, 5 and 9 give finishable levels with no fallbacks; the bot's quick-start party gets through a 4-level Delve on 6 seeds of 8 (one fell on level 4, one got stuck) |
| **M7** | The bestiary. *Done (2026-11-07); target revised in the balance pass (2026-11-07)* | All 10 families, elites, champions and affixes, the 5 bosses, the Waystones | A full 12-level Delve on Standard takes 8–12 hours (bots) and ends at about level 30. *At M7* the bot averaged depth 3.8 with its heroes still level 1. *After the balance pass* (section 31): a bot that clears each level wins 11 of 16 seeds, averages depth 12.2 of 13, and its heroes end at level 22–26. The bot needs about an hour of game time, since it never stops to think, read or manage its packs; the hours a person takes have to come from the user's play. *Revised target:* a full Delve is winnable and ends in the mid-twenties |
| **M8** | Crafting and survival. *Done (2026-11-07)* | Alchemy, Smithing, Enchanting, cooking, food and water, resting encounters | Each craft yields an item that matters by mid-game. *Result:* at skill 45, a test brews a healing potion stronger than a bought one, works a weapon to at least +3, binds two affixes into a helm and cooks a meal whose buff lasts over five minutes (section 30) |
| **M9** | Polish and release | Art from the manifest, audio, the hall of fame, Ironman, help pages, README and screenshots, portfolio card | The user plays through a Delve; v1.0 tag |

**The MVP is M0–M2.** It proves the view, movement and the real-time d20 fights feel right before anything else
is built.

---

## 21. Open questions

1. **Party default.** Is a party of 4 the right default, with the Lone Delver as an option, or should it be the
   other way round?
2. **Art.** Should the art be retro pixel (64×64 textures in 256 colours) or crisper (128×128 in true colour)?
   This decides the renderer's texture path and which packs fit.
3. **Lockpicking.** Should it have the timing mini-game, or only the dice roll?
4. **Pause.** Should real-time with pause be the default, or pure real time?
5. **Gamepad.** Should a controller be supported in v1.0 or v1.1?
6. **Town.** Are the Waystones enough, or should Harrowgate be a visitable town between regions?

---

## 22. Implementation decisions (M0)

- **The renderer is a column ray caster** (section 15.2), not a triangle rasteriser. A grid with level floors and
  full-height walls needs nothing more, and it leaves room in the frame budget. Raised floors, stairs seen from
  a distance, or slopes would need the rasteriser; `IViewRenderer` keeps that open.
- **The party holds its new cell from the start of a step.** The camera glides there over 15 ticks, with an
  ease in and out and a dip of 0.025 cells mid-step. A door can't close on a cell the party has started into.
- **Turning** sets the new facing at once and eases the yaw over 12 ticks.
- **Walking into a wall** plays a 10-tick nudge toward it and drops anything queued, so holding a key against a
  wall doesn't stack up bumps.
- **The queue** holds 2 actions while one is playing. Further presses are dropped. Key repeat therefore walks
  steadily without running ahead of the player.
- **One update runs at most 0.25 s of ticks,** so a stall (a window drag, say) never fast-forwards the game.
- **Doors** take 1 s to open or close and are passable only when fully open. A door waits, raised, while the
  party stands in it. Hand doors open with Use (F) or a click from the next cell. A portcullis only answers its
  lever. A lever is used from its own cell, facing its wall.
- **Use is on F (and Enter, keypad 5),** not Space, because Space is kept for pausing combat (section 9.1).
- **Level files** for handmade levels and test levels: `name:`, `start:` and `lever:` directives, then `map:`
  and the rows (`#` wall, `.` floor, `+` hand door, `P` portcullis, `O` open portcullis). Built-in levels are
  embedded in Core under `Content/Levels/`. The vault format (section 14.3) will extend this one.
- **Golden images** allow up to 0.5% of pixels to differ by more than 24 in a channel, for floating-point
  differences between machines. `AVAHACK_UPDATE_GOLDEN=1` rewrites them.

---

## 23. Implementation decisions (M1)
24. Implementation decisions (M2)
25. Implementation decisions (M3)
26. Implementation decisions (M4)
27. Implementation decisions (M5)
28. Implementation decisions (M6)

- **22 skills, not 20.** The table in section 7.1 always listed 22 (Survival and Lore are in the craft group). The
  count elsewhere was corrected to match.
- **The content files are the schema.** Each file under `Content/Data/` is read into C# records
  (`Content/Definitions.cs`) with strict options. The read fails on any of these:
  - an unknown field;
  - a missing required field;
  - a null where none is allowed;
  - an enum given as a number or an unknown name.

  `ContentDb.Validate` then checks that the files agree with each other:
  - ids are unique, and every skill and condition is defined;
  - callings have six majors and three minors, two saves and a real hit die;
  - perk prerequisites exist, are in the same tree and don't form a cycle;
  - perk ranks rise;
  - each tree has 6–10 perks, at least one of which changes play;
  - traits, effects and dice are recognised.

  `LoadBuiltIn` refuses content with any problem. This replaces separate JSON Schema files, which would have
  needed a second copy of every shape.
- **Minor skills** for each calling were chosen to fit its role. They are listed in `callings.json`.
- **Random numbers.** PCG32 (checked against the reference sequence) with `RngStreams` for world, loot, combat
  and AI, each seeded from the run seed by SplitMix64. Tests script dice with `ScriptedRng`, whose values are die
  faces.
- **Skill XP:**
  - XP is banked toward the next rank, and any remainder carries over.
  - At the skill ceiling, or once a skill has risen 3 ranks in the current fight, extra XP is thrown away, not
    saved for later.
  - Books and trainers also give a rank, and also give character XP. Both respect the ceiling.
  - Trainer lessons reset at each level-up.
- **The anti-grind rules** are only for combat and magic skills:
  - Objects and allies teach combat skills nothing.
  - A spell with no effect teaches nothing.
  - A target more than 5 levels below teaches a tenth.

  Craft skills are limited only by their DCs and the ceiling.
- **Levels:**
  - HP is worked out from the level, so a CON increase adds HP for every level so far, as in the tabletop game.
  - New maximum HP arrives already healed.
  - The 2 attribute points of a 4th level can go at most 2 into one attribute in that level. Scores stop at 20,
    and the item cap of 24 waits for M3.
- **Perk resets** cost 200 gold, doubling each time.
- **Perk effects** are data. Each one has a kind (`damagePct`, `recoveryPct`, `manaCostPct`, `noisePct`,
  `sneakMultiplier` or `flag`), a value and a scope. `PerkTotal` sums them by rank and `HasPerkFlag` finds the
  named behaviours. Only the three sample trees are written so far (22 perks). The Stealth tree gained **Unseen
  Hand** (rank 20: picking locks and disarming traps make no noise) to reach the minimum of six.
- **Conditions** are data too (`conditions.json`):
  - each has its duration, its save attribute, any damage per second, effect flags, penalties and its cures;
  - Cursed has no save and no time limit, so only a cure ends it;
  - Diseased isn't cured by rest;
  - reapplying a condition keeps the longest duration, the highest DC and the biggest damage.
- **Ancestry traits** in the rules so far:
  - Halfling luck: the first natural 1 in a fight is rerolled.
  - Dwarven poison resistance: advantage on the save and half damage.
  - Gnome illusion ward: advantage on saves against illusion.
  - Elven sleep immunity.
  - Orc-blooded Relentless: once per rest, a blow that would drop them leaves them at 1 HP.
  - Saurian hide: counts as 2 armour when unarmoured.
  - Human Versatile: +10% skill XP, and the extra perk point.

  Darkvision, holding breath, raw meat and the Spark cantrip are carried as traits for later milestones.
- **A downed hero doesn't regenerate.** Getting up after a fight is part of combat (M2).
- **Merchant prices** move 5% per CHA point, held between 0.5× and 1.5×.

---

## 24. Implementation decisions (M2)
25. Implementation decisions (M3)
26. Implementation decisions (M4)
27. Implementation decisions (M5)
28. Implementation decisions (M6)

- **One hero, as a Lone Delver.** M2 plays Brannoc, the quick-start dwarf fighter, alone, so the Lone Delver rules
  of section 4 apply: +50% HP, +2 to saves and twice the skill XP. He has a longsword, a round shield and chain
  mail: AC 18 and 19 HP. Weapons, shields and armour have small content files (`weapons.json`, `shields.json`,
  `armour.json`), which M3 will grow into the full item system.
- **The hands** are keys 1 (left, the shield) and 2 (right, the weapon), or a click on their boxes in the hero bar.
  Right-clicking the view also swings.
  - **The weapon swings when the key is released.** Held for 0.5 s or more, the swing is a power attack: +50%
    damage, 15 Stamina, 30% longer recovery and a 30% chance to stagger.
  - **The shield blocks while it is held.** Tapped (released within 0.25 s), it bashes instead: 1d4 + STR
    bludgeoning, trained by Block, 5 Stamina and a 20% chance to stagger.
  - **A perfect block** (raised no more than 0.2 s before the blow lands) stops all of it and staggers the
    attacker.
  - **Any later block** takes off (shield % + Block / 4)%, at most 90%, for 5 Stamina. Blocks only work against
    blows from the cell ahead.
  - **A staggered creature** reels for 0.8 s and loses its tell.
- **Creatures are data** (`creatures.json`, read and checked like the other content), with the behaviour blocks of
  section 12.4. All of them work except patrol and flank. M2 has the six lesser creatures of the Upper Hollow:
  Cellar Rat, Tunnel Spider, Shambler, Bone Sentry, Gutter Goblin and Goblin Slinger.
- **Noticing:**
  - A creature sees the party out to (Perception − 5) cells, at least 2, if it has line of sight. Walls and shut
    wooden doors block sight; a portcullis doesn't.
  - Ambushers wait until the party is within 2 cells.
  - Anything next to the party notices it.
  - A blow is heard within 4 cells, through doors and round corners.
  - A pack's alarm reaches the same kind of creature within 8 cells.
  - Once aware, a creature stays aware. The awareness meter of section 11.1 arrives with stealth.
- **A creature's blow lands at the end of its tell.**
  - If the party has stepped out of the cell ahead by then, it whiffs. This is what makes the square dance work.
  - Creatures must face the party to strike. A quarter turn costs the creature's turn time, and an about-face
    costs double.
  - Attacking a creature that isn't facing the party, or isn't yet aware of it, has advantage.
  - Hitting an unaware creature is a sneak attack: ×2.
- **Thrown stones** travel along straight rows and columns only, at 6 cells a second.
  - They hit the first creature in the way as well as the party.
  - A slinger backs away when the party closes in, and fights hand to hand only when cornered.
- **Paths** come from a breadth-first search on the grid (every step costs the same here, so this matches A*).
  - The first step must be free now; cells further along ignore other creatures, which will have moved on.
  - The mindless walk into pits and die there. Everything else avoids every pit, seen or not.
  - Openers (goblins) open wooden doors.
  - A door never closes on a creature, just as it never closes on the party.
- **Small creatures** share a cell, up to four, each drawn in its own corner. A blow hits the nearest first.
- **Bone Sentries** fall into a pile of bones that stands up again at full HP after 8 s, unless the killing blow was
  bludgeoning. A pile can also be crushed afterwards with a bludgeoning blow; a shield bash will do.
- **The fight** is on while any aware creature is within 10 cells, and ends after 3 s of calm. Skill caps per fight,
  regeneration in a fight, and Halfling luck use this.
- **Pits** drop the party 2d6 damage to their landing cell on the same map. The multi-level dungeon is M6.
  - A hidden pit is spotted when the party enters a cell within 2 of it, if passive Perception meets its DC.
    Spotting one teaches Survival.
  - Creatures falling in are gone.
- **Plates** are toggle (each press flips the linked doors) or hold (the doors are open only while pressed). The
  party or any living creature presses them.
- **Notes** show the first time the party steps on their cell. **Stairs** end the level.
- **Death:** when the lone hero falls, the run is over, and Enter starts a new one with a new seed. Death saves and
  getting up after a fight wait for the party (M3).
- **No rest key yet.** Out of a fight the hero regenerates 1% of HP a second (section 6.2), so waiting heals. Rest
  and its encounters are M8.
- **Level-ups** happen and are kept. The choice of HP, Stamina or Mana waits as "Level up!" on the hero bar until
  the level-up screen in M4.
- **Pause:** Space freezes everything. Queueing hand actions while paused is not done yet.
- **Creatures on screen** are 64×64 pictures drawn in code (`SpriteFactory`), with views from the front, the side
  (mirrored for the other side) and the back. Each view has idle, wind-up, strike and dead frames. They are drawn
  as camera-facing pictures against a per-pixel depth buffer, so they hide behind walls and show through a
  portcullis's gaps. The tell is the wind-up frame plus a pulsing glow; a hit flashes them red. Numbers float up
  from what was hit.
- **The screen** is a fixed 1280×800 canvas scaled to the window:
  - the view (960×600) with the compass;
  - a side panel with the level name, time and kills, an automap of the cells seen (3 cells' sight), and the
    message log;
  - a hero bar with a portrait, HP, Stamina, Mana and XP, the two hands with cooldown sweeps and a power-attack
    charge bar, conditions, and the controls.
- **The Cellars** (`Content/Levels/the-cellars.txt`):
  - 32×32 cells, 24 creatures, two levers, a plate, two pits (one hidden), nine notes and the stairs.
  - The route goes down to a dry cistern for the lever that opens the crypt. The crypt's lever opens the goblin
    warren, and the warren's plate opens the stairs.
  - A test walks the map working the mechanisms in order, and checks that every floor cell, every creature and
    the stairs are reachable.
- **Balance bot.** `BalanceReport` plays the level with a simple bot over 12 seeds (`AVAHACK_BALANCE_RUNS` for
  more). The bot:
  - reacts after 0.15–0.5 s;
  - sees only half the tells, and raises its shield at a random point in the last half second when it does;
  - rests when below half HP.

  It wins about 10 runs in 12, in about 2.6 minutes of game time. A first-time player exploring blind should
  take roughly 10–15 minutes.

---

## 25. Implementation decisions (M3)
26. Implementation decisions (M4)
27. Implementation decisions (M5)
28. Implementation decisions (M6)

- **The party** (`Party`) has four slots: front left, front right, back left and back right. A party of one is a
  Lone Delver.
- **Who gets hit** depends on where a blow comes from: from ahead, the front row; from the left or right, that
  column; from behind, the back row. If those are all down, anyone standing.
- **Back-row attacks.** Melee only reaches from the front row; reach weapons and bows work from the back. A bow
  shoots the first creature down the line ahead, within its range. Arrows are unlimited.
- **The fallen.** A downed hero gets up with 1 HP when the fight ends, if anyone is still standing. The whole party
  down ends the game.
- **Every hero has their own two hands.** Keys 1–8 are heroes 1–4, left then right. Tab selects the next hero, and
  Ctrl+1–4 moves the selected hero to a slot.
- **Encumbrance (spec 9.6):** over 100% of capacity, steps take half as long again and train Athletics; over 150%,
  the party can't move.
- **Items** are instances (`Item`) of a base from any of the item files:
  - weapons, shields, armour;
  - gear: helms, gloves, boots, greaves, cloaks, belts, rings, amulets;
  - consumables, and skill books (one per skill).

  Each has a rarity, rolled affixes, whether it's identified, a quality (Smithing) and a count for stacks.
- **The backpack** has 24 slots, one item per slot (big items don't take two). Gold is a party purse.
- **Rarity** is rolled at Common 60, Fine 25, Enchanted 11, Masterwork 3.5 and Relic 0.5. The Delve's magic-items
  option scales the weights, and chests add a bonus.
  - Fine items have 1 affix, Enchanted 2, Masterwork 3–4. 15% of Masterwork items also carry a curse.
  - There are 31 affixes: to hit, damage, recovery, extra elemental damage, AC, resistances, attributes, HP,
    Stamina, Mana, regeneration, skill XP, skill bonuses and two curses. An affix's value grows with the item
    level.
  - Relics are fixed named items; there are 6. One set (the Harrowgate Watch, 3 pieces) has bonuses for 2 and 3
    pieces worn. More of both come with the bestiary.
- **Identifying.** Enchanted items need Lore 25 to identify; Masterwork items, relics and sets need Lore 50. The
  party's best Lore identifies on pickup, and gains Lore XP for it. A scroll of Identify reveals everything
  carried. Unidentified affixes still work.
- **Curses.** A cursed item won't come off until a shrine or Dispel lifts the curse.
- **Item bonuses:**
  - Attributes can reach 24 with items, but only 20 without.
  - **Skill bonuses** count for rolls (`SkillLevel`), but the bare rank still sets progress.
- **Chests** (`chest: x y table [locked dc]` in a level) block their cell.
  - Use, facing one, opens it, and the contents spill onto the floor in front of the party.
  - The party's best lock picker rolls d20 + DEX + Lockpicking / 5 against a lock's DC.
- **Loot.** Creatures drop their loot table where they die; elites and champions drop more. G picks up everything
  in the party's cell.
- **Saves** (`SaveGame`) hold:
  - the party: every number, item, condition and hand cooldown;
  - for each level, what has changed: doors, levers, plates, pits found, notes read, chests, floor items, explored
    cells, and every creature with its current action and conditions;
  - the four random streams.

  A loaded game plays on exactly as the original would (a test checks this). A step in progress isn't saved.
  Saves are JSON files in `AVAHACK_SAVE_DIR` or the user's local application data: three slots, quick (F5/F9)
  and auto.
- **The screens:**
  - **Title:** quick start, make a party, continue, load, quit.
  - **Making a party:** one to four heroes, each with a name, ancestry, calling, background, and point buy,
    4d6 or heroic attributes, with a preview of HP, AC, best skills and kit.
  - **The game** shows four hero cards in the party bar (portrait, bars, both hands with cooldowns, conditions),
    with overlays for the inventory (I), the Esc menu (save, load, help, quit), the character sheet (C) and help
    (F1). The game pauses under any overlay but help.
- **Starting kit** (`StartingKit`): each calling's weapons and armour, a background item, two torches and two
  bandages.

---

## 26. Implementation decisions (M4)
27. Implementation decisions (M5)
28. Implementation decisions (M6)

- **Perk trees for all 22 skills**: 145 perks, with 6–10 per tree and at least one that changes play in every tree.
  Effect kinds: damage, recovery, Mana cost, noise, sneak multiplier, block %, AC by armour, Stamina cost, crit
  range, Stamina %, carrying, regeneration, lock and trap bonuses, perception, fall damage, skill XP, potion
  strength, spell power, duration, the perfect-block window, saves against spells, smithing quality, hunger and
  identifying.
  - **The combat flags work now:**
    - bleeding crits (Blade, Two-handed);
    - Crushing Blows (blunt crits stagger), Skull Cracker (blunt power attacks may stun) and Shatter Armour;
    - Riposte and Executioner;
    - Whirl, Ground Slam and Cleave (power attacks that hit the creatures beside the party);
    - Sweep;
    - Long Shot, Piercing Shot, Pinning Shot and Bullseye;
    - Stunning Fist and Quivering Palm;
    - Disarming Bash, Deft Movement, Reflect Blows, Evasion and Avoid Death;
    - War Cry, Vault and Bookworm.
  - **Flags waiting for later systems:** spells (M5), traps (M6), crafting and survival (M8).
- **Character XP per rank is a third of the new rank, rounded up**, not the whole rank.
  - With the whole rank, level 8 arrived after about 35 rank-ups. That was too soon for any calling to grow more
    than one or two skills, and made the M4 target impossible.
  - Halving still left the third-best skill in the low 30s. A third gives level 8 after roughly 70–90 rank-ups,
    with a clear best skill and real breadth.
  - The level curve itself ((L + 3) × 25) is unchanged.
- **The M4 exit test, revised.** `ProgressionReport` trains each calling alone, with no Lone Delver bonus:
  - in an arena against creatures scaled to its level (`Bestiary.Scale`);
  - sneaking up on each one;
  - blocking 70% of tells and power-attacking a third of the time;
  - spending level-ups on HP (or Mana for casters) and perks in its best skills.

  At level 8 every calling's best skill is 45–59 and its third-best 35–46, after 400–950 fights. "Three skills over
  40" was reached by three callings and missed narrowly (38–39) by the rest, so the test asks for a specialist
  with breadth instead.
- **Use XP:** Athletics gives 2.5 per use (it was 0.5: power attacks hardly trained it), and Block gives
  1.5 + 0.15 × the damage blocked.
- **Unarmed** damage is 1d4 + floor(Unarmed / 20) × 1d4 (spec 9.3).
- **Sneaking** (X) is a party-wide state:
  - steps take 60% longer (not with Shadow Dancer);
  - the light is shaded to 75%;
  - creatures notice gradually instead of at once. Each tick, one that can see the party fills an awareness meter
    by (Perception + 3 if a torch is lit − the party's worst (Stealth / 5 + DEX), less 3 in heavy armour) / 10 a
    second, faster up close and three times as fast when adjacent. It notices when the meter fills; out of sight,
    the meter drains.
  - Every step sneaking past an unaware creature trains Stealth.
  - The meter is drawn as an eye over the creature.
- **Creatures give up.** An aware creature that hasn't seen the party for 15 s goes back to idling, and the fight
  only counts creatures that saw the party in the last 3 s. Before this, a creature watching through a gate the
  party had walked away from kept the fight (and so the lack of regeneration) going forever.
- **The character sheet (C) is the level-up screen:**
  - take the waiting HP, Stamina or Mana choice;
  - spend attribute points;
  - see all 22 skills with XP bars and item bonuses;
  - open any skill's perk tree, see each perk's ranks, requirement and text, and take it.

  The skills list marks every tree with a perk ready to buy.
- **Trainers** (`Character.Train`) work, but there is nobody to train with until the Waystones (M7). Skill books
  drop as loot.

---

## 27. Implementation decisions (M5)
28. Implementation decisions (M6)

- **48 spells** (`spells.json`, checked by the validator): eight in each school, plus cantrips (Minor Mending, Minor
  Ward, Mage Hand, Detect Traps, Unlock, Frost Touch, Guidance and Beacon; Spark and Light are also cantrips). Some
  names differ from section 10.2:
  - Spell Turning turns missiles back, and Bulwark gives +2 AC.
  - Bound Bow and Fireball are added; Anti-magic Field is left for the traps of M6.
- **A spell has a target and an effect:**
  - Targets: a bolt down the line ahead, the cell ahead, the eight cells around, a 3×3 blast two cells ahead,
    self, the hero who needs it most, or the party.
  - Effects: damage, heal, ward, cure, condition, buff, summon, hazard, revive or utility.
- **Casting:**
  - It takes the spell's cast time and then 1 s of recovery, and uses the caster's right hand.
  - The Mana is spent at the start. Costs fall 0.4% per point of the school's skill (−40% at 100), and the
    Mana perks lower them further.
  - Power is (1 + skill / 200), plus the power perks, and the casting modifier is added to damage and healing.
  - Bolts and spells aimed ahead make an attack roll against AC; blasts and the spells around the party allow a
    save, for half damage (DC 8 + casting modifier + skill / 10).
  - A blow on a caster calls for a CON save against 10 or half the damage, or the spell is lost. Shocked casters
    fizzle a quarter of the time.
- **Wards** soak damage before HP and don't stack. The warder earns Warding XP as theirs soaks blows.
- **Summons** are allied creatures (Familiar, Raised Skeleton, Flame Spirit, Horror):
  - they hunt the nearest foe, stay near the party when there's nothing to fight, and fade when their time runs
    out;
  - the party swaps places with an ally it walks into, and its blows and bolts pass allies by;
  - foes fight an ally that stands next to them;
  - Frenzy makes a creature an ally for a while.
- **Calm** makes a creature forget the party until hurt or until the spell ends; **Fear** makes it flee.
- **Hazards** (Cinder Wall, Acid Rain, Lingering Flame) burn creatures standing in them once a second. They don't
  hurt the party.
- **Light** (spec 10.3) has three levels, for the view and for being noticed:
  - Darkness: 0.42, or 0.6 with a dwarf's darkvision.
  - Torchlight: a torch burns 10 minutes. Torches light themselves when the last goes out, if anyone carries one.
  - The Light spell: brighter still, for 5 minutes.

  Creatures notice a lit party a cell farther away, and an unlit one a cell nearer.
- **Tomes** teach a spell if the school's rank is high enough. **Scrolls** cast once at skill 50, for no Mana.
  **Wands** hold 6–10 charges and cast at 40 + half the Enchanting skill.
  - These spell items come in the loot tables, with a spell suited to the depth.
  - Every calling starts with spells (fighters and barbarians with none, Emberkin with Spark).
- **Keys:** F1–F8 cast the selected hero's quick slots; spells learned fill the empty ones, and right-clicking in
  the spellbook moves them. To make room:
  - quick save and load moved to F10 and F11 (spec 17 said F5 and F9);
  - help moved to H;
  - the frame-rate display moved to F12.
- **The spellbook (B)** lists the selected hero's spells by school, with each one's cost, cast time, quick-slot key
  and text, and casts a spell on a click. Each hero's card shows a casting bar and any ward.

---

## 28. Implementation decisions (M6)

- **The generator** (`World/Generator*.cs`) makes a 32×32 level from the run seed, the depth and the Delve's density
  and trap options:
  - up to 3 vaults first, then 8–14 rooms;
  - joined by the cheapest paths (an A* search that reuses floor and never cuts through a vault) along a
    spanning tree, plus a fifth more links for loops, 15% of them two cells wide;
  - doors at about 30% of room mouths;
  - the start (the stairs up) in the first room, the stairs down in the farthest, and a landing cell for pits
    from above.

  Each attempt is checked and rerolled if it fails (up to 20 times, then a plain fallback). Generating takes 2–6
  ms. The same seed, depth and options always give the same level.
- **Locks.** 1–3 chokepoints on the way from the start to the stairs down become barriers:
  - a locked door with a key (which can also be picked, at DC 14 + depth);
  - a portcullis with a lever;
  - or a portcullis with a toggle plate.

  The key, lever or plate is placed in the part of the level reachable before that barrier, as far from it as
  possible.
- **Secrets:**
  - 2–4 secret doors lead to dead-end treasure rooms with rich chests. Each is a wall until found: by passive
    Perception within 2 cells (Keen Senses helps), or by Use on the wall (a Perception roll).
  - One illusory wall hides a niche with loot. It is drawn as wall, but the party walks through it.
- **The 20 traps** are pits (some spiked) and plates from M2, the illusory wall and the mimic, plus 16 kinds in
  `TrapKind`:
  - **Fire when stepped on:** dart walls and tripwires (darts, or an alarm bell), fire spouts, frost glyphs,
    lightning runes (only if someone wears heavy armour), gas vents (poison or sleep, lingering 10 s), collapsing
    ceilings (leaving rubble), rolling boulders, teleporters, spinners and alarm plates.
  - **On a cycle:** spike floors and swinging blades, visible.
  - **Other triggers:** the cursed altar, when its offering is taken; the flooding room, when the party enters,
    which seals the door until the drain lever is pulled; and anti-magic zones, where spells fail.
  - **Placement:** the generator places 2 + 0.8 × depth (scaled by the trap option, at most 14), with kinds
    unlocked by depth.
  - **The rule from spec 14.2** holds: more than two deadly traps on the main path each get a warning note in
    front.
  - **Spotting** uses passive Perception plus Trap Sense, within 2 cells, or Detect Traps.
  - **Disarming:** Use, facing the trap: d20 + DEX + Traps / 5 + perks against its DC. A miss by 5 or more sets it
    off, unless the hero has Saboteur.
  - **Creatures** know where their traps are: only the mindless step on them.
  - **Not done:** Rig (setting traps you've picked up) still waits.
- **Creatures and loot by depth.** Spawn tables by region: the Upper Hollow has vermin, the restless dead and
  goblin-kin; the Deep Works and the Drowned Choir have their own families, which arrive with M7.
  - Creatures are scaled to about 1.6 × depth, in groups for packs.
  - There are 2–3 chests per level (rich from depth 5), a dead delver or two with their pack, and flavour notes.
- **The 16 vaults** (`Content/Vaults/vaults.txt`) are text maps with directives (levers, plates, chests, monsters,
  traps, altars, secrets, illusory walls and notes), with connectors on their edges:
  - The Gatehouse, The Pillared Hall, Plate and Gate, The Pit Gallery, The Defiled Shrine, The Ossuary;
  - The Armoury, The Hidden Cache, The Hall of Mirrors, The Spike Run, The Blade Gauntlet, The Rune Circle;
  - The Long Slope, The Cistern Vault, The Shifting Rooms, The Dead Air.
- **The check:** working keys, levers and plates in order, and counting secret doors as found, everything must be
  reachable: the stairs, the landing, every creature, chest, floor item and teleporter destination. Chests, pits
  and altars count as blocked. The test suite checks the same thing over 1,000 seeds at each of depths 1, 5 and 9,
  with no fallbacks.
- **The Delve** (`Game/Delve.cs`) owns the engine and swaps levels under it:
  - the stairs down lead to the next level, arriving on its stairs up;
  - pits drop the party to the landing of the level below;
  - the stairs up lead back, arriving beside the stairs down above.

  A visited level keeps everything that happened in it. The deepest stairs end the Delve; the bosses take that over
  in M7.
- **Delve options (spec 4):** seed, depth, monster strength, density, traps, magic items, food, pause and Ironman.
  - The seed is any text (hashed), an 8-letter code (read back exactly; the alphabet leaves out I, O, 0 and 1), or
    blank for random.
  - Monster strength scales creatures' HP, damage and to-hit. On Cruel and Brutal the fallen roll a DC 10 death
    save every 3 s and die after three failures.
  - Pause can be turned off.
  - Ironman keeps one save, written at every change of level and on quitting, and deletes it when the party falls.

  The hall of fame and scores wait for M9.
- **Regions:** levels 1–4 are the Upper Hollow (grey stone), 5–8 the Deep Works (rust-brown) and 9–12 the Drowned
  Choir (damp green); longer Delves go round again. Each region has its own textures, and the view swaps them on
  arrival.
- **The view** draws spotted traps (a warning cross, spikes, glyphs, runes, teleporter sigils), rubble and stairs up
  on the floor, and shows altars, boulders, swinging blades and gas clouds as sprites.
- **Saves** keep every visited level's changes and rebuild each level from its source (`gen:seed:depth:options`):
  locked doors, traps, found secrets and rubble as well as the M3 set. Restoring a level clears the piles it was
  made with first, so items are not doubled.
- **The Delve report.** The bot now:
  - picks up keys;
  - steers round traps, pits it has seen and plates it has already pressed;
  - tries to disarm a trap it can't go round, three times, before walking through it;
  - keeps off the stairs up.

  The quick-start party gets through a 4-level Delve on 6 seeds of 8.

## 29. Implementation decisions (M7)

- **The bestiary.**
  - `creatures.json` holds 58 creatures in the ten families of 12.3, plus the wanderers (Mimic, Gelid Cube and
    Lost Delver) and the four summons.
  - Each creature has a tier, a behaviour profile and attacks with tells.
  - New behaviour blocks:
    - `summoner` (with `summons`): calls up its kind every 12 s, at most 3 about.
    - `splits`: splits once into two halves with half the HP.
    - `explodes`: bursts on death, hitting the party if it is next to it (CON save for half, and Poisoned).
    - `invisible`: unseen beyond 2 cells unless just hurt (Gloomstalker, Mirror Ooze).
    - `raises`: a Spore Shepherd turns a corpse within 5 cells into a Shambler every 15 s.
    - `boss`.
  - The validator checks that a summoner's creature exists.
- **Scaling.** `Bestiary.ScaleTo` also scales down: 10% of the hit dice a level, to 35% at least, and the bonuses fall
  the same way. Bosses, their summons and anything a summoner calls are fitted to the depth with it. A scaled
  creature's level is saved, and its definition is scaled again on load. (M6 lost this on load, a bug fixed here.)
- **Elites and champions** (12.2) appear only in Delve levels, so the Cellars stay as they were.
  - Each creature rolls once: 2% champion from level 3 down, else 8% elite, both times the difficulty's champion
    factor.
  - A test over 60 seeds holds the rates to about these figures.
  - Elite: +50% HP and +2 AC, gold-tinted.
  - Champion: an elite with 1–3 of the 16 affixes, +25% HP again, a generated name ("Grisk the Vampiric"), a violet
    tint and glow, and guaranteed rich loot.
  - What the affixes do:
    - Stoneskin: +3 AC and resists slashing and piercing.
    - Shielded: a ward of 20% HP, back every 10 s.
    - Warded: immune to one school, kept as `ward:<type>`.
    - Blinking: every 6 s, appears next to the party.
    - Mortar: every 7 s at 2–5 cells, an area lob (DEX save for half).
    - Burning Aura: 1d6 fire to the front row every 2 s.
    - Mirrored: two reflections at a quarter HP.
    - Splitting, and Summoner (its family's weakest).
    - Vampiric, Bloodthirsty, Frenzied, Venomous, Frostbound, Thunderous and Unyielding work as in M5.
  - The engine remembers every affix the run's champions showed (`ChampionAffixes`, saved).
- **Bosses** (12.5) guard the stairs down of levels 4, 8 and 12 (when the Delve is that deep).
  - Each stands in the stairs' room, two cells from the stairs, on open floor.
  - The stairs are sealed while it lives, and it wakes when the party comes within 5 cells in sight.
  - Its level is depth + 1–2, not the depth × 1.6 of the level's other creatures, so a party that has kept up can win.
  - A bar across the top of the view shows its name and HP.
  - The scripts:
    - **The Hollow King:** raises a Bone Sentry every 20 s, at most 2. At half HP he splits his crown into two
      Ossuary Knights, each with a fifth of his HP. *Not done:* the sarcophagi to smash.
    - **The Bellwright Engine:** takes no damage until the three pressure valves (levers on the arena walls) are all
      pulled. Its bell tolls every 10 s, with lightning to anyone within 2 cells (DEX save for half).
      *Not done:* the moving floor.
    - **Mother of Coils:** the water rises for 12 s (no damage gets through) and falls for 8 s. Each rise brings a
      Coil Guard, at most 3. She comes with two guards.
    - **The Hollow Choir:** five singers in the largest spare room of level 6, an optional fight. A fallen singer
      rises at half HP 10 s later while another still sings, with no second share of loot; kill all five within
      10 s. *Changed:* not a secret level, and the school-by-school silencing isn't done.
    - **The Unmaker,** in **the Unmade Heart**, the level after a 12-level Delve's last:
      - Phase 1 calls Unmade Hounds every 15 s.
      - At two thirds of its HP it takes up to three of the run's champion affixes (Vampiric, Shielded and Frenzied
        if there were none).
      - Below a third, the Knell moves the party to a random cell near it every 15 s.
      - Its death wins the Delve.

      *Not done:* unmaking walls.
- **Waystones** are a stone next to the start of the level after each boss, and of the Heart.
  - The stone blocks its cell; F on it opens the Waystone screen, which works only next to it and out of a fight.
  - **Merchant:** 10 items made from the level's source (the same every time), sold at twice their worth and bought
    at half.
  - **Trainer:** `Character.Train`, at rank² × 2 gold, 5 lessons a level.
  - **Shrine:**
    - Raises the dead for 100 gold a level, with full HP.
    - Tends the downed for free.
    - Lifts curses for 50 gold a level.
    - Resets perks at the M4 price.
  - **Stash:** shared by every Waystone, kept in the save.
  - **Rest:** a free full rest at the stone.
- **Art.** Every creature has its own code-drawn sprite in all views and frames, built from generic shapes (people
  with more weapons, beasts, blobs, floaters, bugs, fungi, machines, serpents, swarms and a mimic). A test checks that
  every creature in the content has a picture. Large creatures are drawn 18% bigger.
- **Not done** from 12.3's signatures: webs, corroding gear, mine rails and steam valves, lightning stunning
  constructs, elementals' hazards, swimming, aberrations' teleports, and the Unmade each using a champion affix.
- **The 4-level Delve report** now meets the Hollow King. The bot heals up before a boss. 4 of 8 seeds get
  through: 2 fall to the King or on the way, and 2 get stuck (one was stuck before M7 too). Bosses off, it was 5 of 8.
- **The 12-level report** (`A_full_twelve_level_delve`, 6 seeds): no run finishes; average depth 3.8, best 5,
  heroes at level 1. This bot rushes for the stairs and kills about 15 creatures a level, while M4's progression
  wants 50–100 fights a level. Ordinary creatures are level depth × 1.6 from M6, so the party falls ever further
  behind. Meeting the M7 target needs a balance pass. Options:
  - character XP from kills;
  - much faster early skill growth;
  - creatures scaled to the party rather than to depth;
  - or a bot that clears each level.

  That pass is left for after M8, so M8's food, rest and crafting can be weighed with it.

## 30. Implementation decisions (M8)

- **Content** (`alchemy.json`, `reagents.json`, `ingredients.json`, `recipes.json`; the validator checks them all).
  - **30 alchemy effects.**
    - 20 good: healing, Stamina, Mana, six cures, nine buffs, food and water.
    - 10 harmful: six blade coatings, two drains and two burns.
  - **40 reagents,** each hiding four effects. Every effect is in at least two of them, so all can be brewed.
  - **10 cooking ingredients.** Six are creature parts (Rat Haunch, Spider Leg, Beetle Meat, Warg Steak, Puffcap
    Flesh, Coil Fillet); four are finds (onion, flour, salt, tea).
  - **8 recipes.**
  - New item bases: reagents, ingredients, meals (`meal:<id>`), brews, Spirit Shards, Scrap Iron, a mortar and a
    campfire kit.
  - Loot tables now roll reagents, ingredients and tools.
  - New buffs, which work for spells too: `resistPoison`, `might` (+2 weapon damage), `quickness` (20% faster
    recovery), `nightEye` (light 0.85) and `fortitude` (advantage on CON saves).
- **Food and water** (13.6), when the Delve's option is on.
  - Food drains over 40 minutes and water over 33.
  - Below 25 a hero is hungry or thirsty: half regeneration and disadvantage on CON saves.
  - At 0, 1 HP every 10 s.
  - A Delve party sets out with 3 rations and 3 waterskins each, plus a campfire kit and a mortar.
  - The bot eats and drinks below 35.
- **Resting** (13.7, the R key) is instant.
  - It takes 1–8 hours, enough to heal the worst hurt.
  - Each hour costs 4 food and 4 water, and rolls 15% for a wandering monster, × the level's density and × the
    region (Works 1.2, Choir 1.4, Heart 2).
  - The chance is halved behind a closed door, and none in a cleared room shut by doors or at a Waystone.
  - An encounter wakes the party with the completed hours' share of healing, and the creature arrives 4–10 cells
    away, hunting.
  - A full rest is `Character.Rest`: pools, rest cures and once-per-rest traits come back.
  - No rest with enemies within 8 cells.
- **Alchemy** (anywhere; needs a mortar to brew).
  - Tasting uses the reagent up and reveals its next 1 + Alchemy/34 effects. The party's knowledge is saved.
  - Brewing takes 2–3 reagents. The effects shared by at least two make the potion, each at its base × (1 +
    Alchemy/50); with nothing shared you get sludge.
  - A brew whose effects are all harmful coats the weapon instead of being drunk. For a minute each hit passes the
    condition on, against a CON save of DC 10 + Alchemy/8.
  - Brews carry their effects on the item and never stack.
- **Cooking** needs a campfire kit and no enemies near. A meal gives food, water and its buff for its seconds × (1 +
  cook's Survival/100, in tenths).
- **Stations.** Smithing needs an anvil: a forge in half the Deep Works' levels, or any Waystone. Enchanting needs an
  altar: one in a quarter of levels from 3, or any Waystone. Stations block their cell; F opens the craft screen on
  that craft.
- **Smithing.**
  - **Improve:** quality +1, at (quality + 1) × 2 scrap. The cap is Smithing/15, at most +5. Quality already adds
    damage and AC (M3).
  - **Melt:** about 1 scrap per kilogram; cursed items won't melt.
  - **Forge:** any weapon, shield or armour worth up to 20 + Smithing × 4, for scrap of twice its weight, at quality
    Smithing/30.
  - *Not done:* durability, so repairs, whetstones and patch kits; ore and bars (scrap stands in for both).
- **Enchanting.**
  - **Unravel:** destroys a Fine, Enchanted or Masterwork item that has been identified and learns its first
    non-curse affix the party doesn't know yet.
  - **Bind:** puts a learned affix on a weapon, shield, armour or gear it fits, for 1 + its affix count in shards.
    The value is rolled at item level 1 + Enchanting/8. An enchanter can bind 1 affix into one item, 2 from
    Enchanting 40 and 3 from 75. Uniques can't be changed.
  - **Recharge:** a wand to 6 + Enchanting/10 charges for one shard.
- **Spirit shards** drop from 3% of lesser creatures, 10% of common, 30% of greater and 60% of dread, +20% for an
  elite. Champions and bosses leave 1–3. Creature parts drop at each ingredient's chance (Warg Steak 70%).
- **The screens.** K opens crafting (Alchemy, Cooking, Smithing, Enchanting). The Waystone screen has a button for
  its anvil and altar. The party cards show food and water under the bars when they count.
- **Reports after M8:**
  - The lone hero clears the Cellars on 14 of 40 seeds, and the party on 40 of 40, the same as at M7.
  - The 4-level Delve: 6 of 8.
  - The 12-level Delve: average depth 4.8, best 6, heroes still level 1. The M7 balance problem stands.

## 31. Implementation decisions (the balance pass)

The pass that M7's exit test called for, done after M8 so food, rest and crafting were part of it. All the numbers
below come from the bot reports on 16 seeds.

- **What was wrong.**
  - **The bot speedran.** It headed for the stairs and killed about 5 creatures a level, so no progression could
    keep up with creatures at depth × 1.6.
  - **Skill use alone was far too slow for a Delve.** With M4's curve, 30 fights moved a fighter's Blade from 20
    to 21. A Delve holds about 200 creatures, shared by four heroes.
- **The bot now plays a level as a person would** (`Bot.Clears`, used by the 12-level report). It:
  - hunts what lives on the level, sticking with one creature until it falls;
  - opens chests and picks things up, dropping its cheapest item when the packs are full and a key needs room;
  - spends its level-ups (`ProgressionReport.Spend`);
  - at a Waystone, raises the fallen, rests, and buys lessons in each hero's three best skills with up to half the
    purse;
  - pulls levers and steps on toggle plates only while a door they work is shut, and walks round a plate whose
    doors are already open;
  - waits for cyclic spikes and blades to pass, and never rests on them;
  - shuts the Bellwright's valves before fighting it, and goes for the boss last;
  - picks locks, searches walls when the stairs can't be reached, and walks round creatures in the way where it
    can;
  - gives up on a target after two minutes without progress (a kill, a pickup, a lever, a search).

  The four-level report keeps the old stair-rushing bot, as a check on what a hasty party can do.
- **Kills give character XP** (section 7.3): 3 × the creature's level for each standing hero, more for elites,
  champions and bosses, none for what a creature called up. The brief asked for D&D-style as well as Skyrim-style
  progression, and this is the D&D half. Without it no rate of skill growth both kept the heroes level with the
  depth and left skills meaning anything.
- **Skills rise three times faster:** `xpToNext` is divided by 3 (`Progression.SkillPace`). At 2.5 the bot won 10
  of 16 Delves, at 3 it won 11, and at 4 it won 14 (too easy for Standard). At 3 a fighter ends with Blade 50–80.
- **Destruction and Restoration give more use XP**: 2 + 0.15 × damage, and 1 + healed / 4. Casters cast far
  less often than fighters swing, so their schools lagged (Destruction 20 → 30 over a whole Delve). Now a wizard
  ends with Destruction 45–68.
- **Two generator fixes**, found when the bot got stuck. `Check` now also requires:
  - **The stairs from the landing.** A party that fell through a pit could land beyond a gate whose lever was on
    the start's side.
  - **No way on over the stairs up.** Below level 1, the start (the stairs up) could sit in the only corridor
    onward, and stepping back on it climbs a level.

  The 1,000-seed suite checks both, with no fallbacks.
- **Small fixes:** a creature whose name starts with "The" (the Unmaker) is no longer "The The Unmaker"
  (`Monster.TheName`). A kill that levels a hero says so in the log.
- **Results:**
  - **12-level Delve** (clearing bot): 11 of 16 won, average depth 12.2 of 13. Heroes end at level 22–26, and the
    deepest creatures are level 19–21. Losses come at the Bellwright, the Mother of Coils, the Unmaker and deep
    packs. The report asserts at least a quarter won, average depth 9 or more, nobody stuck, and winners at level
    18–32.
  - **4-level Delve** (rushing bot): 7 of 8.
  - **The Cellars:**
    - the party wins 40 of 40;
    - a lone hero wins 26 of 40 (14 before), since its kills now level it during the run.
  - **The arena** (`ProgressionReport`): every calling reaches level 8 in 40–70 fights, with its best skill at
    35–43.
- **Not changed:** creature levels (depth × 1.6 + 0–2), trap damage and the level curve.
- **Time.** The spec's 8–12 hours can't be checked by a bot that moves without pause. The bot takes 45–85 minutes
  of game time for a full Delve, about 5 minutes a level. A person exploring, reading and managing their packs takes
  several times that. Whether it comes to 8 hours is for the user's play to say.

## 32. Implementation decisions (M9: textures from an art pack)

The first piece of M9's "art from the manifest": real textures for walls, floors and ceilings.

- **The pack.** Screaming Brain Studios' *Old School Dungeon Crawler Pack* and *Cave Walls Addon*, from itch.io
  (CC0, retrieved 2026-11-07). Each set has a wall seen face on (Center), side walls, and a floor and ceiling seen
  in perspective, at two distances.
- **Made for compositing, not ray casting.** The pack is drawn for crawlers that paste pre-drawn pieces together.
  `tools/import-sbs-textures.sh` (ImageMagick) turns it into textures:
  - **Walls:** the near face-on wall, as drawn.
  - **Floors and ceilings:** the near trapezoid (256×64, half a cell deep) un-warped by a perspective distort into
    a flat half cell, then stacked twice to make a whole cell. The side walls and the far pieces aren't needed.
  - **The brick sets** lay 16 courses to a cell, which turned to mush at the view's 640×400. The script keeps a
    64 px quarter (the pattern repeats every 16 px, so it still tiles) and enlarges it to 128 px.
  - **Brightness.** The sets ran from 0.18 to 0.81 mean grey, and the view lights them all alike. Each texture is
    scaled to the code-drawn set's means: walls 0.43, floors 0.34, ceilings 0.25.
- **Regions:**
  - cellar: Brick 3 (dark brick, lightened);
  - works: Brick 1 (warm brick);
  - choir: Brick 2 (mossy brick);
  - heart: Shale Cave (bare violet stone).

  The Cellars handmade level uses the cellar set.
- **Textures carry their own size.** `Texture` was a fixed 64×64. It now has a power-of-two side: the pack's textures
  are 128 and the code-drawn ones stay 64, and the renderer samples each by its own size.
- **What stays in code.**
  - Doors, the portcullis, pits, plates and stairs.
  - Levers, trap marks, spikes and sigils, which are drawn over the pack's wall or floor at its size. A 128 texture
    gets 2×2 blocks, so they sit where they did.
  - Door frames: the region's wall itself for the pack sets.
  - `TextureFactory.Drawn` keeps the old all-code set.
- **No image library in Render.** `PngReader` decodes the 8-bit RGB and RGBA PNGs the script writes, using .NET's
  `ZLibStream`. The textures are embedded resources, so the single-file build carries them.
- **The record.**
  - `assets.manifest.json` lists the source, licence, author, changes and files.
  - `tools/build_credits.py` writes `CREDITS.md` from it.
  - The pack's licence ships beside the textures.
  - `AssetTests` fails if a shipped texture is missing from the manifest, a listed file is missing, or the credits
    are out of date.
- **Goldens rewritten.** Every view changed, and three new goldens show the works, choir and heart regions.
- **Next:** creatures, items and icons. No free pack covers 58 creatures in one style, so those need their own
  choice (spec 15.3's gaps rule).

## 33. Implementation decisions (M9: the creatures redrawn)

- **Why in code.** The user chose this. No CC0 or CC-BY pack covers 58 creatures and 4 summons in one style. The
  best, 0x72's *DungeonTileset II* (CC0), has about 22 tiny top-down monsters and no vermin, constructs, fungi,
  serpent-folk, aberrations or Unmade. Mixing packs would clash. So every creature stays original and in code, in
  one style.
- **Twice the resolution.** Drawers still work on the 64-unit grid with the feet on row 63. `Paint.Scale` maps it
  to 128 px: creatures, props and chests are 128, item icons 64, bolts 48.
- **New shapes in `Paint`:**
  - `Taper`: a round-ended segment that narrows, for limbs, tails, horns and blades;
  - `Limb`: two tapers bent at a joint, shaded as one;
  - `Poly`: filled polygons for torsos, robes, wings, plates and flames;
  - lines now have round ends.
- **The finishing pass** (`Paint.Finish`, replacing the black outline):
  - **Parts.** A patch of one colour is a part, and its own rounded surface: the distance to its edge (a chamfer
    transform) gives a circular height profile, and the normal comes from that. Flat marks painted on a part
    (spots, eyes, bubbles) don't break its surface.
  - **Light.** From the upper left and a little in front. The tone is quantised to six steps of a ramp whose dark
    end leans cool violet and light end warm, with a 4×4 Bayer dither between steps.
  - **Depth.** A part with a later shape over it takes a contact shadow within 1.5 units; the front part gets a
    darker inner line where it overlaps.
  - **Materials** (`Paint.Mat`):
    - fur: vertical strands;
    - stone: speckle;
    - cloth: diagonal folds;
    - bone: low contrast;
    - metal: hard contrast and a sharp highlight;
    - gloss, for slime and shells: a sharp highlight.
  - **Outlines.** A deep, cool shade of the colour they border.
  - **Flat shapes** (eyes, mouths, glows, staff orbs) keep their colour, as does baked art (a swarm's members are
    finished pictures copied in).
- **The drawers, rebuilt.**
  - **People** (25 creatures):
    - jointed arms and legs, sleeves, boots, a tapered tunic or floor-length robe, a belt and buckle;
    - faces with sockets, brows, nose and mouth;
    - cowls with shadowed faces for the hooded, goblin ears and fangs;
    - skeletons with skull, jaw, ribs, spine and pelvis;
    - a king's mantle, a crown, a knight's helm with visor and plume, a chieftain's band and feather;
    - weapons in metal, held at an angle that follows the tell and the blow.
  - **Vermin:** rats with ears, muzzle, whiskers, paws and tail; jointed-leg spiders with an abdomen, eye cluster
    and fangs; beetles with split wing cases.
  - **Beasts:** four jointed legs with paws and claws, chest ruff, muzzle, ears and tail; bony ribs on the grave
    hound, and a spear-carrying goblin on the warg.
  - **Oozes:** glossy domes with a foot, sheen, suspended specks and drips; the cube is drawn in three faces with
    what it has swallowed.
  - **Floaters:**
    - layered flames with a white-hot core;
    - an imp with membrane wings, horns and tail;
    - tattered ghosts with reaching arms;
    - a bat with finger-boned wings;
    - eyed moths;
    - a brain trailing nerves;
    - the Unmaker, a ring of eyes round a void, with orbiting shards.
  - **Fungi:** gilled, spotted caps on fibrous stalks with root-legs and arms; the colossus, a heap sprouting
    caps.
  - **Machines:** riveted brass and iron with piston limbs, a gauge or furnace heart and a visor slit; magma
    golems in cracked stone; the rail juggernaut with headlamps, grille, wheels and ram; the Bellwright Engine as
    a bell over a boiler with hammer arms.
  - **Serpents and others:** naga with coils, belly scales, arms, hood markings and a forked tongue; the queen's
    crown; tentacles with suckers round a toothed maw; the mimic with planks, iron bands, teeth, tongue and eyes.
  - Flying things now cast a shadow on the floor instead of trailing a wisp.
- **Tests and art.** The sprite-sheet test still checks every creature, view and frame. The two creature goldens
  were rewritten after looking at them. `Images/AVAHack-bestiary.png` shows every creature from the front. A
  frame still takes 1–3 ms: sprites are drawn once and cached.

## 34. Implementation decisions (M9: item icons)

- **One icon per item base.** Before this, seven generic icons stood for everything (weapon, shield, armour,
  potion, book, gold, other). `SpriteFactory.Items` now draws:
  - every weapon (18), armour (8), shield (3) and piece of gear (12);
  - every consumable (12);
  - books, coloured and marked by their skill (combat red, each magic school its own colour, crafts green);
  - tomes, scrolls and wands;
  - gold and keys;
  - all 40 reagents and the 10 ingredients;
  - the 8 meals;
  - spirit shards, scrap iron, the mortar and the campfire kit;
  - a bare fist and a two-handed cross.
- **Drawing.** Icons are drawn on a 32-unit grid at 64 px, with the same `Paint` finish as the creatures.
  Weapons lie on the diagonal, hilt at the lower left; everything else stands upright. `Paint.Clear` cuts holes
  (a ring, a key's bow).
- **Keys.** `SpriteFactory.IconFor(db, item)` gives the key: the base id, `book:<skill>`, or `brew:<effect kind>`,
  so a home-made brew is red for healing, blue for mana, dark green for a blade coating and so on. Unknown keys
  get a plain sack.
- **Where they show.**
  - the inventory and paper doll;
  - items on the floor in the view;
  - the party cards' hand slots, which now show the held item (a bare fist when empty, a cross when a two-handed
    weapon fills the other hand), replacing the App's vector shapes;
  - buttons that stand for an item: the merchant's goods, the pack to sell, the stash, reagents, the meals,
    Smithing's improve and forge lists, and Enchanting's items. `Button` takes an optional item for this.
- **Tests.** `Every_item_has_its_own_icon` renders every key the game can ask for, fails if one is nearly empty or
  falls back to the sack, checks every item base maps to one, and writes `icons.png` to `AVAHACK_SHOTS`.
  `Images/AVAHack-icons.png` is that sheet.
- **Portraits** followed in section 35.

## 35. Implementation decisions (M9: hero portraits)

- **In the sprite factory.** `SpriteFactory.Portrait(ancestry, calling, name, down)` draws head and shoulders on the
  64-unit grid at 128 px with the same `Paint` finish. The App's vector-shape portraits (`Art.cs`) are gone; the
  party cards draw the bitmap, cached per look.
- **What sets what.** Heroes have no appearance settings, so:
  - **Ancestry** sets the face:
    - skin tones;
    - head shape (a square jaw for dwarves and orcs, a fine one for elves);
    - ears: long for elves, big and round for gnomes, pointed for orcs and halflings;
    - gnomes' noses;
    - a dwarf's beard with braid rings, and orcs' tusks;
    - the saurian's scaled head, snout, slit pupils and crest;
    - the emberkin's red skin, horns and ember eyes.
  - **Calling** sets the clothes and headgear:
    - fighter: mail and leather pauldrons;
    - barbarian: a fur mantle, a baldric and war paint;
    - ranger and rogue: hoods (the rogue's with a scarf over the mouth);
    - cleric: a coif, a stole and a holy symbol;
    - wizard: a starred robe and a pointed hat;
    - paladin: plate, a tabard and a circlet;
    - druid: leaves and antlers;
    - monk: a crossed robe, a shaved head and a brow mark;
    - spellblade: a shoulder rune, a hilt over the shoulder and a gem circlet.

    The background is lit in the calling's colour.
  - **The name**, through an FNV-1a hash, picks the hair style, hair colour, eye colour and, for some humans,
    orcs and gnomes, a beard. Monks are always shaved.
- **Downed** heroes are drawn grey with their eyes closed.
- **Tests.** `Every_ancestry_and_calling_has_a_portrait…` draws all 80 combinations and the quick-start party
  standing and downed. It checks that each portrait fills its frame, that the same hero always draws the same, and
  that different names differ. It writes `portraits.png` to `AVAHACK_SHOTS`; `Images/AVAHack-portraits.png` is
  that sheet at half size.

## 36. Implementation decisions (M9: audio)

- **The engine raises cues; the App plays them.**
  - `GameEngine.Sound` raises a `SoundCue` (an id, a world position, a gain, or "at the party") at each event:
    - steps (in water too), bumps, swings and power attacks, shots;
    - hits by what was struck (flesh, bone, metal or slime), critical hits, misses and blocks;
    - heroes hurt and downed, and the party's death;
    - a creature's cry when it notices the party, and a softer one with each tell;
    - its footsteps (heavy for large creatures and constructs);
    - its death by what it is made of;
    - doors, portcullises, levers, plates, locks, secrets, chests, pits and stairs;
    - every trap, including spikes and blades heard cycling;
    - pickups and gold, drinking, eating, reading and torches;
    - spells by school, and where a bolt lands by its element;
    - level-ups, the Waystone and each craft;
    - a sting when a boss wakes and a chord when it falls;
    - the Bellwright's bell and the Knell.
  - Cues never touch the game's state or dice. A test plays the same presses with and without a listener and gets
    the same game.
- **Hearing** (`GameEngine.Hearing`), deterministic and tested:
  - **Distance.** Sound travels along the passages, looking for the way with the fewest shut doors first, then the
    fewest steps. Its gain is 1 / (1 + 0.22 × steps), so 5 steps away is about half.
  - **Doors and rock.** Each shut door on the way lets 35% through. Rock stops it, and nothing carries past 16
    steps.
  - **Direction.** The pan comes from the source's direction against the party's facing (±0.8 at most), and a
    sound behind is 15% quieter.
- **The sounds** are all made by `tools/make_sounds.py`:
  - **Synthesis.** Standard-library Python: oscillators, white and brown noise, one-pole and biquad filters,
    envelopes, inharmonic partials for metal and bells, formant-filtered sawtooth "voices" for grunts and cries,
    and a small Schroeder reverb.
  - **Reproducible.** Fixed seeds: running it twice gives identical files.
  - **Contents.** 86 effects (96 takes: 2–3 of the common ones, such as steps, swings, hits and hurts), plus four seamless
    20-second ambience loops (cellar, works, choir, heart). They are 22,050 Hz mono 16-bit WAVs, 7.4 MB in all,
    embedded in the App.
  - **Levels.** Effects are peak-normalised; the loops are brought to −24 dB RMS so the regions match.
- **Playback** (`OpenAlAudioEngine`), adapted from AVARadiance's:
  - **Voices.** Effects play on a pool of 24 voices placed left or right of the listener for the pan, with
    distance rolloff off (the engine already worked it out). Each play gets ±4% pitch so repeats don't sound alike.
  - **Ambience.** A looping buffer on one of two sources, crossfaded over about a second when the region changes.
    It plays under the title screen too.
  - **Clicks.** Buttons click, and opening a screen gives a soft cue.
  - **Settings.** The Menu sets the volume, the ambience and the buffer. They are kept in `sound.settings` in the
    saves folder (the saves list reads only `*.json`).
  - **Buffer.** Automatic means 4096 frames in a virtual machine (the AVAWizardry finding), and OpenAL's default
    otherwise. A change takes effect at the next start.
  - **Windows.** x64 and x86 get the official MinGW OpenAL Soft, swapped in by `Directory.Build.targets`.
  - **No sound.** If OpenAL can't start, the game is silent and the Menu says why.
- **Tests.**
  - **Core:** hearing fades with steps, shut doors muffle, sound goes round by the passages and not through rock,
    the pan follows the facing, the main events raise their cues, and cues don't change the game.
  - **App:**
    - every cue the engine or App can raise has a sound, and every sound is used;
    - each take reads as mono 22,050 Hz;
    - the view plays cues and the region's ambience, a creature to the right is heard on the right, and the
      settings round-trip;
    - an opt-in smoke test (`AVAHACK_AUDIO_SMOKE=1`) opens the real device and plays every take at zero volume.
      It passed on this machine.
- **Not done.** No music beyond the stings, as the spec asks. Nobody has listened to the sounds yet: they were
  designed by ear-less numbers (lengths, levels, onsets), so a listening pass by the user is the real test.

## 37. Implementation decisions (a deeper Delve: six regions, and the Ossuary Stacks)

The user asked for a deeper dungeon with more kinds of creature: the structure first, then the first new region.

### The structure

- **Regions are data.** `regions.json` lists them top to bottom. Each has:
  - its id and name;
  - its creature families;
  - its boss;
  - `wanderers`, which scales wandering monsters during a rest;
  - `forgeOneIn`;
  - the Stacks' switches: `hush`, `candles` and `shelves`.

  One region is `final`: the Unmade Heart. `ContentDb.Validate` checks that ids are unique, that exactly one region
  is final, and that every family and boss exists. The generator's hard-coded region switch, spawn table and boss
  list are gone.
- **Six regions of four levels:** the Upper Hollow, the Deep Works, the Drowned Choir, the Ossuary Stacks, the
  Rootdark and the Glass Roads. Then the Unmade Heart, below the last level of any Delve of 12 or more (25 for the
  24-level Delve). A Delve past them all stays in the deepest. The Delve options already offered 6, 12, 18 and
  24 levels; past 12 they now mean something.
- **Bosses and Waystones.**
  - A boss stands on each region's last level, if the region has one.
  - A Waystone waits at the start of every region below the first, boss or no boss. Without that, a 24-level party
    had no rest, shrine or trainer after level 12.
- **Textures and ambience.** The Stacks, the Rootdark and the Glass Roads use the art pack's unused Sandstone,
  Copper and Marble cave sets. Each has its own synthesised ambience loop:
  - the Stacks: rustling pages, guttering candles and a far chime;
  - the Rootdark: creaking roots, seeping water and chittering;
  - the Glass Roads: whistling wind and settling glass.
- **Progression.** Every second level past 20 gives attribute points, as well as every fourth. This keeps a long
  Delve's levels worth having once the skills near their cap.
- **The Rootdark and the Glass Roads** were placeholders, made of existing families with no boss yet. The Rootdark
  now has its own (section 38), and so do the Glass Roads (section 39).

### The Ossuary Stacks (levels 13–16)

- **Two new families**, six creatures each:
  - **The Inkbound**, what the archive's writing became:
    - the Page Swarm: razor-edged pages that make you bleed, and burn in a breath;
    - the Inkling: spilled ink that blinds, and splits when cut;
    - the Quill Sentinel: a clockwork scribe with a quill spear and ink darts;
    - the Wax Warden: wax over iron with a burning wick, weak to fire;
    - the Lettered Scholar: a reciting caster that calls up page swarms;
    - the Errata: struck-out writing given shape, unseen until close, and it curses.
  - **The Mourners**, keepers of the archive's dead:
    - the Candlewight: it carries a candle, and is a herald;
    - the Weeping Bell: its toll is an area blow that stuns, and it is a herald;
    - the Veiled Mourner: a frightening phaser;
    - the Keener: an area keen that frightens;
    - the Shroud Bearer: its wrap paralyses;
    - the Hush: an unseen stalker that weakens.

  Their art goes through the generic drawers:
  - people for the scholar, candlewight and shroud bearer, with a held tome, a candle and a hook;
  - floaters for the pages, bell, Errata, veil, keener and Hush;
  - a blob for the inkling;
  - machines for the sentinel and the wax warden (drips and a lit wick).

  Each family has its own cry, and paper has its own death.
- **The archive loot table**: books, scrolls, tomes and reagents.
- **The hush** (`hush`):
  - **Noise.** A loud sound the party makes wakes unaware creatures within 8 steps along the passages, with a shut
    door counting as three. Loud sounds are swings, blows, misses, blocks, doors, levers, chests, falls, traps and
    Destruction spells.
  - **Range.** That reaches further than a blow's ordinary noise (4 cells), and round corners, through the hearing
    model of section 36.
- **Candles** (`candles`, six a level):
  - **Placing.** They stand as stations, blocking their cell.
  - **Light.** Within 2 cells of a lit one, creatures notice the party from 3 cells further, and sneaking stops
    working.
  - **Snuffing.** Use pinches one out (and lights it again). It's kept in the level's flags, so saves keep it.
- **Shelves** (`shelves`, 35% of room walls):
  - **Look.** Drawn as bookcases of coloured spines over the region's stone, never on a door, a secret or the
    edge.
  - **Searching.** Use searches one once. The party's best reader makes a Lore check (d20 + Lore / 5 + INT
    against 14); success turns up something from the archive table, and either way it trains Lore.
- **Heralds** (a new `herald` behaviour): when one notices the party, everything within 8 cells wakes.
- **The Archivist** (level 16):
  - **His candles.** He reads by four candles round him: each one still lit turns aside a fifth of every blow.
  - **His script.**
    - every 16 seconds he files a hero away (a WIS save, or paralysed for 5 seconds);
    - he calls up page swarms (three at most);
    - at half health he reads aloud from the ledger of the dead (frightened) if any candle still burns.

  Snuffing the candles is the fight. The bot learned it, as it learned the Bellwright's valves.
- **Level files can name their region**: a `theme:` directive in the text level format.
- **The bot**: it snuffs the Archivist's candles before fighting him. It crosses a toggle plate when that's the only
  way on, which fixed a run stuck on level 17.
- **Results** (6 seeds, 24-level Delves, clearing bot):
  - average depth 16.8, best 21, heroes at level 23–38;
  - of the parties that reach the Archivist, about half beat him;
  - the placeholder Rootdark is where the rest fall.

  The 12-level Delve is unaffected: 6 of 8 seeds won, heroes at level 22. So are the 4-level Delve (7 of 8) and
  the Cellars. Nothing gets stuck.
- **Tests.**
  - The six-region structure, the Heart and the Waystones.
  - Region validation and the late attribute levels.
  - Stacks levels with their families, candles, shelves and the Archivist.
  - The hush against an ordinary region, candles and snuffing, a shelf searched once, a herald waking the hall,
    and the Archivist's candle shield and summons.
  - A golden of a Stacks hall, and `Images/AVAHack-stacks.png`.
  - The deep report runs with `AVAHACK_BALANCE_DEEP=1`.

## 38. Implementation decisions (the Rootdark)

The second new region, levels 17–20, after the Stacks. Where deep runs used to die, it now has rules of its own.

- **Two new families**, six creatures each:
  - **The Burrowers**, things that live in the earth between the roots:
    - the Tunnel Grub: an ambusher, and it burrows;
    - the Rootback Beetle: a tough, unyielding guard;
    - the Sapsucker: a flying pack hunter;
    - the Root Wyrm: an unyielding ambusher that burrows;
    - the Burrow Matron: a summoner that calls up tunnel grubs;
    - the Gnawing: a great worm that splits when cut.
  - **The spore-thralls**, delvers and beasts the fungus took:
    - the Thralled Delver: mindless, and it bursts when it dies;
    - the Capped Goblin: a pack archer;
    - the Mossback Warg: a pack hunter;
    - the Spore Carrier: a walking pod that bursts;
    - the Mycel Knight: unyielding armour that opens doors;
    - the Bloomheart: a ranged caster that raises the fallen.

  The spore-kin of the Drowned Choir live here too. A new drawer makes the worms: a ringed body reared out of a
  heap of earth, with a round maw of teeth. The thralls are people with caps grown through their skulls. The rest
  use the bug, beast, floater and fungus drawers. Each family has its own cry.
- **The Rootdark loot table**: reagents, ingredients, gear and potions.
- **The region's switches** in `regions.json`: `pods` (6 a level), `roots` (4 tangles) and `rootWalls` (30% of
  room walls).
- **Spore pods.**
  - **Bursting.** A pod is a station. It bursts when the party, or any creature but a thrall or spore-kin, comes
    within a cell. The cloud is 3×3 of choking spores for 12 seconds. Like a gas trap's cloud, it poisons
    whoever fails a CON save (against 12 + depth / 3) every 2 seconds.
  - **Fire.** Fire reaching a cloud sets it off: a bolt that lands in it, a burning blow, a fire trap or a burning
    hero. The cloud goes up and is gone. Everything in it takes 4d6 + depth fire, and heroes get a DEX save for
    half. So a spore cloud is a weapon, but only from outside it.
  - **Saves.** A burst pod is kept in the level's flags.
- **Root tangles.**
  - **Placing.** Tangles are stations in corridor cells (two open neighbours, not in a room), at least 4 cells
    apart and away from the start and the stairs. They block the way.
  - **Cutting.** Use hacks at one: d20 + Survival / 5 + STR against 15, and the third try always gets through.
    Either way it trains Survival.
  - **Fire.** Fire burns one away at once, and so does a cloud going up over it.
  - **The check.** `Check` counts tangles and pods as open, since both can always be got past, so every level stays
    finishable.
- **Root walls.** Some room walls are grown over with roots, for the look only: a texture drawn over the region's
  stone.
- **Burrowers** (a new `burrower` behaviour). An aware burrower more than a cell away, and no more than 14 steps
  along the passages, waits 10 seconds. Then it comes up out of the earth behind the party, or beside it if that
  cell is taken. It can do this mid-step. Only one comes up in any 8 seconds on a level.
- **Rooted** (a new behaviour) creatures never move. They turn to face the party, and the spawn tables never
  place them.
- **The Mycel Crown** (level 20):
  - **Rooted.** It never moves.
  - **Its nodes.** Three Nodes of the Crown, rooted too, stand in other rooms of its level. While any one lives,
    nothing hurts the Crown.
  - **Regrowth.** A node cut down grows back once, in 90 seconds, while the Crown lives, without dropping loot
    again. A node killed by fire, or cut down a second time, stays dead.
  - **Its script.** While the party is within 6 cells, every 20 seconds it calls up two thralled delvers and
    breathes a spore cloud round itself (CON against 13 + depth / 3). A careless fire bolt sets the cloud off in
    the party's face. Out of its hall, it is quiet.

  The fight is to leave it, burn or cut down its nodes, and come back.
- **The bot**:
  - it hacks through a tangle in its way;
  - it waits for a pod ahead to burst;
  - it treats tangles and pods as open when finding its way;
  - it hunts the Crown's nodes, never strikes the Crown while one lives, and searches the walls when it can't
    reach one. Before, it never hunted the nodes, because they carry a boss tag. That was the main reason
    parties died at the Crown.
- **Teleporters** (all regions). A teleporter must not drop the party in a pocket it can leave only through a hidden
  door. From where it lands, the party must find its way back to the start without finding a secret; the level
  check fails otherwise. One seed put the party in the Crown's hall that way, with every node outside and a
  hidden door (DC 22–25 at that depth) the only way out.
- **Tuning**, from the deep report's logs. The first Rootdark killed every party that reached it:
  - the bot fighting a Crown it couldn't hurt;
  - the Gnawing burrowing up behind the party and splitting;
  - the Burrow Matron's endless grubs.

  The changes:
  - the Crown is rooted, quiet out of its hall, and calls every 20 seconds instead of 15;
  - its nodes grow back once, in 90 seconds instead of 40;
  - the spore DCs are 2 lower;
  - the Gnawing no longer burrows, and hits for 3d10 + 6 instead of 4d10 + 6;
  - burrowers wait 10 seconds instead of 7, one at a time;
  - the Matron has 15d10 + 45 hit points instead of 18d10 + 54.
- **Results** (6 seeds, 24-level Delves, clearing bot):
  - before the tuning, every party that reached the Rootdark died there (4 of 6 runs; one at the Crown);
  - after it, three parties reach the Rootdark and two get through. They fall on levels 21 and 22 in the Glass
    Roads, where the Refractor and the rifts' Unmade now do the killing;
  - average depth 17.0, best 22, heroes at level 20–40, none won. The teleporter rule changes some levels, so
    other seeds now fall earlier (levels 12, 13 and 16). Six seeds is a small sample.

  The 12-level Delve still passes its gate, at 4 of 8 won (6 of 8 before; the teleporter rule changed some of its
  levels too). The 4-level Delve and the Cellars pass.
- **Tests.**
  - Rootdark levels with their families, pods, tangles, root walls, the Crown and its three nodes.
  - A pod bursting for the party, and for a creature but not a thrall. Fire setting a cloud off, burning a creature
    and the party.
  - Tangles blocking, hacked through by the third try, and burned.
  - A burrower coming up beside the party, and only one coming up at a time.
  - The Crown's shield, nodes growing back once unless burned, its summons, and its quiet out of reach.
  - A golden of a Rootdark cavern, and `Images/AVAHack-rootdark.png`.

## 39. Implementation decisions (the Glass Roads)

The last new region, levels 21–24, between the Rootdark and the Unmade Heart. All six regions now have their own
families, rules and boss.

- **Two new families**, six creatures each:
  - **The Shardborn**, glass the Unmade's breaking-through left alive. Glass turns arrows and spears (it resists
    piercing), takes double from bludgeoning, and is immune to poison and bleeding: a hammer is the weapon here.
    - the Shardling: a scuttling pack of splinters that makes you bleed;
    - the Prism Eye: a floating prism whose beam blinds;
    - the Mirrorling: an unseen phaser that frightens;
    - the Glass Strider: four legs of drawn glass, unyielding;
    - the Obsidian Hulk: an area smash that stuns, and it opens doors;
    - the Refractor: a flying caster that blinds an area and calls up Glass Shards, two at a time.
  - **The Hollowed**, delvers the Unmade emptied. They resist necrotic and psychic, and radiant hurts them double.
    - the Hollow Pilgrim: mindless, and it weakens;
    - the Glass-Eyed Archer: a pack archer;
    - the Void-Torn Hound: a pack hunter;
    - the Hollow Templar: an unyielding guard;
    - the Echo of the Fallen: a flying phaser with an area keen;
    - the Unsaid: a caster that curses and raises the fallen as Hollow Pilgrims, three at a time.

  The Unmade come through the rifts.
- **The art.** The Shardborn get new drawers of glass facets in `SpriteFactory.Glass`:
  - kites of black glass, dark on one side and violet-lit on the other;
  - the shardling, the prisms (the eye, the Refractor ringed with shards, the Glass Shards it and the Lens shed) and the Lens;
  - the strider, the hulk, the hound and the Echo use the bug, machine, beast and floater drawers.

  The Hollowed are people with glass eyes. Each family has its own cry. Glass has its own sound when hit and when
  it shatters.
- **The glass loot table**: gold, reagents, gear, scrolls and potions.
- **The region's switches** in `regions.json`: `shards` (8% of floor cells), `rifts` (2 a level) and `mirrorWalls`
  (25% of room walls).
- **Broken glass.**
  - **Placing.** It lies on floor cells away from the start, the stairs and the stations, and is drawn as shards
    over the floor.
  - **Stepping on it.** The first time the party steps on it, it crunches. Anything unaware within 4 steps along
    the passages wakes (a shut door counts as three). Each hero makes a DEX save against 13 + depth / 3, or takes
    2d6 + depth / 3 slashing.
  - **After.** The glass is ground to powder: the cell is kept in the level's flags, so saves keep it.
- **Rifts.**
  - **Placing.** A rift is a station in a room.
  - **Spawning.** While the party is within 6 cells, an open rift lets one of the Unmade through every 12 seconds,
    at depth + 2, already aware. Only those that don't summon come through (Unmade Hounds), so a rift can't
    loose a Hollow Saint and its endless hounds. After three it is spent.
  - **Sealing.** Use seals a rift: d20 + the better of Warding and Lore / 5 + WIS against 16, and the third try
    always works. Either way it trains Warding.
  - **Saves.** Sealed and spent rifts are kept in the level's flags. Spawning stops after three so a level can't
    keep a party (or the bot) fighting for ever.
- **Mirror walls**: some room walls are polished black glass, with cracks and a sheen. Only the look.
- **The Lens** (level 24):
  - **It learns blows.** A blow of the same damage type as the last one that hurt it is three quarters turned
    aside. The party has to keep changing: blade, then hammer, then spell.
  - **Its beam.** Every 10 seconds it turns and gathers light down its row or column, whichever the party is nearer
    to. A second and a half later the beam fires down that line. If the party is still in it, within 10 cells and
    in sight, each hero takes 6d8 + depth radiant, with a DEX save for half. Stepping out of the line dodges it.
  - **Shards.** At half health it sheds three Glass Shards.
- **The bot** seals an open rift within 8 cells before it hunts, and goes for a creature that summons or raises
  the dead (within 8 cells) before anything else, as a player would.
- **Results** (6 seeds, 24-level Delves, clearing bot): at first no party got past the Rootdark. After its tuning
  (section 38) two reach the Glass Roads and fall there, on levels 21 and 22, to the Refractor's shardlings and the
  rifts' Unmade. Tuning the Glass Roads is next.
- **Tuning, first pass.** A new opt-in report, `The_Glass_Roads_from_level_21` (`AVAHACK_BALANCE_GLASS=1`), plays
  deep Delves to level 21 and sets each party that gets there down on level 21 of other seeds (`AVAHACK_BALANCE_SEEDS`),
  so the region gets many runs in a few minutes. Its debug note lists the damage each kind of creature dealt on the
  last level (from the engine's `HeroStruck` event), the creatures awake near the party, and what the bot was hunting.
  What it found, and what changed:
  - **The Unmade were scaled far past the region.** Their base level is 18, so at the region's level (about 34) a
    hound had about 87d10. They no longer spawn here as residents; the rifts bring them, at depth + 2, and never a
    summoner.
  - **Callers.** Hollow Saints from rifts called hounds without end; the Unsaid raised the dead without a limit, and
    as spore Shamblers. Raisers now keep three raised about at most, the Unsaid's rise as Hollow Pilgrims, and a new
    creature field, `summonMax` (3 unless set), holds the Refractor to two shardlings.
  - **Dread creatures** weigh half as much in every region's spawn table, so a level holds fewer of them.
  - **Numbers.** Against the Rootdark at the depths they meet the party, the region's creatures hit 2 to 3 more often,
    had 20–30% more health and up to 40% more damage, and the Shardborn halved the party's blades. Every Shardborn and
    Hollowed attack lost 2 to hit, and their hit dice fell by a fifth; the Shardborn resist only piercing. The
    shardling, which dealt most of the damage, is slower (recovery 2.0 s), bleeds less (15%) and has 11d10+32; the
    Torn Hound does 2d8+6, the Refractor's spectrum 3d8+4 and the Echo's keen 2d10+6.
  - **Noise and rifts.** Glass wakes creatures within 4 steps (was 6), and a level has 2 rifts (was 3).
  - **Teleporters**, in every region: a sigil's disarm DC grows with depth and is out of reach deep down, so a
    level must have a way round every teleporter. A party walked into one, fumbled, and was thrown back for ever.
  - **Results** (one party that reached level 21, on 8 seeds): before, 11 of 12 runs fell on level 21 itself
    (average depth 21.1) and none got through. After, the average is 23.2: one run in eight reaches the Unmade Heart,
    the rest fall on levels 22–24, shardlings still the worst. One run still stalls on the stairs. More tuning is
    needed.
- **Tuning, second pass.**
  - **The Refractor calls Glass Shards**, the lesser shards the Lens sheds (renamed from "Shard of the Lens"), not
    shardlings: a shardling is a full creature of the region, and with its packs it did most of the damage.
  - **The bot steps out of the Lens's beam** when the Lens gathers light down its line, as a player who sees the
    warning would: four times in five it sidesteps (or steps on or back) to a free cell out of the line.
  - **Two bugs.** A curse that lasts until cured had an infinite time left, which JSON can't hold: saving a game with
    a cursed hero failed. It is saved as -1 now (`Character.Endless`). And the bot kept what it was hunting from one
    level to the next, so it could chase a creature of the level above for ever; it forgets it on a new level.
  - **The report** can keep the parties that reach level 21 (`AVAHACK_BALANCE_PARTIES=<folder>`) and start from
    them next time, so a change is measured in minutes, not half an hour.
  - **Results** (4 parties that reached level 21, 6 seeds each): through the Glass Roads 6 of 24 after the first
    pass, 9 of 24 after the second (average depth 23.7). Deaths on the Lens's level fell from 6 to 3; most deaths are
    now on level 23 and in the Unmade Heart, which is the next to tune.
  - **A key it gave up on** is worth another try after three minutes: one run stood for ever at a lock it couldn't
    pick (DC 37), its key reachable but given up on while it was fighting.
- **Tuning, third pass.**
  - **Smaller packs.** A new region field, `packMax` (4 unless set: packs are 2 to it), is 3 here. On the Lens's level
    the creatures are level 38–39, and packs of shardlings, Void-Torn Hounds and archers did most of the killing. A
    level has the same number of creatures; fewer come at once.
  - **Numbers.** The Obsidian Hulk's smash is 3d10+7 and stuns one time in seven (was 4d10+7 and one in four); the
    archers' arrows are 2d8+5; the shardling's shear 2d6+5.
  - **The bot** treats a lock that has beaten it twenty times, with no key, as a wall until it has the key (it stood
    at a DC 37 door, in the sweep of a blade trap, until the party fell); and, fighting on spikes or blades that cycle,
    it steps off them first. (Stepping off them whenever it stood on them made it bounce at the edge of every cycling
    trap: only in a fight.)
  - **The report** names every source of harm: a creature, `hazard` (traps), `brokenGlass`, `lensBeam`, or a
    condition such as `bleeding`. The engine's `HeroStruck` event now carries that name. Traps had been hidden: one
    level's blades did more than 4,000.
  - **Results** (2 parties, 12 seeds each): through the Glass Roads 13 of 24 (was 5 of 24 at the start of this pass),
    average depth 24.1; deaths on the Lens's level 3 (was 9), on level 23 six. Two runs still stall, with the bot
    standing by a rift or a closed door; that is the bot, not the region.
- **Tests.**
  - Glass Roads levels with their families, glass, rifts, mirror walls and the Lens, and the Heart's Waystone after it.
  - Glass crunching once, cutting, waking the hall, and gone.
  - A rift letting three of the Unmade through, then spent. Another sealed by Use by the third try.
  - The Lens learning a blow, and taking a new kind in full.
  - Its beam hitting a party in line, and missing one that stepped aside.
  - It shedding shards at half health.
  - A golden of a Glass Roads hall, and `Images/AVAHack-glass.png`.

## 40. Implementation decisions (the Unmade Heart below a 24-level Delve)

The Heart's creatures (the Unmade and the aberrations) are made for the Heart of a 12-level Delve, at level 20–21.
Below a 24-level Delve they were made at level 40 instead, scaled up from levels 7–18, and scaling that far
outgrows the deep regions' own creatures, which are made near their depth. An Unmade Hound had 670 hit points, AC
25 and +21 to hit; a Glass Roads shardling has 230, AC 20 and +13.

- **The report.** `AVAHACK_BALANCE_HEART=1` plays the Heart on its own. The parties kept at level 21 (section 39)
  play the Glass Roads; those that come down the last stairs are kept in `AVAHACK_BALANCE_HEART_PARTIES`, and each
  plays the Heart of three other seeds.
- **Before:** 0 of 42 won. Every party fell within ten minutes, and none touched the Unmaker.
- **A ceiling.** A new region field, `maxLevel`, is the highest level its creatures are made at, however deep the
  region lies (wanderers during a rest too). The Heart's is 28; it is out of reach below a 12-level Delve, so that
  Heart is unchanged. Its packs are 2 to 3 (`packMax`, section 39). The Unmaker keeps its own level, the depth + 1.
- **Choosing 28.** At a ceiling of 30 the bot won 11 of 42; at 28, 18 of 42 (43%); at 27, 36 of 42. With 54% of
  parties through the Glass Roads, about one 24-level Delve in four is won, where a 12-level Delve is won about
  half the time. Tendril Horrors, Unmade Hounds and Eye Moth Swarms still do most of the harm. Most parties that
  fall do so before they reach the Unmaker, and quickly (half within three minutes).
- **Two bot stalls, fixed.**
  - Setting out for a rift that lay just inside its reach, the bot's way round took it out of that reach, and it
    turned back for a pile of loot nearer by; there the rift was back in reach. It went to and fro for ever. Once it
    sets out for a rift, it keeps on until it's sealed.
  - Fighting on spikes, it stepped off them to a cell from which it couldn't reach the creature, then stepped
    back on to get at it. It steps off only to a cell it can still fight from.
- **The trace** (`AVAHACK_BALANCE_TRACE`) also lists the level's rifts, the traps near the party and the last things
  said, and, once a run has been stuck ten minutes, each new decision the bot makes (`Bot.Why`).
- **Results**, the Glass Roads after the fixes: 14 of 24 through and none stuck (was 13, with two stuck).
- **Tests.** The Heart's creatures are level 28 below a 24-level Delve and 20–21 below a 12-level one. The validator
  rejects a `packMax` under 2 and a `maxLevel` under 1.

## 41. Implementation decisions (heroic heroes)

- **What it is.** A third way to set attributes at creation (section 5.3): better scores (10–18, about 14 on
  average) for slower levels (1.25 × the character XP for each). It is set per hero, so a party can mix them, and
  saved with the hero (`CharacterSave.Heroic`). The sheet marks the XP line "(heroic)".
- **Why levels pay for it.** Attributes help with everything: to-hit, AC, HP, saves and spell power. Slower skills
  would have taken from what the hero is good at, which the scores make up for; slower levels cost perks, HP and
  the attribute points of every 4th level instead. The advantage is largest at the start and narrows as the
  levels fall behind.
- **One roll per hero.** Rolling again until the 18s came would make Heroic an easy mode, so each hero gets one
  roll, 4d6 or heroic, and keeps it. Point buy stays open; choosing the roll again brings back the same scores.
- **Laid out for the calling.** Heroic scores go best first to what the calling needs, as the standard array
  does; the 4d6 roll keeps its order. Either can then have two scores swapped, as often as the player likes; the
  4d6 roll keeps its swaps through point buy and back, and heroic scores are laid out again when the calling
  changes.
- **A fair trade.** The 12-level Delve report with heroic quick-start parties (`AVAHACK_BALANCE_HEROIC=1`, rolled
  from the seed and laid out by the quick-start heroes' own scores), on 16 seeds: normal parties won 10, heroic
  ones 9. The heroic winners ended at level 21–22, the normal ones at 23–26. A quarter more XP is the price.
  (Both runs had a seed stuck on the bot: AAAA62RB on its stairs in both, and AAAAVFWY on a hunt in the heroic
  one. Neither is in the 8 seeds the test suite plays. Both are fixed below.)
- **Tests.** The roll (ones rolled again, a score under 10 rolled again, 10–18 averaging about 14); a heroic hero
  needing 125 XP for level 2 and keeping the flag through a save; creation refusing a heroic score under 10; and,
  in the App, the Heroic button making a heroic fighter with Strength its best score, and a hero's one roll
  coming back after point buy, with 4d6 closed.
- **Three stuck seeds, fixed.**
  - **AAAA62RB, a generator fix.** The party fell through a pit into a pocket of level 12 whose only ways out were
    a hidden door it never found and the start's cell, where the stairs up would have climbed it a level. `Check`
    already required the landing to reach the stairs down, but let that way run through hidden doors and over the
    stairs up. Now it may use neither, as a teleporter's landing already may not. The 1,000-seed suite checks it
    (with the old check it finds four such levels at depths 5 and 9).
  - **AAAAVFWY, the bot.** Hunting a rat in the Hollow Choir's hall, it kept killing singers that rose again, and
    each kill counted as progress, so it never gave up. Only a new high counts now.
  - **AAABKTUF, the bot** (with the heroic party). An Ossuary Knight fell into bones and stood up again without a
    crushing blow. While it lay there the bot turned to other things, and turning back restarted its two minutes.
    Its time on each target now adds up until something is done.
- **Results after the fixes,** 16 seeds of the 12-level Delve: normal parties won 13, heroic ones 11, none stuck.
  The heroic winners ended at level 20–23, the normal ones at 23–26.
