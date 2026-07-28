using Godot;

public partial class BoardView : Node2D
{
    [Export] private GridContainer buttonGrid;
    private Texture2D[] pieceTextures;

    private Board board = new Board();
    private int selectedSquare = -1;
    private Piece promoteType = Piece.WQ;

    public override void _Ready()
    {
        for (int i = 0; i < buttonGrid.GetChildCount(); i++)
        {
            ClickableSquare squareButton = buttonGrid.GetChild<ClickableSquare>(i);
            squareButton.SquarePressed += OnSquarePressed;
        }
        board.SetStartPosition();
        
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
            if (piece != Piece.E)
            {
                squareButton.SetVisibility(true);
                squareButton.GetNode<Sprite2D>("Piece").Texture = pieceTextures[(int)piece];
            }
            else
            {
                squareButton.SetVisibility(false);
            }
        }
    }
    private void OnSquarePressed(int childIndex)
    {
        int file = childIndex % 8;
        int rank = 7 - (childIndex / 8);
        int squareIndex = rank * 8 + file;
        if (selectedSquare == -1)
        {
            selectedSquare = squareIndex;
            GD.Print($"Selected square: {selectedSquare}");
        }
        else
        {
            GD.Print($"Moving from {selectedSquare} to {squareIndex}");
            MoveInfo move = new(selectedSquare, squareIndex, promoteType);
            board.TryMakeMove(move);
            UpdateBoardView();
            selectedSquare = -1;
        }
    }
    private void OnPromoteTypeChanged(int index)
    {
        promoteType = (Piece)index;
    }
}