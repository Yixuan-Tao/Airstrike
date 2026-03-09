# plane_base.gd - Base class for all planes
extends Area2D
class_name PlaneBase

signal shot_down(plane: PlaneBase)
signal crashed(plane: PlaneBase, pos: Vector2)

enum PlaneType { BLUE, GREEN, RED, YELLOW, FRIENDLY }

@export var plane_type: PlaneType = PlaneType.BLUE
@export var health: int = 2
@export var speed: float = 100.0
@export var bomb_interval: float = 3.0

var direction: float = 1.0  # 1 = right, -1 = left
var is_shot_down: bool = false
var fall_speed: float = 0.0
var fall_rotation_speed: float = 0.0
var terrain: Node2D
var bomb_timer: float = 0.0
var has_dropped_loot: bool = false
var plane_image: Image  # Store plane sprite for terrain conversion

@onready var sprite: Sprite2D = $Sprite2D
@onready var collision_shape: CollisionShape2D = $CollisionShape2D

func _ready() -> void:
	if plane_type == PlaneType.FRIENDLY:
		add_to_group("friendly_plane")
	else:
		add_to_group("enemy_plane")
		add_to_group("enemy")
	add_to_group("plane")
	bomb_timer = randf_range(1.0, bomb_interval)
	body_entered.connect(_on_body_entered)

func setup(start_pos: Vector2, dir: float, spd: float) -> void:
	global_position = start_pos
	direction = dir
	speed = spd
	if direction < 0:
		sprite.flip_h = true

func _physics_process(delta: float) -> void:
	if is_shot_down:
		_process_falling(delta)
		return

	# Normal flight
	position.x += speed * direction * delta * GameManager.plane_speed_multiplier

	# Slight bobbing
	position.y += sin(Time.get_ticks_msec() * 0.003) * 0.3

	# Bomb dropping
	if plane_type != PlaneType.FRIENDLY:
		bomb_timer -= delta * GameManager.bomb_rate_multiplier
		if bomb_timer <= 0:
			drop_bomb()
			bomb_timer = bomb_interval

	# Off screen - remove
	if (direction > 0 and position.x > 1400) or (direction < 0 and position.x < -120):
		queue_free()

func _process_falling(delta: float) -> void:
	fall_speed += 300 * delta
	position.y += fall_speed * delta
	rotation += fall_rotation_speed * delta

	# Smoke trail
	if randi() % 3 == 0:
		_emit_smoke()

	# Damage player or enemy soldiers on impact during fall
	_check_fall_damage()

	# Check terrain collision for crash
	if terrain and position.y > 0:
		if terrain.is_solid(int(position.x), int(position.y)) or position.y > 700:
			_crash()
			return

	# Off screen bottom
	if position.y > 750:
		_crash()

func _check_fall_damage() -> void:
	# Kill player if falling plane hits them
	var players := get_tree().get_nodes_in_group("player")
	for p in players:
		if is_instance_valid(p):
			if global_position.distance_to(p.global_position) < 40.0:
				if p.has_method("take_damage"):
					p.take_damage()
	# Kill enemy soldiers if falling plane hits them
	var soldiers := get_tree().get_nodes_in_group("enemy_soldier")
	for s in soldiers:
		if is_instance_valid(s):
			if global_position.distance_to(s.global_position) < 40.0:
				s.queue_free()

func take_hit() -> void:
	health -= 1
	# Flash white
	sprite.modulate = Color.WHITE
	var tween := create_tween()
	tween.tween_property(sprite, "modulate", _get_plane_color(), 0.15)

	if health <= 0:
		shoot_down()

func shoot_down() -> void:
	if is_shot_down:
		return
	is_shot_down = true
	fall_speed = 50.0
	fall_rotation_speed = randf_range(1.5, 3.5) * direction

	# Score
	match plane_type:
		PlaneType.BLUE: GameManager.add_score(100)
		PlaneType.GREEN: GameManager.add_score(150)
		PlaneType.RED: GameManager.add_score(200)
		PlaneType.YELLOW: GameManager.add_score(120)
		PlaneType.FRIENDLY: GameManager.add_score(50)

	shot_down.emit(self)

