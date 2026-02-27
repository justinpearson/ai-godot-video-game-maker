#!/usr/bin/env bash
#
# Forwards all args to the Godot 4.6.x Mono editor executable.
#
# Resolution order (first found wins):
# 1) Environment variable GODOT4_MONO_EXE
# 2) macOS: /Applications/Godot_mono.app/Contents/MacOS/Godot
# 3) macOS: ~/Applications/Godot_mono.app/Contents/MacOS/Godot
# 4) 'godot' on PATH (e.g. via Homebrew or symlink)
# 5) Linux: Standard paths
#
# Usage:
#   ./tools/godot.sh                           # Launch Godot editor
#   ./tools/godot.sh --headless --script ...   # Run headless script
#
# To source the resolve function without running Godot:
#   source ./tools/godot.sh --resolve-only
#   godot_path=$(resolve_godot_path)

set -euo pipefail

resolve_godot_path() {
    local version="${GODOT_VERSION:-4.6}"

    # 1) Environment variable
    if [[ -n "${GODOT4_MONO_EXE:-}" ]] && [[ -x "$GODOT4_MONO_EXE" ]]; then
        echo "$GODOT4_MONO_EXE"
        return 0
    fi

    # 2) macOS /Applications
    local mac_app="/Applications/Godot_mono.app/Contents/MacOS/Godot"
    if [[ -x "$mac_app" ]]; then
        echo "$mac_app"
        return 0
    fi

    # 3) macOS ~/Applications
    local mac_user_app="$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot"
    if [[ -x "$mac_user_app" ]]; then
        echo "$mac_user_app"
        return 0
    fi

    # 4) On PATH (works with Homebrew, symlinks, etc.)
    if command -v godot &>/dev/null; then
        command -v godot
        return 0
    fi

    # 5) Linux standard paths
    local linux_path="/usr/local/bin/godot"
    if [[ -x "$linux_path" ]]; then
        echo "$linux_path"
        return 0
    fi

    echo "ERROR: Godot executable not found." >&2
    echo "Set GODOT4_MONO_EXE, install Godot_mono.app to /Applications, or add 'godot' to PATH." >&2
    return 1
}

# If --resolve-only, just define the function and exit (for sourcing)
if [[ "${1:-}" == "--resolve-only" ]]; then
    return 0 2>/dev/null || exit 0
fi

# Otherwise, run Godot with provided args
godot_exe=$(resolve_godot_path)
exec "$godot_exe" "$@"
