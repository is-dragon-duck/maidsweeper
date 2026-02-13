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

    // Campaign state
    public IReadOnlyList<Card> PersistentDeck { get; init; } = [];
    public string CurrentLevelId { get; init; } = "";
    public GamePhase GamePhase { get; init; } = GamePhase.Playing;
    public IReadOnlyList<Card>? CardRewardOptions { get; init; }
}
