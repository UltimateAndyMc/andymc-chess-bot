extends Button

signal square_pressed(child: int)

func set_visibility(visibility):
	if visibility:
		visibility.self_modulate.a = 1
	else:
		visibility.self_modulate.a = 0


func _on_pressed() -> void:
	square_pressed.emit(get_index())
