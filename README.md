# AVAHack

A real-time, grid-based, first-person dungeon crawler in C# on .NET 10 and Avalonia 12. A party of delvers goes
down into the Hollow beneath Harrowgate, a dungeon that rearranges itself each time the town's bell tolls. It
mixes tabletop-style character building (six attributes, ancestries, callings, d20 rolls) with growth by use:
skills rise as you use them, and perk trees hang off each skill.

The design is in [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

**Status: milestone M8, plus a balance pass.** What's in so far:

- **The Delve.** A seeded, generated descent of up to 12 levels across three regions, ending in the Unmade Heart.
  Choose the seed, depth, monster strength, density, traps, magic items, food and Ironman before you start.
- **Your party.** Up to four heroes, made from ancestries, callings and backgrounds. Skills grow with use and
  kills bring experience, with level-ups and perk trees.
- **The bestiary.** 58 creatures in ten families, plus wanderers, each drawn in code. Elites and named champions
  carry the 16 affixes, and five bosses guard the way down.
- **Magic and loot.** 48 spells, and items with rarity, affixes, sets and uniques.
- **Crafting.** Alchemy with hidden reagent effects, cooking, Smithing and Enchanting.
- **Survival.** Food and water, and rests that can be interrupted.
- **Waystones** after each boss, with a merchant, a trainer, a shrine and a stash.
- **Saves.** Save slots, quick saves and Ironman.

The handmade first level, The Cellars, is still there to learn on. A full 12-level Delve is tuned to be winnable,
ending with heroes in their mid-twenties (spec section 31).

![A fight in the rat nest](Images/AVAHack-rat-fight.png)

| | |
| --- | --- |
| ![The crypt](Images/AVAHack-crypt.png) | ![The goblin warren](Images/AVAHack-warren.png) |
| ![The Waystone](Images/AVAHack-waystone.png) | ![Crafting](Images/AVAHack-craft.png) |

## Playing

```bash
dotnet run --project src/AVAHack.App -c Release
```

| Action | Keys |
| --- | --- |
| Step forward / back | W / S, the Up / Down arrows, keypad 8 / 2 |
| Strafe left / right | A / D, keypad 4 / 6 |
| Turn left / right | Q / E, the Left / Right arrows, keypad 7 / 9 |
| Each hero's left / right hand: swing (hold for a power attack), block, cast | 1 2 · 3 4 · 5 6 · 7 8, or click the hand |
| Use what is ahead: door, lever, chest, Waystone, forge, altar | F, Enter, keypad 5, or click it in the view |
| Pick up | G |
| Next hero / move the hero to a slot | Tab / Ctrl+1–4 |
| Inventory, character sheet, spellbook, crafting | I, C, B, K |
| Quick spells | F1–F8 |
| Sneak / rest | X / R |
| Pause | Space |
| Menu (save, load, quit) / help | Esc / H |
| Quick save / quick load | F10 / F11 |
| Frame rate and render time | F12 |

Tips:
- **Every attack has a tell.** Watch for the wind-up and the glow. Step out of reach before the blow lands, or
  raise the shield just as it does: a perfect block costs nothing and staggers the attacker.
- **Hit them where they aren't looking.** A creature that isn't facing you is hit with advantage, and one that
  hasn't noticed you takes double damage.
- **Bone sentries stand up again** unless the killing blow is a crushing one. Bash the bones with the shield.
- **Out of a fight you heal slowly.** Rest with R, but behind a shut door if you can: things wander.
- **A boss seals the stairs down.** The Bellwright Engine can't be hurt until its three valves are shut, and Mother of
  Coils only when the water drops.
- **Taste reagents to learn them,** then brew two or three that share an effect. A brew of pure poison coats your
  blade.

## How it is drawn

The view is drawn on the CPU by a column ray caster into a 640×400 framebuffer, which is then scaled up to the
window. Walls, floor and ceiling are textured and lit by the party's torch. Doors slide up into the ceiling, and
you can see through a portcullis. Creatures are camera-facing pictures with front, side and back views, drawn
against a depth buffer. The textures and creatures are drawn in code as placeholders. Because there is no GPU in the path, the
tests render real frames and compare them with golden images. A frame takes about 2.3 ms on an 8-core ARM
machine.

## Building and testing

```bash
dotnet build AVAHack.slnx
dotnet test AVAHack.slnx                       # no window opens
AVAHACK_SHOTS=/tmp/shots dotnet test AVAHack.slnx   # also save the views and window frames as PNGs
dotnet test tests/AVAHack.Core.Tests --filter "BalanceReport|DelveReport" --logger "console;verbosity=detailed"   # the bot's runs
```

## Downloads

Every push to `main` builds self-contained, single-file programs for Windows, macOS and Linux (x64 and ARM).
They are attached to the [latest pre-release](../../releases/tag/latest). Tags `v*` make proper releases.

## Licence

MIT. See [LICENSE](LICENSE).
