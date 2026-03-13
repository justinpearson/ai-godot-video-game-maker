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

## Distributing Your Game

Once your game is working, you can package it as a standalone macOS app that anyone can run — no Godot, no .NET, no dev tools required.

### Prerequisites (one-time)

Install the Godot .NET export templates. These must match your exact Godot version:

```bash
# Check your Godot version
./tools/godot.sh --version   # e.g. 4.6.stable.mono.official.89cea1439

# Download the .NET export templates from GitHub (requires gh CLI)
gh release download 4.6-stable --repo godotengine/godot-builds \
  --pattern "Godot_v4.6-stable_mono_export_templates.tpz" --dir /tmp/

# Extract and install
cd /tmp && unzip -o Godot_v4.6-stable_mono_export_templates.tpz -d godot_templates_tmp
mkdir -p ~/Library/Application\ Support/Godot/export_templates/4.6.stable.mono
cp /tmp/godot_templates_tmp/templates/* \
  "$HOME/Library/Application Support/Godot/export_templates/4.6.stable.mono/"
```

> **Note:** Replace `4.6-stable` and `4.6.stable.mono` with your actual version if different.

### Enable texture compression

Add this to `project.godot` under the `[rendering]` section (required for arm64 export):

```ini
[rendering]
textures/vram_compression/import_etc2_astc=true
```

### Create an export preset

Create `export_presets.cfg` in the project root:

```ini
[preset.0]

name="macOS"
platform="macOS"
runnable=true
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter=""
exclude_filter="addons/gdUnit4/*, test/*, tools/*, TO_SORT/*, *.disabled, *.md"
export_path="build/MyGame.app"
encrypt_pck=false
encrypt_directory=false
script_export_mode=2

[preset.0.options]

application/display_name="My Game"
application/bundle_identifier="com.example.mygame"
application/app_category="Games"
application/short_version="1.0.0"
application/version="1.0.0"
application/copyright="2026"
application/min_macos_version="10.12"
display/high_res=true
codesign/codesign=0
notarization/notarization=0
dotnet/export_mode=1
```

Key settings:
- **`exclude_filter`** — keeps test frameworks, dev tools, and working files out of the shipped app.
- **`dotnet/export_mode=1`** — self-contained build. Bundles the .NET runtime so players don't need to install anything.
- **`codesign/codesign=0`** — no code signing. Players will need to right-click > Open the first time (Gatekeeper bypass). Set to `1` and provide an identity if you have an Apple Developer account ($99/yr).

### Export the app

```bash
mkdir -p build
./tools/godot.sh --headless --export-release "macOS" build/MyGame.app
```

### Create a DMG for sharing

```bash
hdiutil create -volname "My Game" -srcfolder build/MyGame.app \
  -ov build/MyGame.dmg -format UDZO
```

This compresses the ~450MB `.app` down to ~185MB. Recipients double-click the DMG and drag the app to run it.

### Other distribution options

| Platform | Notes |
|---|---|
| **itch.io** | Free, no approval process. Upload the DMG or zip. Great for sharing indie games. Create an account at [itch.io](https://itch.io) and use their web uploader or [butler CLI](https://itch.io/docs/butler/). |
| **Steam** | $100 one-time fee per game. Wider audience but requires Steamworks integration. |
| **Direct sharing** | Zip the `.app` or share the `.dmg` via any file-sharing service. |

> **Gatekeeper note:** Unsigned apps trigger macOS Gatekeeper. Tell your players: *right-click the app > Open > Open* on first launch. To eliminate this, sign and notarize with an Apple Developer account.

---

## What AI Can Do vs. What Humans Must Do

One of the goals of this project is to figure out how much of game development Claude Code can handle autonomously. Here's what we've found:

### Claude Code can do (autonomously, no human intervention)

- **Install prerequisites** — `brew install dotnet@8 coreutils`, download Godot, configure PATH
- **Write all gameplay code** — C# scripts, scenes, resources, from a natural-language description
- **Generate sound effects** — Python scripts that synthesize WAV files with `struct` and `wave`
- **Build, test, and lint** — runs the full `dotnet build` / `dotnet test` / lint pipeline and fixes errors
- **Launch the game and take screenshots** — uses DevTools to verify the game looks correct
- **Import assets** — runs `--headless --import` for audio files, textures, etc.
- **Set up export templates** — downloads, extracts, and installs them
- **Export the game** — creates the `.app` bundle and DMG
- **Git operations** — branching, committing, pushing, creating PRs
- **Debug and iterate** — reads error output, adjusts code, re-runs until things work

### Humans must do

- **Provide the game idea** — Claude Code is creative but needs a starting direction
- **Playtest and give feedback** — "the jump feels floaty", "make enemies faster", "I like this, now add X"
- **Approve tool executions** — Claude Code asks permission before running commands (unless you skip permissions)
- **Supply external assets** — if you have specific audio files, images, or art you want to use
- **Apple Developer signing/notarization** — requires a paid account and human identity verification
- **Upload to distribution platforms** — itch.io, Steam, etc. require human accounts and agreements
- **Judge "fun"** — Claude Code can build mechanically correct games, but whether they're *fun* is a human call

### The sweet spot

The most productive workflow is: **human provides creative direction, Claude Code does all the building.** A single sentence like *"make the wrong answers pop like popcorn with a sound effect"* translates into multi-file code changes, asset management, and testing — all done autonomously. The human stays in the creative director seat.

---

## Making Multiple Games

This repo is a **framework for making games**, not a single game. Each game lives on its own branch.

### Starting a new game

```bash
# Start from the clean framework branch (master)
git checkout master
git checkout -b claude/my-new-game

# Launch Claude Code and describe your game
claude
```

> *"Make a 2D tower defense game where you place turrets to stop waves of bugs from reaching your garden."*

Claude Code will create all the game-specific files (scripts, scenes, assets) on this branch, leaving the framework intact.

### Branch strategy

| Branch | Purpose |
|---|---|
| `master` | Clean framework — setup tools, CLAUDE.md, README, project skeleton. No game-specific code. |
| `claude/<game-name>` | A specific game built on the framework |

Each game branch diverges from `master` and contains that game's scripts, scenes, and assets. Framework improvements (new tools, better CLAUDE.md instructions, README updates) go to `master` and can be merged into game branches.

### What lives on master vs. game branches

**Master (framework):**
- `project.godot` (base config, no game-specific scenes)
- `CLAUDE.md`, `README.md`
- `tools/`, `addons/`, `game/` (AutoLoads, DevTools)
- Test infrastructure

**Game branches (everything else):**
- `scripts/` — gameplay code
- `actors/`, `levels/`, `ui/` — game scenes
- `assets/` — sounds, images, music
- `export_presets.cfg` — game-specific export configuration
- Game-specific modifications to `project.godot` (main scene, input actions)

### Example games

| Branch | Game |
|---|---|
| [`claude/math-adventure-game`](https://github.com/justinpearson/ai-godot-video-game-maker/tree/claude/math-adventure-game) | Children's math addition game — drive a character to collect correct answers |

---

## Upstream / Related Projects

- **[tea-leaves](https://github.com/cleak/tea-leaves)** — The original repo (Windows, dog-keyboard input)
- **[Quasar Saz](https://github.com/cleak/quasar-saz)** — A finished game built on the original foundation, designed by a dog and developed by Claude Code ([watch the video](https://youtu.be/8BbPlPou3Bg))
- **[DogKeyboard](https://github.com/cleak/DogKeyboard)** — All the routing and miscellaneous tasks for reading input from Momo, dispensing treats, and playing chimes for her

## License

MIT. See `LICENSE`.
