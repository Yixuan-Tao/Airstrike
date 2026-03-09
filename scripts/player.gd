# player.gd - Player character with movement, jumping, and rocket shooting
extends CharacterBody2D

signal rocket_fired(pos: Vector2, target: Vector2)
signal player_died(pos: Vector2)

const SPEED: float = 200.0
const JUMP_VELOCITY: float = -350.0
const GRAVITY: float = 980.0
const MAX_FALL_SPEED: float = 600.0
const HALF_HEIGHT: float = 12.0  # Half player collision box height
const STEP_HEIGHT: int = 12  # Max slope step-up in pixels (half player height)

var terrain: Node2D  # Reference to destructible terrain
var facing_right: bool = true
var is_invincible: bool = false
var invincible_timer: float = 0.0
const INVINCIBLE_DURATION: float = 2.0

# Shooting - burst system
var can_shoot: bool = true
var shoot_cooldown: float = 0.8  # CD between bursts
var shoot_timer: float = 0.0
var burst_remaining: int = 0  # Rockets left in current burst
var burst_target: Vector2 = Vector2.ZERO  # Target for current burst
var burst_delay: float = 0.06  # Tiny delay between burst shots (visual only)
var burst_delay_timer: float = 0.0

# Poison state
var is_poisoned: bool = false
var poison_timer: float = 0.0
var poison_damage_timer: float = 0.0
const POISON_DURATION: float = 4.0
const POISON_TICK: float = 1.0

@onready var sprite: Sprite2D = $Sprite2D
@onready var collision_shape: CollisionShape2D = $CollisionShape2D
@onready var muzzle: Marker2D = $Muzzle
@onready var anim_player: AnimationPlayer = $AnimationPlayer

func _ready() -> void:
	add_to_group("player")
	_create_player_sprite()

func _create_player_sprite() -> void:
	# Procedural pixel art soldier with rocket launcher
	var img := Image.create(12, 16, false, Image.FORMAT_RGBA8)
	var skin := Color(0.85, 0.7, 0.55)
	var helmet := Color(0.25, 0.35, 0.2)
	var body := Color(0.3, 0.45, 0.25)
	var pants := Color(0.35, 0.35, 0.25)
	var boot := Color(0.2, 0.15, 0.1)
	var launcher := Color(0.4, 0.4, 0.45)

	# Helmet (rows 0-2)
	for x in range(3, 9):
		img.set_pixel(x, 0, helmet)
	for x in range(2, 10):
		img.set_pixel(x, 1, helmet)
		img.set_pixel(x, 2, helmet)

	# Face (rows 3-4)
	for x in range(3, 9):
		img.set_pixel(x, 3, skin)
		img.set_pixel(x, 4, skin)
	# Eyes
	img.set_pixel(4, 3, Color(0.1, 0.1, 0.1))
	img.set_pixel(7, 3, Color(0.1, 0.1, 0.1))

	# Body (rows 5-9)
	for y in range(5, 10):
		for x in range(3, 9):
			img.set_pixel(x, y, body)

	# Rocket launcher on shoulder (right side)
	for x in range(8, 12):
		img.set_pixel(x, 5, launcher)
		img.set_pixel(x, 6, launcher)
	# Launcher tip
	img.set_pixel(11, 4, Color(0.5, 0.2, 0.1))
	img.set_pixel(11, 5, launcher)

	# Arms
	img.set_pixel(2, 6, skin)
	img.set_pixel(2, 7, skin)
	img.set_pixel(9, 6, skin)
	img.set_pixel(9, 7, skin)

	# Pants (rows 10-12)
	for y in range(10, 13):
		for x in range(3, 5):
			img.set_pixel(x, y, pants)
		for x in range(7, 9):
			img.set_pixel(x, y, pants)

	# Boots (rows 13-15)
	for y in range(13, 16):
		for x in range(2, 5):
			img.set_pixel(x, y, boot)
		for x in range(7, 10):
			img.set_pixel(x, y, boot)

	sprite.texture = ImageTexture.create_from_image(img)

var on_ground: bool = false  # Track grounding via terrain pixels

