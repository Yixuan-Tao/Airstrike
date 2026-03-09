# rocket.gd - Player's rocket projectile with optional piercing
extends Area2D

const SPEED: float = 500.0
var direction: Vector2 = Vector2.RIGHT
var terrain: Node2D
var pierce_remaining: int = 0  # How many more enemies this can pierce through

func _ready() -> void:
	add_to_group("player_projectile")
	# Set pierce from GameManager
	pierce_remaining = GameManager.pierce_count
	# Auto-destroy after 3 seconds
	var timer := get_tree().create_timer(3.0)
	timer.timeout.connect(queue_free)

func setup(start_pos: Vector2, target_pos: Vector2) -> void:
	global_position = start_pos
	direction = (target_pos - start_pos).normalized()
	rotation = direction.angle()

func _physics_process(delta: float) -> void:
	position += direction * SPEED * delta

	# Check terrain collision
	if terrain and terrain.is_solid(int(position.x), int(position.y)):
		explode()

	# Out of bounds
	if position.x < -50 or position.x > 1330 or position.y < -50 or position.y > 770:
		queue_free()

func explode() -> void:
	# Small explosion effect
	if terrain:
		terrain.destroy_circle(position, 8.0)
	_spawn_explosion()
	queue_free()

func _hit_target(target: Node2D) -> void:
	if target.has_method("take_hit"):
		target.take_hit()
	if pierce_remaining > 0:
		pierce_remaining -= 1
		# Don't explode, keep going — just show a small spark
		_spawn_spark()
	else:
		explode()

func _spawn_spark() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 4
	particles.lifetime = 0.2
	particles.global_position = global_position
	var mat := ParticleProcessMaterial.new()
	mat.spread = 90.0
	mat.initial_velocity_min = 30.0
	mat.initial_velocity_max = 60.0
	mat.gravity = Vector3(0, 100, 0)
	mat.scale_min = 1.0
	mat.scale_max = 2.0
	mat.color = Color(1.0, 0.8, 0.3)
	particles.process_material = mat
	get_tree().current_scene.call_deferred("add_child", particles)
	get_tree().create_timer(0.5).timeout.connect(particles.queue_free)

func _spawn_explosion() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 12
	particles.lifetime = 0.4
	particles.global_position = global_position

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.spread = 180.0
	mat.initial_velocity_min = 50.0
	mat.initial_velocity_max = 120.0
	mat.gravity = Vector3(0, 200, 0)
	mat.scale_min = 2.0
	mat.scale_max = 4.0
	mat.color = Color(1.0, 0.6, 0.1)
	particles.process_material = mat

	# Auto-cleanup
	get_tree().current_scene.call_deferred("add_child", particles)
	var cleanup_timer := get_tree().create_timer(1.0)
	cleanup_timer.timeout.connect(particles.queue_free)

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("enemy") or body.is_in_group("enemy_plane"):
		_hit_target(body)

func _on_area_entered(area: Area2D) -> void:
	if area.is_in_group("enemy") or area.is_in_group("enemy_plane") or area.is_in_group("enemy_bomb") or area.is_in_group("friendly_plane"):
		_hit_target(area)
