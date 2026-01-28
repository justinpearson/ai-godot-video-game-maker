# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**TeaLeaves** is a Godot 4.6 project using **C# for all gameplay logic** with GDScript reserved for editor tooling only. It uses Jolt Physics and Forward Plus rendering with D3D12 on Windows.

## Core Tenets

- **C# for gameplay**, GDScript only for editor tooling or tiny glue
- **NEVER write GDScript** for gameplay unless absolutely necessary
- **Composition over inheritance** for Nodes
- **Typed EventBus** for cross-system communication
- **Data-driven configs** using Godot Resource assets (`[GlobalClass]` C# classes)
- **Fail-fast validation**: Misconfigured objects are disabled and logged via `GD.PushError()`
- **Deterministic state machines** with explicit state transitions
- **Test-driven debugging**: Create a failing test first, verify it fails, fix the code, verify the test passes

## Language Usage

- **C#** for all gameplay logic, systems, and tests
- **GDScript** ONLY for editor tools and scene glue scripts
- Never use GDScript for gameplay logic

### Namespace Convention
```
TeaLeaves.*           # All gameplay code
TeaLeaves.Systems.*   # Core systems (EventBus, state machines, etc.)
```

## C# Conventions

- All physics logic in `_PhysicsProcess(double delta)`
- Use `[Export]` with proper hints for editor properties
- Use `[GlobalClass]` for custom Resource types
- Validate node setup in `_Ready()` with asserts
- Use `GD.PushError()` for runtime issues, don't create silent fallbacks

## Key Commands

### Building & Testing C#
```powershell
# Build C# code (must pass before commits)
dotnet restore
dotnet build -warnaserror

# Run C# tests (including engine-aware tests via GdUnit4)
dotnet test
```

### Running Godot
```bash
# Run Godot (resolves executable via GODOT4_MONO_EXE env var or standard paths)
pwsh ./tools/godot.ps1

# Run headless (for scripts/linting)
pwsh ./tools/godot.ps1 --headless --script res://path/to/script.gd
```

### Testing (gdUnit4)
```bash
# Run all tests (60s timeout)
pwsh ./tools/test.ps1

# Run tests in specific directory
pwsh ./tools/test.ps1 -Test "res://test/unit/"

# Continue running all tests even after failures
pwsh ./tools/test.ps1 -Continue
```

Test exit codes: 0=pass, 1=failures, 124=timeout.

### Validation & Linting
```bash
# Full project lint (UIDs + scene warnings)
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd

# Lint specific scenes
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd -- --scene res://path/to/scene.tscn

# Lint all shaders
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_shaders.gd

# Lint single shader (use res:// path)
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_shaders.gd -- res://path/to/shader.gdshader

# GDScript linting (gdlint is on PATH)
gdlint path/to/file.gd
```

#### lint_project.gd Options
- `--scene res://path.tscn` - Lint specific scene(s), can be repeated
- `--all` - Lint all scenes (default behavior)
- `--json` - Output results as JSON for machine parsing
- `--fail-on-warn` - Treat warnings as failures (non-zero exit code)
- `--uids-only` - Skip scene warnings, only validate UIDs
- `--warnings-only` - Skip UID validation, only check scene warnings

**Always lint after changes:**
1. `gdlint` for modified GDScript files
2. `lint_project.gd` for scene/UID validation
3. `lint_shaders.gd` for modified shaders
4. Use short timeouts (20s max) when running Godot commands

### Setup
```bash
# Configure default input actions
pwsh ./tools/godot.ps1 --headless --script res://tools/setup_input_actions_cli.gd
```

To add new input actions, edit `tools/setup_input_actions_cli.gd` and add entries to the `actions` dictionary, then re-run the script. Example:
```gdscript
var actions := {
    "my_action": [KEY_F],           # Single key
    "other_action": [KEY_G, KEY_H], # Multiple keys (alternatives)
}
```

## Project Structure

```
res://
  scripts/         # C# gameplay code
  actors/          # Player and NPCs (scenes)
  levels/          # Level scenes and scripts
  ui/              # HUD and menus
  util/            # Camera rigs, markers, utilities
  game/            # AutoLoads and global state
  data/            # Resource definitions (items, verbs)
  test/            # gdUnit4 test suites
  tools/           # Linting and setup scripts (GDScript, not shipped)
  addons/          # Third-party addons (gdUnit4)
```

## GDScript Conventions (Editor Tools Only)

GDScript is reserved for editor tooling and tiny glue scripts. For gameplay logic, use C#.

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
1. `dotnet build -warnaserror` - C# must compile without warnings
2. `dotnet test` - All C# tests must pass
3. `pwsh ./tools/test.ps1` - All gdUnit4 tests must pass (GDScript + C# via Godot runtime)
4. `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd` (UIDs + scene warnings)
5. If GDScript modified: `gdlint path/to/file.gd` for style, plus Godot's `--check-only` for semantic analysis
6. Always check scenes for errors after editing

## Important Notes

- **Physics Layers**: Interactables use layer 2, ground/navigation uses layer 1
- **Platform**: Windows (D3D12 rendering)
- **Godot Version**: Requires Godot 4.6+ Mono (set via `GODOT_VERSION` env var)
- **File Paths**: Use complete absolute Windows paths with drive letters and backslashes for all file operations
- **Commit frequently** to git

## AutoLoads

AutoLoads go in `game/` and are registered in `project.godot`. To add a new AutoLoad:

1. Create the script in `game/`, e.g., `game/game_state.gd`
2. Add to `project.godot` under `[autoload]`:
   ```ini
   [autoload]
   GameState="*res://game/game_state.gd"
   ```
   The `*` prefix means it's a singleton (most common).

## Testing Infrastructure

### C# Testing (Primary) - GdUnit4

**Framework:** GdUnit4 (master branch for Godot 4.6 support)
**Runner:** `pwsh ./tools/test.ps1` (GDScript + C# tests) or `dotnet test` (C# only)

#### Core Guidelines
1. **Framework:** STRICTLY use `GdUnit4`. Do not use `NUnit` or `MSTest` directly.
2. **Imports:** Always include `using GdUnit4;` and `using static GdUnit4.Assertions;`.
3. **Attributes:** Use `[TestSuite]` for classes and `[TestCase]` for methods.
4. **Assertions:** Use GdUnit fluid assertions (e.g., `AssertBool(result).IsTrue()`, `AssertObject(node).IsNotNull()`).
5. **Mocking:** Use GdUnit's built-in `Mock<T>()`. Do not use `Moq` as it fails with Godot objects.

#### Test Categories
* **Logic Tests (POCO):** Test pure C# classes. Fast execution. DO NOT inherit from `Node`.
* **Scene Tests (Integration):** Test Nodes, Scenes, and Physics.
    * **MUST** add `[RequireGodotRuntime]` attribute to the method.
    * **MUST** use `ISceneRunner` to load/instantiate scenes.
    * **MUST** use `await` for frame processing (e.g., `await _runner.AwaitIdleFrame()`).

#### Anti-Patterns to Avoid (Cause Hangs)

**NEVER do this** - causes infinite hangs:
```csharp
// BAD: ToSignal("ready") hangs because ready fires synchronously during AddChild
root.AddChild(_panel);
await _panel.ToSignal(_panel, "ready");  // HANGS FOREVER
```

**Do this instead**:
```csharp
// GOOD: Wait for next frame using process_frame signal
root.AddChild(_panel);
await GetTree().ToSignal(GetTree(), "process_frame");
```

**Other patterns to avoid:**
- `while(true)` loops without break conditions
- Frame loops with >200 iterations (slow tests)
- Async tests without `[RequireGodotRuntime]` attribute

#### Code Patterns

**Pattern 1: Pure Logic Test (No Engine)**
```csharp
[TestSuite]
public class InventoryTests
{
    [TestCase]
    public void AddItem_IncreasesCount()
    {
        // Arrange
        var inv = new Inventory();
        // Act
        inv.Add("Sword", 1);
        // Assert
        AssertInt(inv.Count).IsEqual(1);
        AssertObject(inv.LastItem).IsNotNull();
    }
}
```

**Pattern 2: Scene/Node Test (With Engine)**
```csharp
[TestSuite]
public class PlayerTests
{
    private ISceneRunner _runner;

    [Before]
    public void Setup() => _runner = ISceneRunner.Load("res://Scenes/Player.tscn");

    [TestCase]
    [RequireGodotRuntime] // CRITICAL: Required for Node access
    public async Task TakeDamage_ReducesHealth()
    {
        var player = _runner.GetProperty<Player>("Player");

        await _runner.AwaitIdleFrame(); // Wait for ready
        player.TakeDamage(10);

        AssertInt(player.Health).IsEqual(90);
    }
}
```

### GDScript Testing (Editor Tools Only)
gdUnit4 supports GDScript tests for editor tooling:
- Test files go in `res://test/unit/` and `res://test/integration/`
- Test files must be named `test_*.gd` and extend `GdUnitTestSuite`
- Test methods must start with `test_`
- Use `auto_free()` for automatic cleanup of test objects

Example GDScript test (for editor tools):
```gdscript
extends GdUnitTestSuite

func test_example_passes() -> void:
    assert_bool(true).is_true()

func test_numeric_equality() -> void:
    var expected := 42
    var actual := 40 + 2
    assert_int(actual).is_equal(expected)

func test_with_auto_cleanup() -> void:
    var node := auto_free(Node2D.new())
    assert_object(node).is_not_null()
```

#### GDScript Assertion Reference
| Function | Example |
|----------|---------|
| `assert_bool(v)` | `.is_true()`, `.is_false()` |
| `assert_int(v)` | `.is_equal(n)`, `.is_greater(n)`, `.is_between(a, b)` |
| `assert_float(v)` | `.is_equal_approx(n, tolerance)` |
| `assert_str(v)` | `.is_equal(s)`, `.contains(s)`, `.starts_with(s)` |
| `assert_array(v)` | `.has_size(n)`, `.contains([items])`, `.is_empty()` |
| `assert_object(v)` | `.is_null()`, `.is_not_null()`, `.is_instanceof(Type)` |
| `assert_signal(obj)` | `await assert_signal(obj).is_emitted("signal_name")` |
