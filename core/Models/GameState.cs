namespace Maidsweeper.Core.Models;

public record GameState
{
    public required Board Board { get; init; }
    public IReadOnlyList<Card> Hand { get; init; } = [];
    public IReadOnlyList<Card> DrawPile { get; init; } = [];
    public IReadOnlyList<Card> DiscardPile { get; init; } = [];
    public IReadOnlyList<Card> ExhaustPile { get; init; } = [];
    public int Spoons { get; init; }
    public int MaxSpoons { get; init; } = 3;
    public PlayerType CurrentPlayer { get; init; } = PlayerType.Player;
    public GameStatus GameStatus { get; init; } = GameStatus.Playing;
    public int TurnNumber { get; init; } = 1;
    public int Copper { get; init; }
    public int PlayerTilesRevealedCount { get; init; } // Cumulative across floors, copper every 5th

    // Status effects (per-floor)
    public int ComplaintsStacks { get; init; }
    public bool AcceptHelpDiscount { get; init; }
    public int DistractionStacks { get; init; }
    public int ExcusesStacks { get; init; }

    // Food status effects (multi-floor, decrement at floor end)
    public int ReadStacks { get; init; }
    public int HydrateStacks { get; init; }
    public int AdoptStacks { get; init; }

    // Recall tracking (per-floor)
    public bool RecallPlayedThisFloor { get; init; }

    // Rival intent points (per-floor): per-tile weights driving rival reveals
    public IReadOnlyDictionary<Position, int> RivalIntentPoints { get; init; } =
        new Dictionary<Position, int>();

    // Equipment tracking (per-floor)
    public int Turn1NeutralReveals { get; init; }

    // Rival mine protection (per-floor): remaining # of rival-revealed nobles the
    // floor can absorb without completing as a win. Initialized from LevelConfig.RivalMineProtection.
    public int RivalMineProtectionCount { get; init; }

    // Active Taunt effects (per-floor): each one ends the rival's turn early when
    // enough of its tagged positions have been revealed by the rival.
    public IReadOnlyList<TauntEffect> ActiveTaunts { get; init; } = [];

    // Shop tracking
    public int ShopVisitCount { get; init; }
    public int VisitingBunnyPendingReveals { get; init; }

    // Campaign state
    public IReadOnlyList<Card> PersistentDeck { get; init; } = [];
    public IReadOnlyList<Equipment> Equipment { get; init; } = [];
    public string CurrentLevelId { get; init; } = "";
    public GamePhase GamePhase { get; init; } = GamePhase.Playing;
    public IReadOnlyList<Card>? CardRewardOptions { get; init; }
    public IReadOnlyList<UpgradeOption>? UpgradeOptions { get; init; }
    public IReadOnlyList<Equipment>? EquipmentOptions { get; init; }
    public IReadOnlyList<ShopSlot>? ShopSlots { get; init; }
}

/// <summary>
/// An upgrade option offered between floors.
/// Enhance and BonusSpoon have a pre-selected TargetCard.
/// RemoveCard requires the player to choose which card to remove.
/// </summary>
public record UpgradeOption
{
    public required UpgradeType Type { get; init; }
    public Card? TargetCard { get; init; }
}
