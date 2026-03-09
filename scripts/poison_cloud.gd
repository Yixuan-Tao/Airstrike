# poison_cloud.gd - Yellow poison gas cloud
extends Area2D

var lifetime: float = 5.0
var timer: float = 0.0
var radius: float = 60.0

func _ready() -> void:
	add_to_group("hazard")
	_create_visual()

func setup(pos: Vector2) -> void:
	global_position = pos

func _process(delta: float) -> void:
	timer += delta
	# Expand then shrink
	var progress := timer / lifetime
	var scale_factor: float
	if progress < 0.3:
		scale_factor = progress / 0.3
	elif progress < 0.7:
		scale_factor = 1.0
	else:
		scale_factor = 1.0 - (progress - 0.7) / 0.3
	scale = Vector2(scale_factor, scale_factor) * 1.5

	# Fade out
	modulate.a = 1.0 - progress * 0.5

	if timer >= lifetime:
		queue_free()

func _create_visual() -> void:
	# Yellow-green gas cloud using multiple circles
	for i in range(5):
		var circle := Sprite2D.new()
		var img := Image.create(32, 32, false, Image.FORMAT_RGBA8)
		for y in range(32):
			for x in range(32):
				var dx := x - 16
				var dy := y - 16
				var dist := sqrt(dx * dx + dy * dy)
				if dist < 14:
					var alpha := (1.0 - dist / 14.0) * 0.4
					img.set_pixel(x, y, Color(0.8, 0.85, 0.1, alpha))
		var tex := ImageTexture.create_from_image(img)
		circle.texture = tex
		circle.position = Vector2(randf_range(-20, 20), randf_range(-15, 15))
		circle.scale = Vector2(randf_range(1.5, 3.0), randf_range(1.5, 3.0))
		add_child.call_deferred(circle)

	# Collision
	var col := CollisionShape2D.new()
	var shape := CircleShape2D.new()
	shape.radius = radius
	col.shape = shape
	add_child.call_deferred(col)

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("player"):
		if body.has_method("take_damage"):
			body.take_damage()
		if body.has_method("apply_poison"):
			body.apply_poison()

func _on_area_entered(area: Area2D) -> void:
	pass
