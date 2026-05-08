namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// ConservativeAi: applies constraint propagation (ExclusionLogic) over revealed
/// adjacency counts to identify guaranteed rivals, then prefers them. Falls back
/// to max-points among non-ruled-out tiles. When `RivalNeverNobles` is set on the
/// level, never picks a noble.
///
/// Like NoGuess, it chains revealed rivals — re-running the analysis after each
/// simulated reveal so newly-deducible tiles can be picked next.
/// </summary>
public sealed class ConservativeAi : IRivalAi
{
    public AiType Type => AiType.Conservative;

    public IReadOnlyList<Position> SelectTilesToReveal(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        AiContext context,
        Random rng)
    {
        var picks = new List<Position>();
        var simulated = state;
        var rivalNeverNobles = context.LevelConfig?.RivalNeverNobles ?? false;
        const int maxIterations = 50;

        for (var iter = 0; iter < maxIterations; iter++)
        {
            var analysis = ExclusionLogic.Analyze(simulated);

            // 1) Prefer a guaranteed rival
            var guaranteed = analysis.GuaranteedRivals
                .Where(p => IsRevealable(simulated, p))
                .Where(p => !IsForbiddenNoble(simulated, p, rivalNeverNobles))
                .ToList();

            Position? next = null;
            if (guaranteed.Count > 0)
            {
                next = guaranteed[0];
            }
            else
            {
                // 2) Fall back to max-points among eligible non-ruled-out, non-forbidden tiles
                next = MaxPointsFallback(simulated, intentPoints, analysis.RuledOutRivals,
                    rivalNeverNobles, rng);
            }

            if (next == null) break;

            picks.Add(next.Value);

            // Stop if we revealed a non-rival (this ends the rival's turn)
            var tile = simulated.Board.GetTile(next.Value);
            if (tile.Owner != TileOwner.Rival) break;

            // Simulate the reveal so the next iteration sees fresh adjacency info
            simulated = simulated with
            {
                Board = BoardSystem.RevealTile(simulated.Board, next.Value, PlayerType.Rival)
            };
        }

        return picks;
    }

    private static bool IsRevealable(GameState state, Position pos)
    {
        if (!state.Board.IsUsablePosition(pos)) return false;
        var tile = state.Board.GetTile(pos);
        return !tile.IsRevealed && !tile.IsDestroyed;
    }

    private static bool IsForbiddenNoble(GameState state, Position pos, bool rivalNeverNobles)
    {
        if (!rivalNeverNobles) return false;
        // M40 will extend this to include lounging-noble overlays.
        return state.Board.GetTile(pos).Owner == TileOwner.Noble;
    }

    private static Position? MaxPointsFallback(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        IReadOnlyCollection<Position> ruledOutRivals,
        bool rivalNeverNobles,
        Random rng)
    {
        var candidates = intentPoints
            .Where(kv => kv.Value > 0)
            .Where(kv => IsRevealable(state, kv.Key))
            .Where(kv => !IsForbiddenNoble(state, kv.Key, rivalNeverNobles))
            // Conservative tries to avoid tiles that have been ruled out as rivals,
            // but if ALL remaining candidates are ruled out, allow them as a last resort.
            .ToList();

        if (candidates.Count == 0) return null;

        var preferred = candidates.Where(kv => !ruledOutRivals.Contains(kv.Key)).ToList();
        var pool = preferred.Count > 0 ? preferred : candidates;

        var max = pool.Max(kv => kv.Value);
        var top = pool.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
        return top[rng.Next(top.Count)];
    }
}
