namespace Maidsweeper.Core.Models;

/// <summary>
/// Static card templates. Cards in the game are copies of these with unique IDs.
/// </summary>
public static class CardDefinitions
{
    public static Card ImperiousInstructions => new()
    {
        Id = "",
        Name = "Imperious Instructions",
        Description = "Distribute clue pips across tiles via bag draw.",
        Cost = 2,
        EffectType = CardEffectType.Instructions
    };

    public static Card Spritz => new()
    {
        Id = "",
        Name = "Spritz",
        Description = "Target a tile. Learn if it's safe or dangerous.",
        Cost = 1,
        EffectType = CardEffectType.Scout
    };

    public static Card Tingle => new()
    {
        Id = "",
        Name = "Tingle",
        Description = "Sense a random rival or mine tile.",
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

    /// <summary>
    /// Creates the 10-card starter deck with unique IDs.
    /// 1x Imperious Instructions, 3x Spritz, 3x Tingle, 2x Scurry, 1x Twirl.
    /// </summary>
    public static List<Card> CreateStarterDeck()
    {
        var id = 0;
        string nextId() => $"starter_{id++}";

        return
        [
            ImperiousInstructions with { Id = nextId() },
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
}
