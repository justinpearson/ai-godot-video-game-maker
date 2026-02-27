# AI Godot Video Game Maker

A framework for rapidly building Godot video games with Claude Code. You describe the game you want, and Claude Code designs, implements, play-tests, and iterates on it using this repo's automated verification pipeline.

Forked from [tea-leaves](https://github.com/cleak/tea-leaves), the engineering substrate behind [Quasar Saz](https://github.com/cleak/quasar-saz) (a game designed by a dog and built by Claude Code). This fork replaces the dog-keyboard input with coherent game descriptions and ports everything from Windows to macOS.

## How It Works

1. You provide a coherent description of the video game you want to build.
2. Claude Code interprets the description and generates Godot code (C# gameplay, scenes, resources, sounds).
3. The automation stack validates the result: build, test, lint, runtime checks, and screenshots.
4. You iterate by providing follow-up descriptions to refine or extend the game.

## Prerequisites

- **macOS** (this fork targets macOS; the original targets Windows)
- **Godot 4.6+ Mono** installed to `/Applications/Godot_mono.app` or set `GODOT4_MONO_EXE`
- **.NET 8.0 SDK**
- **Python 3** (for runtime DevTools CLI)

## Quick Start

```bash
# 1. Clone and enter the repo
git clone <this-repo-url>
cd ai-godot-video-game-maker

# 2. Restore dependencies and build
dotnet restore
dotnet build -warnaserror

# 3. Initialize input actions
./tools/godot.sh --headless --script res://tools/setup_input_actions_cli.gd

# 4. Run tests
dotnet test
./tools/test.sh

# 5. Launch Claude Code and describe your game!
```

## Technical Snapshot

- Engine: Godot 4.6 Mono
- Gameplay language: C# (`net8.0`)
- Tooling language: GDScript + Bash + Python
- Physics: Jolt
- Renderer: Forward Plus (Vulkan on macOS)
- Test stack: `dotnet test` + gdUnit4 via `./tools/test.sh`

## Runtime Architecture

### Boot Autoloads

`project.godot` wires two autoloads:

- `WindowSetup` (`game/WindowSetup.cs`): centers the window on the primary monitor at startup.
- `DevTools` (`game/DevTools.cs`): runtime command server used by local automation.

### DevTools File Protocol

`DevTools` uses a file-based transport under `user://`:

- Command inbox: `devtools_commands.json`
- Result outbox: `devtools_results.json`
- Structured log stream: `devtools_log.jsonl`

The autoload polls every ~100 ms, dispatches commands, and writes structured JSON responses for CLI clients.

### Runtime Command Surface

Implemented command families:

- Visual: `screenshot`, `scene_tree`
- Validation: `validate_scene`, `validate_all_scenes`
- Introspection/mutation: `get_state`, `set_state`, `run_method`
- Input simulation: `input_press`, `input_release`, `input_tap`, `input_clear`, `input_actions`, `input_sequence`
- Runtime ops: `performance`, `ping`, `quit`

### Runtime Scene Validation

`SceneValidator` (`game/SceneValidator.cs`) validates scenes in two phases:

1. Static `SceneState` scan for missing scripts/resources, invalid signal connection metadata, and relative `NodePath` usage hints.
2. Instantiation scan for missing meshes, textures, shaders, collision shapes, audio streams, and invalid `AnimationPlayer` track targets.

## Toolchain and Verification

### Godot Launcher Wrapper

`tools/godot.sh` resolves Godot from:

1. `GODOT4_MONO_EXE` environment variable
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot`
3. `~/Applications/Godot_mono.app/Contents/MacOS/Godot`
4. `godot` on PATH (e.g. via Homebrew)

### Project Lint

`tools/lint_project.gd` performs:

- UID consistency checks for `ext_resource` entries in `.tscn/.tres`
- Scene-level NodePath resolution warnings using `SceneState`
- Optional JSON output (`--json`)
- Modes: `--uids-only`, `--warnings-only`, `--fail-on-warn`

### Tests and Test Lint

- `dotnet test`: C# tests
- `./tools/test.sh`: gdUnit4 runtime tests with timeout handling and normalized exit codes
- `./tools/lint_tests.sh`: gdUnit conventions (`extends GdUnitTestSuite`, `test_` naming, assertion presence, loop sanity)

### Input Bootstrap

`tools/setup_input_actions_cli.gd` seeds and persists default actions:

`move_forward`, `move_backward`, `move_left`, `move_right`, `jump`, `crouch`, `sprint`, `swim_up`, `swim_down`

## CLI Workflow

```bash
# Restore/build/test
dotnet restore
dotnet build -warnaserror
dotnet test
./tools/test.sh
./tools/godot.sh --headless --script res://tools/setup_input_actions_cli.gd

# Static project lint
./tools/godot.sh --headless --script res://tools/lint_project.gd

# Run game + runtime verification loop
./tools/godot.sh &
python3 tools/devtools.py ping
python3 tools/devtools.py input list
python3 tools/devtools.py input sequence test/sequences/example_template.json
python3 tools/devtools.py screenshot --filename "verification.png"
python3 tools/devtools.py validate-all
python3 tools/devtools.py performance
python3 tools/devtools.py input clear
```

Screenshots are written to:
`~/Library/Application Support/Godot/app_userdata/TeaLeaves/screenshots/`

## Repository Status

This repo ships the platform and verification infrastructure, not a full game content stack:

- `actors/`, `levels/`, `scripts/`, `ui/`, `data/`, `util/` are scaffolded directories.
- `test/unit/test_example.gd` and `test/sequences/example_template.json` are starter references.

The idea: you describe a game, Claude Code builds it within this framework.

## Upstream / Related Projects

- **[tea-leaves](https://github.com/cleak/tea-leaves)** - The original repo (Windows, dog-keyboard input)
- **[Quasar Saz](https://github.com/cleak/quasar-saz)** - A finished game built on the original foundation, designed by a dog and developed by Claude Code ([watch the video](https://youtu.be/8BbPlPou3Bg))
- **[DogKeyboard](https://github.com/cleak/DogKeyboard)** - All the routing and miscellaneous tasks for reading input from Momo, dispensing treats, and playing chimes for her

## License

MIT. See `LICENSE`.
