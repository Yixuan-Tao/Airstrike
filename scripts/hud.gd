# hud.gd - Heads-up display with pause menu
extends CanvasLayer

@onready var lives_label: Label = $MarginContainer/HBoxContainer/LivesLabel
@onready var score_label: Label = $MarginContainer/HBoxContainer/ScoreLabel
@onready var wave_label: Label = $MarginContainer/HBoxContainer/WaveLabel
@onready var powerup_container: HBoxContainer = $MarginContainer/HBoxContainer/PowerupContainer
@onready var game_over_panel: PanelContainer = $GameOverPanel
@onready var game_over_score: Label = $GameOverPanel/VBoxContainer/FinalScore
@onready var restart_button: Button = $GameOverPanel/VBoxContainer/RestartButton

var pause_panel: PanelContainer

func _ready() -> void:
	GameManager.lives_changed.connect(_on_lives_changed)
	GameManager.score_changed.connect(_on_score_changed)
	GameManager.wave_changed.connect(_on_wave_changed)
	GameManager.powerup_acquired.connect(_on_powerup_acquired)
	GameManager.game_over.connect(_on_game_over)
	GameManager.game_won.connect(_on_game_won)
	game_over_panel.visible = false
	restart_button.pressed.connect(_on_restart)
	_update_display()
	_build_pause_menu()

func _build_pause_menu() -> void:
	pause_panel = PanelContainer.new()
	pause_panel.name = "PausePanel"
	pause_panel.anchors_preset = Control.PRESET_CENTER
	pause_panel.anchor_left = 0.5
	pause_panel.anchor_top = 0.5
	pause_panel.anchor_right = 0.5
	pause_panel.anchor_bottom = 0.5
	pause_panel.offset_left = -120.0
	pause_panel.offset_top = -100.0
	pause_panel.offset_right = 120.0
	pause_panel.offset_bottom = 100.0
	pause_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	pause_panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	pause_panel.visible = false
	# Must process when paused
	pause_panel.process_mode = Node.PROCESS_MODE_ALWAYS

	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.15, 0.9)
	style.border_color = Color(0.5, 0.5, 0.6)
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_width_top = 2
	style.border_width_bottom = 2
	style.content_margin_left = 16
	style.content_margin_right = 16
	style.content_margin_top = 16
	style.content_margin_bottom = 16
	pause_panel.add_theme_stylebox_override("panel", style)

	var vbox := VBoxContainer.new()
	vbox.layout_mode = 2
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	vbox.add_theme_constant_override("separation", 16)
	pause_panel.add_child(vbox)

	var title := Label.new()
	title.text = "PAUSED"
	title.add_theme_font_size_override("font_size", 32)
	title.add_theme_color_override("font_color", Color(1, 0.8, 0.2))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(title)

	var resume_btn := Button.new()
	resume_btn.text = "RESUME"
	resume_btn.add_theme_font_size_override("font_size", 18)
	resume_btn.pressed.connect(_on_resume)
	resume_btn.process_mode = Node.PROCESS_MODE_ALWAYS
	vbox.add_child(resume_btn)

	var menu_btn := Button.new()
	menu_btn.text = "MAIN MENU"
	menu_btn.add_theme_font_size_override("font_size", 18)
	menu_btn.pressed.connect(_on_return_to_menu)
	menu_btn.process_mode = Node.PROCESS_MODE_ALWAYS
	vbox.add_child(menu_btn)

	add_child(pause_panel)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		if GameManager.is_game_over or GameManager.is_game_won:
			return
		GameManager.toggle_pause()
		pause_panel.visible = GameManager.is_paused
		get_viewport().set_input_as_handled()

func _on_resume() -> void:
	GameManager.toggle_pause()
	pause_panel.visible = false

func _on_return_to_menu() -> void:
	# Unpause before changing scene
	if GameManager.is_paused:
		GameManager.toggle_pause()
	GameManager.reset_game()
	get_tree().change_scene_to_file("res://scenes/map_select.tscn")

func _update_display() -> void:
	lives_label.text = "LIVES: %d" % GameManager.lives
	score_label.text = "SCORE: %d" % GameManager.score
	wave_label.text = "WAVE: %d/%d" % [GameManager.current_wave, GameManager.max_waves]

func _on_lives_changed(lives: int) -> void:
	lives_label.text = "LIVES: %d" % lives
	if lives < 3:
		var tween := create_tween()
		lives_label.modulate = Color.RED
		tween.tween_property(lives_label, "modulate", Color.WHITE, 0.5)

func _on_score_changed(new_score: int) -> void:
	score_label.text = "SCORE: %d" % new_score

func _on_wave_changed(wave: int) -> void:
	wave_label.text = "WAVE: %d/%d" % [wave, GameManager.max_waves]
	# Wave announcement
	var announce := Label.new()
	announce.text = "WAVE %d" % wave
	announce.add_theme_font_size_override("font_size", 48)
	announce.add_theme_color_override("font_color", Color(1, 0.8, 0.2))
	announce.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	announce.anchors_preset = Control.PRESET_CENTER
	announce.position = Vector2(440, 200)
	add_child(announce)
	var tween := create_tween()
	tween.tween_property(announce, "modulate:a", 0.0, 2.0)
	tween.tween_callback(announce.queue_free)

func _on_powerup_acquired(type: String) -> void:
	var popup := Label.new()
	match type:
		"jump_boots":
			popup.text = "JUMP BOOTS!"
			popup.add_theme_color_override("font_color", Color(0.2, 0.8, 1.0))
		"chain_rocket":
			popup.text = "BURST +1! (%d/click)" % GameManager.burst_shot_count
			popup.add_theme_color_override("font_color", Color(1.0, 0.5, 0.1))
		"pierce":
			popup.text = "PIERCE +1! (%d)" % GameManager.pierce_count
			popup.add_theme_color_override("font_color", Color(0.2, 1.0, 0.5))
		"emp":
			popup.text = "EMP BLAST!"
			popup.add_theme_color_override("font_color", Color(0.8, 0.3, 1.0))

	popup.add_theme_font_size_override("font_size", 32)
	popup.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	popup.position = Vector2(480, 280)
	add_child(popup)
	var tween := create_tween()
	tween.tween_property(popup, "position:y", 240, 1.0)
	tween.parallel().tween_property(popup, "modulate:a", 0.0, 1.5)
	tween.tween_callback(popup.queue_free)

func _on_game_over() -> void:
	game_over_panel.visible = true
	game_over_score.text = "FINAL SCORE: %d\nWAVE: %d/%d" % [GameManager.score, GameManager.current_wave, GameManager.max_waves]

func _on_game_won() -> void:
	game_over_panel.visible = true
	var game_over_label: Label = game_over_panel.get_node("VBoxContainer/GameOverLabel")
	game_over_label.text = "VICTORY!"
	game_over_label.add_theme_color_override("font_color", Color(1, 0.8, 0.2))
	game_over_score.text = "FINAL SCORE: %d\nALL %d WAVES CLEARED!" % [GameManager.score, GameManager.max_waves]

func _on_restart() -> void:
	GameManager.reset_game()
	get_tree().change_scene_to_file("res://scenes/map_select.tscn")
