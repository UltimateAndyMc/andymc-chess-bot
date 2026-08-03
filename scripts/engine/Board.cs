using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;

public enum Piece {WK, WQ, WR, WB, WN, WP, BK, BQ, BR, BB, BN, BP, E}
public enum Color { White, Black, None }
public enum Square
{
    a1, b1, c1, d1, e1, f1, g1, h1,
    a2, b2, c2, d2, e2, f2, g2, h2,
    a3, b3, c3, d3, e3, f3, g3, h3,
    a4, b4, c4, d4, e4, f4, g4, h4,
    a5, b5, c5, d5, e5, f5, g5, h5,
    a6, b6, c6, d6, e6, f6, g6, h6,
    a7, b7, c7, d7, e7, f7, g7, h7,
    a8, b8, c8, d8, e8, f8, g8, h8,
    None
}

[Flags]
public enum CastleRights
{
    None = 0,
    WhiteKingSide  = 1 << 0,
    WhiteQueenSide = 1 << 1,
    BlackKingSide  = 1 << 2,
    BlackQueenSide = 1 << 3,
    All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide
}
public enum GameResult { Ongoing, WhiteWins, BlackWins, Draw }

public readonly struct MoveInfo(int from, int to, Piece promotion = Piece.E)
{
    public readonly int From = from, To = to;
    public readonly Piece Promotion = promotion;
}
public readonly struct UndoInfo(MoveInfo move, Piece capturedPiece, int capturedSquare, int previousEnPassantSquare, CastleRights previousCastleRights, int previousHalfMoveClock, ulong lastZobristKey)
{
    public readonly MoveInfo Move = move;
    public readonly Piece CapturedPiece = capturedPiece;
    public readonly int CapturedSquare = capturedSquare;
    public readonly int PreviousEnPassantSquare = previousEnPassantSquare;
    public readonly CastleRights PreviousCastleRights = previousCastleRights;
    public readonly int PreviousHalfMoveClock = previousHalfMoveClock;
    public readonly ulong LastZobristKey = lastZobristKey;
}
public partial class Board
{
    public const int MaxLegalMoves = 218;
    private const float BigNum = 1000000f;
    private ulong[] pieceBBs = new ulong[12]; 
    private ulong[] colorBBs = new ulong[2];
    private ulong occupancyBB = 0;
    private Piece[] boardPieces = new Piece[64];
    private CastleRights castleRights = CastleRights.All;
    private int enPassantSquare = -1;
    public Color SideToMove {get; private set;} = Color.White;

    // Zobrist hashing: Only needs 75 due to 75 move rule for draws
    private int zobristKeyIndex = 0;
    private readonly ulong[] zobristKeys = new ulong[75];

    private int moveCount = 1;
    private int halfMoveClock = 0;

    public GameResult CurrentGameState {get; private set;} = GameResult.Ongoing;

    public Board()
    {
        InitZobrist();
    }

