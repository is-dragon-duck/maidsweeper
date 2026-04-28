namespace Maidsweeper.Core.Models;

/// <summary>
/// Per-owner-type neighbor counts for a tile, placed by cards like Eavesdrop.
/// </summary>
public record AdjacencyInfo
{
    public int? PlayerCount { get; init; }
    public int? RivalCount { get; init; }
    public int? NeutralCount { get; init; }
    public int? NobleCount { get; init; }
}

/// <summary>
/// Annotations placed on a tile through card effects and player input.
/// These surface imperfect information to help the player deduce tile owners.
/// </summary>
public record TileAnnotations
{
    /// <summary>
    /// Narrowed set of possible owners for this tile (from Spritz, Tingle, Scurry, etc.).
    /// Null means no information yet (all owners possible).
    /// A single-element set means the owner is confirmed.
    /// Multiple card results intersect to narrow this down.
    /// </summary>
    public HashSet<TileOwner>? OwnerSubset { get; init; }

    /// <summary>
    /// Clue pip results from Instructions cards.
    /// Each Instructions play adds one ClueResult if this tile was affected.
    /// More pips = more likely to be the target owner type.
    /// </summary>
    public IReadOnlyList<ClueResult> ClueResults { get; init; } = [];

    /// <summary>
    /// Per-owner-type adjacency counts placed by cards like Eavesdrop, Accept Help, Deliver.
    /// Null means no adjacency info. Non-null contains counts for each owner type.
    /// </summary>
    public AdjacencyInfo? AdjacencyInfo { get; init; }

    /// <summary>
    /// Owner types the player has manually excluded via annotation UI.
    /// Tracked separately from card-derived OwnerSubset so they don't interfere.
    /// </summary>
    public HashSet<TileOwner>? PlayerExcluded { get; init; }

    /// <summary>
    /// Owner types the player has manually confirmed via annotation UI.
    /// Tracked separately from card-derived OwnerSubset so they don't interfere.
    /// </summary>
    public HashSet<TileOwner>? PlayerConfirmed { get; init; }

    /// <summary>
    /// Whether the player has flagged this tile (black slash = "not mine / skip").
    /// Visual aid only — no game logic impact.
    /// </summary>
    public bool Flagged { get; init; }

    /// <summary>
    /// The effective owner subset combining card annotations and player exclusions.
    /// This is what should be displayed and used for deduction hints.
    /// </summary>
    public HashSet<TileOwner>? EffectiveOwnerSubset
    {
        get
        {
            if (OwnerSubset == null && PlayerExcluded == null && PlayerConfirmed == null)
                return null;

            var allOwners = new HashSet<TileOwner>
                { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble };

            var result = OwnerSubset != null ? new HashSet<TileOwner>(OwnerSubset) : allOwners;

            if (PlayerExcluded != null)
                result.ExceptWith(PlayerExcluded);

            // If player has confirmed types, intersect with those
            if (PlayerConfirmed != null && PlayerConfirmed.Count > 0)
                result.IntersectWith(PlayerConfirmed);

            return result;
        }
    }
}
