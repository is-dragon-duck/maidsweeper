namespace Maidsweeper.Core.Models;

/// <summary>
/// Pixel constants for board rendering, shared between Godot scene config and tests.
/// MaxGridWidthPx / MaxGridHeightPx must match the BoardMargin's reserved area in Main.tscn —
/// any level whose grid exceeds these dimensions will overflow the UI.
/// </summary>
public static class BoardLayout
{
    public const int TileSize = 64;
    public const int TileGap = 4;

    /// <summary>
    /// Maximum interior board area (excluding the BoardMargin's outer padding).
    /// Sized for the largest configured level (Floor 8: 8x7).
    /// </summary>
    public const int MaxGridWidthPx = 540;
    public const int MaxGridHeightPx = 472;

    public static int RequiredWidth(int cols) =>
        cols <= 0 ? 0 : cols * TileSize + (cols - 1) * TileGap;

    public static int RequiredHeight(int rows) =>
        rows <= 0 ? 0 : rows * TileSize + (rows - 1) * TileGap;
}
