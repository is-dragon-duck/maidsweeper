namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// ReasoningAi: constraint propagation + Monte Carlo sampling + intent-weighted priority.
///
/// Per turn-pick:
///   1. Run ExclusionLogic to find guaranteed rivals + ruled-out tiles.
///   2. If a guaranteed rival exists (and isn't a forbidden noble / unreachable), reveal it.
///   3. Otherwise run MC sampling, then score each candidate via:
///        priority = intentPoints
///                 + log2((mc.Rival + bias) / (N + denomBias))
///                 - (1/3) * log2((mc.Noble + bias) / (N + denomBias))
///        (with a small extra penalty when the tile is a noble that has no intent points at all)
///   4. Pick the highest-priority eligible tile.
///   5. If the picked tile is a rival, simulate the reveal and loop; otherwise stop.
///
/// Hill-climbing of the MC samples (alpha's tension reduction) is deferred — the
/// constraint-propagation + raw MC frequencies still produce useful priorities.
/// </summary>
public sealed class ReasoningAi : IRivalAi
{
    public AiType Type => AiType.Reasoning;

    private const int MonteCarloIterations = 20;
    private const int MaxRevealsPerTurn = 50;

    public IReadOnlyList<Position> SelectTilesToReveal(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        AiContext context,
        Random rng)
    {
        var picks = new List<Position>();
        var simulated = state;
        var rivalNeverNobles = context.LevelConfig?.RivalNeverNobles ?? false;

        for (var iter = 0; iter < MaxRevealsPerTurn; iter++)
        {
            var analysis = ExclusionLogic.Analyze(simulated);

            // Phase 1: prefer a guaranteed rival
            var guaranteed = analysis.GuaranteedRivals
                .Where(p => IsRevealable(simulated, p))
                .Where(p => !IsForbiddenNoble(simulated, p, rivalNeverNobles))
                .ToList();

            Position? next;
            if (guaranteed.Count > 0)
            {
                next = guaranteed[0];
            }
            else
            {
                // Phase 2: Monte Carlo + priority
                var mc = MonteCarloSampler.Run(simulated, analysis, rng, MonteCarloIterations);
                next = SelectByPriority(simulated, intentPoints, mc, analysis, rivalNeverNobles, rng);
            }

            if (next == null) break;
            picks.Add(next.Value);

            var tile = simulated.Board.GetTile(next.Value);
            if (tile.Owner != TileOwner.Rival) break;

            simulated = simulated with
            {
                Board = BoardSystem.RevealTile(simulated.Board, next.Value, PlayerType.Rival)
            };
        }

        return picks;
    }

    /// <summary>
    /// Computes a priority for each eligible tile and returns the highest (random tie-break).
    /// </summary>
    private static Position? SelectByPriority(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        MonteCarloResults mc,
        ExclusionAnalysis analysis,
        bool rivalNeverNobles,
        Random rng)
    {
        var iterations = mc.Iterations > 0 ? mc.Iterations : 1;
        var rivalBias = 0.001;
        var nobleBias = 0.001;
        var denomBias = 0.001;
        var guaranteedSet = new HashSet<Position>(analysis.GuaranteedRivals);

        var best = new List<Position>();
        var bestScore = double.NegativeInfinity;

        foreach (var (pos, counts) in mc.OwnerCounts)
        {
            // Skip ineligible
            if (guaranteedSet.Contains(pos)) continue;
            if (analysis.RuledOutRivals.Contains(pos)) continue;
            if (!IsRevealable(state, pos)) continue;
            if (IsForbiddenNoble(state, pos, rivalNeverNobles)) continue;
            // Rival AIs may "cheat" by revealing inner tiles even when no sanctum is open;
            // reachability gating only applies to the player.

            var basePriority = intentPoints.TryGetValue(pos, out var pts) ? pts : 0;

            var rivalBonus = Math.Log2(
                (counts.Rival + rivalBias) / (iterations + denomBias));
            var noblePenalty = (1.0 / 3.0) * Math.Log2(
                (counts.Noble + nobleBias) / (iterations + denomBias));

            var noPointsNoblePenalty = 0.0;
            if (state.Board.GetTile(pos).Owner == TileOwner.Noble && basePriority == 0)
                noPointsNoblePenalty = -0.3;

            var priority = basePriority + rivalBonus - noblePenalty + noPointsNoblePenalty;

            if (priority > bestScore)
            {
                bestScore = priority;
                best = new List<Position> { pos };
            }
            else if (priority == bestScore)
            {
                best.Add(pos);
            }
        }

        if (best.Count == 0) return null;
        return best[rng.Next(best.Count)];
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
        var tile = state.Board.GetTile(pos);
        return tile.Owner == TileOwner.Noble || tile.IsLoungingNoble;
    }
}
