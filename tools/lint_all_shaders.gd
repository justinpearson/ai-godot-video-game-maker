extends SceneTree

func _init():
	print("Linting all shader files...")
	var shader_files = []
	_find_shaders("res://", shader_files)

	if shader_files.is_empty():
		print("No shader files found")
		quit(0)
		return

	print("Found " + str(shader_files.size()) + " shader files")
	var has_errors = false

	for shader_path in shader_files:
		print("Checking: " + shader_path)
		if not _lint_shader(shader_path):
			has_errors = true

	if has_errors:
		print("Shader linting failed - see errors above")
		quit(1)
	else:
		print("All shaders linted successfully")
		quit(0)

func _find_shaders(path: String, result: Array):
	var dir = DirAccess.open(path)
	if not dir:
		return

	dir.list_dir_begin()
	var file_name = dir.get_next()

	while file_name != "":
		var full_path = path + "/" + file_name
		if dir.current_is_dir() and not file_name.begins_with("."):
			_find_shaders(full_path, result)
		elif file_name.ends_with(".gdshader"):
			result.append(full_path)
		file_name = dir.get_next()

func _lint_shader(shader_path: String) -> bool:
	var file = FileAccess.open(shader_path, FileAccess.READ)
	if not file:
		print("  ERROR: Cannot open file: " + shader_path)
		return false

	var code = file.get_as_text()
	file.close()

	# Parse shader type
	var shader_type = "canvas_item"
	if code.contains("shader_type spatial"):
		shader_type = "spatial"
	elif code.contains("shader_type particles"):
		shader_type = "particles"
	elif code.contains("shader_type sky"):
		shader_type = "sky"
	elif code.contains("shader_type fog"):
		shader_type = "fog"

	# Append dummy uniform for detection
	code += "\nuniform float _lint_dummy : hint_range(0, 1);"

	var shader = Shader.new()
	shader.code = code

	# Try to compile
	var material = ShaderMaterial.new()
	material.shader = shader

	# Check if compiled successfully via dummy uniform
	var params = RenderingServer.get_shader_parameter_list(shader.get_rid())
	for p in params:
		if p.name == "_lint_dummy":
			print("  OK")
			return true

	print("  FAILED - Compilation error")
	return false