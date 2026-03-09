# pickup.gd - Collectible items (powerups and POW)
extends Area2D

enum PickupType { JUMP_BOOTS, CHAIN_ROCKET, EMP, POW, PIERCE }

var pickup_type: int = 0
var velocity: Vector2 = Vector2(0, 0)
var fall_gravity: float = 200.0
var terrain: Node2D
var is_on_ground: bool = false
var lifetime: float = 15.0
var blink_time: float = 12.0

@onready var sprite: Sprite2D = $Sprite2D
@onready var label: Label = $Label

func _ready() -> void:
	add_to_group("pickup")
	call_deferred("_setup_visual")

func setup(pos: Vector2, type: int) -> void:
	global_position = pos
	pickup_type = type
	velocity = Vector2(randf_range(-30, 30), -80)
	_setup_visual()

func _setup_visual() -> void:
	var img := Image.create(16, 16, false, Image.FORMAT_RGBA8)
	var color: Color
	var text: String

	match pickup_type:
		PickupType.JUMP_BOOTS:
			color = Color(0.2, 0.8, 1.0)
			text = "B"
		PickupType.CHAIN_ROCKET:
			color = Color(1.0, 0.5, 0.1)
			text = "R"
		PickupType.EMP:
			color = Color(0.8, 0.3, 1.0)
			text = "E"
		PickupType.POW:
			color = Color(0.9, 0.9, 0.9)
			text = "P"
		PickupType.PIERCE:
			color = Color(0.2, 1.0, 0.5)
			text = ">"

	for y in range(16):
		for x in range(16):
			if x == 0 or x == 15 or y == 0 or y == 15:
				img.set_pixel(x, y, Color.WHITE)
			else:
				img.set_pixel(x, y, color)

	sprite.texture = ImageTexture.create_from_image(img)
	sprite.scale = Vector2(2, 2)
	label.text = text
	label.add_theme_font_size_override("font_size", 14)
	label.position = Vector2(-5, -10)

func _physics_process(delta: float) -> void:
	lifetime -= delta

	if lifetime < (15.0 - blink_time):
		visible = fmod(lifetime, 0.3) < 0.15

	if lifetime <= 0:
		queue_free()
		return

	if not is_on_ground:
		velocity.y += fall_gravity * delta
		position += velocity * delta

		if terrain and terrain.is_solid(int(position.x), int(position.y + 8)):
			is_on_ground = true
			velocity = Vector2.ZERO
			for check_y in range(int(position.y + 8), int(position.y) - 10, -1):
				if not terrain.is_solid(int(position.x), check_y):
					position.y = check_y - 8
					break

		if position.y > 750:
			queue_free()

	if is_on_ground:
		sprite.position.y = sin(Time.get_ticks_msec() * 0.005) * 2

func take_hit() -> void:
	if pickup_type == PickupType.POW:
		queue_free()

func collect(_player: Node2D) -> void:
	match pickup_type:
		PickupType.JUMP_BOOTS:
			GameManager.acquire_powerup("jump_boots")
		PickupType.CHAIN_ROCKET:
			GameManager.acquire_powerup("chain_rocket")
		PickupType.EMP:
			GameManager.acquire_powerup("emp")
			for plane in get_tree().get_nodes_in_group("plane"):
				if plane.has_method("shoot_down"):
					plane.shoot_down()
		PickupType.POW:
			GameManager.gain_life()
		PickupType.PIERCE:
			GameManager.acquire_powerup("pierce")

	_spawn_collect_effect()
	queue_free()

func _spawn_collect_effect() -> void:
	var particles := GPUParticles2D.new()
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 10
	particles.lifetime = 0.4
	particles.global_position = global_position
	var mat := ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 30.0
	mat.initial_velocity_max = 80.0
	mat.gravity = Vector3(0, -50, 0)
	mat.scale_min = 2.0
	mat.scale_max = 4.0
	mat.color = Color(1.0, 1.0, 0.5)
	particles.process_material = mat
	get_tree().current_scene.call_deferred("add_child", particles)
	get_tree().create_timer(1.0).timeout.connect(particles.queue_free)

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("player"):
		collect(body)
