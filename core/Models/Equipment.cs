namespace Maidsweeper.Core.Models;

public record Equipment
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required EquipmentEffectType EffectType { get; init; }
    /// <summary>
    /// Equipment that must already be owned for this one to be offered. Empty means
    /// no prereqs. The offering filter is enforced in M47.
    /// </summary>
    public IReadOnlyList<EquipmentEffectType> Prereqs { get; init; } = [];
}
