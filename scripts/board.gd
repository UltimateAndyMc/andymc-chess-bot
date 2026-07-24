extends Node2D

@onready var button_grid: GridContainer = $ColorRect/ButtonGrid

enum {WK, WQ, WR, WB, WN, WP, BK, BQ, BR, BB, BN, BP, E}

# Turns
const BLACK = false
const WHITE = true

var piece_textures: Array = [
	preload("res://textures/white-king.png"),
	preload("res://textures/white-queen.png"),
	preload("res://textures/white-rook.png"),
	preload("res://textures/white-bishop.png"),
	preload("res://textures/white-knight.png"),
	preload("res://textures/white-pawn.png"),
	preload("res://textures/black-king.png"),
	preload("res://textures/black-queen.png"),
	preload("res://textures/black-rook.png"),
	preload("res://textures/black-bishop.png"),
	preload("res://textures/black-knight.png"),
	preload("res://textures/black-pawn.png"),
	ImageTexture.create_from_image(Image.create(1, 1, false, Image.FORMAT_RGBA8))  # E - empty square, no texture
]

# Data about current position and history:
var board_position = [
	BR, BN, BB, BQ, BK, BB, BN, BR,
	BP, BP, BP, BP, BP, BP, BP, BP,
	E,  E,  E,  E,  E,  E,  E,  E,
	E,  E,  E,  E,  E,  E,  E,  E,
	E,  E,  E,  E,  E,  E,  E,  E,
	E,  E,  E,  E,  E,  E,  E,  E,
	WP, WP, WP, WP, WP, WP, WP, WP,
	WR, WN, WB, WQ, WK, WB, WN, WR
]
var previous_move_start = -1
var previous_move_end = -1

var selected_square: int = -1
var turn: bool = WHITE

func set_piece(number: int, piece):
	button_grid.get_child(number).get_node("Piece").texture = piece_textures[piece]
	
func update_board():
	for i in range(64):
		set_piece(i, board_position[i])

# Legal moves should be sorted in ascending order
func update_allowed_moves(legal_moves: Array):
	var legal_move_index: int = 0
	for i in range(64):
		if (legal_move_index >= len(legal_moves) || legal_moves[legal_move_index] != i):
			button_grid.get_child(i).set_visibility(false)
		else:
			legal_move_index += 1
			button_grid.get_child(i).set_visibility(true)

func is_legal_move(start_square, end_square):
	if start_square == end_square:
		return false
		
	var start_file = start_square % 8
	var start_rank = 8 - (start_square / 8)
	var end_file = end_square % 8
	var end_rank = 8 - (end_square / 8)
	
	var file_change = end_file - start_file
	var rank_change = end_rank - start_rank
	var file_changed = file_change != 0
	var rank_changed = rank_change != 0
	
	var start_piece = board_position[start_square]
	var end_piece = board_position[end_square]
	if end_piece <= WP: # Piece being taken is white
		if turn == WHITE:
			return false
	elif end_piece != E: # Piece being taken is black
		if turn == BLACK:
			return false
	
	# King movement restriction
	if start_piece == WK || start_piece == BK: 
		if abs(file_change) > 1 || abs(rank_change) > 1:
			return false
	
	# How did queen move?
	var queen_rook_move: bool = false
	var queen_bishop_move: bool = false
	if start_piece == WQ || start_piece == BQ:
		if file_changed != rank_changed:
			queen_rook_move = true
		elif abs(rank_change) == abs(file_change):
			queen_bishop_move = true
		else:
			return false
	
	# Rook movement restriction
	if start_piece == WR || start_piece == BR || queen_rook_move:
		if file_changed && rank_changed:
			return false
		# Scan
		var scan_square = start_square + sign(file_change) - (8 * sign(rank_change))
		while scan_square != end_square:
			if (board_position[scan_square] != E):
				return false
			scan_square += sign(file_change) - (8 * sign(rank_change))
	
	# Bishop movement restriction
	if start_piece == WB || start_piece == BB || queen_bishop_move:
		if (abs(rank_change) != abs(file_change)):
			return false
		# Scan
		var scan_square = start_square + sign(file_change) - (8 * sign(rank_change))
		while scan_square != end_square:
			if (board_position[scan_square] != E):
				return false
			scan_square += sign(file_change) - (8 * sign(rank_change))
	
	# Knight movement restriction
	if start_piece == WN || start_piece == BN:
		if !((abs(rank_change) == 2 && abs(file_change) == 1) || (abs(rank_change) == 1 && abs(file_change) == 2)):
			return false
	
	# Pawn movement restriction
	if start_piece == WP || start_piece == BP:
		var allowed_rank_change = 1
		var direction = 1 if start_piece == WP else -1
		
		if (start_piece == WP && start_rank == 2) || (start_piece == BP && start_rank == 7):
			allowed_rank_change = 2
		
		# Logic for if staying in the same file
		if !file_changed:
			if ((direction * rank_change < 0 || abs(rank_change) > allowed_rank_change) || 
			board_position[end_square] != E || board_position[start_square - (direction * 8)] != E):
				return false
		elif (rank_change * direction != 1 || abs(file_change) != 1 || end_piece == E):
			return false
	
	return true

func generate_legal_moves(square: int):
	var legal_moves = []
	for i in range(64):
		if is_legal_move(square, i):
			legal_moves.append(i)
	print(legal_moves)
	return legal_moves

func _on_square_pressed(number: int):
	print("Square Pressed: %d" % number)
	print("Previously Selected Square: %d" % selected_square)
	if selected_square == -1:
		if board_position[number] != E && ((turn == WHITE && board_position[number] <= WP) || (turn == BLACK && board_position[number] >= BK)):
			selected_square = number
			var legal_moves = generate_legal_moves(number)
			update_allowed_moves(legal_moves)
	else:
		# Move Piece
		update_allowed_moves([])
		if !is_legal_move(selected_square, number):
			selected_square = -1
			return
		board_position[number] = board_position[selected_square]
		board_position[selected_square] = E
		previous_move_start = selected_square
		previous_move_end = number
		
		selected_square = -1
		update_board()
		turn = !turn

func _ready():
	for square in button_grid.get_children():
		square.square_pressed.connect(_on_square_pressed)
	update_board()
