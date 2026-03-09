# main.gd - Main game scene orchestrator
extends Node2D

@onready var terrain: Node2D = $DestructibleTerrain
@onready var player: CharacterBody2D = $Player
@onready var hud: CanvasLayer = $HUD
@onready var camera: Camera2D = $Camera2D

# Wave management
var wave_spawn_queue: Array = []  # Queue of PlaneType to spawn this wave
var planes_killed_this_wave: int = 0
var planes_needed_this_wave: int = 0
var spawn_timer: float = 0.0
var spawn_interval: float = 2.0
var between_waves: bool = true
var between_wave_timer: float = 3.0

# Plane spawn Y ranges
const SKY_LAYERS := [60.0, 120.0, 180.0, 240.0]
const FRIENDLY_LAYER := 40.0

# Map type received from menu
var selected_map_type: int = 0
var custom_terrain_data: PackedByteArray

func _ready() -> void:
	camera.position = Vector2(0, 0)
	camera.zoom = Vector2(1, 1)

	var map_type: int = GameManager.selected_map_type
	if map_type == 4 and GameManager.custom_terrain_data.size() > 0:
		terrain.custom_terrain_data = GameManager.custom_terrain_data
	terrain.load_map(map_type)

	player.terrain = terrain
	player.rocket_fired.connect(_on_player_rocket_fired)
	player.player_died.connect(_on_player_died)

	var spawn_x: int = 640
	var spawn_y: int = terrain.get_surface_y(spawn_x)
	player.position = Vector2(spawn_x, spawn_y - 20)

	between_waves = true
	between_wave_timer = 2.0
	_create_background()

func _process(delta: float) -> void:
	if GameManager.is_game_over or GameManager.is_game_won:
		return

	if between_waves:
		between_wave_timer -= delta
		if between_wave_timer <= 0:
			_start_next_wave()
		return

	# Spawn planes from queue
	spawn_timer -= delta
	if spawn_timer <= 0 and wave_spawn_queue.size() > 0:
		var next_type: int = wave_spawn_queue.pop_front()
		_spawn_plane_of_type(next_type)
		spawn_timer = spawn_interval / GameManager.plane_speed_multiplier

	# Wave completes when all planes killed
	if planes_killed_this_wave >= planes_needed_this_wave:
		var active_enemies := get_tree().get_nodes_in_group("enemy").size()
		if active_enemies == 0:
			# Check if this was the last wave
			if GameManager.current_wave >= GameManager.max_waves:
				GameManager.is_game_won = true
				GameManager.game_won.emit()
			else:
				between_waves = true
				between_wave_timer = 3.0

func _start_next_wave() -> void:
	GameManager.start_next_wave()
	planes_killed_this_wave = 0
	between_waves = false
	spawn_timer = 1.5
	spawn_interval = max(1.2, 3.0 - GameManager.current_wave * 0.15)

	# Build spawn queue from wave config
	var cfg := GameManager.get_wave_planes(GameManager.current_wave)
	wave_spawn_queue.clear()
	for i in range(cfg["blue"]):
		wave_spawn_queue.append(PlaneBase.PlaneType.BLUE)
	for i in range(cfg["green"]):
		wave_spawn_queue.append(PlaneBase.PlaneType.GREEN)
	for i in range(cfg["red"]):
		wave_spawn_queue.append(PlaneBase.PlaneType.RED)
	for i in range(cfg["yellow"]):
		wave_spawn_queue.append(PlaneBase.PlaneType.YELLOW)
	for i in range(cfg["friendly"]):
		wave_spawn_queue.append(PlaneBase.PlaneType.FRIENDLY)

	# Shuffle to randomize order
	wave_spawn_queue.shuffle()

	# Total enemy planes needed to kill (exclude friendly)
	planes_needed_this_wave = cfg["blue"] + cfg["green"] + cfg["red"] + cfg["yellow"]

