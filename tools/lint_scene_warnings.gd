extends SceneTree

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    var scenes: PackedStringArray = []
    var json := false
    var fail_on_warn := false

    for i in args.size():
        match args[i]:
            "--scene":
                if i + 1 < args.size():
                    scenes.append(args[i + 1])
            "--all":
                scenes = _find_all_scenes("res://")
            "--json":
                json = true
            "--fail-on-warn":
                fail_on_warn = true

    if scenes.is_empty():
        scenes = _find_all_scenes("res://")

    var results := []
    var had_error := false
    var had_warn := false

    for p in scenes:
        var ps: PackedScene = load(p)
        if ps == null:
            had_error = true
            results.append({"scene": p, "error": "Failed to load (syntax error or bad path)."})
            continue

        # Static validation only, using SceneState (no instancing to avoid RID/UI side effects)
        var state := ps.get_state()
        var warnings := []
        if state != null:
            var node_count := state.get_node_count()
            # Build a lookup of all node paths in this scene for fast existence checks
            var path_set := {}
            for i in range(node_count):
                var np: NodePath = state.get_node_path(i, true)
                path_set[String(np)] = true

            for ni in range(node_count):
                var ntype := state.get_node_type(ni)
                if ntype == "MultiplayerSpawner":
                    var spawner_path: NodePath = state.get_node_path(ni, true)
                    var saw_spawn_prop := false
                    var prop_count := state.get_node_property_count(ni)
                    var spawn_rel := ""
                    for pi in range(prop_count):
                        var prop_name := state.get_node_property_name(ni, pi)
                        if String(prop_name) == "spawn_path":
                            saw_spawn_prop = true
                            var prop_value: Variant = state.get_node_property_value(ni, pi)
                            spawn_rel = String(prop_value)
                            if spawn_rel == "":
                                var msg_sp_empty := "SceneState: MultiplayerSpawner.spawn_path is empty (not set in .tscn)"
                                var warn_sp_empty := {"path": String(spawner_path), "messages": [msg_sp_empty]}
                                warnings.append(warn_sp_empty)
                    if not saw_spawn_prop:
                        var warn_missing := {
                            "path": String(spawner_path),
                            "messages": ["SceneState: MultiplayerSpawner.spawn_path missing in .tscn"]
                        }
                        warnings.append(warn_missing)
                    elif spawn_rel != "":
                        var resolved_abs := _resolve_relative_nodepath(String(spawner_path), spawn_rel)
                        if resolved_abs == "":
                            var m1 := "SceneState: spawn_path above root: %s" % spawn_rel
                            var warn_above := {"path": String(spawner_path), "messages": [m1]}
                            warnings.append(warn_above)
                        elif not _path_set_has_relaxed(path_set, resolved_abs):
                            var m2 := "SceneState: spawn_path unresolved: %s (-> %s)" % [spawn_rel, resolved_abs]
                            var warn_unres := {"path": String(spawner_path), "messages": [m2]}
                            warnings.append(warn_unres)

                # General rule: any exported NodePath-like property should be either empty by design or resolve inside the scene
                # We warn when it is empty or resolves to a non-existent node. This catches UIRoot.NetManagerPath and similar.
                var node_abs_path := String(state.get_node_path(ni, true))
                var prop_cnt := state.get_node_property_count(ni)
                for pidx in range(prop_cnt):
                    var p_name := String(state.get_node_property_name(ni, pidx))
                    var p_val: Variant = state.get_node_property_value(ni, pidx)
                    if _is_nodepath_like_property(p_name, p_val):
                        var p_str := String(p_val)
                        if p_str == "":
                            var msg_empty := "SceneState: '%s' NodePath empty" % p_name
                            var warn_empty := {"path": node_abs_path, "messages": [msg_empty]}
                            warnings.append(warn_empty)
                        else:
                            var resolved := _resolve_relative_nodepath(node_abs_path, p_str)
                            if resolved != "" and not _path_set_has_relaxed(path_set, resolved):
                                var m3 := "SceneState: '%s' NodePath unresolved: %s (-> %s)" % [p_name, p_str, resolved]
                                var warn_np := {"path": node_abs_path, "messages": [m3]}
                                warnings.append(warn_np)

        if warnings.size() > 0:
            had_warn = true

        results.append({"scene": p, "warnings": warnings})

    if json:
        print(JSON.stringify(results, "  "))
    else:
        for r in results:
            if "error" in r:
                printerr("%s: %s" % [r.scene, r.error])
            elif r.warnings.is_empty():
                print("%s: OK" % r.scene)
            else:
                for w in r.warnings:
                    print("%s | %s: %s" % [r.scene, w.path, ", ".join(w.messages)])

    var exit_code := 0
    if had_error or (fail_on_warn and had_warn):
        exit_code = 1
    quit(exit_code)

