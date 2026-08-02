using System;
using System.Diagnostics;
using Godot;

enum BotMode {None, White, Black, Both}
public partial class BoardView : Node2D
{
    [Export] private GridContainer buttonGrid;
    [Export] private Label gameStateDisplay;
    private Texture2D[] pieceTextures;
    private Board board = new Board();
    private int selectedSquare = -1;
    private Piece promoteType = Piece.WQ;
    private BotMode botMode = BotMode.Black;

    public override void _Ready()
    {
        for (int i = 0; i < buttonGrid.GetChildCount(); i++)
        {
            ClickableSquare squareButton = buttonGrid.GetChild<ClickableSquare>(i);
            squareButton.SquarePressed += OnSquarePressed;
        }
        board.SetStartPosition();

        Stopwatch stopwatch = Stopwatch.StartNew();
        long nodes = board.Perft(5);
        stopwatch.Stop();
        TimeSpan ts = stopwatch.Elapsed;
        Debug.WriteLine($"Found {nodes} in {ts.TotalMilliseconds}ms");

        
        // Load piece textures
        pieceTextures = [
            GD.Load<Texture2D>("res://textures/white-king.png"),
            GD.Load<Texture2D>("res://textures/white-queen.png"),
            GD.Load<Texture2D>("res://textures/white-rook.png"),
            GD.Load<Texture2D>("res://textures/white-bishop.png"),
            GD.Load<Texture2D>("res://textures/white-knight.png"),
            GD.Load<Texture2D>("res://textures/white-pawn.png"),
            GD.Load<Texture2D>("res://textures/black-king.png"),
            GD.Load<Texture2D>("res://textures/black-queen.png"),
            GD.Load<Texture2D>("res://textures/black-rook.png"),
            GD.Load<Texture2D>("res://textures/black-bishop.png"),
            GD.Load<Texture2D>("res://textures/black-knight.png"),
            GD.Load<Texture2D>("res://textures/black-pawn.png")
        ];
        UpdateBoardView();
    }
    private void UpdateBoardView()
    {
        for (int i = 0; i < 64; i++)
        {
            int file = i % 8;
            int rank = 7 - (i / 8); // Invert rank for display
            int displayIndex = rank * 8 + file;
            ClickableSquare squareButton = buttonGrid.GetChild<ClickableSquare>(displayIndex);
            Piece piece = board.GetPieceAtSquare(i);
            Sprite2D pieceSprite = squareButton.GetNode<Sprite2D>("Piece");
            if (piece != Piece.E)
            {
                pieceSprite.Texture = pieceTextures[(int)piece];
                pieceSprite.Visible = true;
            }
            else
            {
                pieceSprite.Visible = false;
            }
        }
    }

    private void UpdateLegalMovesView()
    {
        Span<MoveInfo> moves = stackalloc MoveInfo[Board.MaxLegalMoves];
		int count = board.GenerateMoves(moves);
        for (int i = 0; i < 64; i++)
        {
            ClickableSquare squareButton = buttonGrid.GetChild<ClickableSquare>(i);
            squareButton.SetVisibility(false);
        }
        for (int i = 0; i < count; i++)
        {
            if (moves[i].From == selectedSquare)
            {
                int file = moves[i].To % 8;
                int rank = 7 - (moves[i].To / 8); // Invert rank for display
                int displayIndex = rank * 8 + file;
                ClickableSquare squareButton = buttonGrid.GetChild<ClickableSquare>(displayIndex);
                squareButton.SetVisibility(true);
            }
        }
    }
    private void OnBotModeChanged(int newBotMode)
    {
        botMode = (BotMode)newBotMode;
    }

    private void PlayBotMove()
    {
        if (board.CurrentGameState != GameResult.Ongoing)
        {
            return;
        }
        if ((botMode == BotMode.White && board.SideToMove != Color.White) ||
            (botMode == BotMode.Black && board.SideToMove != Color.Black) || 
            botMode == BotMode.None
        ) return;

        const int maxDepth = 4;
        const int depthCap = 6;
        board.PlayBestMove(maxDepth, depthCap);
        gameStateDisplay.Text = board.CurrentGameState.ToString();
        UpdateBoardView();
        UpdateLegalMovesView();


    }
    private void OnSquarePressed(int childIndex)
    {
        if (board.CurrentGameState != GameResult.Ongoing)
        {
            return;
        }

        int file = childIndex % 8;
        int rank = 7 - (childIndex / 8);
        int squareIndex = rank * 8 + file;
        if (selectedSquare == -1)
        {
            Piece selectedPiece = board.GetPieceAtSquare(squareIndex);
            Color selectedColor = selectedPiece < Piece.BK ? Color.White : Color.Black;
            if (board.GetPieceAtSquare(squareIndex) == Piece.E || selectedColor != board.SideToMove)
            {
                return;
            }
            selectedSquare = squareIndex;
            UpdateLegalMovesView();
            GD.Print($"Selected square: {selectedSquare}");
        }
        else
        {
            GD.Print($"Moving from {selectedSquare} to {squareIndex}");
            Piece coloredPromotionType = board.SideToMove == Color.White ? promoteType : (Piece)((int)promoteType + 6);
            MoveInfo move = new(selectedSquare, squareIndex, coloredPromotionType);
            board.TryMakeMove(move);
            GD.Print(board.CurrentGameState);
            gameStateDisplay.Text = board.CurrentGameState.ToString();
            selectedSquare = -1;
            UpdateBoardView();
            UpdateLegalMovesView();
            
            PlayBotMove();
        }
    }
    private void OnPromoteTypeChanged(int index)
    {
        // Always white
        promoteType = (Piece)index;
    }
}