func _spawn_plane_of_type(type: int) -> void:
	var plane := Area2D.new()
	var script := load("res://scripts/plane_base.gd")
	plane.set_script(script)

	var spr := Sprite2D.new()
	spr.name = "Sprite2D"
	plane.add_child(spr)

	var col := CollisionShape2D.new()
	col.name = "CollisionShape2D"
	var shape := RectangleShape2D.new()
	shape.size = Vector2(64, 24)
	col.shape = shape
	plane.add_child(col)

	var img := Image.create(64, 24, false, Image.FORMAT_RGBA8)
	var body_color: Color
	match type:
		PlaneBase.PlaneType.BLUE:
			body_color = Color(0.3, 0.5, 1.0)
		PlaneBase.PlaneType.GREEN:
			body_color = Color(0.3, 0.8, 0.3)
		PlaneBase.PlaneType.RED:
			body_color = Color(1.0, 0.3, 0.3)
		PlaneBase.PlaneType.YELLOW:
			body_color = Color(1.0, 0.9, 0.2)
		PlaneBase.PlaneType.FRIENDLY:
			body_color = Color(0.9, 0.9, 0.95)

	_draw_plane_sprite(img, body_color, type == PlaneBase.PlaneType.FRIENDLY)
	spr.texture = ImageTexture.create_from_image(img)
	spr.scale = Vector2(1.5, 1.5)

	plane.plane_type = type
	plane.plane_image = img

	var dir: float = [-1.0, 1.0][randi() % 2]
	var start_x: float = -80.0 if dir > 0 else 1360.0
	var layer: float

	if type == PlaneBase.PlaneType.FRIENDLY:
		layer = FRIENDLY_LAYER
		plane.speed = 40.0
		plane.health = 1
		plane.bomb_interval = 999.0
	else:
		layer = SKY_LAYERS[randi() % SKY_LAYERS.size()]
		plane.speed = randf_range(60, 120)
		plane.health = 1
		match type:
			PlaneBase.PlaneType.BLUE:
				plane.bomb_interval = randf_range(4.0, 6.0)
			PlaneBase.PlaneType.GREEN:
				plane.bomb_interval = randf_range(5.0, 8.0)
			PlaneBase.PlaneType.RED:
				plane.bomb_interval = randf_range(6.0, 9.0)
			PlaneBase.PlaneType.YELLOW:
				plane.bomb_interval = randf_range(5.0, 7.0)

	plane.terrain = terrain
	plane.collision_layer = 4
	plane.collision_mask = 2 | 8

	add_child(plane)
	plane.setup(Vector2(start_x, layer), dir, plane.speed)
	plane.shot_down.connect(_on_plane_shot_down)

func _draw_plane_sprite(img: Image, color: Color, is_friendly: bool) -> void:
	var dark := color.darkened(0.3)
	var light := color.lightened(0.3)
	var w := img.get_width()
	var h := img.get_height()
	var mid_y := h / 2

	for x in range(10, w - 6):
		for y in range(mid_y - 4, mid_y + 4):
			img.set_pixel(x, y, color)

	for x in range(w - 6, w - 1):
		var narrowing := (x - (w - 6)) * 1
		for y in range(mid_y - 3 + narrowing, mid_y + 3 - narrowing):
			if y >= 0 and y < h:
				img.set_pixel(x, y, dark)

	for x in range(w - 10, w - 5):
		for y in range(mid_y - 2, mid_y + 1):
			img.set_pixel(x, y, Color(0.5, 0.7, 0.9))

	for x in range(20, 42):
		for y in range(1, mid_y - 3):
			img.set_pixel(x, y, light)
		for y in range(mid_y + 4, h - 2):
			img.set_pixel(x, y, light)

	for x in range(5, 14):
		for y in range(1, mid_y - 2):
			img.set_pixel(x, y, dark)

	for y in range(mid_y - 2, mid_y + 2):
		for x in range(6, 10):
			img.set_pixel(x, y, Color(0.25, 0.25, 0.3))

	if is_friendly:
		var cx := 32
		var cy := mid_y
		for i in range(-3, 4):
			if cx + i >= 0 and cx + i < w:
				img.set_pixel(cx + i, cy, Color(1.0, 0.2, 0.2))
			if cy + i >= 0 and cy + i < h:
				img.set_pixel(cx, cy + i, Color(1.0, 0.2, 0.2))

