# destructible_terrain.gd - Worms-style pixel destructible terrain with map presets
extends Node2D

const TERRAIN_WIDTH: int = 1280
const TERRAIN_HEIGHT: int = 720
const GROUND_START_Y: int = 450
const HILL_AMPLITUDE: float = 80.0
const HILL_FREQUENCY: float = 0.008

enum MapType { HILLS, FLAT, VALLEY, ISLANDS, CUSTOM }

var current_map: MapType = MapType.HILLS
var terrain_image: Image
var terrain_texture: ImageTexture
var collision_data: PackedByteArray

@onready var sprite: Sprite2D = $Sprite2D
@onready var terrain_body: StaticBody2D = $StaticBody2D

var needs_collision_update: bool = false
var collision_update_timer: float = 0.0
const COLLISION_UPDATE_INTERVAL: float = 0.1

# For custom/editor maps
var custom_terrain_data: PackedByteArray

func _ready() -> void:
	pass  # Don't auto-generate; main scene calls load_map()

func load_map(map_type: MapType) -> void:
	current_map = map_type
	match map_type:
		MapType.HILLS:
			_generate_hills()
		MapType.FLAT:
			_generate_flat()
		MapType.VALLEY:
			_generate_valley()
		MapType.ISLANDS:
			_generate_islands()
		MapType.CUSTOM:
			_load_custom()
	update_texture()
	rebuild_collision()

func _init_image() -> void:
	terrain_image = Image.create(TERRAIN_WIDTH, TERRAIN_HEIGHT, false, Image.FORMAT_RGBA8)
	collision_data.resize(TERRAIN_WIDTH * TERRAIN_HEIGHT)
	collision_data.fill(0)
	terrain_image.fill(Color(0, 0, 0, 0))

func _generate_hills() -> void:
	_init_image()
	var dirt_color := Color(0.45, 0.30, 0.15)
	var dirt_dark := Color(0.35, 0.22, 0.10)
	var grass_color := Color(0.25, 0.55, 0.15)
	var stone_color := Color(0.4, 0.4, 0.42)
	var rng := RandomNumberGenerator.new()
	rng.seed = randi()

	for x in range(TERRAIN_WIDTH):
		var hill1 := sin(x * HILL_FREQUENCY) * HILL_AMPLITUDE
		var hill2 := sin(x * HILL_FREQUENCY * 2.3 + 1.5) * (HILL_AMPLITUDE * 0.4)
		var hill3 := sin(x * HILL_FREQUENCY * 5.1 + 3.2) * (HILL_AMPLITUDE * 0.15)
		var surface_y := int(GROUND_START_Y + hill1 + hill2 + hill3)
		surface_y = clampi(surface_y, 250, TERRAIN_HEIGHT - 50)
		_fill_column(x, surface_y, dirt_color, dirt_dark, grass_color, stone_color, rng)

func _generate_flat() -> void:
	_init_image()
	var dirt_color := Color(0.45, 0.30, 0.15)
	var dirt_dark := Color(0.35, 0.22, 0.10)
	var grass_color := Color(0.25, 0.55, 0.15)
	var stone_color := Color(0.4, 0.4, 0.42)
	var rng := RandomNumberGenerator.new()
	rng.seed = randi()
	var flat_y: int = 550
	for x in range(TERRAIN_WIDTH):
		_fill_column(x, flat_y, dirt_color, dirt_dark, grass_color, stone_color, rng)

func _generate_valley() -> void:
	_init_image()
	var dirt_color := Color(0.45, 0.30, 0.15)
	var dirt_dark := Color(0.35, 0.22, 0.10)
	var grass_color := Color(0.25, 0.55, 0.15)
	var stone_color := Color(0.4, 0.4, 0.42)
	var rng := RandomNumberGenerator.new()
	rng.seed = randi()

	for x in range(TERRAIN_WIDTH):
		# Valley shape: high on edges, low in center
		var center_dist: float = absf(float(x) - TERRAIN_WIDTH / 2.0) / (TERRAIN_WIDTH / 2.0)
		var surface_y := int(550 - center_dist * 180)
		surface_y = clampi(surface_y, 250, TERRAIN_HEIGHT - 50)
		_fill_column(x, surface_y, dirt_color, dirt_dark, grass_color, stone_color, rng)

