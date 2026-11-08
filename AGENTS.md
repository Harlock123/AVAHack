# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing and building; this file
covers how the code is put together and the rules that keep it working.

## What this is

AVAHack, a real-time, grid-based, first-person dungeon crawler, in C# on .NET 10 and Avalonia 12 (12.1.4). It
produces one self-contained executable per platform (Windows, macOS and Linux, x64 and ARM).

The design lives in `docs/SPECIFICATION.md`:
- section 20 has the milestones;
- sections 22–31 have the implementation decisions: one section per milestone (M0–M8), then the balance pass.

Everything in the game is original: the world, names, monsters and art. Keep it that way. Use no names, maps,
art, text or UI layouts from other games, and no trademarked tabletop content (spec section 2).

## Commands

```bash
dotnet build AVAHack.slnx                    # global.json: SDK 10.0.400 or later (rollForward latestFeature)
dotnet test AVAHack.slnx                     # Core, Render and headless App tests; no window opens
dotnet run --project src/AVAHack.App         # play
AVAHACK_SHOTS=<folder> dotnet test AVAHack.slnx          # save the rendered views and window frames
AVAHACK_UPDATE_GOLDEN=1 dotnet test tests/AVAHack.Render.Tests   # rewrite the golden images; look at them first
```

`Directory.Build.props` sets these for every project: `TreatWarningsAsErrors`, nullable reference types,
implicit usings, invariant globalization and central package versions. Package versions are in
`Directory.Packages.props`. A warning fails the build, so keep it at zero.

## Layout

- `src/AVAHack.Core/`: the game, with no UI references.
  - `World/`:
    - `Dir`, `Cell`, `Door`, `Lever`, and `Features.cs` (pits, plates, notes, spawns, secrets, traps, chests).
    - `Level`: the grid and everything on it, including the boss, the Waystone, the valves and the crafting
      stations.
    - `LevelLoader`, which reads the text level format (its doc comment and spec section 22).
    - `Generator*.cs` and `Vault.cs`: seeded 32×32 levels.
      - `Generator.Builder.cs` places rooms, stairs, bosses, Waystones and stations, then barriers, secrets, traps,
        pits, monsters and loot.
      - `Check` proves each level can be finished.
      - `SpawnTables` says which families live where.
  - `Game/`: `GameEngine` is one class split by subject.
    - `GameEngine.cs`: the 60 Hz tick, the mode, the queue and the motions.
    - `.Combat`: hands, blows, blocks, damage and death.
    - `.Monsters`: AI, packs, paths and line of sight.
    - `.Bestiary`: champions, affixes, summoners and the boss scripts.
    - `.Traps` and `.TrapKinds`: traps, stairs and the boss seal.
    - `.Loot`: chests, pickup, light and use.
    - `.Magic`: spells, hazards and summons.
    - `.Stealth`.
    - `.Levels`: difficulty, changing level and winning.
    - `.Waystone`: merchant, trainer, shrine and stash.
    - `.Crafting`: food, rest, Alchemy, cooking, Smithing and Enchanting.
    - `.Sound`: the `Sound` cues and `Hearing`. Sound fades along the passages, shut doors muffle it, and it is
      panned to its side.
    - `.Stacks`: region rules switched on in `regions.json` (the hush, candles, shelves), heralds, and the
      Archivist.
    - `.Rootdark`: spore pods and their fire, root tangles, burrowers, and the Mycel Crown and its nodes (also
      switched on in `regions.json`).
    - `.Glass`: broken glass, rifts and the Lens (also switched on in `regions.json`).
    - `.Save`.
    - The rest:
      - `Delve`: the run across levels, the Unmade Heart, and seed codes.
      - `Bestiary.Scale` and `ScaleTo`.
      - `Monster`.
  - `Rules/` (spec 6): `Rng` (`Pcg32`, `RngStreams`, `ScriptedRng`), `Dice`, `Attributes`, `Checks`, `Damage`,
    `Conditions`.
  - `Characters/` (spec 5, 7 and 8):
    - `Character`, with partials for items and saves.
    - `Party`, `Progression`, `CharacterBuilder` and `StartingKit`.
  - `Items/`: `Item`, `ItemBase` (every base, crafting ones included) and `LootGenerator`.
  - `Magic/`: spell definitions and names.
  - `Saves/`: `SaveGame` and `SaveStore`.
  - `Content/`:
    - `Definitions.cs`, `Creatures.cs`, `Items.cs` and `Crafting.cs`: the record shapes.
    - `ContentDb`: strict loading and `Validate`.
    - `Data/*.json`: skills, ancestries, callings, backgrounds, perks, conditions, creatures, weapons, shields,
      armour, gear, consumables, books, affixes, uniques, sets, loot, spells, alchemy, reagents, ingredients,
      recipes and regions. `regions.json` is the Delve's order of regions: four levels each, with their families,
      boss and rules. A new region needs:
      - its textures, from `tools/import-sbs-textures.sh`;
      - an ambience loop, `amb.<id>`, in `tools/make_sounds.py`.

      Tests fail without either.
    - `Levels/*.txt`: `test-hall` and `the-cellars`.
    - `Vaults/vaults.txt`.
