# shrapnel.gd - Steel ball from blue plane's bomb
extends Area2D

var velocity: Vector2 = Vector2.ZERO
var fall_gravity: float = 400.0
var terrain: Node2D
var damage_radius: float = 10.0

func _ready() -> void:
	add_to_group("enemy")
	add_to_group("enemy_bomb")
	get_tree().create_timer(4.0).timeout.connect(queue_free)

func setup(start_pos: Vector2, vel: Vector2) -> void:
	global_position = start_pos
	velocity = vel

func _physics_process(delta: float) -> void:
	velocity.y += fall_gravity * delta
	position += velocity * delta

	# Terrain collision
	if terrain and terrain.is_solid(int(position.x), int(position.y)):
		if terrain:
			terrain.destroy_circle(position, damage_radius)
		_explode()
		return

	if position.y > 750 or position.x < -50 or position.x > 1330:
		queue_free()

func take_hit() -> void:
	GameManager.add_score(10)
	queue_free()

func _explode() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 6
	particles.lifetime = 0.3
	particles.global_position = global_position
	var mat := ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 20.0
	mat.initial_velocity_max = 60.0
	mat.gravity = Vector3(0, 100, 0)
	mat.scale_min = 1.0
	mat.scale_max = 3.0
	mat.color = Color(0.7, 0.7, 0.7)
	particles.process_material = mat
	get_tree().current_scene.add_child(particles)
	get_tree().create_timer(1.0).timeout.connect(particles.queue_free)
	queue_free()

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("player"):
		if body.has_method("take_damage"):
			body.take_damage()
		_explode()
