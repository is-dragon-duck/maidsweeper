using System.Collections.Generic;
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

    [Signal]
    public delegate void TileHoveredEventHandler(int row, int col);

    [Signal]
    public delegate void TileUnhoveredEventHandler(int row, int col);

    private TileView _view = null!;
    private Position _position;
    private bool _isRevealed;
    private bool _isUnused;
    private bool _isDestroyed;
    private bool _isTargetValid;

    public Position TilePosition => _position;

    public override void _Ready()
    {
        _view = GetNode<TileView>("TileView");

        MouseEntered += () =>
        {
            _view.SetHovered(true);
            EmitSignal(SignalName.TileHovered, _position.Row, _position.Col);
        };
        MouseExited += () =>
        {
            _view.SetHovered(false);
            EmitSignal(SignalName.TileUnhovered, _position.Row, _position.Col);
        };
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_isDestroyed) return;
        // Unused tiles only respond to clicks when they're valid targets (area-center targeting)
        if (_isUnused && !_isTargetValid) return;

        if (@event is InputEventMouseButton { Pressed: true } mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (!_isRevealed || _isTargetValid)
                {
                    EmitSignal(SignalName.TileClicked, _position.Row, _position.Col);
                    AcceptEvent();
                }
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

    public void SetUnused(bool unused)
    {
        _isUnused = unused;
        _view.SetUnused(unused);
    }

    public void UpdateFromTile(Tile tile, List<string> globalClueOrder, TileOwner? viewingPerspective = null, bool saturated = false, int intentPoints = 0, bool canReachInner = true)
    {
        _isRevealed = tile.IsRevealed;
        _isDestroyed = tile.IsDestroyed;
        _view.UpdateVisual(tile, globalClueOrder, viewingPerspective, saturated, intentPoints, canReachInner);
    }

    public void SetTargetValid(bool valid)
    {
        _isTargetValid = valid;
        _view.SetTargetValid(valid);
    }

    public void SetTargetSelected(bool selected) => _view.SetTargetSelected(selected);
    public void SetAreaPreview(bool preview) => _view.SetAreaPreview(preview);
    public void ClearTargetingState()
    {
        _isTargetValid = false;
        _view.ClearTargetingState();
    }
}
