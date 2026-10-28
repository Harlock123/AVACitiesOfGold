# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls, what
the tests check and the project tree; this file covers how the code is put together and the rules that keep it
working.

## What this is

An exploration and trading game in the style of Ozark Softscape's 1984 Seven Cities of Gold, in C# on .NET 10 and
Avalonia 12. A career is a series of voyages: you sail to a new continent, meet its peoples, look for golden cities
and bring the gold home. One self-contained executable per platform. Everything in it is original: each continent
is grown from a seed, the art is drawn in code, and the sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVACitiesOfGold.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVACitiesOfGold.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVACitiesOfGold          # play it; --no-audio for silence
GOLD_SHOTS=$PWD/Images dotnet test tests/AVACitiesOfGold.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVACitiesOfGold/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types, implicit usings and
`InvariantGlobalization` for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVACitiesOfGold/World/`: the model.
  - `Map.cs` has the terrain and grows the continent from a seed.
  - `Peoples.cs` has the six peoples and their settlements.
  - `WorldState.cs` is the expedition and the career, and is what a save holds.
  - `SaveStore.cs` and `Rng.cs`.
- `src/AVACitiesOfGold/Game/`: `GameEngine` (split over partial files: Voyage, Peoples, Court) holds the whole
  game apart from drawing.
  - `Panel.cs` is a menu or message; panels stack over the game.
  - `DemoPilot` is the attract mode's explorer, and `SoundManager` maps `Sfx` to sounds.
- `src/AVACitiesOfGold/Views/`: `GameCanvas` takes the keys and runs a 16 ms timer, and `GameCanvas.Draw.cs` draws
  the land, the chart, the court and the panels.
- `src/AVACitiesOfGold/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVACitiesOfGold.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `Helpers.cs` sets up games with a
  temporary save folder.

## Rules that keep it working

- **Moves, not frames.** There is no fixed-step simulation. The game advances on key presses through
  `GameEngine.Press`, and `Update(dt)` only runs the clock, the idle timer and the attract demo. Tests play by
  pressing keys.
- **Seeded randomness.** A seed always makes the same world: `WorldState.New(seed)` and `Map` build it with
  `new Random(seed)`. Chance during play comes from the engine's seeded `Rng` (`g.Rng`). Never use
  `Random.Shared` or an unseeded `Random`.
- **Engine first, drawing second.** Gameplay belongs in `Game/` and `World/`, where tests reach it without a
  window. `Views/` only reads the engine and draws it.
- **Keys arrive by name.** `GameCanvas.KeyName` turns an Avalonia `Key` into a name ("Up", "Enter", "M") for
  `Press`. Test new controls through `Press`, not by setting state directly.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** Saves live in the app-data folder (`AVACitiesOfGold`). Tests use
  `Helpers.TempSaves()` for a temporary folder, and the screenshot tests run silent.
- **Line endings.** The sources are LF; keep them that way.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
