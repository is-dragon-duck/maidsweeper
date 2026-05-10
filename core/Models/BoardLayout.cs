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
    /// Sized for the largest configured level (alpha L13/L18/L20/L21: 10×10 = 676×676).
    /// </summary>
    public const int MaxGridWidthPx = 676;
    public const int MaxGridHeightPx = 676;

    public static int RequiredWidth(int cols) =>
        cols <= 0 ? 0 : cols * TileSize + (cols - 1) * TileGap;

    public static int RequiredHeight(int rows) =>
        rows <= 0 ? 0 : rows * TileSize + (rows - 1) * TileGap;
}
