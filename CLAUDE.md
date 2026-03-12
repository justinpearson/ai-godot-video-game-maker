# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

## What This Repo Is

A framework for building video games with natural language. You describe a game, Claude Code implements it in Godot 4.6 (C#), builds it, play-tests it, and iterates.

Forked from [tea-leaves](https://github.com/cleak/tea-leaves) — the engineering substrate behind [Quasar Saz](https://github.com/cleak/quasar-saz), a game designed by a dog pressing keys and built by Claude Code. This fork replaces the dog-keyboard input with well-formed human game descriptions.

The original author's agent-steering files (`CLAUDE.md.disabled`, `AGENTS.md.disabled`, `.claude.disabled/`) are preserved but deactivated. This file replaces them.

## Project Stack

| Component | Technology |
|---|---|
| Engine | Godot 4.6 Mono |
| Gameplay code | C# (.NET 8.0) |
| Tooling scripts | GDScript + Bash + Python |
| Physics | Jolt |
| Renderer | Forward Plus (Vulkan) |
| Tests | `dotnet test` (C# unit) + `./tools/test.sh` (gdUnit4 runtime) |
| Platform | macOS |

## Project Structure

```
res://
  scripts/         # C# gameplay code
  actors/          # Player and NPC scenes + scripts
  levels/          # Level scenes
  ui/              # HUD and menus
  util/            # Camera rigs, markers, utilities
  game/            # AutoLoads (WindowSetup, DevTools, EventBus)
  data/            # Resource definitions
  test/            # gdUnit4 test suites
    sequences/     # Input simulation sequences (JSON)
  tools/           # Build/lint/setup scripts (not shipped)
  addons/          # Third-party addons (gdUnit4)
  assets/          # Game assets (sounds, images, etc.)
```

## Setup (One-Time)

Prerequisites: Godot 4.6+ Mono at `/Applications/Godot_mono.app`, .NET 8.0 SDK, Python 3.

```bash
export PATH="/opt/homebrew/opt/dotnet@8/bin:/opt/homebrew/opt/coreutils/libexec/gnubin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"

dotnet restore
dotnet build -warnaserror
./tools/godot.sh --headless --script res://tools/setup_input_actions_cli.gd
```

## Build / Test / Lint Commands

```bash
# Build (must pass clean before any commit)
dotnet build -warnaserror

# Tests
dotnet test                    # C# unit tests (pure C# only, no Node classes)
./tools/test.sh                # gdUnit4 runtime tests (GDScript + C#)
./tools/test.sh --timeout 120  # With longer timeout

# Lint
./tools/godot.sh --headless --script res://tools/lint_project.gd    # UIDs + scenes
./tools/godot.sh --headless --script res://tools/lint_shaders.gd    # Shaders

# Run the game
./tools/godot.sh
```

## C# Conventions

- **C# for all gameplay logic.** GDScript only for editor tooling.
- Namespace: `TeaLeaves` (or `TeaLeaves.Systems` for core systems).
- Composition over inheritance for Nodes.
- `[Export]` with type hints for editor properties.
- `[GlobalClass]` for custom Resource types.
- Fail-fast: `GD.PushError()` for runtime issues, no silent fallbacks.
- Physics logic in `_PhysicsProcess(double delta)`.
- Fields set in `_Ready()` use `= null!` to avoid CS8618 warnings.
- `AddChild()` before setting `GlobalPosition`.

### Hand-Written Scene Files (.tscn)

When writing `.tscn` files by hand (not via editor):
- Use `[Export] NodePath PlayerPath` + resolve in `_Ready()` with `GetNodeOrNull<T>()`.
- Do NOT use typed node exports (`[Export] Player? Player`) — they won't deserialize from NodePath.
- Use `format=3` scene format.
- Run lint after creating scenes — UIDs may be regenerated.
- Always commit `*.uid` files.

## AutoLoads

Registered in `project.godot` under `[autoload]`. AutoLoad classes go in `game/`.

Existing:
- **WindowSetup** (`game/WindowSetup.cs`) — Centers window on primary monitor.
- **DevTools** (`game/DevTools.cs`) — Runtime command server for screenshots, input simulation, validation.

To add a new AutoLoad:
```ini
[autoload]
MySystem="*res://game/MySystem.cs"
```

## EventBus Pattern

Use a typed EventBus (`game/EventBus.cs`) for cross-system communication. Use Godot signals for parent-child / local communication.

```csharp
// Subscribe in _Ready(), unsubscribe in _ExitTree()
EventBus.Instance.SomeEvent += OnSomeEvent;
EventBus.Instance.SomeEvent -= OnSomeEvent;
```

## Testing

Two test runners, same gdUnit4 framework:

| Runner | Use for | Caveat |
|---|---|---|
| `dotnet test` | Pure C# logic (no Godot types) | Node-derived classes cause AccessViolationException |
| `./tools/test.sh` | GDScript tests, scene tests, engine-aware C# tests | Needs Godot runtime |

Keep testable logic in pure C# classes. Use `[RequireGodotRuntime]` for tests that need Godot.

## DevTools (Runtime Verification)

With the game running, use `python3 tools/devtools.py` for:

```bash
python3 tools/devtools.py ping                              # Check game is running
python3 tools/devtools.py screenshot                        # Capture screen
python3 tools/devtools.py screenshot --filename "test.png"  # Named screenshot
python3 tools/devtools.py validate-all                      # Check for missing assets
python3 tools/devtools.py performance                       # FPS, memory, draw calls
python3 tools/devtools.py scene-tree                        # Node hierarchy
python3 tools/devtools.py input tap jump                    # Simulate input
python3 tools/devtools.py input press move_forward          # Hold input
python3 tools/devtools.py input clear                       # Release all inputs
python3 tools/devtools.py quit                              # Quit game
```

Screenshots saved to: `~/Library/Application Support/Godot/app_userdata/TeaLeaves/screenshots/`

## Workflow: Building a Game

1. User describes a game in natural language.
2. Claude Code designs and implements it: C# scripts, scenes, resources, sounds.
3. Build: `dotnet build -warnaserror`
4. Test: `dotnet test` + `./tools/test.sh`
5. Lint: `./tools/godot.sh --headless --script res://tools/lint_project.gd`
6. Launch and screenshot to verify visually.
7. Iterate based on user feedback.

### Quality Standards

- Games must look polished — no placeholder art. Use procedural drawing, downloaded assets, or generated graphics.
- Characters with personality, not abstract shapes.
- Sound design matters — include sound effects and music.
- Animation: leverage the 12 principles. Static scenes are boring.
- Juice: effects, screen shake, particles, satisfying feedback.
- Target 1080p resolution.
- Always validate visually with screenshots. Be critical of results.
- Rename the project once the game concept is clear (TeaLeaves is a placeholder).

### Verification Checklist (Before Declaring Done)

1. `dotnet build -warnaserror` passes.
2. Tests created and passing.
3. `lint_project.gd` passes.
4. Game launches without errors.
5. Screenshot captured and visually verified.
6. Game is fun and matches the description.
