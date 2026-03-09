# game_manager.gd - Global game state manager
extends Node

signal lives_changed(lives: int)
signal score_changed(score: int)
signal wave_changed(wave: int)
signal powerup_acquired(type: String)
signal game_over
signal game_won

# Game state
var lives: int = 3:
	set(value):
		lives = value
		lives_changed.emit(lives)
		if lives <= 0:
			game_over.emit()

var score: int = 0:
	set(value):
		score = value
		score_changed.emit(score)

var current_wave: int = 0:
	set(value):
		current_wave = value
		wave_changed.emit(current_wave)

# Power-up states
var has_jump_boots: bool = false
var burst_shot_count: int = 1  # How many rockets per click (base=1, each chain pickup adds +1)
var pierce_count: int = 0  # How many times each rocket can pierce through enemies
var is_game_over: bool = false
var is_game_won: bool = false
var is_paused: bool = false

# Wave config
var max_waves: int = 20
var wave_interval: float = 8.0
var plane_speed_multiplier: float = 1.0
var bomb_rate_multiplier: float = 1.0

# Wave config data loaded from CSV
# Each entry: {blue: int, green: int, red: int, yellow: int, friendly: int}
var wave_config: Array[Dictionary] = []

# Map selection (persists between scene changes)
var selected_map_type: int = 0
var custom_terrain_data: PackedByteArray

func _ready() -> void:
	load_wave_config()
	reset_game()

func load_wave_config() -> void:
	wave_config.clear()
	# Try loading from CSV override first
	var path := "res://assets/wave_config.csv"
	if FileAccess.file_exists(path):
		var file := FileAccess.open(path, FileAccess.READ)
		if file != null:
			var header := file.get_csv_line()  # Skip header
			while not file.eof_reached():
				var line := file.get_csv_line()
				if line.size() < 6:
					continue
				var wave_num := line[0].strip_edges()
				if wave_num == "" or not wave_num.is_valid_int():
					continue
				var entry := {
					"blue": int(line[1].strip_edges()),
					"green": int(line[2].strip_edges()),
					"red": int(line[3].strip_edges()),
					"yellow": int(line[4].strip_edges()),
					"friendly": int(line[5].strip_edges()),
				}
				wave_config.append(entry)
			file.close()
	# If CSV didn't load anything, use hardcoded default
	if wave_config.size() == 0:
		_generate_default_wave_config()
	max_waves = wave_config.size()

func _generate_default_wave_config() -> void:
	# Default 20-wave config
	# Format: {blue, green, red, yellow, friendly}
	var data := [
		[2, 0, 0, 0, 1],   # Wave 1
		[2, 1, 0, 0, 1],   # Wave 2
		[3, 1, 0, 1, 1],   # Wave 3
		[3, 1, 1, 1, 1],   # Wave 4
		[4, 2, 1, 1, 1],   # Wave 5
		[4, 2, 1, 2, 1],   # Wave 6
		[5, 2, 2, 2, 1],   # Wave 7
		[5, 3, 2, 2, 2],   # Wave 8
		[6, 3, 2, 3, 2],   # Wave 9
		[6, 3, 3, 3, 2],   # Wave 10
		[7, 4, 3, 3, 2],   # Wave 11
		[7, 4, 3, 4, 2],   # Wave 12
		[8, 4, 4, 4, 2],   # Wave 13
		[8, 5, 4, 4, 2],   # Wave 14
		[9, 5, 4, 5, 3],   # Wave 15
		[9, 5, 5, 5, 3],   # Wave 16
		[10, 6, 5, 5, 3],  # Wave 17
		[10, 6, 5, 6, 3],  # Wave 18
		[12, 7, 6, 6, 3],  # Wave 19
		[14, 8, 7, 7, 3],  # Wave 20
	]
	for d in data:
		wave_config.append({
			"blue": d[0],
			"green": d[1],
			"red": d[2],
			"yellow": d[3],
			"friendly": d[4],
		})

func reset_game() -> void:
	lives = 3
	score = 0
	current_wave = 0
	has_jump_boots = false
	burst_shot_count = 1
	pierce_count = 0
	is_game_over = false
	is_game_won = false
	is_paused = false
	plane_speed_multiplier = 1.0
	bomb_rate_multiplier = 1.0

func start_next_wave() -> void:
	current_wave += 1
	# Increase difficulty
	plane_speed_multiplier = 1.0 + (current_wave - 1) * 0.08
	bomb_rate_multiplier = 1.0 + (current_wave - 1) * 0.05
	wave_interval = max(3.0, 8.0 - current_wave * 0.3)

func get_wave_planes(wave_num: int) -> Dictionary:
	# wave_num is 1-based
	var idx := wave_num - 1
	if idx >= 0 and idx < wave_config.size():
		return wave_config[idx]
	# Beyond config: scale last wave
	if wave_config.size() > 0:
		var last := wave_config[wave_config.size() - 1].duplicate()
		var extra := wave_num - wave_config.size()
		last["blue"] += extra
		last["green"] += extra / 2
		last["red"] += extra / 2
		last["yellow"] += extra / 2
		return last
	return {"blue": 3, "green": 1, "red": 1, "yellow": 1, "friendly": 1}

func get_total_planes_this_wave() -> int:
	var cfg := get_wave_planes(current_wave)
	return cfg["blue"] + cfg["green"] + cfg["red"] + cfg["yellow"] + cfg["friendly"]

func add_score(amount: int) -> void:
	score += amount

func lose_life() -> void:
	if not is_game_over:
		lives -= 1
		if lives <= 0:
			is_game_over = true

func gain_life() -> void:
	lives += 1

func acquire_powerup(type: String) -> void:
	match type:
		"jump_boots":
			has_jump_boots = true
		"chain_rocket":
			burst_shot_count += 1  # Each pickup adds +1 rocket per click
		"pierce":
			pierce_count += 1  # Each pickup adds +1 pierce
		"emp":
			pass # Handled by caller
	powerup_acquired.emit(type)

func toggle_pause() -> void:
	is_paused = not is_paused
	get_tree().paused = is_paused
