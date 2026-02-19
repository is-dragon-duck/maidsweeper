using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Handles all visual rendering for a single tile.
/// Colors, adjacency numbers, hover effects, annotations, clue pips,
/// flag icon, adjacency info, and destroyed tile visuals.
/// </summary>
public partial class TileView : Control
{
    private static readonly Color UnrevealedColor = new(0.3f, 0.3f, 0.3f);
    private static readonly Color ExtraDirtyColor = new(0.22f, 0.2f, 0.18f);
    private static readonly Color HoverColor = new(0.4f, 0.4f, 0.4f);
    private static readonly Color PlayerColor = new(1.0f, 0.75f, 0.8f);
    private static readonly Color RivalColor = new(0.7f, 0.85f, 1.0f);
    private static readonly Color NeutralColor = new(0.95f, 0.95f, 0.95f);
    private static readonly Color NobleColor = new(0.8f, 0.6f, 0.9f);
    private static readonly Color DestroyedColor = new(0.12f, 0.12f, 0.12f);

    // Adjacency number colors: tinted to indicate whose perspective
    private static readonly Color PlayerAdjColor = new(0.6f, 0.1f, 0.2f);  // dark pink
    private static readonly Color RivalAdjColor = new(0.1f, 0.2f, 0.6f);   // dark blue

    // Targeting mode colors
    private static readonly Color TargetValidColor = new(0.35f, 0.45f, 0.35f);
    private static readonly Color TargetSelectedColor = new(0.9f, 0.8f, 0.2f);
    private static readonly Color TargetBorderColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color AreaPreviewColor = new(0.4f, 0.5f, 0.4f, 0.6f);

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

    // Flag color
    private static readonly Color FlagColor = new(0.9f, 0.3f, 0.3f);

    private bool _isRevealed;
    private TileOwner _owner;
    private int _adjacencyCount;
    private PlayerType? _revealedBy;
    private bool _isHovered;
    private bool _isTargetValid;
    private bool _isTargetSelected;
    private bool _isAreaPreview;
    private bool _isUnused;
    private bool _isDirty;
    private bool _isDestroyed;
    private TileAnnotations _annotations = new();
    private List<string> _globalClueOrder = [];