func _on_player_rocket_fired(pos: Vector2, target: Vector2) -> void:
	var rocket := Area2D.new()
	var script := load("res://scripts/rocket.gd")
	rocket.set_script(script)

	var spr := Sprite2D.new()
	var img := Image.create(16, 6, false, Image.FORMAT_RGBA8)
	for x in range(16):
		for y in range(6):
			if x < 4:
				img.set_pixel(x, y, Color(1.0, 0.5, 0.0))
			elif x < 13:
				img.set_pixel(x, y, Color(0.6, 0.6, 0.6))
			else:
				img.set_pixel(x, y, Color(1.0, 0.2, 0.2))
	spr.texture = ImageTexture.create_from_image(img)
	spr.name = "Sprite2D"
	spr.scale = Vector2(1.5, 1.5)
	rocket.add_child(spr)

	var col := CollisionShape2D.new()
	var rshape := RectangleShape2D.new()
	rshape.size = Vector2(16, 6)
	col.shape = rshape
	rocket.add_child(col)

	rocket.collision_layer = 8
	rocket.collision_mask = 4 | 32
	rocket.terrain = terrain

	rocket.body_entered.connect(rocket._on_body_entered)
	rocket.area_entered.connect(rocket._on_area_entered)

	add_child(rocket)
	rocket.setup(pos, target)

func spawn_bomb(pos: Vector2, plane_type: PlaneBase.PlaneType) -> void:
	call_deferred("_do_spawn_bomb", pos, plane_type)

func _do_spawn_bomb(pos: Vector2, plane_type: PlaneBase.PlaneType) -> void:
	var bomb_node := Area2D.new()
	var script := load("res://scripts/bomb.gd")
	bomb_node.set_script(script)

	var bomb_type: int
	match plane_type:
		PlaneBase.PlaneType.BLUE: bomb_type = 0
		PlaneBase.PlaneType.GREEN: bomb_type = 1
		PlaneBase.PlaneType.RED: bomb_type = 2
		PlaneBase.PlaneType.YELLOW: bomb_type = 3
		_: return

	var spr := Sprite2D.new()
	spr.name = "Sprite2D"
	var img := Image.create(10, 12, false, Image.FORMAT_RGBA8)
	for x in range(10):
		for y in range(12):
			var dx := x - 5
			var dy := y - 6
			if dx * dx + dy * dy <= 25:
				img.set_pixel(x, y, Color(0.2, 0.2, 0.2))
	img.set_pixel(5, 0, Color(1.0, 0.5, 0.0))
	img.set_pixel(5, 1, Color(0.8, 0.3, 0.0))
	spr.texture = ImageTexture.create_from_image(img)
	spr.scale = Vector2(1.5, 1.5)
	bomb_node.add_child(spr)

	var col := CollisionShape2D.new()
	col.name = "CollisionShape2D"
	var shape := CircleShape2D.new()
	shape.radius = 7.0
	col.shape = shape
	bomb_node.add_child(col)

	bomb_node.collision_layer = 32
	bomb_node.collision_mask = 2 | 8
	bomb_node.terrain = terrain
	bomb_node.body_entered.connect(bomb_node._on_body_entered)
	add_child(bomb_node)
	bomb_node.setup(pos, bomb_type)

func spawn_shrapnel(pos: Vector2, vel: Vector2) -> void:
	call_deferred("_do_spawn_shrapnel", pos, vel)

func _do_spawn_shrapnel(pos: Vector2, vel: Vector2) -> void:
	var shrap := Area2D.new()
	var script := load("res://scripts/shrapnel.gd")
	shrap.set_script(script)

	var spr := Sprite2D.new()
	spr.name = "Sprite2D"
	var img := Image.create(6, 6, false, Image.FORMAT_RGBA8)
	for x in range(6):
		for y in range(6):
			var dx := x - 3
			var dy := y - 3
			if dx * dx + dy * dy <= 9:
				img.set_pixel(x, y, Color(0.7, 0.7, 0.75))
	spr.texture = ImageTexture.create_from_image(img)
	spr.scale = Vector2(1.5, 1.5)
	shrap.add_child(spr)

	var col := CollisionShape2D.new()
	var shape := CircleShape2D.new()
	shape.radius = 4.0
	col.shape = shape
	shrap.add_child(col)

	shrap.collision_layer = 32
	shrap.collision_mask = 2 | 8
	shrap.terrain = terrain
	shrap.body_entered.connect(shrap._on_body_entered)
	add_child(shrap)
	shrap.setup(pos, vel)

func spawn_enemy_soldier(pos: Vector2) -> void:
	call_deferred("_do_spawn_enemy_soldier", pos)

