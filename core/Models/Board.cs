namespace Maidsweeper.Core.Models;

public record Board
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required IReadOnlyList<Tile> Tiles { get; init; }
    public IReadOnlySet<Position> UnusedPositions { get; init; } = new HashSet<Position>();
    public AdjacencyRule AdjacencyRule { get; init; } = AdjacencyRule.King;

    public Tile GetTile(Position pos) => Tiles[pos.Row * Width + pos.Col];

    public int TileIndex(Position pos) => pos.Row * Width + pos.Col;

    public bool IsValidPosition(Position pos) => pos.IsWithinBounds(Height, Width);

    public bool IsUsablePosition(Position pos) => IsValidPosition(pos) && !UnusedPositions.Contains(pos);
}
