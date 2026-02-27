# AI Godot Video Game Maker

Describe the video game you want. Claude Code builds it in Godot.

You provide a game description, Claude Code interprets it and generates C# gameplay code, scenes, resources, and sounds inside this Godot project. Then you iterate — refine mechanics, add features, fix bugs — all through conversation.

Forked from [tea-leaves](https://github.com/cleak/tea-leaves), the engineering substrate behind [Quasar Saz](https://github.com/cleak/quasar-saz) (a game designed by a dog and built by Claude Code).

---

## One-Time Setup

Complete these steps once on a fresh clone. Every step must succeed before moving on.

### 1. Install prerequisites

You need three things installed on your Mac:

| Prerequisite | How to get it |
|---|---|
| **Godot 4.6+ Mono** | Download from [godotengine.org](https://godotengine.org/download/macos/). Install to `/Applications/Godot_mono.app`. |
| **.NET 8.0 SDK** | Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0), or `brew install dotnet@8`. |
| **Python 3** | Comes with macOS. Verify with `python3 --version`. |

Verify they're accessible:

```bash
dotnet --version          # Should print 8.x.x
./tools/godot.sh --version   # Should print Godot Engine v4.6.x.stable.mono
python3 --version         # Should print Python 3.x.x
```

> **Tip:** If `./tools/godot.sh` can't find Godot, either move it to `/Applications/Godot_mono.app` or set the environment variable:
> ```bash
> export GODOT4_MONO_EXE="/path/to/your/Godot"
> ```

### 2. Clone the repo

```bash
git clone https://github.com/justinpearson/ai-godot-video-game-maker.git
cd ai-godot-video-game-maker
```

### 3. Restore NuGet packages

```bash
dotnet restore
```

This downloads the C# dependencies (Godot SDK bindings, gdUnit4 test framework, etc.).

### 4. Build the C# code

```bash
dotnet build -warnaserror
```

This compiles all C# gameplay code. It must exit with no errors and no warnings.

### 5. Initialize input actions

```bash
./tools/godot.sh --headless --script res://tools/setup_input_actions_cli.gd
```

This registers the default input actions (WASD movement, jump, crouch, sprint, etc.) into `project.godot`.

### 6. Run the tests

```bash
dotnet test
./tools/test.sh
```

- `dotnet test` runs pure C# unit tests.
- `./tools/test.sh` runs gdUnit4 tests inside the Godot runtime (GDScript + C# integration tests).

Both must pass. If `./tools/test.sh` times out (exit code 124), retry with a longer timeout: `./tools/test.sh --timeout 120`.

### 7. Verify the project lint passes

```bash
./tools/godot.sh --headless --script res://tools/lint_project.gd
```

This checks UID consistency and scene integrity.

**Setup is done.** If every step above succeeded, you're ready to make games.

---

## Making a Game

### 1. Start Claude Code

Open a terminal in the repo directory and launch Claude Code:

```bash
claude
```

### 2. Describe your game

Type a description of the video game you want. Be as specific or as vague as you like — Claude Code will fill in the gaps creatively. For example:

> *"Make a 2D platformer where a cat collects fish while avoiding seagulls. The cat can double-jump and wall-slide. Pixel art style, beach theme, with upbeat music."*

Or something simpler:

> *"Make a simple top-down space shooter with waves of enemies."*

Claude Code will design the game, write C# code, create scenes, download or generate assets, and build everything inside this project.

### 3. Watch it build

Claude Code will automatically:
- Write C# gameplay code in `scripts/`, `actors/`, `levels/`, `ui/`
- Create Godot scenes (`.tscn`) and resources (`.tres`)
- Build the project (`dotnet build -warnaserror`)
- Run tests (`dotnet test`, `./tools/test.sh`)
- Lint the project for correctness
- Launch the game and take screenshots to verify it looks right

### 4. Play your game

Once Claude Code finishes, launch the game:

```bash
./tools/godot.sh
```

Or Claude Code may launch it for you during the build process.

### 5. Iterate

Each new message you send is treated as an update to the existing game. Ask for anything:

- *"Make the enemies faster and add a boss at wave 5"*
- *"The jump feels too floaty, tighten it up"*
- *"Add a main menu with a start button and high score display"*
- *"There's a bug where the player falls through the floor"*

Claude Code will modify the existing game — it won't start from scratch unless you explicitly ask.

---

## Technical Details

### Stack

| Component | Technology |
|---|---|
| Engine | Godot 4.6 Mono |
| Gameplay code | C# (net8.0) |
| Tooling | GDScript + Bash + Python |
| Physics | Jolt |
| Renderer | Forward Plus (Vulkan) |
| Tests | `dotnet test` + gdUnit4 via `./tools/test.sh` |

### Project structure

```
res://
  scripts/         # C# gameplay code
  actors/          # Player and NPC scenes
  levels/          # Level scenes
  ui/              # HUD and menus
  util/            # Camera rigs, markers, utilities
  game/            # AutoLoads (WindowSetup, DevTools, EventBus)
  data/            # Resource definitions
  test/            # gdUnit4 test suites
  tools/           # Build/lint/setup scripts (not shipped)
  addons/          # Third-party addons (gdUnit4)
```

### AutoLoads

Registered in `project.godot`:
- **WindowSetup** (`game/WindowSetup.cs`) — Centers the window on the primary monitor at startup.
- **DevTools** (`game/DevTools.cs`) — Runtime command server for automated verification (screenshots, input simulation, validation).

### Key commands reference

```bash
# Build
dotnet restore           # Restore NuGet packages
dotnet build -warnaserror  # Compile C# (must pass clean)

# Test
dotnet test              # C# unit tests
./tools/test.sh          # gdUnit4 runtime tests

# Lint
./tools/godot.sh --headless --script res://tools/lint_project.gd   # UIDs + scenes
./tools/godot.sh --headless --script res://tools/lint_shaders.gd   # Shaders
./tools/lint_tests.sh     # Test file conventions
gdlint path/to/file.gd   # GDScript style

# Run
./tools/godot.sh          # Launch the game

# DevTools (game must be running)
python3 tools/devtools.py ping
python3 tools/devtools.py screenshot
python3 tools/devtools.py validate-all
python3 tools/devtools.py input tap jump
python3 tools/devtools.py quit
```

## Upstream / Related Projects

- **[tea-leaves](https://github.com/cleak/tea-leaves)** — The original repo (Windows, dog-keyboard input)
- **[Quasar Saz](https://github.com/cleak/quasar-saz)** — A finished game built on the original foundation, designed by a dog and developed by Claude Code ([watch the video](https://youtu.be/8BbPlPou3Bg))
- **[DogKeyboard](https://github.com/cleak/DogKeyboard)** — All the routing and miscellaneous tasks for reading input from Momo, dispensing treats, and playing chimes for her

## License

MIT. See `LICENSE`.
