#!/usr/bin/env python3
"""
devtools.py - CLI for interacting with running Godot instance via DevTools autoload.

Commands send JSON to user://devtools_commands.json and read results from
user://devtools_results.json. The DevTools autoload polls for commands.

Usage:
    python tools/devtools.py ping                     # Check if game is running
    python tools/devtools.py screenshot               # Capture screenshot
    python tools/devtools.py validate-all             # Validate all scenes
    python tools/devtools.py scene-tree               # Get node hierarchy
    python tools/devtools.py performance              # Get FPS, memory, etc.
    python tools/devtools.py get-state --node "/root/Game/Player"
    python tools/devtools.py set-state --node "/root/Game/Player" --property Health --value 100
    python tools/devtools.py quit

Environment:
    Set project path via --project or run from project root.
"""

import argparse
import json
import os
import sys
import time
from pathlib import Path
from typing import Optional


COMMANDS_FILE = "devtools_commands.json"
RESULTS_FILE = "devtools_results.json"
LOG_FILE = "devtools_log.jsonl"


def get_user_data_path(project_path: Path) -> Path:
    """Get the user:// directory for the Godot project."""
    project_file = project_path / "project.godot"
    if not project_file.exists():
        raise FileNotFoundError(f"No project.godot found in {project_path}")

    project_name = None
    with open(project_file, encoding="utf-8") as f:
        for line in f:
            if line.startswith("config/name="):
                project_name = line.split("=", 1)[1].strip().strip('"')
                break

    if not project_name:
        project_name = project_path.name

    # Platform-specific user data location
    if sys.platform == "win32":
        base = Path(os.environ.get("APPDATA", ""))
        return base / "Godot" / "app_userdata" / project_name
    elif sys.platform == "darwin":
        return Path.home() / "Library" / "Application Support" / "Godot" / "app_userdata" / project_name
    else:  # Linux
        return Path.home() / ".local" / "share" / "godot" / "app_userdata" / project_name


def send_command(project_path: Path, action: str, args: dict = None, timeout: float = 30.0) -> dict:
    """Send a command to the running Godot instance and wait for result."""
    user_data = get_user_data_path(project_path)
    user_data.mkdir(parents=True, exist_ok=True)

    commands_path = user_data / COMMANDS_FILE
    results_path = user_data / RESULTS_FILE

    # Clear any existing result
    if results_path.exists():
        results_path.unlink()

    # Write command
    command = {"action": action, "args": args or {}}
    commands_path.write_text(json.dumps(command), encoding="utf-8")

    # Wait for result
    start_time = time.time()
    while time.time() - start_time < timeout:
        if results_path.exists():
            try:
                result = json.loads(results_path.read_text(encoding="utf-8"))
                results_path.unlink()
                return result
            except json.JSONDecodeError:
                pass
        time.sleep(0.1)

    raise TimeoutError(f"No response from Godot after {timeout}s. Is the game running with DevTools?")


def cmd_screenshot(args, project_path: Path):
    """Take a screenshot of the running game."""
    result = send_command(project_path, "screenshot", {"filename": args.filename} if args.filename else {})
    if result["success"]:
        print(f"Screenshot saved: {result['data']['path']}")
        print(f"Size: {result['data']['size']['width']}x{result['data']['size']['height']}")
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_validate(args, project_path: Path):
    """Validate a specific scene."""
    if not args.scene:
        print("Error: --scene is required", file=sys.stderr)
        sys.exit(1)
    result = send_command(project_path, "validate_scene", {"path": args.scene})
    print_validation_result(result)


def cmd_validate_all(args, project_path: Path):
    """Validate all scenes in the project."""
    result = send_command(project_path, "validate_all_scenes", timeout=60.0)
    print_validation_result(result)


