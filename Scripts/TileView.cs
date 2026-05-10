using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Handles all visual rendering for a single tile.
/// Colors, adjacency numbers, hover effects, annotations, clue pips,
/// flag icon, adjacency info, and destroyed tile visuals.
/// Shape code: Player=square, Rival=circle, Neutral=diamond, Noble=octagon.
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

    // Targeting mode colors
    private static readonly Color TargetValidColor = new(0.35f, 0.45f, 0.35f);
    private static readonly Color TargetSelectedColor = new(0.9f, 0.8f, 0.2f);
    private static readonly Color TargetBorderColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color AreaPreviewColor = new(0.4f, 0.5f, 0.4f, 0.6f);

    // Owner shape fill colors (saturated, for annotation grids)
    private static readonly Color OwnerGridPlayer = new(1.0f, 0.55f, 0.65f);   // saturated pink
    private static readonly Color OwnerGridRival = new(0.45f, 0.65f, 1.0f);    // saturated blue
    private static readonly Color OwnerGridNeutral = new(0.85f, 0.85f, 0.85f); // light gray
    private static readonly Color OwnerGridNoble = new(0.7f, 0.4f, 0.85f);     // saturated purple

    // Adjacency badge background colors (lighter, for shape backgrounds behind numbers)
    private static readonly Color PlayerBadgeBg = new(1.0f, 0.85f, 0.88f);
    private static readonly Color RivalBadgeBg = new(0.82f, 0.88f, 1.0f);
    private static readonly Color NeutralBadgeBg = new(1.0f, 1.0f, 1.0f);
    private static readonly Color NobleBadgeBg = new(0.88f, 0.75f, 0.95f);

    // Adjacency badge text colors (darker)
    private static readonly Color PlayerAdjColor = new(0.6f, 0.1f, 0.2f);  // dark pink
    private static readonly Color RivalAdjColor = new(0.1f, 0.2f, 0.6f);   // dark blue
    private static readonly Color NeutralAdjColor = new(0.1f, 0.1f, 0.1f); // black
    private static readonly Color NobleAdjColor = new(0.4f, 0.15f, 0.5f);  // dark purple

    // Clue pip colors
    private static readonly Color PipColor = new(0.95f, 0.7f, 0.1f); // gold (positive clues)
    private static readonly Color AntiPipColor = new(0.9f, 0.25f, 0.25f); // red (anti-clues / Sarcastic)

    // Annotation marker colors
    private static readonly Color ExcludedMarkColor = new(0.9f, 0.3f, 0.3f);    // red crossout

    // Rival intent point marks (bottom-center): tiny blue X per point, large X per 5 points
    private static readonly Color IntentMarkColor = new(0.45f, 0.7f, 1.0f);

    // Stage 5 special-tile colors
    private static readonly Color CourtierColor = new(0.6f, 0.85f, 0.4f);          // bright green
    private static readonly Color CourtierArrowColor = new(0.4f, 0.65f, 0.25f);    // dark green
    private static readonly Color SoireeColor = new(0.95f, 0.55f, 0.25f);          // orange
    private static readonly Color LoungingNobleColor = new(0.7f, 0.4f, 0.85f);     // noble purple, applied as overlay
    private static readonly Color SanctumOuterColor = new(0.85f, 0.75f, 0.95f);    // pale purple ring (unrevealed)
    private static readonly Color SanctumPortalColor = new(0.55f, 0.3f, 0.75f);    // deep purple core (revealed)
    private static readonly Color InnerLockColor = new(0.45f, 0.45f, 0.55f);       // gray padlock for unreachable

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
    // Stage 5 special-tile flags
    private bool _isCourtier;
    private bool _isSoiree;
    private bool _isLoungingNoble;
    private bool _isSanctum;
    private bool _isInner;
    private bool _canReachInner;
    private Position? _courtierMoveTarget;
    private Position _selfPosition;
    private TileAnnotations _annotations = new();
    private List<string> _globalClueOrder = [];
    private TileOwner? _viewingPerspective;

    // Track previous adjacency for Brat un-reveal (dimmed display)
    private int? _previousAdjacencyCount;
    private PlayerType? _previousRevealedBy;
    private bool _isSaturated;

    // Rival intent points on this tile (0 = none drawn)
    private int _intentPoints;

    // ───────────────────────────────────────────────
    // Shape drawing primitives
    // ───────────────────────────────────────────────

    /// <summary>
    /// Draws the owner-coded shape (filled) at center with given half-size.
    /// Player=square, Rival=circle, Neutral=diamond, Noble=octagon.
    /// </summary>
    private void DrawOwnerShape(Vector2 center, float half, TileOwner owner, Color color)
    {
        switch (owner)
        {
            case TileOwner.Player:
                DrawRect(new Rect2(center.X - half, center.Y - half, half * 2, half * 2), color);
                break;
            case TileOwner.Rival:
                DrawCircle(center, half, color);
                break;
            case TileOwner.Neutral:
                DrawPolygon([
                    new Vector2(center.X, center.Y - half),
                    new Vector2(center.X + half, center.Y),
                    new Vector2(center.X, center.Y + half),
                    new Vector2(center.X - half, center.Y)
                ], [color]);
                break;
            case TileOwner.Noble:
                var cut = half * 0.38f;
                DrawPolygon([
                    new Vector2(center.X - half + cut, center.Y - half),
                    new Vector2(center.X + half - cut, center.Y - half),
                    new Vector2(center.X + half, center.Y - half + cut),
                    new Vector2(center.X + half, center.Y + half - cut),
                    new Vector2(center.X + half - cut, center.Y + half),
                    new Vector2(center.X - half + cut, center.Y + half),
                    new Vector2(center.X - half, center.Y + half - cut),
                    new Vector2(center.X - half, center.Y - half + cut)
                ], [color]);
                break;
        }
    }

    private static Color GetOwnerGridColor(TileOwner owner) => owner switch
    {
        TileOwner.Player => OwnerGridPlayer,
        TileOwner.Rival => OwnerGridRival,
        TileOwner.Neutral => OwnerGridNeutral,
        TileOwner.Noble => OwnerGridNoble,
        _ => OwnerGridNeutral
    };

    private static Color GetBadgeBgColor(TileOwner owner) => owner switch
    {
        TileOwner.Player => PlayerBadgeBg,
        TileOwner.Rival => RivalBadgeBg,
        TileOwner.Neutral => NeutralBadgeBg,
        TileOwner.Noble => NobleBadgeBg,
        _ => NeutralBadgeBg
    };

    private static Color GetBadgeTextColor(TileOwner owner) => owner switch
    {
        TileOwner.Player => PlayerAdjColor,
        TileOwner.Rival => RivalAdjColor,
        TileOwner.Neutral => NeutralAdjColor,
        TileOwner.Noble => NobleAdjColor,
        _ => NeutralAdjColor
    };

    /// <summary>
    /// Maps revealer to the owner type whose neighbors are being counted.
    /// Player reveals count Player neighbors, Rival reveals count Rival neighbors.
    /// </summary>
    private static TileOwner RevealerToOwner(PlayerType? revealedBy) =>
        revealedBy == PlayerType.Rival ? TileOwner.Rival : TileOwner.Player;

    // ───────────────────────────────────────────────
    // Adjacency badge: shape + number
    // ───────────────────────────────────────────────

    /// <summary>
    /// Draws an adjacency badge: owner-coded shape background with number text inside.
    /// </summary>
    private void DrawAdjacencyBadge(Vector2 center, float shapeHalf, int count,
        TileOwner owner, int fontSize, float alpha = 1.0f)
    {
        var bgColor = GetBadgeBgColor(owner);
        if (alpha < 1.0f) bgColor = new Color(bgColor.R, bgColor.G, bgColor.B, alpha);
        DrawOwnerShape(center, shapeHalf, owner, bgColor);

        var textColor = GetBadgeTextColor(owner);
        if (alpha < 1.0f) textColor = new Color(textColor.R, textColor.G, textColor.B, alpha);

        var font = ThemeDB.FallbackFont;
        var text = count.ToString();
        var textSize = font.GetStringSize(text, fontSize: fontSize);
        var textPos = new Vector2(
            center.X - textSize.X / 2,
            center.Y + textSize.Y / 2 - 2
        );
        DrawString(font, textPos, text, fontSize: fontSize, modulate: textColor);
    }

    // ───────────────────────────────────────────────
    // Main draw
    // ───────────────────────────────────────────────

    public override void _Draw()
    {
        if (_isUnused)
        {
            // A soirée sits on an unused (hole) tile and must still be visible.
            if (_isSoiree) DrawSoireeMark();
            return;
        }

        var rect = new Rect2(Vector2.Zero, Size);

        // Destroyed tiles: dark void with X
        if (_isDestroyed)
        {
            DrawRect(rect, DestroyedColor);
            DrawRect(rect, new Color(0.2f, 0.2f, 0.2f), false, 1.0f);
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
            else if (_isAreaPreview && _isHovered)
                bgColor = AreaPreviewColor.Lightened(0.2f);
            else if (_isAreaPreview)
                bgColor = AreaPreviewColor;
            else if (_isTargetValid && _isHovered)
                bgColor = TargetValidColor.Lightened(0.15f);
            else if (_isTargetValid)
                bgColor = TargetValidColor;
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

        // Border
        var borderColor = _isTargetValid ? TargetBorderColor : new Color(0.2f, 0.2f, 0.2f);
        var borderWidth = _isTargetSelected ? 3.0f : 1.0f;
        DrawRect(rect, borderColor, false, borderWidth);

        if (_isRevealed)
        {
            DrawRevealedAdjacency();
        }
        else
        {
            if (_previousAdjacencyCount.HasValue)
            {
                DrawDimmedAdjacencyBadge();
            }
            DrawAnnotations();
            if (_isDirty)
            {
                DrawDirtyIndicator();
            }
            DrawIntentMarks();
            if (_isLoungingNoble)
            {
                DrawLoungingNobleOverlay();
            }
            if (_isCourtier)
            {
                DrawCourtierOverlay();
            }
        }

        // Sanctum/inner-tile overlays paint on top of either revealed or unrevealed state.
        if (_isSanctum)
        {
            DrawSanctumOverlay();
        }
        if (_isInner && !_canReachInner)
        {
            DrawInnerLockOverlay();
        }
    }

    // ───────────────────────────────────────────────
    // Stage 5 special-tile overlays
    // ───────────────────────────────────────────────

    /// <summary>
    /// Soirée: orange filled diamond at tile center. Drawn even on hole tiles
    /// (where the rest of the tile is invisible).
    /// </summary>
    private void DrawSoireeMark()
    {
        var center = new Vector2(Size.X / 2, Size.Y / 2);
        var half = 10f;
        DrawPolygon([
            new Vector2(center.X, center.Y - half),
            new Vector2(center.X + half, center.Y),
            new Vector2(center.X, center.Y + half),
            new Vector2(center.X - half, center.Y)
        ], [SoireeColor]);
        // Thin outline so it stays visible against darker UI backgrounds.
        DrawLine(new Vector2(center.X, center.Y - half), new Vector2(center.X + half, center.Y),
            SoireeColor.Darkened(0.4f), 1f);
        DrawLine(new Vector2(center.X + half, center.Y), new Vector2(center.X, center.Y + half),
            SoireeColor.Darkened(0.4f), 1f);
        DrawLine(new Vector2(center.X, center.Y + half), new Vector2(center.X - half, center.Y),
            SoireeColor.Darkened(0.4f), 1f);
        DrawLine(new Vector2(center.X - half, center.Y), new Vector2(center.X, center.Y - half),
            SoireeColor.Darkened(0.4f), 1f);
    }

    /// <summary>
    /// Lounging noble: small noble-shaped (octagon) inset, top-left of unrevealed tile.
    /// Indicates the tile will function as a noble if revealed by the player.
    /// </summary>
    private void DrawLoungingNobleOverlay()
    {
        var center = new Vector2(11f, 11f);
        var half = 7f;
        DrawOwnerShape(center, half, TileOwner.Noble, LoungingNobleColor);
        DrawOwnerShape(center, half, TileOwner.Noble, LoungingNobleColor.Darkened(0.5f));
    }

    /// <summary>
    /// Courtier: small green triangle in the bottom-left of the tile, plus a thin
    /// arrow line pointing toward its MoveTarget (clamped to the tile edge).
    /// </summary>
    private void DrawCourtierOverlay()
    {
        var center = new Vector2(11f, Size.Y - 11f);
        var r = 7f;
        // Triangle pointing up
        DrawPolygon([
            new Vector2(center.X, center.Y - r),
            new Vector2(center.X + r, center.Y + r),
            new Vector2(center.X - r, center.Y + r)
        ], [CourtierColor]);
        DrawLine(new Vector2(center.X, center.Y - r), new Vector2(center.X + r, center.Y + r),
            CourtierColor.Darkened(0.4f), 1f);
        DrawLine(new Vector2(center.X + r, center.Y + r), new Vector2(center.X - r, center.Y + r),
            CourtierColor.Darkened(0.4f), 1f);
        DrawLine(new Vector2(center.X - r, center.Y + r), new Vector2(center.X, center.Y - r),
            CourtierColor.Darkened(0.4f), 1f);

        // Arrow toward MoveTarget (if any), drawn from tile center to the target edge
        if (_courtierMoveTarget is { } target)
        {
            var dRow = target.Row - _selfPosition.Row;
            var dCol = target.Col - _selfPosition.Col;
            if (dRow == 0 && dCol == 0) return;

            var tileCenter = new Vector2(Size.X / 2f, Size.Y / 2f);
            var dir = new Vector2(dCol, dRow).Normalized();
            var arrowLen = 14f;
            var tip = tileCenter + dir * arrowLen;
            DrawLine(tileCenter, tip, CourtierArrowColor, 2f);

            // Arrowhead
            var perp = new Vector2(-dir.Y, dir.X);
            var headBase = tip - dir * 4f;
            DrawLine(tip, headBase + perp * 3f, CourtierArrowColor, 2f);
            DrawLine(tip, headBase - perp * 3f, CourtierArrowColor, 2f);
        }
    }

    /// <summary>
    /// Sanctum: when unrevealed, draw a pale purple ring border.
    /// When revealed, the tile already shows its adjacency badge — overlay a
    /// darker purple ring to flag it as a portal that bridges adjacency.
    /// </summary>
    private void DrawSanctumOverlay()
    {
        var center = new Vector2(Size.X / 2f, Size.Y / 2f);
        var radius = Size.X * 0.42f;
        if (!_isRevealed)
        {
            DrawArc(center, radius, 0f, Mathf.Tau, 32, SanctumOuterColor, 3f);
        }
        else
        {
            DrawArc(center, radius, 0f, Mathf.Tau, 32, SanctumPortalColor, 2.5f);
            // Small portal "core" dot at upper-right corner
            DrawCircle(new Vector2(Size.X - 9f, 9f), 4f, SanctumPortalColor);
        }
    }

    /// <summary>
    /// Inner tile that can't currently be reached (no adjacent sanctum revealed).
    /// Dim the whole tile and overlay a small padlock-style icon.
    /// </summary>
    private void DrawInnerLockOverlay()
    {
        // Dim the tile contents
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0f, 0f, 0f, 0.45f));

        // Padlock: small rectangle with a U-shaped shackle on top
        var bodyW = 12f;
        var bodyH = 10f;
        var cx = Size.X / 2f;
        var cy = Size.Y / 2f + 2f;
        var bodyRect = new Rect2(cx - bodyW / 2f, cy - bodyH / 2f, bodyW, bodyH);
        DrawRect(bodyRect, InnerLockColor);
        DrawRect(bodyRect, InnerLockColor.Darkened(0.4f), false, 1f);

        // Shackle (arc above the body)
        var shackleCenter = new Vector2(cx, cy - bodyH / 2f);
        DrawArc(shackleCenter, 4f, Mathf.Pi, Mathf.Tau, 12, InnerLockColor.Darkened(0.4f), 1.5f);
    }

    /// <summary>
    /// Bottom-center: a single intent-strength marker.
    /// 0–2 points: nothing. 3–5 points: small blue X. 6+ points: large blue X.
    /// </summary>
    private void DrawIntentMarks()
    {
        if (_intentPoints < 3) return;

        var isLarge = _intentPoints >= 6;
        var halfSize = isLarge ? 4f : 2.5f;
        var lineWidth = isLarge ? 1.5f : 1.0f;
        var center = new Vector2(Size.X / 2f, Size.Y - 8f);
        DrawXMark(center, halfSize, lineWidth);
    }

    private void DrawXMark(Vector2 center, float halfSize, float lineWidth)
    {
        DrawLine(
            new Vector2(center.X - halfSize, center.Y - halfSize),
            new Vector2(center.X + halfSize, center.Y + halfSize),
            IntentMarkColor, lineWidth);
        DrawLine(
            new Vector2(center.X - halfSize, center.Y + halfSize),
            new Vector2(center.X + halfSize, center.Y - halfSize),
            IntentMarkColor, lineWidth);
    }

    // ───────────────────────────────────────────────
    // Revealed tile adjacency (shape-coded badge)
    // ───────────────────────────────────────────────

    private void DrawRevealedAdjacency()
    {
        var owner = RevealerToOwner(_revealedBy);
        var center = new Vector2(Size.X / 2, Size.Y / 2);
        DrawAdjacencyBadge(center, 13f, _adjacencyCount, owner, 18);

        if (_isSaturated)
        {
            DrawSaturationCheck();
        }
    }

    private static readonly Color SaturationCheckColor = new(0.7f, 0.2f, 0.3f); // dark pink/red

    /// <summary>
    /// Draws a small check mark at the bottom-right corner of the adjacency badge.
    /// </summary>
    private void DrawSaturationCheck()
    {
        // Position at bottom-right of the badge area
        var cx = Size.X / 2 + 12f;
        var cy = Size.Y / 2 + 12f;
        // Check mark: short down-right stroke, then longer up-right stroke
        var p1 = new Vector2(cx - 4f, cy - 1f);
        var p2 = new Vector2(cx - 1f, cy + 2f);
        var p3 = new Vector2(cx + 4f, cy - 4f);
        DrawLine(p1, p2, SaturationCheckColor, 2.0f);
        DrawLine(p2, p3, SaturationCheckColor, 2.0f);
    }

    private void DrawDimmedAdjacencyBadge()
    {
        var owner = RevealerToOwner(_previousRevealedBy);
        var center = new Vector2(Size.X / 2, Size.Y / 2);
        DrawAdjacencyBadge(center, 13f, _previousAdjacencyCount!.Value, owner, 18, 0.35f);
    }

    // ───────────────────────────────────────────────
    // Annotations on unrevealed tiles
    // ───────────────────────────────────────────────

    private void DrawAnnotations()
    {
        DrawCluePips();
        DrawOwnerGrid();
        DrawPlayerAnnotationGrid();
        DrawEavesdropAdjacency();
        DrawPerspectiveCrossout();
    }

    /// <summary>
    /// When viewing from a specific owner perspective (any annotation mode),
    /// draw a thin red diagonal line on tiles whose combined annotations exclude that owner.
    /// </summary>
    private void DrawPerspectiveCrossout()
    {
        if (_viewingPerspective == null) return;

        var effective = _annotations.EffectiveOwnerSubset;
        if (effective == null) return;
        if (effective.Contains(_viewingPerspective.Value)) return;

        var margin = 6f;
        DrawLine(
            new Vector2(margin, Size.Y - margin),
            new Vector2(Size.X - margin, margin),
            ExcludedMarkColor, 1.5f);
    }

    /// <summary>
    /// Top-left: clue pips from Recall cards.
    /// Green circles = positive clues (Imperious, Vague).
    /// Red squares = anti-clues (Sarcastic) — "probably NOT yours."
    /// </summary>
    private void DrawCluePips()
    {
        var clues = _annotations.ClueResults;
        if (clues.Count == 0) return;

        var pipRadius = 3.5f;
        var pipSpacing = 10f;
        var startY = 10f;
        var rowHeight = 10f;

        foreach (var clue in clues)
        {
            var globalRow = _globalClueOrder.IndexOf(clue.ClueId);
            if (globalRow < 0) globalRow = 0;

            var y = startY + globalRow * rowHeight;
            var startX = 5f + pipRadius;

            for (var i = 0; i < clue.PipStrength; i++)
            {
                var x = startX + i * pipSpacing;
                if (clue.IsAntiClue)
                {
                    // Red square for anti-clue pips
                    var halfSize = pipRadius * 0.85f;
                    DrawRect(new Rect2(x - halfSize, y - halfSize, halfSize * 2, halfSize * 2), AntiPipColor);
                }
                else
                {
                    // Green/gold circle for positive clue pips
                    DrawCircle(new Vector2(x, y), pipRadius, PipColor);
                }
            }
        }
    }

    // ───────────────────────────────────────────────
    // Owner annotation grids (shape-coded, smaller)
    // ───────────────────────────────────────────────

    /// <summary>
    /// Lower-right: 2x2 grid of owner-coded shapes showing the effective owner subset.
    /// Shows "?" when player and game annotations conflict.
    /// </summary>
    private void DrawOwnerGrid()
    {
        var subset = _annotations.EffectiveOwnerSubset;
        if (subset == null) return;

        if (HasAnnotationConflict())
        {
            DrawOwnerGridQuestionMark();
            return;
        }

        var half = 3f;      // shape half-size (6px total)
        var spacing = 8f;   // center-to-center distance
        // Grid of 4 shapes: 2 columns, 2 rows
        var gridWidth = spacing;
        var gridHeight = spacing;
        var centerX = Size.X - gridWidth / 2 - 5;
        var centerY = Size.Y - gridHeight / 2 - 5;

        // NW = Player
        if (subset.Contains(TileOwner.Player))
            DrawOwnerShape(new Vector2(centerX - spacing / 2, centerY - spacing / 2), half, TileOwner.Player, OwnerGridPlayer);
        // NE = Neutral
        if (subset.Contains(TileOwner.Neutral))
            DrawOwnerShape(new Vector2(centerX + spacing / 2, centerY - spacing / 2), half, TileOwner.Neutral, OwnerGridNeutral);
        // SW = Rival
        if (subset.Contains(TileOwner.Rival))
            DrawOwnerShape(new Vector2(centerX - spacing / 2, centerY + spacing / 2), half, TileOwner.Rival, OwnerGridRival);
        // SE = Noble
        if (subset.Contains(TileOwner.Noble))
            DrawOwnerShape(new Vector2(centerX + spacing / 2, centerY + spacing / 2), half, TileOwner.Noble, OwnerGridNoble);
    }

    private bool HasAnnotationConflict()
    {
        var gameSubset = _annotations.OwnerSubset;
        if (gameSubset == null) return false;

        var confirmed = _annotations.PlayerConfirmed;
        if (confirmed != null && confirmed.Count > 0)
        {
            if (!confirmed.Any(c => gameSubset.Contains(c)))
                return true;
        }

        var excluded = _annotations.PlayerExcluded;
        if (excluded != null)
        {
            var remaining = new HashSet<TileOwner>(gameSubset);
            remaining.ExceptWith(excluded);
            if (remaining.Count == 0)
                return true;
        }

        return false;
    }

    private void DrawOwnerGridQuestionMark()
    {
        var font = ThemeDB.FallbackFont;
        var fontSize = 14;
        var text = "?";
        var textSize = font.GetStringSize(text, fontSize: fontSize);
        var x = Size.X - textSize.X - 6;
        var y = Size.Y - 6;
        DrawString(font, new Vector2(x, y), text, fontSize: fontSize,
            modulate: new Color(0.9f, 0.7f, 0.2f));
    }

    #nullable enable
    private HashSet<TileOwner>? GetPlayerAnnotationSet()
    {
        var excluded = _annotations.PlayerExcluded;
        var confirmed = _annotations.PlayerConfirmed;
        if (excluded == null && confirmed == null) return null;

        var possible = new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble };
        if (excluded != null) possible.ExceptWith(excluded);
        if (confirmed != null && confirmed.Count > 0) possible.IntersectWith(confirmed);
        return possible.Count == 4 ? null : possible;
    }
    #nullable restore

    /// <summary>
    /// Top-right: 2x2 grid showing player's manual annotations (shape-coded).
    /// Only drawn when the player set differs from the effective (combined) set.
    /// </summary>
    private void DrawPlayerAnnotationGrid()
    {
        var playerSet = GetPlayerAnnotationSet();
        if (playerSet == null) return;

        var effective = _annotations.EffectiveOwnerSubset;
        if (effective != null && playerSet.SetEquals(effective)) return;

        var half = 2.5f;    // shape half-size (5px total)
        var spacing = 7f;
        var centerX = Size.X - spacing / 2 - 5;
        var centerY = spacing / 2 + 4;

        if (playerSet.Contains(TileOwner.Player))
            DrawOwnerShape(new Vector2(centerX - spacing / 2, centerY - spacing / 2), half, TileOwner.Player, OwnerGridPlayer);
        if (playerSet.Contains(TileOwner.Neutral))
            DrawOwnerShape(new Vector2(centerX + spacing / 2, centerY - spacing / 2), half, TileOwner.Neutral, OwnerGridNeutral);
        if (playerSet.Contains(TileOwner.Rival))
            DrawOwnerShape(new Vector2(centerX - spacing / 2, centerY + spacing / 2), half, TileOwner.Rival, OwnerGridRival);
        if (playerSet.Contains(TileOwner.Noble))
            DrawOwnerShape(new Vector2(centerX + spacing / 2, centerY + spacing / 2), half, TileOwner.Noble, OwnerGridNoble);
    }

    // ───────────────────────────────────────────────
    // Eavesdrop/AcceptHelp/Deliver adjacency info
    // (shape-coded badges on unrevealed tiles)
    // ───────────────────────────────────────────────

    private void DrawEavesdropAdjacency()
    {
        var info = _annotations.AdjacencyInfo;
        if (info == null) return;

        // Count how many owner types have data
        var count = 0;
        if (info.PlayerCount.HasValue) count++;
        if (info.RivalCount.HasValue) count++;
        if (info.NeutralCount.HasValue) count++;
        if (info.NobleCount.HasValue) count++;

        var center = new Vector2(Size.X / 2, Size.Y / 2);

        if (count == 1)
        {
            // Single adjacency info: one badge in center
            if (info.PlayerCount.HasValue)
                DrawAdjacencyBadge(center, 10f, info.PlayerCount.Value, TileOwner.Player, 14);
            else if (info.RivalCount.HasValue)
                DrawAdjacencyBadge(center, 10f, info.RivalCount.Value, TileOwner.Rival, 14);
            else if (info.NeutralCount.HasValue)
                DrawAdjacencyBadge(center, 10f, info.NeutralCount.Value, TileOwner.Neutral, 14);
            else if (info.NobleCount.HasValue)
                DrawAdjacencyBadge(center, 10f, info.NobleCount.Value, TileOwner.Noble, 14);
        }
        else
        {
            // Multiple: 2x2 formation in center, smaller badges
            var half = 7f;
            var gap = 9f; // center-to-center
            // NW=Player, NE=Neutral, SW=Rival, SE=Noble (same layout)
            if (info.PlayerCount.HasValue)
                DrawAdjacencyBadge(new Vector2(center.X - gap / 2, center.Y - gap / 2),
                    half, info.PlayerCount.Value, TileOwner.Player, 10);
            if (info.NeutralCount.HasValue)
                DrawAdjacencyBadge(new Vector2(center.X + gap / 2, center.Y - gap / 2),
                    half, info.NeutralCount.Value, TileOwner.Neutral, 10);
            if (info.RivalCount.HasValue)
                DrawAdjacencyBadge(new Vector2(center.X - gap / 2, center.Y + gap / 2),
                    half, info.RivalCount.Value, TileOwner.Rival, 10);
            if (info.NobleCount.HasValue)
                DrawAdjacencyBadge(new Vector2(center.X + gap / 2, center.Y + gap / 2),
                    half, info.NobleCount.Value, TileOwner.Noble, 10);
        }
    }

    // ───────────────────────────────────────────────
    // Misc drawing helpers
    // ───────────────────────────────────────────────

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

    // ───────────────────────────────────────────────
    // State updates
    // ───────────────────────────────────────────────

    public void UpdateVisual(Tile tile, List<string> globalClueOrder, TileOwner? viewingPerspective = null, bool saturated = false, int intentPoints = 0, bool canReachInner = true)
    {
        // Track Brat un-reveal: if tile was revealed and is now unrevealed, preserve adjacency
        if (_isRevealed && !tile.IsRevealed)
        {
            _previousAdjacencyCount = _adjacencyCount;
            _previousRevealedBy = _revealedBy;
        }
        else if (tile.IsRevealed)
        {
            _previousAdjacencyCount = null;
            _previousRevealedBy = null;
        }

        _isRevealed = tile.IsRevealed;
        _owner = tile.Owner;
        _adjacencyCount = tile.AdjacencyCount;
        _revealedBy = tile.RevealedBy;
        _annotations = tile.Annotations;
        _isDirty = tile.IsDirty;
        _isDestroyed = tile.IsDestroyed;
        _isSaturated = saturated;
        _globalClueOrder = globalClueOrder;
        _viewingPerspective = viewingPerspective;
        _intentPoints = intentPoints;
        _isCourtier = tile.IsCourtier;
        _isSoiree = tile.IsSoiree;
        _isLoungingNoble = tile.IsLoungingNoble;
        _isSanctum = tile.IsSanctum;
        _isInner = tile.IsInner;
        _canReachInner = canReachInner;
        _courtierMoveTarget = tile.CourtierMoveTarget;
        _selfPosition = tile.Position;
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