func _crash() -> void:
	# Only some planes become terrain (30% chance)
	if terrain and plane_image != null and randf() < 0.3:
		_embed_plane_in_terrain()
	elif terrain and randf() < 0.15:
		# Rare fallback rectangle
		var crash_rect := Rect2i(
			int(position.x) - 30, int(position.y) - 8,
			60, 16
		)
		terrain.add_terrain_rect(crash_rect, Color(0.3, 0.3, 0.35))

	crashed.emit(self, global_position)
	# Drop loot
	if not has_dropped_loot:
		_try_drop_loot()
	queue_free()

func _embed_plane_in_terrain() -> void:
	# Paint the original (unrotated) plane sprite onto terrain at crash position
	var img_w := plane_image.get_width()
	var img_h := plane_image.get_height()
	var cx := int(position.x)
	# Find the terrain surface at crash x to place plane flush
	var surface_y := cx
	if terrain.has_method("get_surface_y"):
		surface_y = terrain.get_surface_y(cx)
	else:
		surface_y = int(position.y)
	# Center the plane image vertically at the surface
	var cy := surface_y - img_h / 2
	var flip := direction < 0

	for py in range(img_h):
		for px in range(img_w):
			var src_x := (img_w - 1 - px) if flip else px
			var pixel := plane_image.get_pixel(src_x, py)
			if pixel.a > 0.1:
				var tx := cx - img_w / 2 + px
				var ty := cy + py
				if tx >= 0 and tx < 1280 and ty >= 0 and ty < 720:
					terrain.terrain_image.set_pixel(tx, ty, pixel)
					terrain.collision_data[ty * 1280 + tx] = 1
	terrain.needs_collision_update = true
	terrain.update_texture()

func _try_drop_loot() -> void:
	has_dropped_loot = true
	# POW drop chance (enemy only)
	if plane_type != PlaneType.FRIENDLY and randf() < 0.12:
		_spawn_drop("pow")
	# Friendly plane always drops powerup
	elif plane_type == PlaneType.FRIENDLY:
		var powerups := ["jump_boots", "chain_rocket", "emp", "pierce"]
		_spawn_drop(powerups[randi() % powerups.size()])

func _spawn_drop(drop_type: String) -> void:
	# This is connected to the main scene which handles spawning
	var main := get_tree().current_scene
	if main.has_method("spawn_pickup"):
		main.spawn_pickup(global_position, drop_type)

func drop_bomb() -> void:
	var main := get_tree().current_scene
	if main.has_method("spawn_bomb"):
		main.spawn_bomb(global_position, plane_type)

func _emit_smoke() -> void:
	var smoke := GPUParticles2D.new()
	smoke.emitting = true
	smoke.one_shot = true
	smoke.amount = 4
	smoke.lifetime = 0.6
	smoke.global_position = global_position

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 30.0
	mat.initial_velocity_min = 10.0
	mat.initial_velocity_max = 30.0
	mat.gravity = Vector3(0, -50, 0)
	mat.scale_min = 3.0
	mat.scale_max = 6.0
	mat.color = Color(0.3, 0.3, 0.3, 0.6)
	smoke.process_material = mat

	get_tree().current_scene.add_child(smoke)
	var t := get_tree().create_timer(1.5)
	t.timeout.connect(smoke.queue_free)

func _get_plane_color() -> Color:
	match plane_type:
		PlaneType.BLUE: return Color(0.3, 0.5, 1.0)
		PlaneType.GREEN: return Color(0.3, 0.8, 0.3)
		PlaneType.RED: return Color(1.0, 0.3, 0.3)
		PlaneType.YELLOW: return Color(1.0, 0.9, 0.2)
		PlaneType.FRIENDLY: return Color(1.0, 1.0, 1.0)
	return Color.WHITE

func _on_body_entered(body: Node2D) -> void:
	# Plane crashes into player - damage them
	if body.is_in_group("player") and body.has_method("take_damage"):
		body.take_damage()
