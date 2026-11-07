# AVAHack

A real-time, grid-based, first-person dungeon crawler in C# on .NET 10 and Avalonia 12. A party of delvers goes
down into the Hollow beneath Harrowgate, a dungeon that rearranges itself each time the town's bell tolls. It
mixes tabletop-style character building (six attributes, ancestries, callings, d20 rolls) with growth by use:
skills rise as you use them, and perk trees hang off each skill.

The design is in [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

**Status: milestone M2.** A playable first level, The Cellars. A lone dwarf fighter fights his way down
through rats, spiders, shamblers, bone sentries that won't stay dead, and a warren of goblins. On the way are
hidden pits, pressure plates, levers and portcullises. Under the hood are the rules core of M1 (attributes, d20
checks, conditions, skills that grow by use, levels and perks) and the view of M0. There is no party, no items
to pick up and no saving yet.

![A fight in the rat nest](Images/AVAHack-rat-fight.png)

| | |
| --- | --- |
| ![The crypt](Images/AVAHack-crypt.png) | ![The goblin warren](Images/AVAHack-warren.png) |

## Playing

```bash
dotnet run --project src/AVAHack.App -c Release
```

| Action | Keys |
| --- | --- |
| Step forward / back | W / S, the Up / Down arrows, keypad 8 / 2 |
| Strafe left / right | A / D, keypad 4 / 6 |
| Turn left / right | Q / E, the Left / Right arrows, keypad 7 / 9 |
| Swing the sword (hold, then let go, for a power attack) | 2, or right-click the view, or click the sword |
| Raise the shield to block (tap it to bash instead) | 1, or click the shield |
| Use what is ahead: open a door, pull a lever | F, Enter, keypad 5, or click it in the view |
| Pause | Space |
| Start again after falling or winning | Enter |
| Frame rate and render time | F3 |

Tips:
- **Every attack has a tell.** Watch for the wind-up and the glow. Step out of reach before the blow lands, or
  raise the shield just as it does: a perfect block costs nothing and staggers the attacker.
- **Hit them where they aren't looking.** A creature that isn't facing you is hit with advantage, and one that
  hasn't noticed you takes double damage.
- **Bone sentries stand up again** unless the killing blow is a crushing one. Bash the bones with the shield.
- **Out of a fight you heal slowly**, so it's worth waiting before the next room.

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
dotnet test tests/AVAHack.Core.Tests --filter BalanceReport --logger "console;verbosity=detailed"   # the bot's runs
```

## Downloads

Every push to `main` builds self-contained, single-file programs for Windows, macOS and Linux (x64 and ARM).
They are attached to the [latest pre-release](../../releases/tag/latest). Tags `v*` make proper releases.

## Licence

MIT. See [LICENSE](LICENSE).
