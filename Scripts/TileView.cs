using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Handles all visual rendering for a single tile.
/// Colors, adjacency numbers, hover effects, annotations, clue pips.
/// </summary>
public partial class TileView : Control
{
    private static readonly Color UnrevealedColor = new(0.3f, 0.3f, 0.3f);
    private static readonly Color HoverColor = new(0.4f, 0.4f, 0.4f);
    private static readonly Color PlayerColor = new(1.0f, 0.75f, 0.8f);
    private static readonly Color RivalColor = new(0.7f, 0.85f, 1.0f);
    private static readonly Color NeutralColor = new(0.95f, 0.95f, 0.95f);
    private static readonly Color NobleColor = new(0.8f, 0.6f, 0.9f);

    // Adjacency number colors: tinted to indicate whose perspective
    private static readonly Color PlayerAdjColor = new(0.6f, 0.1f, 0.2f);  // dark pink
    private static readonly Color RivalAdjColor = new(0.1f, 0.2f, 0.6f);   // dark blue

    // Targeting mode colors
    private static readonly Color TargetValidColor = new(0.35f, 0.45f, 0.35f);
    private static readonly Color TargetSelectedColor = new(0.9f, 0.8f, 0.2f);
    private static readonly Color TargetBorderColor = new(0.2f, 0.8f, 0.2f);

    // Owner grid colors (used in the 2x2 annotation grid)
    private static readonly Color OwnerGridPlayer = new(1.0f, 0.55f, 0.65f);   // saturated pink
    private static readonly Color OwnerGridRival = new(0.45f, 0.65f, 1.0f);    // saturated blue
    private static readonly Color OwnerGridNeutral = new(0.85f, 0.85f, 0.85f); // light gray
    private static readonly Color OwnerGridNoble = new(0.7f, 0.4f, 0.85f);     // saturated purple

    // Pip colors — one per clue cast, cycling
    private static readonly Color[] PipColors =
    [
        new(0.95f, 0.7f, 0.1f),   // gold
        new(0.3f, 0.7f, 0.95f),   // sky blue
        new(0.9f, 0.4f, 0.7f),    // pink
        new(0.5f, 0.9f, 0.4f),    // lime
        new(0.7f, 0.5f, 0.9f),    // lavender
        new(0.95f, 0.5f, 0.2f),   // orange
    ];