def print_validation_result(result: dict):
    """Pretty-print validation results."""
    if result["success"]:
        print("[OK] " + result["message"])
    else:
        print("[FAIL] " + result["message"])

    data = result.get("data", {})
    issues = data.get("issues", {})

    if isinstance(issues, dict):  # Multiple scenes
        for scene, scene_issues in issues.items():
            print(f"\n{scene}:")
            for issue in scene_issues:
                severity = {"error": "ERROR", "warning": "WARN", "info": "INFO"}.get(issue["severity"], "???")
                print(f"  [{severity}] {issue['code']}: {issue['message']}")
    elif isinstance(issues, list):  # Single scene
        for issue in issues:
            severity = {"error": "ERROR", "warning": "WARN", "info": "INFO"}.get(issue["severity"], "???")
            print(f"  [{severity}] {issue['code']}: {issue['message']}")

    if not result["success"]:
        sys.exit(1)


def cmd_scene_tree(args, project_path: Path):
    """Get the current scene tree."""
    result = send_command(project_path, "scene_tree", {"depth": args.depth})
    if result["success"]:
        print(json.dumps(result["data"], indent=2))
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_performance(args, project_path: Path):
    """Get performance metrics."""
    result = send_command(project_path, "performance")
    if result["success"]:
        data = result["data"]
        print(f"FPS:              {data['fps']:.1f}")
        print(f"Frame time:       {data['frame_time_ms']:.2f} ms")
        print(f"Physics FPS:      {int(data['physics_fps'])}")
        print(f"Draw calls:       {int(data['draw_calls'])}")
        print(f"Objects:          {int(data['objects'])}")
        print(f"Static memory:    {data['static_memory_mb']:.1f} MB")
        print(f"Video memory:     {data['video_memory_mb']:.1f} MB")
        print(f"Total nodes:      {int(data['nodes'])}")
        print(f"Orphan nodes:     {int(data['orphan_nodes'])}")
        print(f"Physics 2D objs:  {int(data['physics_2d_active_objects'])}")
        print(f"Physics 3D objs:  {int(data['physics_3d_active_objects'])}")
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_get_state(args, project_path: Path):
    """Get node state."""
    result = send_command(project_path, "get_state", {"node_path": args.node} if args.node else {})
    if result["success"]:
        print(json.dumps(result["data"], indent=2))
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_set_state(args, project_path: Path):
    """Set a node property."""
    value = args.value
    # Try to parse as JSON, otherwise use as string
    try:
        value = json.loads(args.value)
    except json.JSONDecodeError:
        # Try numeric conversion (handles negative numbers)
        try:
            value = int(args.value)
        except ValueError:
            try:
                value = float(args.value)
            except ValueError:
                pass  # Keep as string

    result = send_command(project_path, "set_state", {
        "node_path": args.node,
        "property": args.property,
        "value": value
    })
    if result["success"]:
        print("State updated")
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_run_method(args, project_path: Path):
    """Call a method on a node."""
    method_args = []
    if args.args:
        try:
            method_args = json.loads(args.args)
            if not isinstance(method_args, list):
                print("Error: --args must be a JSON array, e.g., '[25, \"name\"]'", file=sys.stderr)
                sys.exit(1)
        except json.JSONDecodeError as e:
            print(f"Error: Invalid JSON in --args: {e}", file=sys.stderr)
            sys.exit(1)

    result = send_command(project_path, "run_method", {
        "node_path": args.node,
        "method": args.method,
        "args": method_args
    })
    if result["success"]:
        print(f"Result: {result['data'].get('result')}")
    else:
        print(f"Failed: {result['message']}", file=sys.stderr)
        sys.exit(1)


