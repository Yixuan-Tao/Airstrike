# crosshair.gd - Custom crosshair cursor that follows mouse
extends Node2D

var crosshair_tex: ImageTexture

func _ready() -> void:
	# Create crosshair image
	var img := Image.create(16, 16, false, Image.FORMAT_RGBA8)
	var c := Color(1.0, 0.3, 0.2, 0.9)

	# Horizontal line
	for x in range(0, 16):
		if x < 5 or x > 10:
			img.set_pixel(x, 7, c)
			img.set_pixel(x, 8, c)

	# Vertical line
	for y in range(0, 16):
		if y < 5 or y > 10:
			img.set_pixel(7, y, c)
			img.set_pixel(8, y, c)

	# Center dot
	for x in range(6, 10):
		for y in range(6, 10):
			var dx := x - 7.5
			var dy := y - 7.5
			if dx * dx + dy * dy <= 2:
				img.set_pixel(x, y, Color(1.0, 0.2, 0.1))

	crosshair_tex = ImageTexture.create_from_image(img)

	# Hide default cursor
	var cursor_img := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	cursor_img.set_pixel(0, 0, Color(0, 0, 0, 0))
	var cursor_tex := ImageTexture.create_from_image(cursor_img)
	Input.set_custom_mouse_cursor(cursor_tex)

func _process(_delta: float) -> void:
	queue_redraw()

func _draw() -> void:
	var mouse_pos := get_global_mouse_position()
	draw_texture(crosshair_tex, mouse_pos - Vector2(8, 8))
