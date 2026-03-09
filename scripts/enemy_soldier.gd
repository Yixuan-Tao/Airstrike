# enemy_soldier.gd - Homing soldier dropped by green plane
extends CharacterBody2D

const SPEED: float = 60.0
const GRAVITY: float = 600.0
const JUMP_VELOCITY: float = -250.0
var health: int = 1
var target: Node2D
var terrain: Node2D
var jump_timer: float = 0.0

@onready var sprite: Sprite2D = $Sprite2D

func _ready() -> void:
	add_to_group("enemy")
	add_to_group("enemy_soldier")
	# Find player
	var players := get_tree().get_nodes_in_group("player")
	if players.size() > 0:
		target = players[0]

func _physics_process(delta: float) -> void:
	# Gravity
	if not is_on_floor():
		velocity.y += GRAVITY * delta
	else:
		velocity.y = 0

	# Chase player
	if target and is_instance_valid(target):
		var dir: float = sign(target.global_position.x - global_position.x)
		velocity.x = dir * SPEED
		sprite.flip_h = dir < 0

		# Jump over obstacles
		jump_timer += delta
		if is_on_floor() and jump_timer > 0.8:
			# Check if blocked
			var check_x := int(global_position.x + dir * 20)
			var check_y := int(global_position.y)
			if terrain and terrain.is_solid(check_x, check_y):
				velocity.y = JUMP_VELOCITY
				jump_timer = 0.0

		# Check if close enough to damage player
		if global_position.distance_to(target.global_position) < 25:
			if target.has_method("take_damage"):
				target.take_damage()
			queue_free()
	else:
		velocity.x = 0

	move_and_slide()

	# Off screen
	if position.y > 750:
		queue_free()

func take_hit() -> void:
	health -= 1
	if health <= 0:
		GameManager.add_score(50)
		queue_free()
