namespace Maidsweeper.Core.Models;

/// <summary>
/// Result of a single tile's participation in a clue (Instructions card).
/// A tile that received 3 pips from a clue has PipStrength = 3.
/// </summary>
public record ClueResult
{
    public required Position TilePosition { get; init; }
    public required int PipStrength { get; init; }
    public required IReadOnlyList<Position> AllAffectedTiles { get; init; }
    public required string ClueId { get; init; }
    public int ClueOrder { get; init; }
    public int ClueRowPosition { get; init; }
}
