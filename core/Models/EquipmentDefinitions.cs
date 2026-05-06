namespace Maidsweeper.Core.Models;

/// <summary>
/// Static equipment templates. Equipment instances in the game are copies of these with unique IDs.
/// Effects themselves are implemented in M30/M31; this milestone defines the data and offering flow.
/// </summary>
public static class EquipmentDefinitions
{
    // --- Set 1: Simple passives (M30) ---

    public static Equipment Coffee => new()
    {
        Id = "",
        Name = "Coffee",
        Description = "+1 max spoon per turn, but draw 1 fewer card (except turn 1).",
        EffectType = EquipmentEffectType.Coffee
    };

    public static Equipment FrillyDress => new()
    {
        Id = "",
        Name = "Frilly Dress",
        Description = "On turn 1, the first 4 neutral reveals don't end your turn.",
        EffectType = EquipmentEffectType.FrillyDress
    };

    public static Equipment DustBunny => new()
    {
        Id = "",
        Name = "Dust Bunny",
        Description = "Reveal 1 random player tile at floor start.",
        EffectType = EquipmentEffectType.DustBunny
    };

    public static Equipment Handbag => new()
    {
        Id = "",
        Name = "Handbag",
        Description = "Draw 2 extra cards on the first turn.",
        EffectType = EquipmentEffectType.Handbag
    };

    public static Equipment Eyeshadow => new()
    {
        Id = "",
        Name = "Eyeshadow",
        Description = "Gain 1 Distraction stack at the start of each turn.",
        EffectType = EquipmentEffectType.Eyeshadow
    };

    public static Equipment Glasses => new()
    {
        Id = "",
        Name = "Glasses",
        Description = "Free Tingle effect at the start of each turn.",
        EffectType = EquipmentEffectType.Glasses
    };

    // --- Set 2: Deck modifiers (M31) ---

    public static Equipment Bleach => new()
    {
        Id = "",
        Name = "Bleach",
        Description = "Enhances all Spritz, Sweep, and Brush in your deck. Future ones too.",
        EffectType = EquipmentEffectType.Bleach
    };

    public static Equipment Estrogen => new()
    {
        Id = "",
        Name = "Estrogen",
        Description = "Adds bonus spoon to 3 random cards in your deck.",
        EffectType = EquipmentEffectType.Estrogen
    };

    public static Equipment Progesterone => new()
    {
        Id = "",
        Name = "Progesterone",
        Description = "Enhances 3 random cards in your deck.",
        EffectType = EquipmentEffectType.Progesterone
    };

    public static Equipment CrystalBall => new()
    {
        Id = "",
        Name = "Crystal Ball",
        Description = "Adds 3 doubly-upgraded Tingle cards to your deck.",
        EffectType = EquipmentEffectType.CrystalBall
    };

    public static Equipment Boots => new()
    {
        Id = "",
        Name = "Boots",
        Description = "Replaces 1 random card with a doubly-upgraded random reward card.",
        EffectType = EquipmentEffectType.Boots
    };

    public static Equipment Tiara => new()
    {
        Id = "",
        Name = "Tiara",
        Description = "Doubles all copper rewards.",
        EffectType = EquipmentEffectType.Tiara
    };

    /// <summary>
    /// Returns all equipment templates available as offerings.
    /// </summary>
    public static List<Equipment> CreateOfferingPool()
    {
        return
        [
            Coffee, FrillyDress, DustBunny, Handbag, Eyeshadow, Glasses,
            Bleach, Estrogen, Progesterone, CrystalBall, Boots, Tiara
        ];
    }
}
