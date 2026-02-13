namespace Maidsweeper.Core.Models;

/// <summary>
/// Static card templates. Cards in the game are copies of these with unique IDs.
/// </summary>
public static class CardDefinitions
{
    public static Card RecallImperious => new()
    {
        Id = "",
        Name = "Recall - Imperious",
        Description = "Distribute clue pips across tiles via bag draw.",
        Cost = 2,
        EffectType = CardEffectType.Recall
    };

    public static Card Spritz => new()
    {
        Id = "",
        Name = "Spritz",
        Description = "Target a tile. Learn if it's safe or dangerous.",
        Cost = 1,
        EffectType = CardEffectType.Spritz
    };

    public static Card Tingle => new()
    {
        Id = "",
        Name = "Tingle",
        Description = "Sense a random rival or noble tile.",
        Cost = 1,
        EffectType = CardEffectType.Tingle
    };

    public static Card Scurry => new()
    {
        Id = "",
        Name = "Scurry",
        Description = "Select 2 tiles. The safer one is revealed.",
        Cost = 1,
        EffectType = CardEffectType.Scurry
    };

    public static Card Twirl => new()
    {
        Id = "",
        Name = "Twirl",
        Description = "Gain 3 copper.",
        Cost = 3,
        Exhaust = true,
        EffectType = CardEffectType.Twirl
    };

    // --- Reward Cards ---

    public static Card Brush => new()
    {
        Id = "",
        Name = "Brush",
        Description = "Target a 3x3 area. For each tile, exclude a random non-owner.",
        Cost = 1,
        EffectType = CardEffectType.Brush
    };

    public static Card Sweep => new()
    {
        Id = "",
        Name = "Sweep",
        Description = "Target a 5x5 area. Remove dirt from all tiles.",
        Cost = 1,
        EffectType = CardEffectType.Sweep
    };

    public static Card Caffeinate => new()
    {
        Id = "",
        Name = "Caffeinate",
        Description = "Gain 2 spoons.",
        Cost = 1,
        Exhaust = true,
        EffectType = CardEffectType.Caffeinate
    };

    public static Card Breathe => new()
    {
        Id = "",
        Name = "Breathe",
        Description = "Draw 3 cards.",
        Cost = 1,
        EffectType = CardEffectType.Breathe
    };

    public static Card LockIn => new()
    {
        Id = "",
        Name = "Lock In",
        Description = "Draw 2 cards.",
        Cost = 0,
        Exhaust = true,
        EffectType = CardEffectType.LockIn
    };

    public static Card Rendezvous => new()
    {
        Id = "",
        Name = "Rendezvous",
        Description = "Reveal a random player tile with rival adjacency and a random rival tile with player adjacency.",
        Cost = 1,
        EffectType = CardEffectType.Rendezvous
    };

    /// <summary>
    /// Creates the 10-card starter deck with unique IDs.
    /// 1x Recall - Imperious, 3x Spritz, 3x Tingle, 2x Scurry, 1x Twirl.
    /// </summary>
    public static List<Card> CreateStarterDeck()
    {
        var id = 0;
        string nextId() => $"starter_{id++}";

        return
        [
            RecallImperious with { Id = nextId() },
            Spritz with { Id = nextId() },
            Spritz with { Id = nextId() },
            Spritz with { Id = nextId() },
            Tingle with { Id = nextId() },
            Tingle with { Id = nextId() },
            Tingle with { Id = nextId() },
            Scurry with { Id = nextId() },
            Scurry with { Id = nextId() },
            Twirl with { Id = nextId() },
        ];
    }

    /// <summary>
    /// Returns all reward pool card templates (for card selection between floors).
    /// </summary>
    public static List<Card> CreateRewardPool()
    {
        return [Brush, Sweep, Caffeinate, Breathe, LockIn, Rendezvous];
    }
}
