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

    // --- Stage 3 Reward Cards ---

    public static Card Argue => new()
    {
        Id = "",
        Name = "Argue",
        Description = "Target a 3x3 area. Annotate neutrals; mark the rest as not neutral.",
        Cost = 1,
        EffectType = CardEffectType.Argue
    };

    public static Card AcceptHelp => new()
    {
        Id = "",
        Name = "Accept Help",
        Description = "Target a cross area. Reveal the safest tiles. Future Accept Helps cost 0 this floor.",
        Cost = 3,
        EffectType = CardEffectType.AcceptHelp
    };

    public static Card Eavesdrop => new()
    {
        Id = "",
        Name = "Eavesdrop",
        Description = "Target a tile. Learn if it's yours without revealing. Get player adjacency info.",
        Cost = 1,
        EffectType = CardEffectType.Eavesdrop
    };

    public static Card Peek => new()
    {
        Id = "",
        Name = "Peek",
        Description = "Target a cross area. Find nobles. Exhaust only if nobles found.",
        Cost = 0,
        EffectType = CardEffectType.Peek
    };

    public static Card Explode => new()
    {
        Id = "",
        Name = "Explode",
        Description = "Destroy a tile. Gain 1 Complaint and a Mollify card.",
        Cost = 1,
        EffectType = CardEffectType.Explode
    };

    public static Card Deliver => new()
    {
        Id = "",
        Name = "Deliver",
        Description = "Target a tile. If noble, convert to neutral and reveal safely. Gain 2 copper.",
        Cost = 1,
        EffectType = CardEffectType.Deliver
    };

    public static Card Brat => new()
    {
        Id = "",
        Name = "Brat",
        Description = "Target a revealed tile. Unreveal it.",
        Cost = 1,
        Exhaust = true,
        EffectType = CardEffectType.Brat
    };

    public static Card Ramble => new()
    {
        Id = "",
        Name = "Ramble",
        Description = "Add 2 Distraction stacks to the rival.",
        Cost = 1,
        EffectType = CardEffectType.Ramble
    };

    public static Card Glaze => new()
    {
        Id = "",
        Name = "Glaze",
        Description = "Gain 1 Excuses stack. Protects against the next noble reveal.",
        Cost = 0,
        Exhaust = true,
        EffectType = CardEffectType.Glaze
    };

    public static Card Mask => new()
    {
        Id = "",
        Name = "Mask",
        Description = "Play another card from your hand for free. Exhaust both.",
        Cost = 0,
        Exhaust = true,
        EffectType = CardEffectType.Mask
    };

    public static Card Nap => new()
    {
        Id = "",
        Name = "Nap",
        Description = "Retrieve a card from your exhaust pile.",
        Cost = 1,
        Exhaust = true,
        EffectType = CardEffectType.Nap
    };

    public static Card Mollify => new()
    {
        Id = "",
        Name = "Mollify",
        Description = "Reduce Complaints by 1.",
        Cost = 1,
        Exhaust = true,
        EffectType = CardEffectType.Mollify
    };

    // --- Stage 4 Food Cards ---

    public static Card Read => new()
    {
        Id = "",
        Name = "Read",
        Description = "+1 card draw per turn for 2 floors.",
        Cost = 2,
        Exhaust = true,
        EffectType = CardEffectType.Read
    };

    public static Card Hydrate => new()
    {
        Id = "",
        Name = "Hydrate",
        Description = "+1 spoon on copper-granting reveals for 2 floors.",
        Cost = 2,
        Exhaust = true,
        EffectType = CardEffectType.Hydrate
    };

    public static Card Adopt => new()
    {
        Id = "",
        Name = "Adopt",
        Description = "Reveal 1 random player tile at floor start for 2 floors.",
        Cost = 2,
        Exhaust = true,
        EffectType = CardEffectType.Adopt
    };

    // --- Stage 4 Recall Variants ---

    public static Card RecallVague => new()
    {
        Id = "",
        Name = "Recall - Vague",
        Description = "Distribute clue pips broadly across 5 target tiles.",
        Cost = 2,
        EffectType = CardEffectType.RecallVague
    };

    public static Card RecallSarcastic => new()
    {
        Id = "",
        Name = "Recall - Sarcastic",
        Description = "Anti-pips show where tiles probably aren't yours.",
        Cost = 2,
        EffectType = CardEffectType.RecallSarcastic
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
        return
        [
            Brush, Sweep, Caffeinate, Breathe, LockIn, Rendezvous,
            Argue, AcceptHelp, Eavesdrop, Peek, Explode, Deliver,
            Brat, Ramble, Glaze, Mask, Nap,
            Read, Hydrate, Adopt,
            RecallVague, RecallSarcastic
        ];
    }
}
