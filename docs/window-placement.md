# Window Placement: Monitor Selection & Positioning

## Problem

Godot's `project.godot` setting `window/size/initial_screen` is unreliable — the game window often opens on the primary monitor regardless of the configured screen index.

## Solution

Force window placement via `DisplayServer` API in `_Ready()`. This runs after the engine creates the window and reliably moves it to the correct monitor.

### Code Pattern

```csharp
public override void _Ready()
{
    int targetScreen = 0; // 0-indexed: 0 = primary, 1 = second, 2 = third
    if (targetScreen < DisplayServer.GetScreenCount())
    {
        // Move window to the target monitor
        DisplayServer.WindowSetCurrentScreen(targetScreen);

        // Center within usable area (excludes menu bar and dock on macOS)
        var usable = DisplayServer.ScreenGetUsableRect(targetScreen);
        var winSize = DisplayServer.WindowGetSize();
        var pos = usable.Position + (usable.Size - winSize) / 2;
        DisplayServer.WindowSetPosition(pos);
    }
}
```

### Why `ScreenGetUsableRect`

`ScreenGetUsableRect` returns the monitor area minus OS chrome (menu bar and dock on macOS). Using this instead of `ScreenGetSize` prevents the window from extending behind the dock.

### Key API Reference

| Method | Purpose |
|--------|---------|
| `DisplayServer.GetScreenCount()` | Number of connected monitors |
| `DisplayServer.WindowSetCurrentScreen(idx)` | Move window to monitor |
| `DisplayServer.ScreenGetUsableRect(idx)` | Monitor area excluding menu bar/dock |
| `DisplayServer.WindowGetSize()` | Current window dimensions |
| `DisplayServer.WindowSetPosition(pos)` | Set window top-left position |
| `DisplayServer.WindowSetFlag(flag, val)` | Set window flags (always-on-top, etc.) |

## project.godot Settings (Kept as Fallback)

These still live in `project.godot` under `[display]` but are not sufficient on their own:

```ini
[display]
window/size/viewport_width=1920
window/size/viewport_height=1080
```

The `DisplayServer` code path in `WindowSetup.cs` is the reliable approach for positioning.