func _generate_islands() -> void:
	_init_image()
	var dirt_color := Color(0.45, 0.30, 0.15)
	var dirt_dark := Color(0.35, 0.22, 0.10)
	var grass_color := Color(0.25, 0.55, 0.15)
	var stone_color := Color(0.4, 0.4, 0.42)
	var rng := RandomNumberGenerator.new()
	rng.seed = randi()

	# Create several floating islands
	var islands := [
		{"cx": 200, "w": 180, "y": 500},
		{"cx": 500, "w": 200, "y": 450},
		{"cx": 800, "w": 160, "y": 520},
		{"cx": 1050, "w": 200, "y": 470},
		{"cx": 640, "w": 280, "y": 580},  # big bottom island
	]
	for island in islands:
		var cx: int = island["cx"]
		var half_w: int = island["w"] / 2
		var iy: int = island["y"]
		for x in range(cx - half_w, cx + half_w):
			if x < 0 or x >= TERRAIN_WIDTH:
				continue
			var dx: int = absi(x - cx)
			var edge_factor := 1.0 - float(dx) / float(half_w)
			var thickness := int(edge_factor * 40) + 8
			var top_y := iy - 2
			for y in range(top_y, top_y + thickness):
				if y < 0 or y >= TERRAIN_HEIGHT:
					continue
				var depth := y - top_y
				var color: Color
				if depth < 3:
					color = grass_color
				elif depth < 25:
					color = dirt_color.lerp(dirt_dark, rng.randf() * 0.3)
				else:
					color = stone_color
				terrain_image.set_pixel(x, y, color)
				collision_data[y * TERRAIN_WIDTH + x] = 1

func _load_custom() -> void:
	_init_image()
	if custom_terrain_data.size() == TERRAIN_WIDTH * TERRAIN_HEIGHT:
		# Restore from saved editor data
		collision_data = custom_terrain_data.duplicate()
		for y in range(TERRAIN_HEIGHT):
			for x in range(TERRAIN_WIDTH):
				if collision_data[y * TERRAIN_WIDTH + x] == 1:
					terrain_image.set_pixel(x, y, Color(0.45, 0.35, 0.2))
	else:
		# Empty canvas for editor
		pass

func _fill_column(x: int, surface_y: int, dirt: Color, dirt_dark: Color, grass: Color, stone: Color, rng: RandomNumberGenerator) -> void:
	for y in range(surface_y, TERRAIN_HEIGHT):
		var depth := y - surface_y
		var color: Color
		if depth < 3:
			color = grass
		elif depth < 40:
			var noise_val := rng.randf()
			if noise_val < 0.1:
				color = dirt_dark
			else:
				color = dirt.lerp(dirt_dark, rng.randf() * 0.3)
		else:
			var noise_val := rng.randf()
			color = stone.lerp(Color(0.35, 0.35, 0.37), noise_val * 0.3)
		terrain_image.set_pixel(x, y, color)
		collision_data[y * TERRAIN_WIDTH + x] = 1

func update_texture() -> void:
	if terrain_texture == null:
		terrain_texture = ImageTexture.create_from_image(terrain_image)
	else:
		terrain_texture.update(terrain_image)
	sprite.texture = terrain_texture

func destroy_circle(center: Vector2, radius: float) -> void:
	var cx := int(center.x)
	var cy := int(center.y)
	var r := int(radius)
	var min_x := maxi(cx - r, 0)
	var max_x := mini(cx + r, TERRAIN_WIDTH - 1)
	var min_y := maxi(cy - r, 0)
	var max_y := mini(cy + r, TERRAIN_HEIGHT - 1)

	for y in range(min_y, max_y + 1):
		for x in range(min_x, max_x + 1):
			var dx := x - cx
			var dy := y - cy
			if dx * dx + dy * dy <= r * r:
				terrain_image.set_pixel(x, y, Color(0, 0, 0, 0))
				collision_data[y * TERRAIN_WIDTH + x] = 0

	needs_collision_update = true
	update_texture()

