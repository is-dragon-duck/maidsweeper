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

    // --- Set 3: Stage 5 mechanics (M45) ---

    public static Equipment Mop => new()
    {
        Id = "",
        Name = "Mop",
        Description = "When you clean a courtier, draw 1 card.",
        EffectType = EquipmentEffectType.Mop
    };

    public static Equipment Espresso => new()
    {
        Id = "",
        Name = "Espresso",
        Description = "At the start of each turn, draw an extra card and auto-play your cheapest non-targeting card (if you can afford it).",
        EffectType = EquipmentEffectType.Espresso,
        Prereqs = [EquipmentEffectType.Coffee]
    };

    // --- Stage 4 Stretch (deferred, M46) ---

    public static Equipment Hyperfocus => new()
    {
        Id = "",
        Name = "Hyperfocus",
        Description = "At floor start, pull one net-cost-0 card from your deck into your hand.",
        EffectType = EquipmentEffectType.Hyperfocus
    };

    public static Equipment Choker => new()
    {
        Id = "",
        Name = "Choker",
        Description = "Rival's turn ends early when 5 or fewer unrevealed tiles remain on the board.",
        EffectType = EquipmentEffectType.Choker
    };

    public static Equipment Mirror => new()
    {
        Id = "",
        Name = "Mirror",
        Description = "At floor start, reveal 1 random rival tile and add player adjacency info to its neighbors.",
        EffectType = EquipmentEffectType.Mirror
    };

    public static Equipment BusyCanary => new()
    {
        Id = "",
        Name = "Busy Canary",
        Description = "At floor start, scan up to 2 cross areas (random) for nobles.",
        EffectType = EquipmentEffectType.BusyCanary
    };

    public static Equipment DoubleBroom => new()
    {
        Id = "",
        Name = "Double Broom",
        Description = "When you reveal a tile, Brush 2 random adjacent unrevealed tiles.",
        EffectType = EquipmentEffectType.DoubleBroom
    };

    public static Equipment BroomCloset => new()
    {
        Id = "",
        Name = "Broom Closet",
        Description = "On acquisition: remove all Spritz cards from your deck and add 3 Sweep cards.",
        EffectType = EquipmentEffectType.BroomCloset
    };

    public static Equipment Cocktail => new()
    {
        Id = "",
        Name = "Cocktail",
        Description = "On acquisition: remove all Scurry cards from your deck and add 2 random bonus-spoon cards.",
        EffectType = EquipmentEffectType.Cocktail
    };

    public static Equipment Novel => new()
    {
        Id = "",
        Name = "Novel",
        Description = "On acquisition: replace all Recall cards in your deck with doubly-upgraded Sarcastic Recalls.",
        EffectType = EquipmentEffectType.Novel
    };

    // --- Stage 5 Prerequisite Chains (M47) ---

    public static Equipment Tea => new()
    {
        Id = "",
        Name = "Tea",
        Description = "Removes the cap on Frilly Dress: unlimited turn-1 neutral reveals don't end your turn.",
        EffectType = EquipmentEffectType.Tea,
        Prereqs = [EquipmentEffectType.FrillyDress]
    };

    public static Equipment Mascara => new()
    {
        Id = "",
        Name = "Mascara",
        Description = "Adds 2 more rival distractions at turn start (stacks with Eyeshadow).",
        EffectType = EquipmentEffectType.Mascara,
        Prereqs = [EquipmentEffectType.Eyeshadow]
    };

    public static Equipment Pockets => new()
    {
        Id = "",
        Name = "Pockets",
        Description = "Draws +3 cards on turn 1 (upgrade of Handbag's +2).",
        EffectType = EquipmentEffectType.Pockets,
        Prereqs = [EquipmentEffectType.Handbag]
    };

    public static Equipment MatedPair => new()
    {
        Id = "",
        Name = "Mated Pair",
        Description = "Reveal 2 random player tiles at floor start (upgrade of Dust Bunny's 1).",
        EffectType = EquipmentEffectType.MatedPair,
        Prereqs = [EquipmentEffectType.DustBunny]
    };

    public static Equipment BabyBunny => new()
    {
        Id = "",
        Name = "Baby Bunny",
        Description = "Reveal 3 random player tiles at floor start (upgrade of Mated Pair's 2).",
        EffectType = EquipmentEffectType.BabyBunny,
        Prereqs = [EquipmentEffectType.MatedPair]
    };

    public static Equipment TripleBroom => new()
    {
        Id = "",
        Name = "Triple Broom",
        Description = "When you reveal a tile, Brush 3 random adjacent (upgrade of Double Broom's 2).",
        EffectType = EquipmentEffectType.TripleBroom,
        Prereqs = [EquipmentEffectType.DoubleBroom]
    };

    public static Equipment QuadrupleBroom => new()
    {
        Id = "",
        Name = "Quadruple Broom",
        Description = "When you reveal a tile, Brush 4 random adjacent (upgrade of Triple Broom's 3).",
        EffectType = EquipmentEffectType.QuadrupleBroom,
        Prereqs = [EquipmentEffectType.TripleBroom]
    };

    public static Equipment DiyGel => new()
    {
        Id = "",
        Name = "DIY Gel",
        Description = "All future cards added to your deck are automatically enhanced.",
        EffectType = EquipmentEffectType.DiyGel,
        Prereqs = [EquipmentEffectType.Progesterone]
    };

    public static Equipment Geode => new()
    {
        Id = "",
        Name = "Geode",
        Description = "Playing Tingle draws a card.",
        EffectType = EquipmentEffectType.Geode,
        Prereqs = [EquipmentEffectType.CrystalBall]
    };

    public static Equipment DiscoBall => new()
    {
        Id = "",
        Name = "Disco Ball",
        Description = "On acquisition: adds 2 doubly-upgraded Tingles to your deck.",
        EffectType = EquipmentEffectType.DiscoBall,
        Prereqs = [EquipmentEffectType.Geode]
    };

    public static Equipment Fanfic => new()
    {
        Id = "",
        Name = "Fanfic",
        Description = "Playing Sarcastic Recall draws a card and costs 1 copper.",
        EffectType = EquipmentEffectType.Fanfic,
        Prereqs = [EquipmentEffectType.Novel]
    };

    public static Equipment Favor => new()
    {
        Id = "",
        Name = "Favor",
        Description = "Win the floor when only 1 player tile remains unrevealed.",
        EffectType = EquipmentEffectType.Favor,
        Prereqs = [EquipmentEffectType.Tea, EquipmentEffectType.Cocktail]
    };

    /// <summary>
    /// Returns all equipment templates available as offerings.
    /// </summary>
    public static List<Equipment> CreateOfferingPool()
    {
        return
        [
            Coffee, FrillyDress, DustBunny, Handbag, Eyeshadow, Glasses,
            Bleach, Estrogen, Progesterone, CrystalBall, Boots, Tiara,
            Mop, Espresso,
            Hyperfocus, Choker, Mirror, BusyCanary, DoubleBroom,
            BroomCloset, Cocktail, Novel,
            Tea, Mascara, Pockets, MatedPair, BabyBunny,
            TripleBroom, QuadrupleBroom, DiyGel,
            Geode, DiscoBall, Fanfic, Favor
        ];
    }
}
