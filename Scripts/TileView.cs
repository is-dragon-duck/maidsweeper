using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Handles all visual rendering for a single tile.
/// Colors, adjacency numbers, hover effects.
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
    private static readonly Color TargetValidColor = new(0.35f, 0.45f, 0.35f);  // greenish gray
    private static readonly Color TargetSelectedColor = new(0.9f, 0.8f, 0.2f);  // yellow highlight
    private static readonly Color TargetBorderColor = new(0.2f, 0.8f, 0.2f);    // green border

    private bool _isRevealed;
    private TileOwner _owner;
    private int _adjacencyCount;
    private PlayerType? _revealedBy;
    private bool _isHovered;
    private bool _isTargetValid;
    private bool _isTargetSelected;

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

        // Adjacency number when revealed (always shown, including 0)
        if (_isRevealed)
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
    }

    public void UpdateVisual(Tile tile)
    {
        _isRevealed = tile.IsRevealed;
        _owner = tile.Owner;
        _adjacencyCount = tile.AdjacencyCount;
        _revealedBy = tile.RevealedBy;
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
