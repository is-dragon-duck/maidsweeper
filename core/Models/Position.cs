namespace Maidsweeper.Core.Models;

public readonly record struct Position(int Row, int Col)
{
    public static readonly (int DRow, int DCol)[] KingOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1),
        ( 0, -1),          ( 0, 1),
        ( 1, -1), ( 1, 0), ( 1, 1)
    ];

    /// <summary>
    /// Manhattan-2 offsets: all positions within Manhattan distance ≤ 2 (up to 12 neighbors).
    /// Includes king adjacency (distance 1-2) plus the 4 extended cardinal positions (distance 2).
    /// </summary>
    public static readonly (int DRow, int DCol)[] Manhattan2Offsets =
    [
        (-2,  0),
        (-1, -1), (-1, 0), (-1, 1),
        ( 0, -2), ( 0,-1), ( 0, 1), ( 0, 2),
        ( 1, -1), ( 1, 0), ( 1, 1),
        ( 2,  0)
    ];

    public bool IsWithinBounds(int height, int width)
        => Row >= 0 && Row < height && Col >= 0 && Col < width;

    public override string ToString() => $"({Row},{Col})";
}