func add_terrain_rect(rect: Rect2i, color: Color) -> void:
	var min_x := maxi(rect.position.x, 0)
	var max_x := mini(rect.end.x, TERRAIN_WIDTH - 1)
	var min_y := maxi(rect.position.y, 0)
	var max_y := mini(rect.end.y, TERRAIN_HEIGHT - 1)

	for y in range(min_y, max_y + 1):
		for x in range(min_x, max_x + 1):
			terrain_image.set_pixel(x, y, color)
			collision_data[y * TERRAIN_WIDTH + x] = 1

	needs_collision_update = true
	update_texture()

func is_solid(x: int, y: int) -> bool:
	if x < 0 or x >= TERRAIN_WIDTH or y < 0 or y >= TERRAIN_HEIGHT:
		return false
	return collision_data[y * TERRAIN_WIDTH + x] == 1

func get_surface_y(x: int) -> int:
	x = clampi(x, 0, TERRAIN_WIDTH - 1)
	for y in range(TERRAIN_HEIGHT):
		if collision_data[y * TERRAIN_WIDTH + x] == 1:
			return y
	return TERRAIN_HEIGHT

func _process(delta: float) -> void:
	if needs_collision_update:
		collision_update_timer += delta
		if collision_update_timer >= COLLISION_UPDATE_INTERVAL:
			rebuild_collision()
			collision_update_timer = 0.0
			needs_collision_update = false

func rebuild_collision() -> void:
	for child in terrain_body.get_children():
		if child is CollisionShape2D:
			child.queue_free()

	var step := 4
	for y in range(0, TERRAIN_HEIGHT, step):
		var span_start := -1
		for x in range(0, TERRAIN_WIDTH, step):
			var solid := false
			for sy in range(y, mini(y + step, TERRAIN_HEIGHT)):
				for sx in range(x, mini(x + step, TERRAIN_WIDTH)):
					if collision_data[sy * TERRAIN_WIDTH + sx] == 1:
						solid = true
						break
				if solid:
					break

			if solid and span_start == -1:
				span_start = x
			elif not solid and span_start != -1:
				_add_collision_span(span_start, y, x - span_start, step)
				span_start = -1

		if span_start != -1:
			_add_collision_span(span_start, y, TERRAIN_WIDTH - span_start, step)

func _add_collision_span(x: int, y: int, w: int, h: int) -> void:
	var shape := RectangleShape2D.new()
	shape.size = Vector2(w, h)
	var col := CollisionShape2D.new()
	col.shape = shape
	col.position = Vector2(x + w / 2.0, y + h / 2.0)
	terrain_body.add_child(col)

# Editor support
func editor_paint(pos: Vector2, radius: float, add: bool) -> void:
	var cx := int(pos.x)
	var cy := int(pos.y)
	var r := int(radius)
	var min_x := maxi(cx - r, 0)
	var max_x := mini(cx + r, TERRAIN_WIDTH - 1)
	var min_y := maxi(cy - r, 0)
	var max_y := mini(cy + r, TERRAIN_HEIGHT - 1)

	for y in range(min_y, max_y + 1):
		for x in range(min_x, max_x + 1):
			var dx := x - cx
			var dy := y - cy
			if dx * dx + dy * dy <= r * r:
				if add:
					terrain_image.set_pixel(x, y, Color(0.45, 0.35, 0.2))
					collision_data[y * TERRAIN_WIDTH + x] = 1
				else:
					terrain_image.set_pixel(x, y, Color(0, 0, 0, 0))
					collision_data[y * TERRAIN_WIDTH + x] = 0

	needs_collision_update = true
	update_texture()

func save_custom_data() -> void:
	custom_terrain_data = collision_data.duplicate()
