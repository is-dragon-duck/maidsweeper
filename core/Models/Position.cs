namespace Maidsweeper.Core.Models;

public readonly record struct Position(int Row, int Col)
{
    public static readonly (int DRow, int DCol)[] KingOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1),
        ( 0, -1),          ( 0, 1),
        ( 1, -1), ( 1, 0), ( 1, 1)
    ];

    public bool IsWithinBounds(int height, int width)
        => Row >= 0 && Row < height && Col >= 0 && Col < width;

    public override string ToString() => $"({Row},{Col})";
}
