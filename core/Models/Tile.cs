namespace Maidsweeper.Core.Models;

public record Tile
{
    public required Position Position { get; init; }
    public required TileOwner Owner { get; init; }
    public bool IsRevealed { get; init; }
    public PlayerType? RevealedBy { get; init; }
    public int AdjacencyCount { get; init; }
    public TileAnnotations Annotations { get; init; } = new();
}