    public override string ToString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int rank = 7; rank >= 0; rank--)
        {
            for (int file = 0; file < 8; file++)
            {
                int square = rank * 8 + file;
                Piece piece = boardPieces[square];
                char pieceChar = piece switch
                {
                    Piece.WK => 'K',
                    Piece.WQ => 'Q',
                    Piece.WR => 'R',
                    Piece.WB => 'B',
                    Piece.WN => 'N',
                    Piece.WP => 'P',
                    Piece.BK => 'k',
                    Piece.BQ => 'q',
                    Piece.BR => 'r',
                    Piece.BB => 'b',
                    Piece.BN => 'n',
                    Piece.BP => 'p',
                    _ => '.'
                };
                sb.Append(pieceChar);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private ulong[,] pieceKeys = new ulong[12, 64]; // Zobrist keys for each piece on each square
    private ulong[] castleKeys = new ulong[16]; // Zobrist keys for each castle rights combination
    private ulong[] enPassantKeys = new ulong[8]; // Zobrist keys for each file for en passant
    private ulong sideToMoveKey; // Zobrist key for side to move

    ulong state = 1804289383;
    private ulong GetRandom64()
    {
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state;
    }
    private void InitZobrist()
    {
        for (int i = 0; i < 12; i++) {
            for (int sq = 0; sq < 64; sq++) {
                pieceKeys[i, sq] = GetRandom64();
            }
        }
        for (int i = 0; i < 16; i++) castleKeys[i] = GetRandom64();
        for (int i = 0; i < 8; i++) enPassantKeys[i] = GetRandom64();
        sideToMoveKey = GetRandom64();
        }
    private ulong ZobristHash()
    {
        ulong finalKey = 0;

        for (int i = 0; i < 64; i++)
        {
            Piece piece = boardPieces[i];
            if (piece != Piece.E)
            {
                finalKey ^= pieceKeys[(int)piece, i];
            }
        }
        if (SideToMove == Color.Black) 
        {
            finalKey ^= sideToMoveKey;
        }
        int enPassantFile = enPassantSquare % 8;
        if (enPassantSquare != -1)
        {
            finalKey ^= enPassantKeys[enPassantFile];
        }
        finalKey ^= castleKeys[(int)castleRights];

        return finalKey;
    }
    private Piece PieceFromFenLetter(char letter)
    {
        return letter switch
        {
            'K' => Piece.WK,
            'Q' => Piece.WQ,
            'R' => Piece.WR,
            'B' => Piece.WB,
            'N' => Piece.WN,
            'P' => Piece.WP,
            'k' => Piece.BK,
            'q' => Piece.BQ,
            'r' => Piece.BR,
            'b' => Piece.BB,
            'n' => Piece.BN,
            'p' => Piece.BP,
            _   => Piece.E
        };
    }
    private void LoadFEN(string fen)
    {
        int fenIndex = 0;
        int rank = 7;
        int file = 0;
        for (int i = 0; i < 12; i++)
        {
            pieceBBs[i] = 0;
        }
        // Board setup from FEN string
        while (fen[fenIndex] != ' ')
        {
            char c = fen[fenIndex];
            if (c == '/')
            {
                fenIndex++;
                rank--;
                file = 0;
                continue;
            }
            if (char.IsDigit(c))
            {
                int emptySquares = c - '0';
                for (int j = 0; j < emptySquares; j++)
                {
                    int emptySquareIndex = rank * 8 + file + j;
                    boardPieces[emptySquareIndex] = Piece.E;
                }
                file += emptySquares;
                fenIndex++;
                continue;
            }
            Piece piece = PieceFromFenLetter(c);
            int squareIndex = rank * 8 + file;

            boardPieces[squareIndex] = piece;
            pieceBBs[(int)piece] |= 1UL << squareIndex;
            file++;
            fenIndex++;
        }
        for (int i = 0; i < 2; i++)
        {
            colorBBs[i] = 0;
            for (int j = 0; j < 6; j++)
            {
                colorBBs[i] |= pieceBBs[i * 6 + j];
            }
        }
        occupancyBB = colorBBs[0] | colorBBs[1];

        // Other FEN fields
        fenIndex++;
        SideToMove = fen[fenIndex] == 'w' ? Color.White : Color.Black;
        fenIndex += 2;
        castleRights = CastleRights.None;
        while (fen[fenIndex] != ' ')
        {
            char c = fen[fenIndex];
            switch (c)
            {
                case 'K': castleRights |= CastleRights.WhiteKingSide; break;
                case 'Q': castleRights |= CastleRights.WhiteQueenSide; break;
                case 'k': castleRights |= CastleRights.BlackKingSide; break;
                case 'q': castleRights |= CastleRights.BlackQueenSide; break;
            }
            fenIndex++;
        }

        fenIndex++;
        if (fen[fenIndex] == '-')
        {
            enPassantSquare = -1;
        }
        else
        {
            int enPassantFile = fen[fenIndex] - 'a';
            int enPassantRank = fen[fenIndex + 1] - '1';
            enPassantSquare = enPassantRank * 8 + enPassantFile;
        }
        fenIndex += 2;

        halfMoveClock = 0;
        while (fen[fenIndex] != ' ')
        {
            halfMoveClock = halfMoveClock * 10 + (fen[fenIndex] - '0');
            fenIndex++;
        }
        fenIndex++;
        moveCount = 0;
        while (fenIndex < fen.Length && fen[fenIndex] != ' ')
        {
            moveCount = moveCount * 10 + (fen[fenIndex] - '0');
            fenIndex++;
        }
    }
    public void SetStartPosition()
    {
        // Set up the initial position
        const string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        LoadFEN(startFen);
        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);
        UpdateGameState(count);
    }
    public Piece GetPieceAtSquare(int square)
    {
        return boardPieces[square];
    }
    public bool IsLegalMove(int from, int to)
    {
        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);
        for (int i = 0; i < count; i++)
        {
            if (moves[i].From == from && moves[i].To == to)
            {
                return true;
            }
        }
        return false;
    }
    private bool IsSquareAttacked(int square, Color by)
    {
        int offset = (int)by * 6;
        int kingSquare = BitOperations.TrailingZeroCount(pieceBBs[(int)Piece.WK + offset]);
        if (kingSquare < 64 && (AttackTables.KingAttacks[kingSquare] & (1UL << square)) != 0) return true;
        ulong knightBB = pieceBBs[(int)Piece.WN + offset];
        while (knightBB > 0)
        {
            int knightSquare = BitOperations.TrailingZeroCount(knightBB);
            if ((AttackTables.KnightAttacks[knightSquare] & (1UL << square)) != 0) return true;
            knightBB &= knightBB - 1; // Clear the least significant bit
        }
        ulong rookBB = pieceBBs[(int)Piece.WR + offset];
        while (rookBB > 0)
        {
            int rookSquare = BitOperations.TrailingZeroCount(rookBB);
            ulong rookAttacks = GenerateRookMoves(rookSquare);
            if ((rookAttacks & (1UL << square)) != 0) return true;
            rookBB &= rookBB - 1; // Clear the least significant bit
        }
        ulong bishopBB = pieceBBs[(int)Piece.WB + offset];
        while (bishopBB > 0)
        {
            int bishopSquare = BitOperations.TrailingZeroCount(bishopBB);
            ulong bishopAttacks = GenerateBishopMoves(bishopSquare);
            if ((bishopAttacks & (1UL << square)) != 0) return true;
            bishopBB &= bishopBB - 1; // Clear the least significant bit
        }
        ulong queenBB = pieceBBs[(int)Piece.WQ + offset];
        while (queenBB > 0)
        {
            int queenSquare = BitOperations.TrailingZeroCount(queenBB);
            ulong queenAttacks = GenerateRookMoves(queenSquare) | GenerateBishopMoves(queenSquare);
            if ((queenAttacks & (1UL << square)) != 0) return true;
            queenBB &= queenBB - 1; // Clear the least significant bit
        }
        ulong pawnBB = pieceBBs[(int)Piece.WP + offset];
        while (pawnBB > 0)
        {
            int pawnSquare = BitOperations.TrailingZeroCount(pawnBB);
            int pawnFile = pawnSquare % 8;
            int direction = 1 - (2 * (int)by);
            int leftSquare = pawnSquare + 8 * direction - 1;
            int rightSquare = pawnSquare + 8 * direction + 1;
            if (pawnFile > 0 && leftSquare == square) return true;
            if (pawnFile < 7 && rightSquare == square) return true;
            pawnBB &= pawnBB - 1; // Clear the least significant bit
        }
        return false;
    }
    private ulong GenerateRookMoves(int square)
    {
        int rank = square / 8;
        int file = square % 8;

        ulong attacks = 0;

        for (int r = rank + 1; r < 8; r++)
        {
            int targetSquare = r * 8 + file;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int r = rank - 1; r >= 0; r--)
        {
            int targetSquare = r * 8 + file;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int f = file + 1; f < 8; f++)
        {
            int targetSquare = rank * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int f = file - 1; f >= 0; f--)
        {
            int targetSquare = rank * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        return attacks;
    }
    private ulong GenerateBishopMoves(int square)
    {
        int rank = square / 8;
        int file = square % 8;

        ulong attacks = 0;

        for (int r = rank + 1, f = file + 1; r < 8 && f < 8; r++, f++)
        {
            int targetSquare = r * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int r = rank + 1, f = file - 1; r < 8 && f >= 0; r++, f--)
        {
            int targetSquare = r * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int r = rank - 1, f = file + 1; r >= 0 && f < 8; r--, f++)
        {
            int targetSquare = r * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        for (int r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
        {
            int targetSquare = r * 8 + f;
            attacks |= 1UL << targetSquare;
            if ((occupancyBB & (1UL << targetSquare)) != 0) break;
        }
        return attacks;
    }
    private ulong GeneratePawnMoves(int square)
    {
        int rank = square / 8;
        int file = square % 8;

        ulong attacks = 0;

        int direction = 1 - (2 * (int)SideToMove); 


        if (boardPieces[square + 8 * direction] == Piece.E) 
        {
            attacks |= 1UL << square + 8 * direction; // Move forward
            if (rank == (SideToMove == Color.White ? 1 : 6) && boardPieces[square + 16 * direction] == Piece.E)
            {
                attacks |= 1UL << square + 16 * direction; // Move forward two
            }
        }
        int leftSquare = square + 8 * direction - 1;
        int rightSquare = square + 8 * direction + 1;
        
        if (file > 0 && (boardPieces[leftSquare] != Piece.E || leftSquare == enPassantSquare)) 
        {
            attacks |= 1UL << leftSquare;
        }
        if (file < 7 && (boardPieces[rightSquare] != Piece.E || rightSquare == enPassantSquare)) 
        {
            attacks |= 1UL << rightSquare;
        }
        return attacks;
    }
    // Generates pseudo-legal moves for a given piece on a given square
    public void GeneratePieceMoves(Span<MoveInfo> moves, ref int count, int square, Piece piece)
    {
        ulong attacks = piece switch
        {
            Piece.WK or Piece.BK => AttackTables.KingAttacks[square],
            Piece.WN or Piece.BN => AttackTables.KnightAttacks[square],
            Piece.WR or Piece.BR => GenerateRookMoves(square),
            Piece.WB or Piece.BB => GenerateBishopMoves(square),
            Piece.WQ or Piece.BQ => GenerateRookMoves(square) | GenerateBishopMoves(square),
            Piece.WP or Piece.BP => GeneratePawnMoves(square),
            _ => 0
        };
        if (piece == Piece.WK)
        {
            if (castleRights.HasFlag(CastleRights.WhiteKingSide))
            {
                ulong blockingSquares = (1UL << (int)Square.f1) | (1UL << (int)Square.g1);
                int transitSquare = (int)Square.f1;
                if ((occupancyBB & blockingSquares) == 0 && !IsSquareAttacked(transitSquare, Color.Black) && !IsSquareAttacked((int)Square.e1, Color.Black))
                {
                    attacks |= 1UL << (int)Square.g1;
                }
            }
            if (castleRights.HasFlag(CastleRights.WhiteQueenSide))
            {
                ulong blockingSquares = (1UL << (int)Square.b1) | (1UL << (int)Square.c1) | (1UL << (int)Square.d1);
                int transitSquare = (int)Square.d1;
                if ((occupancyBB & blockingSquares) == 0 && !IsSquareAttacked(transitSquare, Color.Black) && !IsSquareAttacked((int)Square.e1, Color.Black))
                {
                    attacks |= 1UL << (int)Square.c1;
                }
            }
        }
        else if (piece == Piece.BK)
        {
            if (castleRights.HasFlag(CastleRights.BlackKingSide))
            {
                ulong blockingSquares = (1UL << (int)Square.f8) | (1UL << (int)Square.g8);
                int transitSquare = (int)Square.f8;
                if ((occupancyBB & blockingSquares) == 0 && !IsSquareAttacked(transitSquare, Color.White) && !IsSquareAttacked((int)Square.e8, Color.White))
                {
                    attacks |= 1UL << (int)Square.g8;
                }
            }
            if (castleRights.HasFlag(CastleRights.BlackQueenSide))
            {
                ulong blockingSquares = (1UL << (int)Square.b8) | (1UL << (int)Square.c8) | (1UL << (int)Square.d8);
                int transitSquare = (int)Square.d8;
                if ((occupancyBB & blockingSquares) == 0 && !IsSquareAttacked(transitSquare, Color.White) && !IsSquareAttacked((int)Square.e8, Color.White))
                {
                    attacks |= 1UL << (int)Square.c8;
                }
            }
        }

        int targetSquares = BitOperations.PopCount(attacks);
        for (int i = 0; i < targetSquares; i++)
        {
            int targetSquare = BitOperations.TrailingZeroCount(attacks);
            attacks &= attacks - 1; // Clear the least significant bit

            if ((colorBBs[(int)SideToMove] & (1UL << targetSquare)) != 0) // Skip if the target square has a piece of the same color
            {
                continue;
            }

            MoveInfo move = new(square, targetSquare);

            int offset = (int)SideToMove * 6;
            UndoInfo undo = MakeMove(move);
            int kingSquare = BitOperations.TrailingZeroCount(pieceBBs[(int)Piece.WK + offset]);
            bool isInCheck = IsSquareAttacked(kingSquare, SideToMove);
            UndoMove(undo);

            if (isInCheck)
            {
                continue; // Skip this move if it leaves the king in check
            }

            int rank = targetSquare / 8;
            if ((piece == Piece.WP || piece == Piece.BP) && (rank == 0 || rank == 7))
            {
                moves[count++] = new MoveInfo(square, targetSquare, SideToMove == Color.White ? Piece.WQ : Piece.BQ);
                moves[count++] = new MoveInfo(square, targetSquare, SideToMove == Color.White ? Piece.WR : Piece.BR);
                moves[count++] = new MoveInfo(square, targetSquare, SideToMove == Color.White ? Piece.WB : Piece.BB);
                moves[count++] = new MoveInfo(square, targetSquare, SideToMove == Color.White ? Piece.WN : Piece.BN);
            }
            else
            {
                moves[count++] = move;
            }
        }
    }
    public int GenerateMoves(Span<MoveInfo> moves)
    {
        if (CurrentGameState != GameResult.Ongoing)
        {
            return 0; // No moves can be generated if the game is over
        }
        int count = 0;
        int pieceOffset = (int)Piece.BK * (int)SideToMove;
        for (int i = 0; i < 6; i++)
        {
            ulong pieceBB = pieceBBs[pieceOffset + i];
            while (pieceBB > 0)
            {
                int square = BitOperations.TrailingZeroCount(pieceBB);
                Piece piece = (Piece)(pieceOffset + i);
                GeneratePieceMoves(moves, ref count, square, piece);
                pieceBB &= pieceBB - 1; // Clear the least significant bit
            }
        } 
        return count;
    }

    private void UpdateGameState(int count)
    {
        if (halfMoveClock >= 150)
        {
            CurrentGameState = GameResult.Draw;
            return;
        }
        if (count != 0)
        {
            CurrentGameState = GameResult.Ongoing;

            // Check for threefold repetition
            int limit = Math.Min(halfMoveClock, 74); // Can't repeat past the last irreversible move
            int repetitionCount = 1;
            for (int i = 2; i <= limit; i+=2)
            {
                int index = (zobristKeyIndex - i + 75) % 75;
                if (zobristKeys[index] == zobristKeys[zobristKeyIndex])
                {
                    repetitionCount++;
                    if (repetitionCount >= 3)
                    {
                        CurrentGameState = GameResult.Draw;
                        return;
                    }
                }

            }
            return;
        }
        Piece kingPiece = SideToMove == Color.White ? Piece.WK : Piece.BK;
        int kingSquare = BitOperations.TrailingZeroCount(pieceBBs[(int)kingPiece]);
        Color opponentColor = (Color)(1 - (int)SideToMove);
        if (IsSquareAttacked(kingSquare, opponentColor))
        {
            CurrentGameState = opponentColor == Color.White ? GameResult.WhiteWins : GameResult.BlackWins;
        }
        else CurrentGameState = GameResult.Draw;
    }
    private void UndoMove(UndoInfo undo)
    {
        CurrentGameState = GameResult.Ongoing;
        halfMoveClock = undo.PreviousHalfMoveClock;
        zobristKeys[zobristKeyIndex] = undo.LastZobristKey;
        zobristKeyIndex = (zobristKeyIndex - 1 + 75) % 75;
        Color moverColor = (Color)(1 - (int)SideToMove);
        MoveInfo move = undo.Move;
        Piece fromPiece = boardPieces[move.To];
        Piece toPiece = boardPieces[move.To];
        bool promoted = move.Promotion != Piece.E;
        if (promoted)
        {
            fromPiece = moverColor == Color.White ? Piece.WP : Piece.BP;
        }
        boardPieces[move.From] = fromPiece;
        boardPieces[move.To] = Piece.E;
        
        ulong fromPosition = 1UL << move.From;
        ulong toPosition = 1UL << move.To;

        pieceBBs[(int)fromPiece] ^= fromPosition;
        pieceBBs[(int)toPiece] ^= toPosition;
        colorBBs[(int)moverColor] ^= fromPosition;
        colorBBs[(int)moverColor] ^= toPosition;
        occupancyBB ^= fromPosition;
        occupancyBB ^= toPosition;

        // Castling undo
        if (fromPiece == Piece.WK || fromPiece == Piece.BK)
        {
            int fileChange = move.To - move.From;
            Piece rookPiece = moverColor == Color.White ? Piece.WR : Piece.BR;
            if (fileChange == 2)
            {
                int rookFrom = move.From + 3;
                int rookTo = move.From + 1;
                pieceBBs[(int)rookPiece] ^= (1UL << rookFrom) | (1UL << rookTo);
                boardPieces[rookFrom] = rookPiece;
                boardPieces[rookTo] = Piece.E;
                colorBBs[(int)moverColor] ^= (1UL << rookFrom) | (1UL << rookTo);
                occupancyBB ^= (1UL << rookFrom) | (1UL << rookTo);
            }
            if (fileChange == -2)
            {
                int rookFrom = move.From - 4;
                int rookTo = move.From - 1;
                pieceBBs[(int)rookPiece] ^= (1UL << rookFrom) | (1UL << rookTo);
                boardPieces[rookFrom] = rookPiece;
                boardPieces[rookTo] = Piece.E;
                colorBBs[(int)moverColor] ^= (1UL << rookFrom) | (1UL << rookTo);
                occupancyBB ^= (1UL << rookFrom) | (1UL << rookTo);
            }
        }

        if (undo.CapturedPiece != Piece.E)
        {
            boardPieces[undo.CapturedSquare] = undo.CapturedPiece;
            ulong capturedPosition = 1UL << undo.CapturedSquare;
            pieceBBs[(int)undo.CapturedPiece] ^= capturedPosition;
            colorBBs[(int)SideToMove] ^= capturedPosition;
            occupancyBB ^= capturedPosition;
        }

        enPassantSquare = undo.PreviousEnPassantSquare;
        castleRights = undo.PreviousCastleRights;
        SideToMove = moverColor;
    }
    private UndoInfo MakeMove(MoveInfo move)
    {
        int fromFile = move.From % 8;
        int fromRank = move.From / 8;
        int toFile = move.To % 8;
        int toRank = move.To / 8;
        int fileChange = toFile - fromFile;
        int rankChange = toRank - fromRank;

        int previousEnPassantSquare = enPassantSquare;
        CastleRights previousCastleRights = castleRights;
        int previousHalfMoveClock = halfMoveClock;
        ulong lastZobristKey = zobristKeys[(zobristKeyIndex + 1) % 75];

        halfMoveClock++;

        Piece movedPiece = boardPieces[move.From];
        // Ensure only pawns can be promoted and only when they reach the last rank
        if ((movedPiece != Piece.WP && movedPiece != Piece.BP) || (toRank != 0 && toRank != 7))
        {
            move = new MoveInfo(move.From, move.To, Piece.E);
        }
        int capturedSquare = move.To;
        Piece capturedPiece = boardPieces[capturedSquare];

        // En Passant
        if ((movedPiece == Piece.WP || movedPiece == Piece.BP) && move.To == enPassantSquare)
        {
            capturedSquare = move.From + fileChange;
            capturedPiece = boardPieces[capturedSquare];
            boardPieces[capturedSquare] = Piece.E; 
        }
        // Double pawn push
        if ((movedPiece == Piece.WP || movedPiece == Piece.BP) && Math.Abs(rankChange) == 2)
        {
            int direction = 1 - (2 * (int)SideToMove);
            enPassantSquare = move.From + 8 * direction;
        }
        else
        {
            enPassantSquare = -1;
        }

        // Update half move clock
        if (movedPiece == Piece.WP || movedPiece == Piece.BP || capturedPiece != Piece.E)
        {
            halfMoveClock = 0;
        }

        // Castling
        if (movedPiece == Piece.WK || movedPiece == Piece.BK)
        {
            Piece rookPiece = movedPiece == Piece.WK ? Piece.WR : Piece.BR;
            if (fileChange == 2) // King-side castling
            {
                int rookFrom = move.From + 3;
                int rookTo = move.From + 1;
                boardPieces[rookTo] = boardPieces[rookFrom];
                boardPieces[rookFrom] = Piece.E;
                pieceBBs[(int)rookPiece] ^= (1UL << rookFrom) | (1UL << rookTo);
                colorBBs[(int)SideToMove] ^= (1UL << rookFrom) | (1UL << rookTo);
                occupancyBB ^= (1UL << rookFrom) | (1UL << rookTo);
            }
            else if (fileChange == -2) // Queen-side castling
            {
                int rookFrom = move.From - 4;
                int rookTo = move.From - 1;
                boardPieces[rookTo] = boardPieces[rookFrom];
                boardPieces[rookFrom] = Piece.E;
                pieceBBs[(int)rookPiece] ^= (1UL << rookFrom) | (1UL << rookTo);
                colorBBs[(int)SideToMove] ^= (1UL << rookFrom) | (1UL << rookTo);
                occupancyBB ^= (1UL << rookFrom) | (1UL << rookTo);
            }

            // Remove castling rights for the moving side
            if (SideToMove == Color.White)
            {
                castleRights &= ~(CastleRights.WhiteKingSide | CastleRights.WhiteQueenSide);
            }
            else
            {
                castleRights &= ~(CastleRights.BlackKingSide | CastleRights.BlackQueenSide);
            }

            // Reset half move clock
            if (capturedPiece != Piece.E)
            {
                halfMoveClock = 0;
            }
        }

        if (move.From == (int)Square.a1 || capturedSquare == (int)Square.a1) castleRights &= ~CastleRights.WhiteQueenSide;
        if (move.From == (int)Square.h1 || capturedSquare == (int)Square.h1) castleRights &= ~CastleRights.WhiteKingSide;
        if (move.From == (int)Square.a8 || capturedSquare == (int)Square.a8) castleRights &= ~CastleRights.BlackQueenSide;
        if (move.From == (int)Square.h8 || capturedSquare == (int)Square.h8) castleRights &= ~CastleRights.BlackKingSide;
        
        boardPieces[move.To] = boardPieces[move.From];
        boardPieces[move.From] = Piece.E;
        
        ulong fromPosition = 1UL << move.From;
        ulong toPosition = 1UL << move.To;
        ulong capturedPosition = 1UL << capturedSquare;
        pieceBBs[(int)movedPiece] ^= fromPosition; // Flips the bit. It should be 1 so should set it to 0
        pieceBBs[(int)movedPiece] ^= toPosition; // Flips the bit. It should be 0 so should set it to 1
        colorBBs[(int)SideToMove] ^= fromPosition;
        colorBBs[(int)SideToMove] ^= toPosition;
        occupancyBB ^= fromPosition;
        occupancyBB ^= toPosition;
        if (capturedPiece != Piece.E)
        {
            pieceBBs[(int)capturedPiece] ^= capturedPosition;
            colorBBs[1 - (int)SideToMove] ^= capturedPosition;
            occupancyBB ^= capturedPosition;
        }

        if ((toRank == 0 || toRank == 7) && (movedPiece == Piece.WP || movedPiece == Piece.BP) && move.Promotion != Piece.E)
        {
            boardPieces[move.To] = move.Promotion;
            pieceBBs[(int)movedPiece] ^= toPosition; // Remove the pawn from the bitboard
            pieceBBs[(int)move.Promotion] ^= toPosition; // Add the promoted piece to the bitboard
        }

        SideToMove = SideToMove == Color.White ? Color.Black : Color.White;
        
        zobristKeyIndex = (zobristKeyIndex + 1) % 75;
        zobristKeys[zobristKeyIndex] = ZobristHash();

        return new(move, capturedPiece, capturedSquare, previousEnPassantSquare, previousCastleRights, previousHalfMoveClock, lastZobristKey);
    }
    public bool TryMakeMove(MoveInfo move)
    {
        if (CurrentGameState != GameResult.Ongoing || !IsLegalMove(move.From, move.To))
        {
            return false;
        }
        UndoInfo undo = MakeMove(move);

        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);
        UpdateGameState(count);
        return true;
    }

    public long Perft(int depth)
    {
        if (depth == 0) return 1;

        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);

        long nodes = 0;
        for (int i = 0; i < count; i++)
        {
            UndoInfo undo = MakeMove(moves[i]);
            nodes += Perft(depth - 1);
            UndoMove(undo);
        }
        return nodes;
    }

    readonly float[] points = [0f, 9f, 5f, 3.1f, 3f, 1f];
    private float Evaluate()
    {
        float eval = 0;
        for (int i = 0; i < 12; i++)
        {
            int direction = i < 6 ? 1 : -1;
            eval += BitOperations.PopCount(pieceBBs[i]) * points[i % 6] * direction;
        }


        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        for (int color = 0; color < 2; color++)
        {
            ulong pawns = pieceBBs[(int)Piece.WP + color * 6];
            while (pawns > 0)
            {
                int square = BitOperations.TrailingZeroCount(pawns);
                int rank = square / 8;
                int rankStart = color == 0 ? 1 : 6;
                int rankDifference = Math.Abs(rank - rankStart);
                eval += (color == 0 ? 1 : -1) * rankDifference * 0.12f; // Encourage advancing pawns
                pawns &= pawns - 1; // Clear the least significant bit
            }
            bool[] controlledSquares = new bool[64];

            ulong knights = pieceBBs[(int)Piece.WN + color * 6];
            while (knights > 0)
            {
                int square = BitOperations.TrailingZeroCount(knights);
                ulong attacks = AttackTables.KnightAttacks[square];
                int numAttacks = BitOperations.PopCount(attacks);
                eval += (color == 0 ? 1 : -1) * numAttacks * 0.1f;
                knights &= knights - 1; // Clear the least significant bit
            }
        }
        

        return eval;
    }
    // Depth is how far it has looked ahead,
    // maxDepth determined what the depth can reach,
    // depthCap is the maximum value that maxDepth can reach
    public float PlayBestMove(int maxDepth, int depthCap)
    {
        MoveInfo bestMove = default;
        float bestEval = float.NegativeInfinity;

        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);

        UpdateGameState(count);
        if (CurrentGameState == GameResult.WhiteWins)
        {
            return BigNum;
        }
        if (CurrentGameState == GameResult.BlackWins)
        {
            return -BigNum;
        }
        if (CurrentGameState == GameResult.Draw)
        {
            return 0f;
        }

        for (int i = 0; i < count; i++)
        {
            UndoInfo undo = MakeMove(moves[i]);
            float eval = -Search(1, maxDepth, depthCap, -BigNum, BigNum);
            UndoMove(undo);

            if (eval > bestEval)
            {
                bestEval = eval;
                bestMove = moves[i];
            }
        }

        MakeMove(bestMove);
        count = GenerateMoves(moves);
        UpdateGameState(count);
        bestEval = bestEval * (SideToMove == Color.White ? -1 : 1);
        return bestEval;

    }
    private float Search(int depth, int maxDepth, int depthCap, float alpha, float beta)
    {
        if (depth >= maxDepth || depth >= depthCap)
        {
            return Evaluate() * (SideToMove == Color.White ? 1 : -1);
        }
        float bestEval = -BigNum;

        Span<MoveInfo> moves = stackalloc MoveInfo[MaxLegalMoves];
        int count = GenerateMoves(moves);

        UpdateGameState(count);
        if (CurrentGameState == GameResult.WhiteWins || CurrentGameState == GameResult.BlackWins)
        {
            return depth - BigNum;
        }
        else if (CurrentGameState == GameResult.Draw)
        {
            return 0f;
        }

        for (int i = 0; i < count; i++)
        {
            UndoInfo undo = MakeMove(moves[i]);
            Piece opponentKingPiece = SideToMove == Color.White ? Piece.BK : Piece.WK;
            int kingSquare = BitOperations.TrailingZeroCount(pieceBBs[(int)opponentKingPiece]);
            int extension = undo.CapturedPiece != Piece.E || IsSquareAttacked(kingSquare, SideToMove) ? 1 : 0;
            float eval = -Search(depth + 1, maxDepth + extension, depthCap, -beta, -alpha);
            UndoMove(undo);

            if (eval > bestEval)
            {
                bestEval = eval;
            }
            if (eval > alpha)
            {
                alpha = eval;
            }
            if (alpha >= beta)
            {
                break;
            }
        }
        return bestEval;
    }
}
