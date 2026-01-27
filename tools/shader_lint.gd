extends SceneTree

func _init():
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MINIMIZED)
	var args = OS.get_cmdline_user_args()
	if args.size() != 1:
		print("Usage: godot --script shader_lint.gd -- path/to/shader.gdshader")
		quit(1)
		return
	var shader_path = args[0]
	var file = FileAccess.open(shader_path, FileAccess.READ)
	if not file:
		print("Cannot open file: " + shader_path)
		quit(1)
		return
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
	var material = ShaderMaterial.new()
	material.shader = shader

	# Set up viewport and node based on type
	var viewport = SubViewport.new()
	viewport.size = Vector2(64, 64)
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	viewport.transparent_bg = true

	var node
	if shader_type == "canvas_item":
		node = Sprite2D.new()
		var tex = Image.create(64, 64, false, Image.FORMAT_RGBA8)
		node.texture = ImageTexture.create_from_image(tex)
		node.material = material
	elif shader_type == "spatial":
		viewport.world_3d = World3D.new()
		var camera = Camera3D.new()
		camera.current = true
		viewport.add_child(camera)
		node = MeshInstance3D.new()
		node.mesh = BoxMesh.new()
		node.material_override = material
	elif shader_type == "particles":
		node = GPUParticles2D.new()
		node.process_material = material
		node.amount = 1
		node.emitting = true
	elif shader_type == "sky":
		viewport.world_3d = World3D.new()
		var env = Environment.new()
		env.background_mode = Environment.BG_SKY
		env.sky = Sky.new()
		env.sky.sky_material = material
		viewport.environment = env
		node = Node3D.new()  # Dummy node
	elif shader_type == "fog":
		viewport.world_3d = World3D.new()
		var env = Environment.new()
		env.volumetric_fog_enabled = true
		viewport.environment = env
		node = Node3D.new()  # Dummy node
	else:
		print("Unsupported shader type: " + shader_type)
		quit(1)
		return

	viewport.add_child(node)
	root.add_child(viewport)

	# Force render to trigger compilation and error printing
	var vp_tex = viewport.get_texture()
	vp_tex.get_image()  # Ensure render completes

	# Check if compiled successfully via dummy uniform
	var params = RenderingServer.get_shader_parameter_list(shader.get_rid())
	var success = false
	for p in params:
		if p.name == "_lint_dummy":
			success = true
			break
	if success:
		print("Shader linted successfully: " + shader_path)
		quit(0)
	else:
		print("Shader compilation failed (see errors above): " + shader_path)
		quit(1)