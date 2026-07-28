using Godot;
using System;

public partial class ClickableSquare : Button
{
    [Signal] public delegate void SquarePressedEventHandler(int squareIndex);

    public void SetVisibility(bool visible)
    {
        SelfModulate = new Godot.Color(SelfModulate.R, SelfModulate.G, SelfModulate.B, visible ? 1.0f : 0.0f);
    }

    private void OnButtonPressed()
    {
        EmitSignal("SquarePressed", GetIndex());
    }
}