    private bool _isRevealed;
    private TileOwner _owner;
    private int _adjacencyCount;
    private PlayerType? _revealedBy;
    private bool _isHovered;
    private bool _isTargetValid;
    private bool _isTargetSelected;
    private TileAnnotations _annotations = new();
    private List<string> _globalClueOrder = [];

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);

        // Background
        Color bgColor;
        if (!_isRevealed)
        {
            if (_isTargetSelected)
                bgColor = TargetSelectedColor;
            else if (_isTargetValid && _isHovered)
                bgColor = TargetValidColor.Lightened(0.15f);
            else if (_isTargetValid)
                bgColor = TargetValidColor;
            else if (_isHovered)
                bgColor = HoverColor;
            else
                bgColor = UnrevealedColor;
        }
        else
        {
            bgColor = _owner switch
            {
                TileOwner.Player => PlayerColor,
                TileOwner.Rival => RivalColor,
                TileOwner.Neutral => NeutralColor,
                TileOwner.Noble => NobleColor,
                _ => UnrevealedColor
            };
        }

        DrawRect(rect, bgColor);

        // Border (green when valid target)
        var borderColor = _isTargetValid && !_isRevealed ? TargetBorderColor : new Color(0.2f, 0.2f, 0.2f);
        var borderWidth = _isTargetSelected ? 3.0f : 1.0f;
        DrawRect(rect, borderColor, false, borderWidth);

        if (_isRevealed)
        {
            DrawAdjacencyNumber();
        }
        else
        {
            DrawAnnotations();
        }
    }

    private void DrawAdjacencyNumber()
    {
        var font = ThemeDB.FallbackFont;
        var fontSize = 24;
        var text = _adjacencyCount.ToString();
        var textSize = font.GetStringSize(text, fontSize: fontSize);
        var textPos = new Vector2(
            (Size.X - textSize.X) / 2,
            (Size.Y + textSize.Y) / 2 - 4
        );
        var numColor = _revealedBy == PlayerType.Rival ? RivalAdjColor : PlayerAdjColor;
        DrawString(font, textPos, text, fontSize: fontSize, modulate: numColor);
    }

    private void DrawAnnotations()
    {
        DrawCluePips();
        DrawOwnerGrid();
    }

    /// <summary>
    /// Top-left: clue pips from Recall cards.
    /// Each clue cast gets a different color and a consistent row across all tiles.
    /// Row is determined by global clue ordering (first Recall played = row 0, etc.).
    /// </summary>
    private void DrawCluePips()
    {
        var clues = _annotations.ClueResults;
        if (clues.Count == 0) return;

        var pipRadius = 3.5f;
        var pipSpacing = 10f;
        var startY = 10f;     // top area
        var rowHeight = 10f;

        foreach (var clue in clues)
        {
            var globalRow = _globalClueOrder.IndexOf(clue.ClueId);
            if (globalRow < 0) globalRow = 0; // fallback

            var colorIndex = globalRow % PipColors.Length;
            var pipColor = PipColors[colorIndex];
            var y = startY + globalRow * rowHeight;

            // Draw pips left-to-right from left edge
            var startX = 5f + pipRadius;

            for (var i = 0; i < clue.PipStrength; i++)
            {
                var x = startX + i * pipSpacing;
                DrawCircle(new Vector2(x, y), pipRadius, pipColor);
            }
        }
    }

    /// <summary>
    /// Lower-right: 2x2 grid of owner type boxes.
    /// NW=Player, NE=Neutral, SW=Rival, SE=Noble.
    /// Only boxes for owners still in the OwnerSubset are visible.
    /// Hidden entirely when no annotation exists (all owners possible).
    /// </summary>
    private void DrawOwnerGrid()
    {
        var subset = _annotations.OwnerSubset;
        if (subset == null) return;

        var boxSize = 10f;
        var gap = 2f;
        var gridWidth = boxSize * 2 + gap;
        var gridHeight = boxSize * 2 + gap;
        var originX = Size.X - gridWidth - 4;
        var originY = Size.Y - gridHeight - 4;

        // NW = Player
        if (subset.Contains(TileOwner.Player))
            DrawRect(new Rect2(originX, originY, boxSize, boxSize), OwnerGridPlayer);

        // NE = Neutral
        if (subset.Contains(TileOwner.Neutral))
            DrawRect(new Rect2(originX + boxSize + gap, originY, boxSize, boxSize), OwnerGridNeutral);

        // SW = Rival
        if (subset.Contains(TileOwner.Rival))
            DrawRect(new Rect2(originX, originY + boxSize + gap, boxSize, boxSize), OwnerGridRival);

        // SE = Noble
        if (subset.Contains(TileOwner.Noble))
            DrawRect(new Rect2(originX + boxSize + gap, originY + boxSize + gap, boxSize, boxSize), OwnerGridNoble);
    }

    public void UpdateVisual(Tile tile, List<string> globalClueOrder)
    {
        _isRevealed = tile.IsRevealed;
        _owner = tile.Owner;
        _adjacencyCount = tile.AdjacencyCount;
        _revealedBy = tile.RevealedBy;
        _annotations = tile.Annotations;
        _globalClueOrder = globalClueOrder;
        QueueRedraw();
    }

    public void SetHovered(bool hovered)
    {
        if (_isRevealed) return;
        _isHovered = hovered;
        QueueRedraw();
    }

    public void SetTargetValid(bool valid)
    {
        _isTargetValid = valid;
        QueueRedraw();
    }

    public void SetTargetSelected(bool selected)
    {
        _isTargetSelected = selected;
        QueueRedraw();
    }

    public void ClearTargetingState()
    {
        _isTargetValid = false;
        _isTargetSelected = false;
        QueueRedraw();
    }
}
