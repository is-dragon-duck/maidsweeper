using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

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

    [Signal]
    public delegate void TileHoveredEventHandler(int row, int col);

    [Signal]
    public delegate void TileUnhoveredEventHandler(int row, int col);

    private const int TileSize = BoardLayout.TileSize;
    private const int TileGap = BoardLayout.TileGap;

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

        // Node2D doesn't participate in Control layout, so the parent
        // MarginContainer's margins don't offset us. Apply manually.
        if (GetParent() is MarginContainer margin)
        {
            Position = new Vector2(
                margin.GetThemeConstant("margin_left"),
                margin.GetThemeConstant("margin_top")
            );
        }

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
                tileNode.TileHovered += OnTileHovered;
                tileNode.TileUnhovered += OnTileUnhovered;

                AddChild(tileNode);
                _tileNodes[row, col] = tileNode;

                // Mark unused positions (after AddChild so _Ready has fired)
                if (board.UnusedPositions.Contains(pos))
                {
                    tileNode.SetUnused(true);
                }

                // Set initial visual state (no clues at game start)
                var tile = board.GetTile(pos);
                var canReach = !tile.IsInner || BoardSystem.CanReachInnerTile(board, pos);
                tileNode.UpdateFromTile(tile, [], canReachInner: canReach);
            }
        }
    }

    #nullable enable
    public void UpdateBoard(Board board, List<string> globalClueOrder, TileOwner? viewingPerspective = null, IReadOnlyDictionary<Position, int>? intentPoints = null)
    #nullable restore
    {
        if (_tileNodes == null) return;

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var pos = new Position(row, col);
                var tile = board.GetTile(pos);
                // Saturation check mark is for the player's own info — only show on player-revealed tiles.
                var saturated = tile.IsRevealed
                    && tile.RevealedBy == PlayerType.Player
                    && BoardSystem.IsSaturated(board, pos);
                var intent = intentPoints != null && intentPoints.TryGetValue(pos, out var pts) ? pts : 0;
                var canReach = !tile.IsInner || BoardSystem.CanReachInnerTile(board, pos);
                _tileNodes[row, col].UpdateFromTile(tile, globalClueOrder, viewingPerspective, saturated, intent, canReach);
            }
        }
    }

    /// <summary>
    /// Sets targeting highlights for unrevealed tiles (standard targeting).
    /// </summary>
    public void SetTargetingHighlights(Board board)
    {
        SetTargetingHighlights(board, targetRevealed: false);
    }

    /// <summary>
    /// Sets targeting highlights for tiles. When targetRevealed is true (Brat),
    /// highlights revealed non-destroyed tiles instead of unrevealed.
    /// When areaCenterMode is true (Sweep), all non-unused positions are valid.
    /// </summary>
    public void SetTargetingHighlights(Board board, bool targetRevealed, bool areaCenterMode = false)
    {
        if (_tileNodes == null) return;

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var pos = new Position(row, col);
                var tile = board.GetTile(pos);
                bool isValidTarget;
                if (areaCenterMode)
                    // Sweep can be centered anywhere in-bounds, including unused positions.
                    isValidTarget = true;
                else if (targetRevealed)
                    isValidTarget = board.IsUsablePosition(pos) && tile.IsRevealed && !tile.IsDestroyed;
                else
                    isValidTarget = board.IsUsablePosition(pos) && !tile.IsRevealed && !tile.IsDestroyed;
                _tileNodes[row, col].SetTargetValid(isValidTarget);
            }
        }
    }

    public void SetTargetSelected(Position pos, bool selected)
    {
        if (_tileNodes == null) return;
        _tileNodes[pos.Row, pos.Col].SetTargetSelected(selected);
    }

    /// <summary>
    /// Sets area highlight on all usable tiles within the given radius of center.
    /// Used for Brush (3x3), Sweep (5x5), and Argue (3x3) preview.
    /// </summary>
    public void SetAreaHighlight(Position center, int radius, Board board)
    {
        if (_tileNodes == null) return;

        var tilesInArea = BoardSystem.GetTilesInArea(board, center, radius);
        var areaPositions = tilesInArea.Select(t => t.Position).ToHashSet();

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var pos = new Position(row, col);
                _tileNodes[row, col].SetAreaPreview(areaPositions.Contains(pos));
            }
        }
    }

    /// <summary>
    /// Sets cross-shaped area highlight (for Peek, AcceptHelp).
    /// </summary>
    public void SetCrossHighlight(Position center, Board board)
    {
        if (_tileNodes == null) return;

        var tilesInCross = BoardSystem.GetTilesInCross(board, center);
        var crossPositions = tilesInCross.Select(t => t.Position).ToHashSet();

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var pos = new Position(row, col);
                _tileNodes[row, col].SetAreaPreview(crossPositions.Contains(pos));
            }
        }
    }

    public void ClearAreaHighlight()
    {
        if (_tileNodes == null) return;

        for (var row = 0; row < _tileNodes.GetLength(0); row++)
        {
            for (var col = 0; col < _tileNodes.GetLength(1); col++)
            {
                _tileNodes[row, col].SetAreaPreview(false);
            }
        }
    }

    public void ClearTargetingHighlights()
    {
        if (_tileNodes == null) return;
        ClearAreaHighlight();

        for (var row = 0; row < _tileNodes.GetLength(0); row++)
        {
            for (var col = 0; col < _tileNodes.GetLength(1); col++)
            {
                _tileNodes[row, col].ClearTargetingState();
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

    private void OnTileHovered(int row, int col)
    {
        EmitSignal(SignalName.TileHovered, row, col);
    }

    private void OnTileUnhovered(int row, int col)
    {
        EmitSignal(SignalName.TileUnhovered, row, col);
    }
}
