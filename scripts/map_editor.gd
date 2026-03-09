# map_editor.gd - Simple terrain editor
extends Node2D

signal editor_done(terrain_data: PackedByteArray)
signal editor_cancel

const WIDTH: int = 1280
const HEIGHT: int = 720

var canvas_image: Image
var canvas_texture: ImageTexture
var collision_data: PackedByteArray
var brush_radius: float = 15.0
var is_painting: bool = false
var paint_mode: bool = true  # true = add, false = erase

@onready var canvas_sprite: Sprite2D = $CanvasSprite

func _ready() -> void:
	_init_canvas()
	_build_ui()

func _init_canvas() -> void:
	canvas_image = Image.create(WIDTH, HEIGHT, false, Image.FORMAT_RGBA8)
	canvas_image.fill(Color(0, 0, 0, 0))
	collision_data.resize(WIDTH * HEIGHT)
	collision_data.fill(0)
	canvas_texture = ImageTexture.create_from_image(canvas_image)
	canvas_sprite.texture = canvas_texture
	canvas_sprite.centered = false

	# Draw grid lines for reference
	for x in range(0, WIDTH, 64):
		for y in range(0, HEIGHT):
			canvas_image.set_pixel(x, y, Color(0.15, 0.15, 0.2, 0.3))
	for y in range(0, HEIGHT, 64):
		for x in range(0, WIDTH):
			canvas_image.set_pixel(x, y, Color(0.15, 0.15, 0.2, 0.3))
	canvas_texture.update(canvas_image)

func _build_ui() -> void:
	# Background
	var bg := ColorRect.new()
	bg.color = Color(0.12, 0.14, 0.22)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.z_index = -20
	add_child(bg)

	# Top bar
	var bar := HBoxContainer.new()
	bar.position = Vector2(10, 10)
	bar.add_theme_constant_override("separation", 10)
	bar.z_index = 50
	add_child(bar)

	# Paint mode button
	var paint_btn := Button.new()
	paint_btn.name = "PaintBtn"
	paint_btn.text = "PAINT [LMB]"
	paint_btn.custom_minimum_size = Vector2(120, 35)
	paint_btn.pressed.connect(func() -> void: paint_mode = true; _update_mode_labels())
	bar.add_child(paint_btn)

	# Erase mode button
	var erase_btn := Button.new()
	erase_btn.name = "EraseBtn"
	erase_btn.text = "ERASE [RMB]"
	erase_btn.custom_minimum_size = Vector2(120, 35)
	erase_btn.pressed.connect(func() -> void: paint_mode = false; _update_mode_labels())
	bar.add_child(erase_btn)

	# Brush size label
	var brush_label := Label.new()
	brush_label.name = "BrushLabel"
	brush_label.text = "Brush: %d" % int(brush_radius)
	brush_label.add_theme_font_size_override("font_size", 16)
	brush_label.add_theme_color_override("font_color", Color.WHITE)
	bar.add_child(brush_label)

	# Brush size buttons
	var smaller := Button.new()
	smaller.text = "[-]"
	smaller.custom_minimum_size = Vector2(40, 35)
	smaller.pressed.connect(func() -> void:
		brush_radius = maxf(3.0, brush_radius - 5.0)
		_update_mode_labels()
	)
	bar.add_child(smaller)

	var bigger := Button.new()
	bigger.text = "[+]"
	bigger.custom_minimum_size = Vector2(40, 35)
	bigger.pressed.connect(func() -> void:
		brush_radius = minf(60.0, brush_radius + 5.0)
		_update_mode_labels()
	)
	bar.add_child(bigger)

	# Spacer
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(100, 0)
	bar.add_child(spacer)

	# Fill flat button
	var fill_btn := Button.new()
	fill_btn.text = "FILL FLAT"
	fill_btn.custom_minimum_size = Vector2(100, 35)
	fill_btn.pressed.connect(_fill_flat)
	bar.add_child(fill_btn)

	# Clear button
	var clear_btn := Button.new()
	clear_btn.text = "CLEAR"
	clear_btn.custom_minimum_size = Vector2(80, 35)
	clear_btn.pressed.connect(_clear_canvas)
	bar.add_child(clear_btn)

	# Done button
	var done_btn := Button.new()
	done_btn.text = "PLAY"
	done_btn.custom_minimum_size = Vector2(80, 35)
	done_btn.add_theme_color_override("font_color", Color(0.2, 1.0, 0.3))
	done_btn.pressed.connect(_on_done)
	bar.add_child(done_btn)

	# Cancel button
	var cancel_btn := Button.new()
	cancel_btn.text = "BACK"
	cancel_btn.custom_minimum_size = Vector2(80, 35)
	cancel_btn.pressed.connect(func() -> void:
		get_tree().change_scene_to_file("res://scenes/map_select.tscn")
	)
	bar.add_child(cancel_btn)

	# Mode indicator
	var mode_label := Label.new()
	mode_label.name = "ModeLabel"
	mode_label.text = "Mode: PAINT"
	mode_label.add_theme_font_size_override("font_size", 14)
	mode_label.add_theme_color_override("font_color", Color(0.5, 0.8, 0.5))
	mode_label.position = Vector2(10, 55)
	mode_label.z_index = 50
	add_child(mode_label)

