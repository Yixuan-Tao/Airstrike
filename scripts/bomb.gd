# bomb.gd - Various bomb types dropped by enemy planes
extends Area2D

enum BombType { BLUE_SHRAPNEL, GREEN_SOLDIER, RED_HEAVY, YELLOW_POISON }

@export var bomb_type: BombType = BombType.BLUE_SHRAPNEL
var velocity: Vector2 = Vector2.ZERO
var fall_gravity: float = 300.0
var terrain: Node2D
var is_active: bool = true

func _ready() -> void:
	add_to_group("enemy_bomb")
	add_to_group("enemy")

func setup(start_pos: Vector2, type: int) -> void:
	global_position = start_pos
	bomb_type = type
	velocity = Vector2(randf_range(-20, 20), 50)

	# Color based on type
	match bomb_type:
		BombType.BLUE_SHRAPNEL:
			modulate = Color(0.4, 0.6, 1.0)
		BombType.GREEN_SOLDIER:
			modulate = Color(0.3, 0.9, 0.3)
		BombType.RED_HEAVY:
			modulate = Color(1.0, 0.3, 0.2)
			# Bigger bomb visual
			scale = Vector2(1.3, 1.3)
		BombType.YELLOW_POISON:
			modulate = Color(1.0, 0.9, 0.2)

func _physics_process(delta: float) -> void:
	if not is_active:
		return

	velocity.y += fall_gravity * delta
	position += velocity * delta

	# Check terrain collision
	if terrain and terrain.is_solid(int(position.x), int(position.y)):
		detonate()
		return

	# Out of bounds
	if position.y > 750:
		detonate()

func take_hit() -> void:
	# Player shot this bomb
	GameManager.add_score(25)
	_spawn_small_explosion()
	queue_free()

func detonate() -> void:
	if not is_active:
		return
	is_active = false

	match bomb_type:
		BombType.BLUE_SHRAPNEL:
			_detonate_shrapnel()
		BombType.GREEN_SOLDIER:
			_detonate_soldier()
		BombType.RED_HEAVY:
			_detonate_heavy()
		BombType.YELLOW_POISON:
			_detonate_poison()

	queue_free()

func _detonate_shrapnel() -> void:
	# Small terrain destruction + spawn 3 shrapnel balls
	if terrain:
		terrain.destroy_circle(position, 15.0)
	_spawn_small_explosion()
	_damage_nearby_player(15.0)

	# Spawn 3 steel balls
	for i in range(3):
		var main := get_tree().current_scene
		if main.has_method("spawn_shrapnel"):
			var angle := randf_range(-PI * 0.8, -PI * 0.2)
			var spd := randf_range(150, 280)
			var vel := Vector2(cos(angle), sin(angle)) * spd
			main.spawn_shrapnel(position, vel)

func _detonate_soldier() -> void:
	# Spawn a homing soldier
	var main := get_tree().current_scene
	if main.has_method("spawn_enemy_soldier"):
		main.spawn_enemy_soldier(position)
	_spawn_small_explosion()

func _detonate_heavy() -> void:
	# Large terrain destruction
	if terrain:
		terrain.destroy_circle(position, 50.0)
	_spawn_big_explosion()
	_damage_nearby_player(50.0)

func _detonate_poison() -> void:
	# No terrain destruction, creates poison cloud
	var main := get_tree().current_scene
	if main.has_method("spawn_poison_cloud"):
		main.spawn_poison_cloud(position)
	_spawn_yellow_explosion()

func _spawn_small_explosion() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 16
	particles.lifetime = 0.5
	particles.global_position = global_position

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 180.0
	mat.initial_velocity_min = 40.0
	mat.initial_velocity_max = 100.0
	mat.gravity = Vector3(0, 200, 0)
	mat.scale_min = 2.0
	mat.scale_max = 5.0
	mat.color = Color(1.0, 0.5, 0.1)
	particles.process_material = mat

	get_tree().current_scene.add_child(particles)
	get_tree().create_timer(1.5).timeout.connect(particles.queue_free)

func _spawn_big_explosion() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 40
	particles.lifetime = 0.8
	particles.global_position = global_position

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 180.0
	mat.initial_velocity_min = 80.0
	mat.initial_velocity_max = 200.0
	mat.gravity = Vector3(0, 300, 0)
	mat.scale_min = 4.0
	mat.scale_max = 10.0
	mat.color = Color(1.0, 0.3, 0.0)
	particles.process_material = mat

	get_tree().current_scene.add_child(particles)
	get_tree().create_timer(2.0).timeout.connect(particles.queue_free)

func _spawn_yellow_explosion() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 20
	particles.lifetime = 0.6
	particles.global_position = global_position

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 180.0
	mat.initial_velocity_min = 30.0
	mat.initial_velocity_max = 80.0
	mat.gravity = Vector3(0, 50, 0)
	mat.scale_min = 4.0
	mat.scale_max = 8.0
	mat.color = Color(0.9, 0.85, 0.1, 0.7)
	particles.process_material = mat

	get_tree().current_scene.add_child(particles)
	get_tree().create_timer(2.0).timeout.connect(particles.queue_free)

func _on_body_entered(body: Node2D) -> void:
	# Direct hit on player - kill them
	if body.is_in_group("player") and body.has_method("take_damage"):
		body.take_damage()
		detonate()

func _damage_nearby_player(radius: float) -> void:
	# Check if player is within explosion radius and damage them
	var players := get_tree().get_nodes_in_group("player")
	for p in players:
		if is_instance_valid(p):
			var dist := global_position.distance_to(p.global_position)
			if dist <= radius * 1.5 and p.has_method("take_damage"):
				p.take_damage()