func _do_spawn_enemy_soldier(pos: Vector2) -> void:
	var soldier := CharacterBody2D.new()
	var script := load("res://scripts/enemy_soldier.gd")
	soldier.set_script(script)

	var spr := Sprite2D.new()
	spr.name = "Sprite2D"
	var img := Image.create(10, 16, false, Image.FORMAT_RGBA8)
	for x in range(10):
		for y in range(16):
			if y < 4:
				var dx := x - 5
				var dy := y - 2
				if dx * dx + dy * dy <= 6:
					img.set_pixel(x, y, Color(0.8, 0.65, 0.5))
			elif y < 6:
				if x >= 2 and x <= 7:
					img.set_pixel(x, y, Color(0.2, 0.4, 0.2))
			elif y < 12:
				if x >= 2 and x <= 7:
					img.set_pixel(x, y, Color(0.25, 0.45, 0.2))
			else:
				if (x >= 2 and x <= 4) or (x >= 6 and x <= 8):
					img.set_pixel(x, y, Color(0.3, 0.3, 0.2))
	spr.texture = ImageTexture.create_from_image(img)
	spr.scale = Vector2(2.5, 2.5)
	soldier.add_child(spr)

	var col := CollisionShape2D.new()
	col.name = "CollisionShape2D"
	var shape := RectangleShape2D.new()
	shape.size = Vector2(16, 28)
	col.shape = shape
	soldier.add_child(col)

	soldier.collision_layer = 4
	soldier.collision_mask = 1 | 8
	soldier.terrain = terrain
	add_child(soldier)
	soldier.global_position = pos

func spawn_poison_cloud(pos: Vector2) -> void:
	call_deferred("_do_spawn_poison_cloud", pos)

func _do_spawn_poison_cloud(pos: Vector2) -> void:
	var cloud := Area2D.new()
	var script := load("res://scripts/poison_cloud.gd")
	cloud.set_script(script)
	cloud.collision_layer = 32
	cloud.collision_mask = 2
	cloud.body_entered.connect(cloud._on_body_entered)
	add_child(cloud)
	cloud.setup(pos)

func spawn_pickup(pos: Vector2, type_str: String) -> void:
	call_deferred("_do_spawn_pickup", pos, type_str)

func _do_spawn_pickup(pos: Vector2, type_str: String) -> void:
	var pickup_node := Area2D.new()
	var script := load("res://scripts/pickup.gd")
	pickup_node.set_script(script)

	var ptype: int
	match type_str:
		"jump_boots": ptype = 0
		"chain_rocket": ptype = 1
		"emp": ptype = 2
		"pow": ptype = 3
		"pierce": ptype = 4
		_: ptype = 0

	var spr := Sprite2D.new()
	spr.name = "Sprite2D"
	pickup_node.add_child(spr)

	var lbl := Label.new()
	lbl.name = "Label"
	pickup_node.add_child(lbl)

	var col := CollisionShape2D.new()
	var shape := CircleShape2D.new()
	shape.radius = 24.0
	col.shape = shape
	pickup_node.add_child(col)

	pickup_node.collision_layer = 16
	pickup_node.collision_mask = 2
	pickup_node.terrain = terrain
	pickup_node.pickup_type = ptype
	pickup_node.body_entered.connect(pickup_node._on_body_entered)
	add_child(pickup_node)
	pickup_node.setup(pos, ptype)

func _on_plane_shot_down(_plane: PlaneBase) -> void:
	# Only count enemy plane kills toward wave progression
	if _plane.plane_type != PlaneBase.PlaneType.FRIENDLY:
		planes_killed_this_wave += 1

func _on_player_died(pos: Vector2) -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 30
	particles.lifetime = 0.8
	particles.global_position = pos
	var mat := ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 60.0
	mat.initial_velocity_max = 150.0
	mat.gravity = Vector3(0, 200, 0)
	mat.scale_min = 3.0
	mat.scale_max = 6.0
	mat.color = Color(1.0, 0.4, 0.1)
	particles.process_material = mat
	add_child(particles)
	get_tree().create_timer(2.0).timeout.connect(particles.queue_free)

func _create_background() -> void:
	var bg := Sprite2D.new()
	bg.z_index = -10
	var img := Image.create(2, 720, false, Image.FORMAT_RGBA8)
	for y in range(720):
		var t := float(y) / 720.0
		var sky_top := Color(0.1, 0.15, 0.35)
		var sky_bottom := Color(0.45, 0.6, 0.85)
		var color := sky_top.lerp(sky_bottom, t)
		img.set_pixel(0, y, color)
		img.set_pixel(1, y, color)
	bg.texture = ImageTexture.create_from_image(img)
	bg.centered = false
	bg.scale = Vector2(640, 1)
	bg.position = Vector2(0, 0)
	add_child(bg)
	move_child(bg, 0)
