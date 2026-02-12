using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Renders the game board by spawning and positioning TileNode instances.
/// </summary>
public partial class BoardNode : Node2D
{
    [Signal]
    public delegate void TileClickedEventHandler(int row, int col);

    [Signal]
    public delegate void TileRightClickedEventHandler(int row, int col);

    private const int TileSize = 64;
    private const int TileGap = 4;

    private readonly PackedScene _tileScene = GD.Load<PackedScene>("res://Scenes/Tile.tscn");
    #nullable enable
    private TileNode[,]? _tileNodes;
    #nullable restore

    public void BuildBoard(Board board)
    {
        // Clear existing children
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _tileNodes = new TileNode[board.Height, board.Width];

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var tileNode = _tileScene.Instantiate<TileNode>();
                var pos = new Position(row, col);

                tileNode.Setup(pos);
                tileNode.Position = new Vector2(
                    col * (TileSize + TileGap),
                    row * (TileSize + TileGap)
                );

                tileNode.TileClicked += OnTileClicked;
                tileNode.TileRightClicked += OnTileRightClicked;

                AddChild(tileNode);
                _tileNodes[row, col] = tileNode;

                // Set initial visual state
                var tile = board.GetTile(pos);
                tileNode.UpdateFromTile(tile);
            }
        }
    }

    public void UpdateBoard(Board board)
    {
        if (_tileNodes == null) return;

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var tile = board.GetTile(new Position(row, col));
                _tileNodes[row, col].UpdateFromTile(tile);
            }
        }
    }

    private void OnTileClicked(int row, int col)
    {
        EmitSignal(SignalName.TileClicked, row, col);
    }

    private void OnTileRightClicked(int row, int col)
    {
        EmitSignal(SignalName.TileRightClicked, row, col);
    }
}