func _update_mode_labels() -> void:
	var mode_label := get_node_or_null("ModeLabel")
	if mode_label:
		mode_label.text = "Mode: %s | Brush: %d | Scroll to resize" % [
			"PAINT" if paint_mode else "ERASE", int(brush_radius)
		]
		mode_label.add_theme_color_override("font_color",
			Color(0.5, 0.8, 0.5) if paint_mode else Color(0.8, 0.5, 0.5))

	var brush_label := get_node_or_null("BrushLabel")
	if brush_label:
		brush_label.text = "Brush: %d" % int(brush_radius)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			is_painting = event.pressed
			paint_mode = true
			if is_painting:
				_paint_at(get_global_mouse_position())
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			is_painting = event.pressed
			paint_mode = false
			if is_painting:
				_paint_at(get_global_mouse_position())
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
			brush_radius = minf(60.0, brush_radius + 2.0)
			_update_mode_labels()
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			brush_radius = maxf(3.0, brush_radius - 2.0)
			_update_mode_labels()

	if event is InputEventMouseMotion and is_painting:
		_paint_at(get_global_mouse_position())

func _paint_at(pos: Vector2) -> void:
	var cx := int(pos.x)
	var cy := int(pos.y)
	var r := int(brush_radius)
	var color := Color(0.45, 0.35, 0.2) if paint_mode else Color(0, 0, 0, 0)

	for y in range(maxi(cy - r, 0), mini(cy + r, HEIGHT - 1) + 1):
		for x in range(maxi(cx - r, 0), mini(cx + r, WIDTH - 1) + 1):
			var dx := x - cx
			var dy := y - cy
			if dx * dx + dy * dy <= r * r:
				canvas_image.set_pixel(x, y, color)
				collision_data[y * WIDTH + x] = 1 if paint_mode else 0

	canvas_texture.update(canvas_image)

func _fill_flat() -> void:
	canvas_image.fill(Color(0, 0, 0, 0))
	collision_data.fill(0)
	var grass := Color(0.25, 0.55, 0.15)
	var dirt := Color(0.45, 0.35, 0.2)
	for x in range(WIDTH):
		for y in range(550, HEIGHT):
			var c := grass if (y - 550) < 3 else dirt
			canvas_image.set_pixel(x, y, c)
			collision_data[y * WIDTH + x] = 1
	canvas_texture.update(canvas_image)

func _clear_canvas() -> void:
	canvas_image.fill(Color(0, 0, 0, 0))
	collision_data.fill(0)
	canvas_texture.update(canvas_image)

func _on_done() -> void:
	GameManager.selected_map_type = 4  # CUSTOM
	GameManager.custom_terrain_data = collision_data.duplicate()
	GameManager.reset_game()
	get_tree().change_scene_to_file("res://scenes/main.tscn")

func _process(_delta: float) -> void:
	queue_redraw()

func _draw() -> void:
	# Draw brush cursor
	var mouse_pos := get_global_mouse_position()
	var color := Color(0.5, 1.0, 0.5, 0.4) if paint_mode else Color(1.0, 0.4, 0.4, 0.4)
	draw_arc(mouse_pos, brush_radius, 0, TAU, 32, color, 1.5)