func _collect_warnings(_node: Node, _out: Array) -> void:
    pass

func _find_all_scenes(root_path: String) -> PackedStringArray:
    var out: PackedStringArray = []
    var d := DirAccess.open(root_path)
    if d == null:
        return out
    d.list_dir_begin()
    var name := d.get_next()
    while name != "":
        var full := d.get_current_dir() + "/" + name
        if d.current_is_dir():
            if not name.begins_with("."):
                out.append_array(_find_all_scenes(full))
        elif name.ends_with(".tscn") or name.ends_with(".scn"):
            out.append(full)
        name = d.get_next()
    d.list_dir_end()
    return out

# Remove UI/Viewport heavy subtrees to minimize RID leaks in headless runs
func _strip_heavy_subtrees(_n: Node) -> void:
    pass

func _strip_ui_before_add(_n: Node) -> void:
    pass

func _resolve_relative_nodepath(spawner_abs: String, rel: String) -> String:
    # Convert absolute spawner path (e.g., "Main/SpawnerRoot/PlayerSpawner") and a relative NodePath
    # (e.g., "../WorldRoot") into an absolute scene path string (e.g., "Main/WorldRoot").
    if rel.begins_with("/"):
        # Absolute path inside scene: strip leading slash if any root marker used
        return _normalize_against_root(spawner_abs, rel.trim_prefix("/"))
    var base_had_dot := spawner_abs.begins_with("./")
    var base_abs := spawner_abs
    if base_had_dot:
        base_abs = spawner_abs.substr(2) # remove "./"
    var base_parts := base_abs.split("/")
    # Start from the spawner node as base
    var rel_parts := rel.split("/")
    for part in rel_parts:
        if part == "." or part == "":
            continue
        elif part == "..":
            if base_parts.size() == 0:
                return "" # above root -> invalid
            base_parts.remove_at(base_parts.size() - 1)
        else:
            base_parts.append(part)
    var joined := "/".join(base_parts)
    return _normalize_against_root(spawner_abs, joined)

func _normalize_against_root(base_abs: String, abs_path: String) -> String:
    if abs_path == "":
        return ""
    var base_had_dot := base_abs.begins_with("./") or base_abs == "."
    if base_had_dot and not abs_path.begins_with("./") and not abs_path.contains("/"):
        return "./" + abs_path
    return abs_path

func _path_set_has_relaxed(path_set: Dictionary, path: String) -> bool:
    if path_set.has(path):
        return true
    # Treat "Node" and "./Node" as equivalent for scene-local children
    if path.begins_with("./"):
        var alt := path.substr(2)
        if path_set.has(alt):
            return true
    else:
        var alt2 := "./" + path
        if path_set.has(alt2):
            return true
    return false

func _is_nodepath_like_property(name: String, value: Variant) -> bool:
    # Heuristics: treat explicit NodePath values as NodePath-like, and also string properties
    # with conventional suffixes indicating NodePath usage (e.g., *_path, *Path).
    if typeof(value) == TYPE_NODE_PATH:
        return true
    if typeof(value) == TYPE_STRING:
        var lname := name.to_lower()
        if lname.ends_with("_path") or lname.ends_with("path"):
            return true
    return false

