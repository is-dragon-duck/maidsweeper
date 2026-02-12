namespace Maidsweeper.Core.Models;

/// <summary>
/// Annotations placed on a tile through card effects.
/// These surface imperfect information to help the player deduce tile owners.
/// </summary>
public record TileAnnotations
{
    /// <summary>
    /// Narrowed set of possible owners for this tile (from Spritz, Tingle, Scurry).
    /// Null means no information yet (all owners possible).
    /// A single-element set means the owner is confirmed.
    /// Multiple Spritz/Tingle results intersect to narrow this down.
    /// </summary>
    public HashSet<TileOwner>? OwnerSubset { get; init; }

    /// <summary>
    /// Clue pip results from Instructions cards.
    /// Each Instructions play adds one ClueResult if this tile was affected.
    /// More pips = more likely to be the target owner type.
    /// </summary>
    public IReadOnlyList<ClueResult> ClueResults { get; init; } = [];
}
