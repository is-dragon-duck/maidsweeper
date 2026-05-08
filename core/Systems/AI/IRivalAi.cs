namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// Per-rival-turn context: the level config (so the AI can read special behaviors)
/// and any other turn-time state the AI needs.
/// </summary>
public record AiContext
{
    public LevelConfig? LevelConfig { get; init; }
}

/// <summary>
/// Pluggable rival AI. Returns an ordered list of positions to reveal this turn.
/// The caller (TurnSystem.ExecuteRivalTurn) reveals them in order, stopping after
/// the first non-rival reveal (which ends the rival's turn).
/// </summary>
public interface IRivalAi
{
    AiType Type { get; }

    IReadOnlyList<Position> SelectTilesToReveal(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        AiContext context,
        Random rng);
}
