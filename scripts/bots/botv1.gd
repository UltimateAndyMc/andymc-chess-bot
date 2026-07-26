extends Node

enum {WK, WQ, WR, WB, WN, WP, BK, BQ, BR, BB, BN, BP, E}
var values: Array[int] = [1000000, 9, 5, 3, 3, 1, -1000000, -9, -5, -3, -3, -1, 0]

@onready var board = $".."
var playing_as: bool = BLACK

var moved_pieces


# Turns
const BLACK = false
const WHITE = true

func move_played(next_turn: bool):
	if (next_turn == playing_as):
		var search_data = search(board.board_position.duplicate(), board.moved_pieces, playing_as, 3)
		print(search_data)
		board.attempt_make_move(search_data[1], search_data[2], true)

func evaluate(board_position):
	var eval: int = 0
	for square in board_position:
		eval += values[square]
	return eval

func search(board_position, moved_pieces, turn, depth_remaining):
	var best_eval = -1000000000 * (1 if turn == WHITE else -1)
	var best_start
	var best_end
	for i in range(64):
		var piece = board_position[i]
		if piece == E: continue
		if turn == WHITE && piece >= BK: continue
		if turn == BLACK && piece <= WP: continue
		for j in range(64):
			var other_data = {}
			if board.is_legal_move(board_position, turn, moved_pieces, i, j, true, other_data):
				var new_board_position = board_position.duplicate()
				new_board_position[j] = board_position[i]
				new_board_position[i] = E
				if other_data.has("deleted_square"):
					new_board_position[other_data["deleted_square"]] = E
				
				if other_data.has("rook_start"):
					new_board_position[other_data["rook_end"]] = board_position[other_data["rook_start"]]
					new_board_position[other_data["rook_start"]] = E
				
				# Update moved_pieces
				var new_moved_pieces = moved_pieces.duplicate()
				for moving_square in [i, j]:
					match (moving_square):
						0:
							new_moved_pieces[2] = true
						4:
							new_moved_pieces[1] = true
						7:
							new_moved_pieces[3] = true
						56:
							new_moved_pieces[4] = true
						60:
							new_moved_pieces[0] = true
						63:
							new_moved_pieces[5] = true
				var end_rank = 7 - (j / 8)
				if (end_rank == 7 || end_rank == 0):
					if new_board_position[j] == WP:
						new_board_position[j] = board.promote_type
					elif new_board_position[j] == BP:
						new_board_position[j] = board.promote_type + BK # Offset to black pieces
				var new_eval = 0
				var new_start = 0
				var new_end = 0
				if depth_remaining > 0:
					var search_data = search(new_board_position, new_moved_pieces, !turn, depth_remaining - 1)
					new_eval = search_data[0]
					new_start = i
					new_end = j
				else:
					new_eval = evaluate(new_board_position)
					new_start = i
					new_end = j
				if  (1 if turn == WHITE else -1) * new_eval > (1 if turn == WHITE else -1) * best_eval:
					best_eval = new_eval
					best_start = new_start
					best_end = new_end
	return [best_eval, best_start, best_end]
				
