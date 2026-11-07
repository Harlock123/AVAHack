# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing and building; this file
covers how the code is put together and the rules that keep it working.

## What this is

AVAHack, a real-time, grid-based, first-person dungeon crawler, in C# on .NET 10 and Avalonia 12 (12.1.4). It
produces one self-contained executable per platform (Windows, macOS and Linux, x64 and ARM).

The design lives in `docs/SPECIFICATION.md`:
- section 20 has the milestones;
- section 22 has the implementation decisions.

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
    - `Dir` (compass directions and yaw).
    - `Cell`.
    - `Level` (the grid, doors and levers).
    - `Door` and `Lever`.
    - `LevelLoader`, which reads the text level format; the format is in its doc comment and in spec
      section 22.
  - `Game/`:
    - `GameEngine`: the fixed 60 Hz `Tick`, `Update` and `Alpha`, the action queue, the step, turn and bump
      motions, `Camera(alpha)`, doors and levers, `Press(GameAction)` and `ClickWorld(WorldTarget)`.
    - `GameAction`, `CameraPose` and `WorldTarget`.
  - `Rules/` (spec 6):
    - `Rng`: `Pcg32`, `RngStreams` and `ScriptedRng` for tests.
    - `Dice`: parses expressions such as `2d6+3`.
    - `Attributes`: `AttributeSet`, modifiers, point buy, and 4d6 drop the lowest.
    - `Checks`: d20 rolls, advantage, proficiency, spell DCs.
    - `Damage`: `Combat.Attack` and `Combat.Damage`, damage types, `Resistances`.
    - `Conditions`: `ConditionSet`, with saves every 3 s, damage over time and cures.
  - `Characters/` (spec 5, 7 and 8):
    - `Character`: one hero's attributes, derived statistics, saves, attacks, pools and regeneration, skills and
      skill XP, books and trainers, levels and level-up choices, and perks and respecs.
    - `Progression`: the formulas as pure functions.
    - `CharacterBuilder`: creation checks and the quick-start party.
    - `SkillId`.
  - `Content/`:
    - `Definitions.cs`: the record shape of each data file.
    - `ContentDb`: strict loading and `Validate`.
    - `Data/*.json`: skills, ancestries, callings, backgrounds, perks and conditions.
    - `Levels/*.txt`: built-in levels.
- `src/AVAHack.Render/`: the first-person view, depending only on Core.
  - `IViewRenderer`.
  - `SoftwareViewRenderer`: the column ray caster, doors, light and picking.
  - `Texture`, `TextureSet` and `TextureFactory`: the procedural placeholder textures.
- `src/AVAHack.App/`: Avalonia.
  - `Views/GameView`: the frame loop (`RequestAnimationFrame`), copying the framebuffer to a `WriteableBitmap`,
    fitting the view at 16:10, the compass, messages, F3 stats, and clicks.
  - `Input/InputHandler`: keys to actions.
- `tests/AVAHack.Core.Tests`:
  - levels, movement, the queue, timing, doors, levers and determinism (M0);
  - every formula in spec sections 6–8, conditions, and the content's strictness and validation (M1).
  - `Heroes` makes the quick-start heroes and levels them up.
- `tests/AVAHack.Render.Tests`: golden images in `Golden/` (compared by `GoldenImage`, PNG I/O through
  SkiaSharp), full coverage from every cell and facing, and a frame-time check.
- `tests/AVAHack.App.Tests`: the real `GameView` in an Avalonia.Headless window, with keys and clicks.

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
- **The eye stays in the party's cell.** `SoftwareViewRenderer.EyeBack` must stay below 0.5, or rays can start
  inside a wall.
- **Changing the look means changing the goldens.** If a render change is meant to be seen, rewrite the goldens
  with `AVAHACK_UPDATE_GOLDEN=1`, open the PNGs and check them, and commit them with the change. If the change
  isn't meant to be seen and a golden fails, the change is wrong.
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