    public override void _Draw()
    {
        // Unused positions: draw nothing (gap in the grid)
        if (_isUnused) return;

        var rect = new Rect2(Vector2.Zero, Size);

        // Destroyed tiles: dark void with X
        if (_isDestroyed)
        {
            DrawRect(rect, DestroyedColor);
            DrawRect(rect, new Color(0.2f, 0.2f, 0.2f), false, 1.0f);
            // Draw X
            var margin = 12f;
            var xColor = new Color(0.35f, 0.35f, 0.35f);
            DrawLine(new Vector2(margin, margin), new Vector2(Size.X - margin, Size.Y - margin), xColor, 2.0f);
            DrawLine(new Vector2(Size.X - margin, margin), new Vector2(margin, Size.Y - margin), xColor, 2.0f);
            return;
        }

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
            else if (_isAreaPreview)
                bgColor = AreaPreviewColor;
            else if (_isHovered)
                bgColor = HoverColor;
            else if (_isDirty)
                bgColor = ExtraDirtyColor;
            else
                bgColor = UnrevealedColor;
        }
        else
        {
            if (_isTargetValid)
            {
                // Brat targeting: highlighted revealed tile
                bgColor = _isTargetSelected ? TargetSelectedColor : TargetValidColor.Lightened(0.1f);
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
        }

        DrawRect(rect, bgColor);

        // Border (green when valid target)
        var borderColor = _isTargetValid ? TargetBorderColor : new Color(0.2f, 0.2f, 0.2f);
        var borderWidth = _isTargetSelected ? 3.0f : 1.0f;
        DrawRect(rect, borderColor, false, borderWidth);

        if (_isRevealed)
        {
            DrawAdjacencyNumber();
        }
        else
        {
            DrawAnnotations();
            if (_isDirty)
            {
                DrawDirtyIndicator();
            }
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
        DrawFlagIcon();
        DrawAdjacencyInfo();
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
    /// Uses EffectiveOwnerSubset (combines card annotations + player exclusions).
    /// </summary>
    private void DrawOwnerGrid()
    {
        var subset = _annotations.EffectiveOwnerSubset;
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

    /// <summary>
    /// Top-right: flag icon when tile is flagged by player.
    /// Drawn as an "F" in red.
    /// </summary>
    private void DrawFlagIcon()
    {
        if (!_annotations.Flagged) return;

        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(Size.X - 14, 14), "F",
            fontSize: 12, modulate: FlagColor);
    }

    /// <summary>
    /// Below the owner grid: per-owner adjacency counts from Eavesdrop/AcceptHelp/Deliver.
    /// Shows colored count labels for each known owner type.
    /// </summary>
    private void DrawAdjacencyInfo()
    {
        var info = _annotations.AdjacencyInfo;
        if (info == null) return;

        var font = ThemeDB.FallbackFont;
        var fontSize = 8;
        var y = Size.Y - 4;
        var x = 4f;

        if (info.PlayerCount.HasValue)
        {
            var text = $"P:{info.PlayerCount.Value}";
            DrawString(font, new Vector2(x, y), text, fontSize: fontSize, modulate: OwnerGridPlayer);
            x += font.GetStringSize(text, fontSize: fontSize).X + 3;
        }

        if (info.RivalCount.HasValue)
        {
            var text = $"R:{info.RivalCount.Value}";
            DrawString(font, new Vector2(x, y), text, fontSize: fontSize, modulate: OwnerGridRival);
            x += font.GetStringSize(text, fontSize: fontSize).X + 3;
        }

        if (info.NeutralCount.HasValue)
        {
            var text = $"N:{info.NeutralCount.Value}";
            DrawString(font, new Vector2(x, y), text, fontSize: fontSize, modulate: OwnerGridNeutral);
            x += font.GetStringSize(text, fontSize: fontSize).X + 3;
        }

        if (info.NobleCount.HasValue)
        {
            var text = $"X:{info.NobleCount.Value}";
            DrawString(font, new Vector2(x, y), text, fontSize: fontSize, modulate: OwnerGridNoble);
        }
    }

    /// <summary>
    /// Draws diagonal hatching lines to indicate ExtraDirty tile.
    /// </summary>
    private void DrawDirtyIndicator()
    {
        var hatchColor = new Color(0.5f, 0.4f, 0.2f, 0.5f);
        var spacing = 12f;
        var maxDim = Size.X + Size.Y;

        for (var offset = spacing; offset < maxDim; offset += spacing)
        {
            var x1 = Mathf.Max(0, offset - Size.Y);
            var y1 = Mathf.Min(offset, Size.Y);
            var x2 = Mathf.Min(offset, Size.X);
            var y2 = Mathf.Max(0, offset - Size.X);
            DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), hatchColor, 1.5f);
        }
    }

    public void UpdateVisual(Tile tile, List<string> globalClueOrder)
    {
        _isRevealed = tile.IsRevealed;
        _owner = tile.Owner;
        _adjacencyCount = tile.AdjacencyCount;
        _revealedBy = tile.RevealedBy;
        _annotations = tile.Annotations;
        _isDirty = tile.IsDirty;
        _isDestroyed = tile.IsDestroyed;
        _globalClueOrder = globalClueOrder;
        QueueRedraw();
    }

    public void SetUnused(bool unused)
    {
        _isUnused = unused;
        QueueRedraw();
    }

    public void SetHovered(bool hovered)
    {
        if (_isRevealed || _isUnused || _isDestroyed) return;
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

    public void SetAreaPreview(bool preview)
    {
        _isAreaPreview = preview;
        QueueRedraw();
    }

    public void ClearTargetingState()
    {
        _isTargetValid = false;
        _isTargetSelected = false;
        _isAreaPreview = false;
        QueueRedraw();
    }
}
