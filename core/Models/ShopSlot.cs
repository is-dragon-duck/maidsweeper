namespace Maidsweeper.Core.Models;

/// <summary>
/// One slot in the between-floor shop. The Kind determines what's being purchased
/// and which optional fields (Card / Equipment) are populated.
/// </summary>
public record ShopSlot
{
    public required int Index { get; init; }
    public required ShopSlotKind Kind { get; init; }
    public required int Price { get; init; }
    public Card? Card { get; init; }
    public Equipment? Equipment { get; init; }
    public bool IsPurchased { get; init; }
}
