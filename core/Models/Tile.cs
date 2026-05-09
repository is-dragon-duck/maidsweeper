namespace Maidsweeper.Core.Models;

public record Tile
{
    public required Position Position { get; init; }
    public required TileOwner Owner { get; init; }
    public bool IsRevealed { get; init; }
    public PlayerType? RevealedBy { get; init; }
    public int AdjacencyCount { get; init; }
    public TileAnnotations Annotations { get; init; } = new();
    public SpecialTileType Specials { get; init; } = SpecialTileType.None;
    public bool IsDestroyed { get; init; }
    public bool ProtectedByExcuses { get; init; }
    /// <summary>
    /// Predetermined destination for the courtier on this tile (if IsCourtier).
    /// Visible to the player so they can see where the courtier will move when interacted with.
    /// Null when there's no courtier here (or no valid adjacent target exists).
    /// </summary>
    public Position? CourtierMoveTarget { get; init; }

    public bool IsDirty => Specials.HasFlag(SpecialTileType.ExtraDirty);
    public bool IsCourtier => Specials.HasFlag(SpecialTileType.Courtier);
    public bool IsSoiree => Specials.HasFlag(SpecialTileType.Soiree);
    public bool IsLoungingNoble => Specials.HasFlag(SpecialTileType.LoungingNoble);
    public bool IsSanctum => Specials.HasFlag(SpecialTileType.Sanctum);
    public bool IsInner => Specials.HasFlag(SpecialTileType.InnerTile);

    public Tile WithSpecial(SpecialTileType flag) =>
        this with { Specials = Specials | flag };

    public Tile WithoutSpecial(SpecialTileType flag) =>
        this with { Specials = Specials & ~flag };
}
