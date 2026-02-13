namespace Maidsweeper.Core.Models;

public record Card
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required int Cost { get; init; }
    public bool Exhaust { get; init; }
    public required CardEffectType EffectType { get; init; }
    public bool Enhanced { get; init; }
    public bool SpoonReduced { get; init; }
}
