# Credits

Generated from `assets.manifest.json` by `tools/build_credits.py`. Every third-party asset the game ships is listed here.

## Old School Dungeon Crawler Pack and Cave Walls Addon

- Source: [https://screamingbrainstudios.itch.io/dungeon-crawler-pack](https://screamingbrainstudios.itch.io/dungeon-crawler-pack) (retrieved 2026-11-07)
- Licence: CC0 1.0 (public domain) (`src/AVAHack.Render/Assets/Textures/LICENSE-ScreamingBrainStudios.txt`)
- Authors: Screaming Brain Studios
- Changes: tools/import-sbs-textures.sh keeps each set's face-on Layer 1 wall and un-warps its near floor and ceiling into a flat cell. For the brick sets it enlarges a 64 px quarter to 128 px, and it scales every texture to one brightness. Sets: Brick 3 (cellar), Brick 1 (works), Brick 2 (choir), Shale Cave (heart).
- Files: 12 (listed in `assets.manifest.json`)

## OpenAL Soft 1.23.1 for Windows (x64, x86)

- Source: [https://github.com/kcat/openal-soft/releases/tag/1.23.1](https://github.com/kcat/openal-soft/releases/tag/1.23.1) (retrieved 2026-11-07)
- Licence: LGPL-2.0-or-later (`src/AVAHack.App/Audio/native/COPYING-openal-soft.txt`)
- Authors: Chris Robinson and the OpenAL Soft contributors
- Changes: None: the official MinGW builds, which need no Visual C++ runtime, swapped in for Silk.NET's by Directory.Build.targets (see src/AVAHack.App/Audio/native/README.md). Other platforms use Silk.NET.OpenAL.Soft.Native's builds.
- Files: 2 (listed in `assets.manifest.json`)

## Everything else

The creatures, items, portraits, doors, levers, pits, plates, stairs and markings are drawn in code. Every sound effect and ambience loop is synthesised by `tools/make_sounds.py`. Both are original to AVAHack, and the code is under the licence in `LICENSE`.