def cmd_logs(args, project_path: Path):
    """View DevTools logs."""
    user_data = get_user_data_path(project_path)
    log_path = user_data / LOG_FILE

    if not log_path.exists():
        print("No logs found")
        return

    lines = log_path.read_text(encoding="utf-8").strip().split("\n")

    if args.category:
        lines = [l for l in lines if f'"category":"{args.category}"' in l or f'"category": "{args.category}"' in l]

    if args.tail:
        lines = lines[-args.tail:]

    for line in lines:
        try:
            entry = json.loads(line)
            ts = time.strftime("%H:%M:%S", time.localtime(entry["timestamp"]))
            cat = entry["category"]
            msg = entry["message"]
            print(f"[{ts}] [{cat}] {msg}")
        except json.JSONDecodeError:
            print(line)


def cmd_ping(args, project_path: Path):
    """Check if Godot DevTools is responding."""
    try:
        result = send_command(project_path, "ping", timeout=5.0)
        if result["success"]:
            print(f"DevTools is running (timestamp: {result['data']['timestamp']:.0f})")
        else:
            print("DevTools responded but with error")
            sys.exit(1)
    except TimeoutError:
        print("No response - is the game running with DevTools autoload?")
        sys.exit(1)


def cmd_quit(args, project_path: Path):
    """Quit the running Godot instance."""
    try:
        send_command(project_path, "quit", {"exit_code": args.exit_code or 0}, timeout=5.0)
        print("Quit command sent")
    except TimeoutError:
        print("Quit command sent (no response expected)")


def main():
    parser = argparse.ArgumentParser(description="DevTools CLI - interact with running Godot instance")
    parser.add_argument("--project", "-p", help="Path to Godot project", default=".")

    subparsers = parser.add_subparsers(dest="command", required=True)

    # ping
    p = subparsers.add_parser("ping", help="Check if DevTools is running")
    p.set_defaults(func=cmd_ping)

    # screenshot
    p = subparsers.add_parser("screenshot", help="Take a screenshot")
    p.add_argument("--filename", "-f", help="Output filename")
    p.set_defaults(func=cmd_screenshot)

    # validate
    p = subparsers.add_parser("validate", help="Validate a scene")
    p.add_argument("--scene", "-s", help="Scene path (res://...)")
    p.set_defaults(func=cmd_validate)

    # validate-all
    p = subparsers.add_parser("validate-all", help="Validate all scenes")
    p.set_defaults(func=cmd_validate_all)

    # scene-tree
    p = subparsers.add_parser("scene-tree", help="Get scene tree")
    p.add_argument("--depth", "-d", type=int, default=10, help="Max depth")
    p.set_defaults(func=cmd_scene_tree)

    # performance
    p = subparsers.add_parser("performance", help="Get performance metrics")
    p.set_defaults(func=cmd_performance)

    # get-state
    p = subparsers.add_parser("get-state", help="Get node state")
    p.add_argument("--node", "-n", help="Node path")
    p.set_defaults(func=cmd_get_state)

    # set-state
    p = subparsers.add_parser("set-state", help="Set node property")
    p.add_argument("--node", "-n", required=True, help="Node path")
    p.add_argument("--property", required=True, help="Property name")
    p.add_argument("--value", required=True, help="Property value")
    p.set_defaults(func=cmd_set_state)

    # run-method
    p = subparsers.add_parser("run-method", help="Call a method")
    p.add_argument("--node", "-n", required=True, help="Node path")
    p.add_argument("--method", "-m", required=True, help="Method name")
    p.add_argument("--args", "-a", help="Method arguments as JSON array")
    p.set_defaults(func=cmd_run_method)

    # logs
    p = subparsers.add_parser("logs", help="View logs")
    p.add_argument("--tail", "-t", type=int, help="Show last N entries")
    p.add_argument("--category", "-c", help="Filter by category")
    p.set_defaults(func=cmd_logs)

    # quit
    p = subparsers.add_parser("quit", help="Quit Godot")
    p.add_argument("--exit-code", type=int, help="Exit code")
    p.set_defaults(func=cmd_quit)

    args = parser.parse_args()

    project_path = Path(args.project).resolve()
    args.func(args, project_path)


if __name__ == "__main__":
    main()
