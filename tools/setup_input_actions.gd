extends EditorScript

# Adds basic input actions for prototype. Safe to run multiple times.
func _run() -> void:
	var actions = [
		"move_forward", "move_backward", "move_left", "move_right",
		"accelerate", "brake", "turn_left", "turn_right",
		"fire_primary", "interact", "open_compiler", "toggle_bike"
	]
	for a in actions:
		if not InputMap.has_action(a):
			InputMap.add_action(a)
			print("[InputSetup] Added action: ", a)
	print("[InputSetup] Done.")

