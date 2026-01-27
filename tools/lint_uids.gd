@tool
extends EditorScript

# Renamed from tscn_lint.gd
# Usage: godot --headless --path . -s res://tools/lint_uids.gd

func _run() -> void:
    var ok := true
    for path in _scan(["tscn", "tres"]):
        if not _check_one(path):
            ok = false
    if not ok:
        push_error("TSCN/TRES UID mismatches detected")
        get_editor_interface().exit_editor() # exit code 1 in headless
        return
    print("TSCN/TRES UID check passed")

func _scan(exts: Array[String]) -> Array[String]:
    var files: Array[String] = []
    var dir := DirAccess.open("res://")
    if dir:
        files += _scan_dir(dir, exts)
    return files

func _scan_dir(dir: DirAccess, exts: Array[String]) -> Array[String]:
    var out: Array[String] = []
    dir.list_dir_begin()
    while true:
        var f := dir.get_next()
        if f == "":
            break
        if dir.current_is_dir():
            if f != ".godot":
                out += _scan_dir(DirAccess.open(dir.get_current_dir() + "/" + f), exts)
        else:
            for e in exts:
                if f.ends_with("." + e):
                    out.append(dir.get_current_dir() + "/" + f)
    dir.list_dir_end()
    return out

func _check_one(p: String) -> bool:
    # Fast smoke-load to catch parse/import errors.
    var res := ResourceLoader.load(p)
    if res == null:
        push_error("%s: failed to load" % p)
        return false

    # Cross-check ext_resource UIDs in text against ResourceUID.
    var text := FileAccess.get_file_as_string(p)
    var ok := true
    for line in text.split("\n"):
        if line.begins_with("[ext_resource "):
            var path := _extract(line, "path")
            var uid := _extract(line, "uid")
            if path != "":
                var id: int = ResourceLoader.get_resource_uid(path)
                var expected := ResourceUID.id_to_text(id)
                if uid != "" and uid != expected:
                    printerr("%s: uid mismatch for %s -> file has %s, expected %s" % [p, path, uid, expected])
                    ok = false
    return ok

func _extract(line: String, key: String) -> String:
    var m := RegEx.new()
    m.compile(key + "=\"([^\"]+)\"")
    var r := m.search(line)
    return r.get_string(1) if r != null else ""