- `src/AVAHack.Render/`: the first-person view, depending only on Core.
  - `SoftwareViewRenderer`: the ray caster.
  - `TextureFactory`: each region's textures. Walls, floors and ceilings come from the art in `Assets/Textures`
    (embedded, read by `PngReader`); doors, levers, pits, plates, stairs and markings are drawn in code over them.
    `TextureFactory.Drawn` is the all-code set.
  - `SpriteFactory`, `.Bestiary`, `.Creatures`, `.Rootdark` and `.Glass`: code-drawn creatures, props and icons,
    drawn on a 64-unit grid at 128 px. `Paint` lays down flat shapes (set `Mat` for fur, metal, bone, slime, stone or cloth; unshaded shapes
    are eyes and glows), then `Paint.Finish` shades, dithers and outlines them as pixel art. Change the look there,
    not creature by creature. `SpriteFactory.Items` draws an icon for every item base; `IconFor` gives an item's
    icon key, and a test fails if any base has no icon of its own. `SpriteFactory.Portraits` draws the heroes'
    portraits from ancestry, calling and a hash of the name.
  - `Scene`: the engine's creatures, missiles and props as sprites, with elite and champion tints.
- `src/AVAHack.App/`: Avalonia. `Views/GameView*.cs` is one control split by screen:
  - play;
  - menus (title, party creation, load, menu, help and inventory);
  - the sheet, magic and the Delve options;
  - the Waystone and crafting.

  `Input/InputHandler` maps keys. `Audio/` plays the engine's cues through OpenAL Soft (Silk.NET):
  - the sounds, embedded from `Assets/Sounds` and made by `tools/make_sounds.py`;
  - a bigger mixing period in a VM;
  - Windows' MinGW `soft_oal.dll`, swapped in by `Directory.Build.targets`;
  - the settings, in `sound.settings` in the saves folder.
- `tests/AVAHack.Core.Tests`: every rule and formula.
  - Subject tests: combat, traps, Cellars, party, items, saves, perks, magic, the generator, the Delve, the bestiary
    and crafting.
  - Reports:
    - `BalanceReport`: the Cellars.
    - `ProgressionReport`: callings to level 8.
    - `DelveReport`: 4-level and 12-level Delves. `AVAHACK_BALANCE_RUNS` sets the seeds and `AVAHACK_BALANCE_DEBUG=1`
      adds the heroes' skills. To see why one seed gets stuck, set `AVAHACK_BALANCE_TRACE=<seed code>` and
      `AVAHACK_BALANCE_TRACE_FILE=<path>`: it writes a map of each level. `AVAHACK_BALANCE_DEEP=1` adds the 24-level
      Delve, `AVAHACK_BALANCE_GLASS=1` the Glass Roads on their own, from parties that reached level 21, and
      `AVAHACK_BALANCE_HEART=1` the Unmade Heart on its own, from parties that came down to it.
  - `Bot` plays like a fallible person. With `Clears` it clears each level first (hunting, looting, Waystones,
    valves, locks, searching), as the 12-level report needs.