func _physics_process(delta: float) -> void:
	if GameManager.is_game_over:
		return

	# Gravity - always apply, terrain collision handles grounding
	velocity.y += GRAVITY * delta
	velocity.y = minf(velocity.y, MAX_FALL_SPEED)

	# Movement
	var direction := Input.get_axis("move_left", "move_right")
	if direction != 0:
		velocity.x = direction * SPEED
		facing_right = direction > 0
		sprite.flip_h = not facing_right
		muzzle.position.x = abs(muzzle.position.x) * (1 if facing_right else -1)
	else:
		velocity.x = move_toward(velocity.x, 0, SPEED * 5 * delta)

	# Jump - use our own on_ground flag, not is_on_floor()
	if Input.is_action_just_pressed("jump"):
		if on_ground:
			velocity.y = JUMP_VELOCITY
			on_ground = false
		elif GameManager.has_jump_boots:
			velocity.y = JUMP_VELOCITY * 0.85

	# Shooting cooldown
	if not can_shoot:
		shoot_timer += delta
		if shoot_timer >= shoot_cooldown:
			can_shoot = true
			shoot_timer = 0.0

	# Burst firing: fire remaining burst shots with tiny visual delay
	if burst_remaining > 0:
		burst_delay_timer += delta
		if burst_delay_timer >= burst_delay:
			burst_delay_timer = 0.0
			burst_remaining -= 1
			# Slight spread for burst shots
			var spread := Vector2(randf_range(-15, 15), randf_range(-15, 15))
			rocket_fired.emit(muzzle.global_position, burst_target + spread)

	# Invincibility timer
	if is_invincible:
		invincible_timer += delta
		sprite.modulate.a = 0.3 if fmod(invincible_timer, 0.2) < 0.1 else 1.0
		if invincible_timer >= INVINCIBLE_DURATION:
			is_invincible = false
			invincible_timer = 0.0
			sprite.modulate.a = 1.0

	# Poison effect
	if is_poisoned:
		poison_timer += delta
		poison_damage_timer += delta
		sprite.modulate = Color(0.7, 0.9, 0.3)
		if poison_damage_timer >= POISON_TICK:
			poison_damage_timer = 0.0
		if poison_timer >= POISON_DURATION:
			is_poisoned = false
			poison_timer = 0.0
			poison_damage_timer = 0.0
			if not is_invincible:
				sprite.modulate = Color.WHITE

	# Apply velocity to position directly (skip move_and_slide for terrain)
	position += velocity * delta

	# Terrain collision AFTER position update
	_check_terrain_collision()

	# Keep in bounds
	position.x = clampf(position.x, 16, 1264)
	if position.y > 750:
		take_damage()
		position = _find_safe_spawn()

func _unhandled_input(event: InputEvent) -> void:
	if GameManager.is_game_over:
		return
	if event is InputEventMouseButton:
		if event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			shoot(get_global_mouse_position())

func shoot(target_pos: Vector2) -> void:
	if not can_shoot:
		return
	can_shoot = false
	shoot_timer = 0.0

	# Fire first rocket immediately
	rocket_fired.emit(muzzle.global_position, target_pos)

	# Queue remaining burst shots (burst_shot_count - 1 more)
	var extra_shots := GameManager.burst_shot_count - 1
	if extra_shots > 0:
		burst_remaining = extra_shots
		burst_target = target_pos
		burst_delay_timer = 0.0

func take_damage() -> void:
	if is_invincible or GameManager.is_game_over:
		return
	GameManager.lose_life()
	if GameManager.lives > 0:
		is_invincible = true
		invincible_timer = 0.0
	else:
		player_died.emit(global_position)

func apply_poison() -> void:
	is_poisoned = true
	poison_timer = 0.0
	poison_damage_timer = 0.0

func _check_terrain_collision() -> void:
	if terrain == null:
		return

	var center_x := int(position.x)
	var feet_y := int(position.y + HALF_HEIGHT)
	var head_y := int(position.y - HALF_HEIGHT)

	# --- Head collision: push down if head hits terrain ---
	if head_y >= 0 and terrain.is_solid(center_x, head_y) and velocity.y < 0:
		velocity.y = 0

	# --- Horizontal wall collision ---
	if velocity.x != 0:
		var move_dir_sign: float = 1.0 if velocity.x > 0 else -1.0
		var check_x := int(position.x + move_dir_sign * 7)
		# Check at waist level
		var waist_y := int(position.y)
		if terrain.is_solid(check_x, waist_y):
			# Wall at waist — try stepping up
			var can_step := false
			for sy in range(feet_y - 1, feet_y - STEP_HEIGHT - 1, -1):
				if sy < 0:
					break
				if not terrain.is_solid(check_x, sy):
					position.y = float(sy) - HALF_HEIGHT
					velocity.y = 0
					on_ground = true
					can_step = true
					break
			if not can_step:
				# Too steep, block movement and undo horizontal motion
				position.x -= velocity.x * get_physics_process_delta_time()
				velocity.x = 0

	# --- Vertical terrain collision ---
	feet_y = int(position.y + HALF_HEIGHT)
	center_x = int(position.x)

	if terrain.is_solid(center_x, feet_y):
		if velocity.y >= 0:
			# Falling or on ground — find surface by scanning up
			for check_y in range(feet_y, feet_y - STEP_HEIGHT * 2 - 4, -1):
				if check_y < 0:
					break
				if not terrain.is_solid(center_x, check_y):
					position.y = float(check_y) - HALF_HEIGHT
					velocity.y = 0
					on_ground = true
					break
		else:
			# Moving upward through terrain (shouldn't happen often)
			# Just push up
			for check_y in range(feet_y, feet_y - 30, -1):
				if check_y < 0:
					break
				if not terrain.is_solid(center_x, check_y):
					position.y = float(check_y) - HALF_HEIGHT
					break
	else:
		# Feet not in terrain — check if we should snap down (small slope)
		if velocity.y >= 0 and on_ground:
			var found_ground := false
			for check_y in range(feet_y + 1, feet_y + STEP_HEIGHT + 4):
				if check_y >= 720:
					break
				if terrain.is_solid(center_x, check_y):
					position.y = float(check_y) - HALF_HEIGHT - 1.0
					velocity.y = 0
					found_ground = true
					break
			if not found_ground:
				on_ground = false
		elif velocity.y > 0:
			on_ground = false

func _find_safe_spawn() -> Vector2:
	if terrain:
		var sx: int = 640
		var sy: int = terrain.get_surface_y(sx)
		return Vector2(sx, sy - 20)
	return Vector2(640, 300)
