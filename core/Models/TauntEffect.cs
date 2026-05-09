namespace Maidsweeper.Core.Models;

/// <summary>
/// One Taunt status effect: a set of tagged tile positions and a threshold of
/// rival reveals that ends the rival's turn early. Created by playing a Taunt
/// card. Multiple Taunts can coexist; each is independently triggered.
/// </summary>
public sealed record TauntEffect
{
    public required IReadOnlySet<Position> Positions { get; init; }
    public required int RequiredReveals { get; init; }
}
