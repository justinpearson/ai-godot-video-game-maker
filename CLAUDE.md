# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**TeaLeaves** is a Godot 4.6 project using GDScript as the primary language with C# support (Mono). It uses Jolt Physics and Forward Plus rendering with D3D12 on Windows.

## Key Commands

### Running Godot
```bash
# Run Godot (resolves executable via GODOT4_MONO_EXE env var or standard paths)
pwsh ./tools/godot.ps1

# Run headless (for scripts/linting)
pwsh ./tools/godot.ps1 --headless --script res://path/to/script.gd
```

### Testing (GUT)
```bash
# Run all tests (60s timeout)
pwsh ./tools/test.ps1

# Run tests matching a pattern
pwsh ./tools/test.ps1 -Select "player"

# Run specific test file
pwsh ./tools/test.ps1 -Test "res://test/unit/test_inventory.gd"
```

Test exit codes: 0=pass, 1=failures, 124=timeout.

### Validation & Linting
```bash
# Full project lint (UIDs + scene warnings)
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd

# Lint specific scenes
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd -- --scene res://path/to/scene.tscn

# Validate script parsing (syntax check)
pwsh ./tools/godot.ps1 --headless --script res://tools/validate_scripts.gd

# Full context test with Game AutoLoad (catches runtime errors)
pwsh ./tools/godot.ps1 --headless res://tools/test_interaction_scene.tscn

# Lint all shaders
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_all_shaders.gd

# Lint single shader
pwsh ./tools/godot.ps1 --headless --script res://tools/shader_lint.gd -- path/to/shader.gdshader

# GDScript linting (gdlint is on PATH)
gdlint path/to/file.gd
```

**Always lint after changes:**
1. `gdlint` for modified GDScript files
2. `lint_project.gd` for scene/UID validation
3. `shader_lint.gd` for modified shaders
4. Use short timeouts (20s max) when running Godot commands

### Setup
```bash
# Configure input actions
pwsh ./tools/godot.ps1 --headless --script res://tools/setup_input_actions_cli.gd
```

## Project Structure

```
res://
  actors/          # Player and NPCs
  levels/          # Level scenes and scripts
  ui/              # HUD and menus
  util/            # Camera rigs, markers, utilities
  game/            # AutoLoads and global state
  data/            # Resource definitions (items, verbs)
  tools/           # Linting and setup scripts (not shipped)
  addons/          # Third-party addons (GUT testing framework)
```

## GDScript Conventions

### Code Style
- **Static typing everywhere**: `var player: Player`, `func get_items() -> Array[ItemData]`
- **@export** for editor-exposed properties with clear type hints
- **Signals** for decoupling: define at top of class, emit with descriptive names
- Group related functionality with comment headers: `# --- Navigation ---`
- Avoid abbreviations unless extremely common (`hp_max` is ok, avoid `hp_dmg`)

### Comments
- Every script should have a short description of what it does
- Keep comments meaningful and up to date
- Do NOT add temporary comments like "Removed old system" or "Updated from 1.0"

### Resource Patterns
- Custom Resource classes for data (`extends Resource`)
- Name resource files with descriptive suffixes: `.verb.tres`, `.item.tres`

### Node Organization
- Keep nodes self-contained with clear responsibilities
- Use groups for querying: `add_to_group("interactable")`
- Nav meshes should be done by groups
- Validate node setup in `_ready()` with asserts for required children/properties

### Error Handling
- **Fail fast**: use `assert()` for invariants
- Use `push_error()` for runtime issues needing logging
- Implement `_get_configuration_warnings()` for editor-time validation
- Do NOT create fallbacks when code is expected to work—raise errors and flag problems clearly

## Validation Pipeline

Before committing:
1. `gdlint path/to/file.gd` for modified GDScript files
2. `pwsh ./tools/godot.ps1 --headless --script res://tools/validate_scripts.gd` (syntax check)
3. `pwsh ./tools/godot.ps1 --headless res://tools/test_interaction_scene.tscn` (catches runtime errors with AutoLoads)
4. `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd` (UIDs + scene warnings)
5. Always check scenes for errors after editing

## Important Notes

- **Physics Layers**: Interactables use layer 2, ground/navigation uses layer 1
- **Input Map**: Configured with `click_move`, `click_context`, `ui_cancel`
- **Platform**: Windows (D3D12 rendering)
- **Godot Version**: Requires Godot 4.6+ Mono (set via `GODOT_VERSION` env var)
- **Pre-push Hook**: Runs tests (90s timeout). Bypass with `git push --no-verify`
- **File Paths**: Use complete absolute Windows paths with drive letters and backslashes for all file operations
- **Commit frequently** to git

## Testing Infrastructure

The project uses GUT (Godot Unit Test) for GDScript testing:
- Test files go in `res://test/unit/` and `res://test/integration/`
- Test files must be named `test_*.gd` and extend `GutTest`
- Test methods must start with `test_`
- Configuration in `.gutconfig.json`

Example test:
```gdscript
extends GutTest

func test_player_starts_with_full_health() -> void:
    var player := Player.new()
    assert_eq(player.health, player.max_health)
```
