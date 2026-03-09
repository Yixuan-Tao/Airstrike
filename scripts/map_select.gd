# map_select.gd - Map selection menu with preview and editor access
extends Control

signal map_selected(map_type: int)
signal open_editor

var selected_map: int = 0  # MapType enum value
var preview_image: Image
var preview_texture: ImageTexture

func _ready() -> void:
	_build_ui()
	_update_preview()

func _build_ui() -> void:
	# Title
	var title := Label.new()
	title.text = "ROCKET DEFENSE"
	title.add_theme_font_size_override("font_size", 48)
	title.add_theme_color_override("font_color", Color(1.0, 0.8, 0.2))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.set_anchors_preset(Control.PRESET_TOP_WIDE)
	title.position.y = 30
	add_child(title)

	# Subtitle
	var subtitle := Label.new()
	subtitle.text = "SELECT MAP"
	subtitle.add_theme_font_size_override("font_size", 24)
	subtitle.add_theme_color_override("font_color", Color(0.8, 0.8, 0.9))
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	subtitle.set_anchors_preset(Control.PRESET_TOP_WIDE)
	subtitle.position.y = 90
	add_child(subtitle)

	# Map buttons container
	var btn_container := HBoxContainer.new()
	btn_container.set_anchors_preset(Control.PRESET_CENTER)
	btn_container.position = Vector2(340, 140)
	btn_container.add_theme_constant_override("separation", 16)
	add_child(btn_container)

	var map_names := ["Hills", "Flat", "Valley", "Islands", "Editor"]
	var map_colors := [
		Color(0.3, 0.6, 0.2),
		Color(0.5, 0.5, 0.3),
		Color(0.4, 0.3, 0.5),
		Color(0.2, 0.4, 0.6),
		Color(0.6, 0.4, 0.2),
	]
	for i in range(map_names.size()):
		var btn := Button.new()
		btn.text = map_names[i]
		btn.custom_minimum_size = Vector2(100, 40)
		btn.add_theme_font_size_override("font_size", 16)
		var idx := i
		btn.pressed.connect(func() -> void: _on_map_button(idx))
		btn_container.add_child(btn)

	# Preview area
	var preview_sprite := Sprite2D.new()
	preview_sprite.name = "Preview"
	preview_sprite.position = Vector2(640, 400)
	preview_sprite.scale = Vector2(0.7, 0.7)
	add_child(preview_sprite)

	# Preview border
	var border := Panel.new()
	border.position = Vector2(192, 210)
	border.size = Vector2(896, 365)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0, 0, 0, 0)
	style.border_color = Color(0.5, 0.5, 0.6)
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_width_top = 2
	style.border_width_bottom = 2
	border.add_theme_stylebox_override("panel", style)
	add_child(border)

	# Start button
	var start_btn := Button.new()
	start_btn.name = "StartButton"
	start_btn.text = "START GAME"
	start_btn.custom_minimum_size = Vector2(200, 50)
	start_btn.add_theme_font_size_override("font_size", 24)
	start_btn.position = Vector2(540, 610)
	start_btn.pressed.connect(_on_start)
	add_child(start_btn)

	# Controls help
	var help := Label.new()
	help.text = "A/D or Arrows: Move | W/Space/Up: Jump | Click: Shoot Rocket"
	help.add_theme_font_size_override("font_size", 14)
	help.add_theme_color_override("font_color", Color(0.6, 0.6, 0.7))
	help.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	help.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	help.position.y = -40
	add_child(help)

func _on_map_button(idx: int) -> void:
	if idx == 4:
		# Open editor
		get_tree().change_scene_to_file("res://scenes/map_editor.tscn")
		return
	selected_map = idx
	_update_preview()

func _update_preview() -> void:
	# Generate a small preview of the selected map
	preview_image = Image.create(1280, 720, false, Image.FORMAT_RGBA8)
	preview_image.fill(Color(0.2, 0.25, 0.4))

	var grass := Color(0.25, 0.55, 0.15)
	var dirt := Color(0.45, 0.30, 0.15)

	match selected_map:
		0:  # Hills
			for x in range(1280):
				var hill1 := sin(x * 0.008) * 80.0
				var hill2 := sin(x * 0.008 * 2.3 + 1.5) * 32.0
				var sy := int(450 + hill1 + hill2)
				sy = clampi(sy, 250, 670)
				for y in range(sy, 720):
					var c := grass if (y - sy) < 3 else dirt
					preview_image.set_pixel(x, y, c)
		1:  # Flat
			for x in range(1280):
				for y in range(550, 720):
					var c := grass if (y - 550) < 3 else dirt
					preview_image.set_pixel(x, y, c)
		2:  # Valley
			for x in range(1280):
				var center_dist: float = absf(float(x) - 640.0) / 640.0
				var sy := int(550 - center_dist * 180)
				sy = clampi(sy, 250, 670)
				for y in range(sy, 720):
					var c := grass if (y - sy) < 3 else dirt
					preview_image.set_pixel(x, y, c)
		3:  # Islands
			var islands := [
				[200, 180, 500], [500, 200, 450], [800, 160, 520],
				[1050, 200, 470], [640, 280, 580]
			]
			for island_data in islands:
				var cx: int = island_data[0]
				var hw: int = island_data[1] / 2
				var iy: int = island_data[2]
				for xi in range(cx - hw, cx + hw):
					if xi < 0 or xi >= 1280:
						continue
					var edge: float = 1.0 - absf(float(xi - cx)) / float(hw)
					var thick := int(edge * 30) + 5
					for yi in range(iy, iy + thick):
						if yi >= 0 and yi < 720:
							preview_image.set_pixel(xi, yi, dirt if (yi - iy) > 2 else grass)

	if preview_texture == null:
		preview_texture = ImageTexture.create_from_image(preview_image)
	else:
		preview_texture.update(preview_image)

	var preview_node := get_node_or_null("Preview")
	if preview_node and preview_node is Sprite2D:
		preview_node.texture = preview_texture

func _on_start() -> void:
	GameManager.selected_map_type = selected_map
	GameManager.reset_game()
	get_tree().change_scene_to_file("res://scenes/main.tscn")
