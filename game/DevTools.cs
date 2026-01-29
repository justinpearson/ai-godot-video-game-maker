using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeaLeaves.Systems
{
    /// <summary>
    /// DevTools autoload - provides a file-based command interface for Claude Code.
    /// Add as autoload in Project Settings > Autoload with name "DevTools"
    ///
    /// Commands are sent via user://devtools_commands.json and results written to
    /// user://devtools_results.json. This enables agentic coding tools to:
    /// - Take screenshots for visual verification
    /// - Validate scenes at runtime
    /// - Inspect/modify node state
    /// - Monitor performance
    /// </summary>
    public partial class DevTools : Node
    {
        private const string CommandsPath = "user://devtools_commands.json";
        private const string ResultsPath = "user://devtools_results.json";
        private const string LogPath = "user://devtools_log.jsonl";

        private string _commandsAbsPath = "";
        private string _resultsAbsPath = "";
        private string _logAbsPath = "";
        private DateTime _lastCommandCheck;
        private bool _headlessMode;

        private Dictionary<string, Func<JsonElement, CommandResult>> _handlers = new();

        public static DevTools? Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
            _headlessMode = DisplayServer.GetName() == "headless";

            _commandsAbsPath = ProjectSettings.GlobalizePath(CommandsPath);
            _resultsAbsPath = ProjectSettings.GlobalizePath(ResultsPath);
            _logAbsPath = ProjectSettings.GlobalizePath(LogPath);

            InitializeHandlers();
            ClearStaleFiles();

            Log("system", "DevTools initialized", new { headless = _headlessMode, pid = OS.GetProcessId() });

            ProcessCommandLineArgs();
        }

        public override void _Process(double delta)
        {
            // Poll for commands every 100ms
            if ((DateTime.Now - _lastCommandCheck).TotalMilliseconds > 100)
            {
                _lastCommandCheck = DateTime.Now;
                CheckForCommands();
            }
        }

        private void InitializeHandlers()
        {
            _handlers = new Dictionary<string, Func<JsonElement, CommandResult>>
            {
                ["screenshot"] = CmdScreenshot,
                ["scene_tree"] = CmdSceneTree,
                ["validate_scene"] = CmdValidateScene,
                ["validate_all_scenes"] = CmdValidateAllScenes,
                ["get_state"] = CmdGetState,
                ["set_state"] = CmdSetState,
                ["run_method"] = CmdRunMethod,
                ["performance"] = CmdPerformance,
                ["quit"] = CmdQuit,
                ["ping"] = _ => new CommandResult(true, "pong", new { timestamp = Time.GetUnixTimeFromSystem() }),
            };
        }

        private void ProcessCommandLineArgs()
        {
            var args = OS.GetCmdlineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--devtools-screenshot":
                        GetTree().CreateTimer(0.5).Timeout += () =>
                        {
                            var result = CmdScreenshot(default);
                            WriteResult("screenshot", result);
                            GetTree().Quit();
                        };
                        break;

                    case "--devtools-validate":
                        GetTree().CreateTimer(0.1).Timeout += () =>
                        {
                            var result = CmdValidateAllScenes(default);
                            WriteResult("validate_all_scenes", result);
                            GetTree().Quit(result.Success ? 0 : 1);
                        };
                        break;
                }
            }
        }

        private void CheckForCommands()
        {
            if (!File.Exists(_commandsAbsPath)) return;

            try
            {
                var json = File.ReadAllText(_commandsAbsPath);
                File.Delete(_commandsAbsPath);

                var command = JsonSerializer.Deserialize<DevToolsCommand>(json);
                if (command == null || command.Action == null) return;

                Log("command", $"Received command: {command.Action}", command);

                CommandResult result;
                if (_handlers.TryGetValue(command.Action, out var handler))
                {
                    try
                    {
                        result = handler(command.Args);
                    }
                    catch (Exception ex)
                    {
                        result = new CommandResult(false, ex.Message, new { exception = ex.ToString() });
                    }
                }
                else
                {
                    result = new CommandResult(false, $"Unknown command: {command.Action}");
                }

                WriteResult(command.Action, result);
            }
            catch (Exception ex)
            {
                Log("error", "Failed to process command", new { error = ex.Message });
            }
        }

        private void WriteResult(string action, CommandResult result)
        {
            var response = new
            {
                action,
                success = result.Success,
                message = result.Message,
                data = result.Data,
                timestamp = Time.GetUnixTimeFromSystem()
            };

            File.WriteAllText(_resultsAbsPath, JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
            Log("result", $"Command {action} completed", new { success = result.Success });
        }

        private void ClearStaleFiles()
        {
            if (File.Exists(_commandsAbsPath)) File.Delete(_commandsAbsPath);
            if (File.Exists(_resultsAbsPath)) File.Delete(_resultsAbsPath);
        }

        // ==================== COMMAND HANDLERS ====================

        private CommandResult CmdScreenshot(JsonElement args)
        {
            var filename = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty("filename", out var fn)
                ? fn.GetString() ?? $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                : $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var dir = "user://screenshots";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));

            var path = $"{dir}/{filename}";
            var absPath = ProjectSettings.GlobalizePath(path);

            var image = GetViewport().GetTexture().GetImage();
            var error = image.SavePng(absPath);

            if (error != Error.Ok)
                return new CommandResult(false, $"Failed to save screenshot: {error}");

            return new CommandResult(true, "Screenshot captured", new {
                path = absPath,
                size = new { width = image.GetWidth(), height = image.GetHeight() }
            });
        }

        private CommandResult CmdSceneTree(JsonElement args)
        {
            var root = GetTree().CurrentScene;
            if (root == null)
                return new CommandResult(false, "No current scene");

            var depth = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty("depth", out var d) ? d.GetInt32() : 10;
            var tree = SerializeNode(root, depth);
            return new CommandResult(true, "Scene tree captured", tree);
        }

        private object SerializeNode(Node node, int maxDepth, int currentDepth = 0)
        {
            var data = new Dictionary<string, object?>
            {
                ["name"] = node.Name.ToString(),
                ["type"] = node.GetClass(),
                ["path"] = node.GetPath().ToString()
            };

            if (node is Node2D n2d)
            {
                data["position"] = new { x = n2d.Position.X, y = n2d.Position.Y };
                data["rotation"] = n2d.Rotation;
                data["visible"] = n2d.Visible;
            }
            else if (node is Node3D n3d)
            {
                data["position"] = new { x = n3d.Position.X, y = n3d.Position.Y, z = n3d.Position.Z };
                data["rotation"] = new { x = n3d.Rotation.X, y = n3d.Rotation.Y, z = n3d.Rotation.Z };
                data["visible"] = n3d.Visible;
            }
            else if (node is Control ctrl)
            {
                data["position"] = new { x = ctrl.Position.X, y = ctrl.Position.Y };
                data["size"] = new { x = ctrl.Size.X, y = ctrl.Size.Y };
                data["visible"] = ctrl.Visible;
            }

            if (currentDepth < maxDepth && node.GetChildCount() > 0)
            {
                var children = new List<object>();
                foreach (var child in node.GetChildren())
                {
                    children.Add(SerializeNode(child, maxDepth, currentDepth + 1));
                }
                data["children"] = children;
            }

            return data;
        }

        private CommandResult CmdValidateScene(JsonElement args)
        {
            if (args.ValueKind == JsonValueKind.Undefined || !args.TryGetProperty("path", out var pathEl))
                return new CommandResult(false, "Missing 'path' argument");

            var scenePath = pathEl.GetString();
            if (string.IsNullOrEmpty(scenePath))
                return new CommandResult(false, "Invalid 'path' argument");

            var issues = SceneValidator.ValidateScene(scenePath);

            return new CommandResult(
                issues.Count == 0,
                issues.Count == 0 ? "Scene valid" : $"Found {issues.Count} issues",
                new { scene = scenePath, issues }
            );
        }

        private CommandResult CmdValidateAllScenes(JsonElement args)
        {
            var allIssues = new Dictionary<string, List<ValidationIssue>>();
            var sceneFiles = FindAllScenes("res://");

            foreach (var scenePath in sceneFiles)
            {
                var issues = SceneValidator.ValidateScene(scenePath);
                if (issues.Count > 0)
                    allIssues[scenePath] = issues;
            }

            return new CommandResult(
                allIssues.Count == 0,
                allIssues.Count == 0 ? "All scenes valid" : $"Found issues in {allIssues.Count} scenes",
                new { total_scenes = sceneFiles.Count, scenes_with_issues = allIssues.Count, issues = allIssues }
            );
        }

        private List<string> FindAllScenes(string path)
        {
            var scenes = new List<string>();
            var dir = DirAccess.Open(path);
            if (dir == null) return scenes;

            dir.ListDirBegin();
            var fileName = dir.GetNext();

            while (!string.IsNullOrEmpty(fileName))
            {
                if (dir.CurrentIsDir() && !fileName.StartsWith(".") && fileName != "addons")
                {
                    scenes.AddRange(FindAllScenes($"{path}/{fileName}".Replace("//", "/")));
                }
                else if (fileName.EndsWith(".tscn") || fileName.EndsWith(".scn"))
                {
                    scenes.Add($"{path}/{fileName}".Replace("//", "/"));
                }
                fileName = dir.GetNext();
            }

            return scenes;
        }

        private CommandResult CmdGetState(JsonElement args)
        {
            var path = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty("node_path", out var p) ? p.GetString() : null;

            var currentScene = GetTree().CurrentScene;
            if (currentScene == null)
                return new CommandResult(false, "No current scene");

            Node target = string.IsNullOrEmpty(path)
                ? currentScene
                : currentScene.GetNodeOrNull(path);

            if (target == null)
                return new CommandResult(false, $"Node not found: {path}");

            var state = new Dictionary<string, object?>
            {
                ["node_class"] = target.GetClass(),
                ["node_path"] = target.GetPath().ToString()
            };

            foreach (var prop in target.GetPropertyList())
            {
                var name = prop["name"].AsString();
                var usage = (PropertyUsageFlags)prop["usage"].AsInt32();

                if ((usage & PropertyUsageFlags.ScriptVariable) != 0 ||
                    (usage & PropertyUsageFlags.Storage) != 0)
                {
                    var value = target.Get(name);
                    state[name] = SerializeVariant(value);
                }
            }

            return new CommandResult(true, "State retrieved", state);
        }

        private CommandResult CmdSetState(JsonElement args)
        {
            if (args.ValueKind == JsonValueKind.Undefined || !args.TryGetProperty("node_path", out var pathEl))
                return new CommandResult(false, "Missing 'node_path' argument");
            if (!args.TryGetProperty("property", out var propEl))
                return new CommandResult(false, "Missing 'property' argument");
            if (!args.TryGetProperty("value", out var valueEl))
                return new CommandResult(false, "Missing 'value' argument");

            var nodePath = pathEl.GetString();
            var propName = propEl.GetString();
            if (string.IsNullOrEmpty(nodePath) || string.IsNullOrEmpty(propName))
                return new CommandResult(false, "Invalid arguments");

            var currentScene = GetTree().CurrentScene;
            if (currentScene == null)
                return new CommandResult(false, "No current scene");

            var target = currentScene.GetNodeOrNull(nodePath);
            if (target == null)
                return new CommandResult(false, $"Node not found: {nodePath}");

            target.Set(propName, JsonToVariant(valueEl));
            return new CommandResult(true, "State updated");
        }

        private CommandResult CmdRunMethod(JsonElement args)
        {
            if (args.ValueKind == JsonValueKind.Undefined || !args.TryGetProperty("node_path", out var pathEl))
                return new CommandResult(false, "Missing 'node_path' argument");
            if (!args.TryGetProperty("method", out var methodEl))
                return new CommandResult(false, "Missing 'method' argument");

            var nodePath = pathEl.GetString();
            var methodName = methodEl.GetString();
            if (string.IsNullOrEmpty(nodePath) || string.IsNullOrEmpty(methodName))
                return new CommandResult(false, "Invalid arguments");

            var currentScene = GetTree().CurrentScene;
            if (currentScene == null)
                return new CommandResult(false, "No current scene");

            var target = currentScene.GetNodeOrNull(nodePath);
            if (target == null)
                return new CommandResult(false, $"Node not found: {nodePath}");

            var methodArgs = new Godot.Collections.Array();
            if (args.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var arg in argsEl.EnumerateArray())
                    methodArgs.Add(JsonToVariant(arg));
            }

            var result = target.Callv(methodName, methodArgs);
            return new CommandResult(true, "Method called", new { result = SerializeVariant(result) });
        }

        private CommandResult CmdPerformance(JsonElement args)
        {
            var data = new Dictionary<string, object>
            {
                ["fps"] = Engine.GetFramesPerSecond(),
                ["frame_time_ms"] = 1000.0 / Math.Max(1, Engine.GetFramesPerSecond()),
                ["physics_fps"] = Engine.PhysicsTicksPerSecond,
                ["static_memory_mb"] = OS.GetStaticMemoryUsage() / (1024.0 * 1024.0),
                ["video_memory_mb"] = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0),
                ["draw_calls"] = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                ["objects"] = Performance.GetMonitor(Performance.Monitor.ObjectCount),
                ["nodes"] = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
                ["orphan_nodes"] = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount),
                ["physics_2d_active_objects"] = Performance.GetMonitor(Performance.Monitor.Physics2DActiveObjects),
                ["physics_3d_active_objects"] = Performance.GetMonitor(Performance.Monitor.Physics3DActiveObjects),
            };

            return new CommandResult(true, "Performance data captured", data);
        }

        private CommandResult CmdQuit(JsonElement args)
        {
            var exitCode = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty("exit_code", out var ec) ? ec.GetInt32() : 0;
            GetTree().Quit(exitCode);
            return new CommandResult(true, "Quitting");
        }

        // ==================== UTILITIES ====================

        private object? SerializeVariant(Variant value)
        {
            return value.VariantType switch
            {
                Variant.Type.Nil => null,
                Variant.Type.Bool => value.AsBool(),
                Variant.Type.Int => value.AsInt64(),
                Variant.Type.Float => value.AsDouble(),
                Variant.Type.String => value.AsString(),
                Variant.Type.Vector2 => new { x = value.AsVector2().X, y = value.AsVector2().Y },
                Variant.Type.Vector3 => new { x = value.AsVector3().X, y = value.AsVector3().Y, z = value.AsVector3().Z },
                Variant.Type.Color => new { r = value.AsColor().R, g = value.AsColor().G, b = value.AsColor().B, a = value.AsColor().A },
                _ => value.ToString()
            };
        }

        private Variant JsonToVariant(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
                JsonValueKind.String => el.GetString() ?? "",
                _ => el.ToString()
            };
        }

        // ==================== PUBLIC LOGGING API ====================

        /// <summary>
        /// Log a structured message to the DevTools log file.
        /// Use for debugging and tracing during development.
        /// </summary>
        public static void Log(string category, string message, object? data = null)
        {
            Instance?.WriteLog(category, message, data);
        }

        private void WriteLog(string category, string message, object? data)
        {
            var entry = new
            {
                timestamp = Time.GetUnixTimeFromSystem(),
                frame = Engine.GetProcessFrames(),
                category,
                message,
                data
            };

            var json = JsonSerializer.Serialize(entry);

            using var file = new StreamWriter(_logAbsPath, append: true);
            file.WriteLine(json);

            if (OS.IsDebugBuild())
                GD.Print($"[{category}] {message}");
        }
    }

    // ==================== DATA CLASSES ====================

    internal class DevToolsCommand
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    internal record CommandResult(bool Success, string Message, object? Data = null);
}