- `tests/AVAHack.Render.Tests`:
  - golden images;
  - a sprite sheet that checks every creature has art;
  - a frame-time check.
- `tests/AVAHack.App.Tests`: the real `GameView` in Avalonia.Headless, driven by keys and clicks.

## Rules that keep it working

- **The engine first, the drawing second.** Gameplay belongs in Core. Render and App only read state and send
  commands. Anything a player can do must be doable through `GameEngine` in a test.
- **Rules are data where they can be.** Ancestries, callings, backgrounds, skills' use XP, perks and conditions
  live in `Content/Data/*.json`. A new field needs its record in `Definitions.cs`, and a new cross-file rule
  needs a check in `ContentDb.Validate` and a case in `ContentTests`. Code that looks for a trait, effect or flag
  uses the names in `Traits` (Definitions.cs), so the validator knows them.
- **Every formula has a test.** When a rule in spec sections 6–8 changes, change its test and the spec together.
- **Deterministic.** The same level and the same presses give the same run (a test checks this). Content made
  in code (textures, and later levels) uses integer hashing or seeded generators, never `Random.Shared` or the
  clock.
- **Keys go through `InputHandler`, matched by `PhysicalKey`.** Test new keys through `InputHandler` too. On
  this desktop, Shift's release can arrive as CapsLock, so never track held keys by `Key`.
- **New content gets its validation.** A new creature behaviour, effect kind or buff needs its name in the lists
  `ContentDb.Validate` knows (`CreatureDef` behaviours, effect kinds, `Traits.Buffs`).
- **Saves keep everything.** New engine or monster state goes in `SaveGame` or `MonsterSave`, and a test reloads it.
- **A level change gets its checks.** Change `the-cellars.txt` and `CellarsTests` must still pass: every floor
  cell, creature and the stairs must be reachable while working the mechanisms in order. Read the
  `BalanceReport` before and after a change to creatures, combat or the level.
- **The eye stays in the party's cell.** `SoftwareViewRenderer.EyeBack` must stay below 0.5, or rays can start
  inside a wall.
- **Changing the look means changing the goldens.** If a render change is meant to be seen, rewrite the goldens
  with `AVAHACK_UPDATE_GOLDEN=1`, open the PNGs and check them, and commit them with the change. If the change
  isn't meant to be seen and a golden fails, the change is wrong.
- **Third-party art is recorded.** Each file goes in `assets.manifest.json` with its source, licence and changes,
  and its licence file ships beside it. Run `tools/build_credits.py` to remake `CREDITS.md`. `AssetTests` fails if a
  shipped texture isn't listed. Prefer CC0 or CC-BY, never share-alike, and never art taken from another game.
  Textures are made by scripts in `tools/` (`import-sbs-textures.sh`), not edited by hand.
- **Every sound has a maker.** A new cue gets its sound in `tools/make_sounds.py` (run it, and commit the WAVs). The
  App's `SoundTests` fail if a cue the engine raises has no sound, or a sound is never played. Cues never change
  the game: a test plays the same game with and without a listener.
- **Never trim.** Avalonia loads XAML by reflection, so `PublishTrimmed` stays false.
- **Tests never touch the user's folders.** Anything a test writes goes to a temp folder or to the folder in
  `AVAHACK_SHOTS`.

## Making a change

- Run `dotnet test AVAHack.slnx`. A new rule or formula from the spec gets a unit test in Core.Tests.
- If the UI or view changes, refresh `Images/` from `AVAHACK_SHOTS` and update `README.md`.
- When a milestone finishes, mark it in spec section 20 and add its decisions to the spec's decisions section.

## CI and releases

`.github/workflows/build.yml` runs on every push. It builds, runs the tests, and publishes single-file,
self-contained builds for eight platforms. Pushes to `main` refresh the rolling `latest` pre-release, and a
`v*` tag makes a proper release. After a release, it asks the portfolio site to rebuild.
