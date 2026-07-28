using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

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

public readonly struct MoveInfo(int from, int to, Piece promotion = Piece.E)
{
    public readonly int From = from, To = to;
    public readonly Piece Promotion = promotion;
}
public readonly struct UndoInfo(MoveInfo move, Piece capturedPiece, int capturedSquare, int previousEnPassantSquare, CastleRights previousCastleRights)
{
    public readonly MoveInfo Move = move;
    public readonly Piece CapturedPiece = capturedPiece;
    public readonly int CapturedSquare = capturedSquare;
    public readonly int PreviousEnPassantSquare = previousEnPassantSquare;
    public readonly CastleRights PreviousCastleRights = previousCastleRights;
    
}
public partial class Board
{
    private const int MaxLegalMoves = 218;
    private ulong[] pieceBBs = new ulong[12]; 
    private ulong[] colorBBs = new ulong[2];
    private ulong occupancyBB = 0;
    private Piece[] boardPieces = new Piece[64];
    private CastleRights castleRights = CastleRights.All;
    private int enPassantSquare = -1;
    private Color sideToMove = Color.White;
    private MoveInfo[] moves = new MoveInfo[MaxLegalMoves];
    private int legalMoveCount = 0;

    private int moveCount = 1;
    private int halfMoveClock = 0;

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
            Debug.WriteLine($"Placing piece {piece} at square {squareIndex}");

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
        sideToMove = fen[fenIndex] == 'w' ? Color.White : Color.Black;
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
    }
    public Piece GetPieceAtSquare(int square)
    {
        return boardPieces[square];
    }


    // Generates pseudo-legal moves for a given piece on a given square
    public void GeneratePieceMoves(int square, Piece piece)
    {
        ulong attacks = piece switch
        {
            Piece.WK or Piece.BK => AttackTables.KingAttacks[square],
            Piece.WN or Piece.BN => AttackTables.KnightAttacks[square],
            _ => 0
        };
        int targetSquares = BitOperations.PopCount(attacks);
        for (int i = 0; i < targetSquares; i++)
        {
            int targetSquare = BitOperations.TrailingZeroCount(attacks);
            moves[legalMoveCount++] = new MoveInfo(square, targetSquare);
            attacks &= attacks - 1; // Clear the least significant bit
        }
    }

    public void GenerateMoves()
    {
        legalMoveCount = 0;
        int pieceOffset = (int)Piece.BK * (int)sideToMove;
        for (int i = 0; i < 6; i++)
        {
            ulong pieceBB = pieceBBs[pieceOffset + i];
            while (pieceBB > 0)
            {
                int square = BitOperations.TrailingZeroCount(pieceBB);
                Piece piece = (Piece)(pieceOffset + i);
                GeneratePieceMoves(square, piece);
                pieceBB &= pieceBB - 1; // Clear the least significant bit
            }
        } 
    }

    private void MakeMove(MoveInfo move)
    {
        Piece movedPiece = boardPieces[move.From];
        Piece takenPiece = boardPieces[move.To];
        boardPieces[move.To] = boardPieces[move.From];
        boardPieces[move.From] = Piece.E;
        
        pieceBBs[(int)movedPiece] ^= 1UL << move.From; // Flips the bit. It should be one so should set it to 0
        if (takenPiece != Piece.E)
        {
            pieceBBs[(int)takenPiece] ^= 1UL << move.To;
        }
        sideToMove = sideToMove == Color.White ? Color.White : Color.Black;
    }
    public bool TryMakeMove(MoveInfo move)
    {
        GenerateMoves();
        for (int i = 0; i < legalMoveCount; i++)
        {
            if (moves[i].To == move.To && moves[i].From == moves[i].From)
            {
                MakeMove(move);
                return true;
            }
        }
        return false;
    }
}
