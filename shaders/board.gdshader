shader_type canvas_item;
uniform vec4 white : source_color = vec4(1.0);
uniform vec4 black : source_color = vec4(0.0, 0.0, 0.0, 1.0);

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	
	bool is_odd_column = int(UV.x * 8.0) % 2 == 1;
	bool is_odd_row = int(UV.y * 8.0) % 2 == 1;
	bool is_odd_square = is_odd_column == is_odd_row;
	if (is_odd_square)
		COLOR = white;
	else
		COLOR = black;
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
