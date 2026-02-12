using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root control for a single tile. Handles input and delegates rendering to TileView.
/// </summary>
public partial class TileNode : Control
{
    [Signal]
    public delegate void TileClickedEventHandler(int row, int col);

    [Signal]
    public delegate void TileRightClickedEventHandler(int row, int col);

    private TileView _view = null!;
    private Position _position;
    private bool _isRevealed;

    public Position TilePosition => _position;

    public override void _Ready()
    {
        _view = GetNode<TileView>("TileView");

        MouseEntered += () => _view.SetHovered(true);
        MouseExited += () => _view.SetHovered(false);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left && !_isRevealed)
            {
                EmitSignal(SignalName.TileClicked, _position.Row, _position.Col);
                AcceptEvent();
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.TileRightClicked, _position.Row, _position.Col);
                AcceptEvent();
            }
        }
    }

    public void Setup(Position position)
    {
        _position = position;
    }

    public void UpdateFromTile(Tile tile)
    {
        _isRevealed = tile.IsRevealed;
        _view.UpdateVisual(tile);
    }
}
