#!/usr/bin/env bash
#
# Run gdUnit4 tests with timeout protection (macOS/Linux compatible)
#
# Usage:
#   ./tools/test.sh                                  # Run all tests
#   ./tools/test.sh --test "res://test/unit/"         # Run tests in specific directory
#   ./tools/test.sh --timeout 120                     # Custom timeout (default 60s)
#   ./tools/test.sh --continue                        # Don't stop on first failure
#
# Exit codes:
#   0   = All tests passed
#   1   = One or more test failures (mapped from gdUnit4's 100)
#   124 = Timeout (process killed)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Defaults
TIMEOUT_SECONDS=60
TEST_PATH="res://test"
CONTINUE_FLAG=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --timeout|-t)
            TIMEOUT_SECONDS="$2"
            shift 2
            ;;
        --test)
            TEST_PATH="$2"
            shift 2
            ;;
        --continue|-c)
            CONTINUE_FLAG="-c"
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [--timeout N] [--test PATH] [--continue]"
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            exit 1
            ;;
    esac
done

echo "Running gdUnit4 tests (timeout: ${TIMEOUT_SECONDS}s)"

# Resolve Godot path
source "$SCRIPT_DIR/godot.sh" --resolve-only
GODOT_EXE=$(resolve_godot_path)

# Build argument list for gdUnit4 CLI
GDUNIT_ARGS=(
    --headless
    -s "res://addons/gdUnit4/bin/GdUnitCmdTool.gd"
    --ignoreHeadlessMode
    -a "$TEST_PATH"
)

if [[ -n "$CONTINUE_FLAG" ]]; then
    GDUNIT_ARGS+=(-c)
    echo "  Mode: Continue on failure"
fi

echo "  Test path: $TEST_PATH"

# Create temp files for output capture
STDOUT_FILE=$(mktemp)
STDERR_FILE=$(mktemp)
cleanup() {
    rm -f "$STDOUT_FILE" "$STDERR_FILE"
}
trap cleanup EXIT

# Run with timeout
EXIT_CODE=0
if command -v gtimeout &>/dev/null; then
    # macOS with coreutils installed via Homebrew
    TIMEOUT_CMD="gtimeout"
elif command -v timeout &>/dev/null; then
    # Linux or macOS with GNU coreutils
    TIMEOUT_CMD="timeout"
else
    # Fallback: use background process with manual timeout
    TIMEOUT_CMD=""
fi

if [[ -n "$TIMEOUT_CMD" ]]; then
    "$TIMEOUT_CMD" "$TIMEOUT_SECONDS" "$GODOT_EXE" "${GDUNIT_ARGS[@]}" \
        >"$STDOUT_FILE" 2>"$STDERR_FILE" || EXIT_CODE=$?
else
    # Manual timeout fallback
    "$GODOT_EXE" "${GDUNIT_ARGS[@]}" >"$STDOUT_FILE" 2>"$STDERR_FILE" &
    PID=$!
    ELAPSED=0
    while kill -0 "$PID" 2>/dev/null; do
        if [[ $ELAPSED -ge $TIMEOUT_SECONDS ]]; then
            echo "TIMEOUT: Tests exceeded ${TIMEOUT_SECONDS}s limit, killing process..." >&2
            kill -TERM "$PID" 2>/dev/null || true
            sleep 1
            kill -KILL "$PID" 2>/dev/null || true
            wait "$PID" 2>/dev/null || true
            cat "$STDOUT_FILE"
            cat "$STDERR_FILE" >&2
            exit 124
        fi
        sleep 1
        ELAPSED=$((ELAPSED + 1))
    done
    wait "$PID" || EXIT_CODE=$?
fi

# Show output
cat "$STDOUT_FILE"
if [[ -s "$STDERR_FILE" ]]; then
    cat "$STDERR_FILE" >&2
fi

# Handle timeout from 'timeout' command (exit code 124)
if [[ $EXIT_CODE -eq 124 ]]; then
    echo "TIMEOUT: Tests exceeded ${TIMEOUT_SECONDS}s limit" >&2
    exit 124
fi

# gdUnit4 exit codes: 0=pass, 100=failure, 101=warnings
# Map to standard: 0=pass, 1=failure
if [[ $EXIT_CODE -eq 100 ]]; then
    exit 1
elif [[ $EXIT_CODE -eq 101 ]]; then
    echo "Tests passed with warnings"
    exit 0
fi

exit $EXIT_CODE
