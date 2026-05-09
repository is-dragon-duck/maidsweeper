namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// Per-tile owner-frequency tally produced by Monte Carlo sampling.
/// </summary>
public sealed record MonteCarloResults
{
    public IReadOnlyDictionary<Position, OwnerCounts> OwnerCounts { get; init; }
        = new Dictionary<Position, OwnerCounts>();
    public int Iterations { get; init; }
}

public struct OwnerCounts
{
    public int Player;
    public int Rival;
    public int Neutral;
    public int Noble;
}

/// <summary>
/// Generates N random valid ownership assignments respecting per-tile possibilities and
/// the level's total tile counts, and tallies per-tile owner frequencies. The simpler
/// "no-hill-climbing" MVP for the Reasoning AI: it gives useful per-tile probability
/// estimates whenever ExclusionLogic has narrowed down possibilities.
/// </summary>
public static class MonteCarloSampler
{
    public const int DefaultIterations = 20;

    public static MonteCarloResults Run(
        GameState state,
        ExclusionAnalysis analysis,
        Random rng,
        int iterations = DefaultIterations)
    {
        var totals = TotalUnrevealedCounts(state);
        var possibilities = analysis.Possibilities;
        var unrevealedPositions = possibilities.Keys.ToList();

        var counts = new Dictionary<Position, OwnerCounts>();
        foreach (var pos in unrevealedPositions)
            counts[pos] = new OwnerCounts();

        for (var i = 0; i < iterations; i++)
        {
            var assignment = TryRandomAssignment(state, analysis, possibilities, totals, rng);
            if (assignment == null) continue;

            foreach (var (pos, owner) in assignment)
            {
                if (!counts.ContainsKey(pos)) continue;
                var c = counts[pos];
                switch (owner)
                {
                    case TileOwner.Player: c.Player++; break;
                    case TileOwner.Rival: c.Rival++; break;
                    case TileOwner.Neutral: c.Neutral++; break;
                    case TileOwner.Noble: c.Noble++; break;
                }
                counts[pos] = c;
            }
        }

        return new MonteCarloResults { OwnerCounts = counts, Iterations = iterations };
    }

    /// <summary>
    /// Counts how many of each owner type are still unassigned (board total minus revealed).
    /// </summary>
    private static OwnerCounts TotalUnrevealedCounts(GameState state)
    {
        var c = new OwnerCounts();
        foreach (var t in state.Board.Tiles)
        {
            if (!state.Board.IsUsablePosition(t.Position)) continue;
            if (t.IsRevealed || t.IsDestroyed) continue;
            switch (t.Owner)
            {
                case TileOwner.Player: c.Player++; break;
                case TileOwner.Rival: c.Rival++; break;
                case TileOwner.Neutral: c.Neutral++; break;
                case TileOwner.Noble: c.Noble++; break;
            }
        }
        return c;
    }

    /// <summary>
    /// Attempts a single random valid assignment of unrevealed tiles. Pre-assigns guaranteed
    /// rivals, then assigns remaining counts in priority order (rival, player, noble, neutral).
    /// Returns null if constraints prove unsatisfiable for this seed.
    /// </summary>
    private static Dictionary<Position, TileOwner>? TryRandomAssignment(
        GameState state,
        ExclusionAnalysis analysis,
        IReadOnlyDictionary<Position, IReadOnlySet<TileOwner>> possibilities,
        OwnerCounts totals,
        Random rng)
    {
        var assignment = new Dictionary<Position, TileOwner>();

        // Step 1: pre-assign guaranteed rivals
        var guaranteedSet = new HashSet<Position>(analysis.GuaranteedRivals);
        foreach (var pos in guaranteedSet)
        {
            assignment[pos] = TileOwner.Rival;
        }

        var remaining = totals;
        remaining.Rival -= guaranteedSet.Count;

        // Step 2: gather unassigned positions in shuffled order
        var unassigned = possibilities.Keys
            .Where(p => !assignment.ContainsKey(p))
            .ToList();
        Shuffle(unassigned, rng);

        // Step 3: assign by owner type in priority order
        foreach (var owner in new[] { TileOwner.Rival, TileOwner.Player, TileOwner.Noble, TileOwner.Neutral })
        {
            var needed = OwnerCount(remaining, owner);
            if (needed <= 0) continue;
            var assignedCount = 0;

            foreach (var pos in unassigned)
            {
                if (assignedCount >= needed) break;
                if (assignment.ContainsKey(pos)) continue;
                if (!possibilities[pos].Contains(owner)) continue;

                assignment[pos] = owner;
                assignedCount++;
            }
            SetOwnerCount(ref remaining, owner, needed - assignedCount);
        }

        // Step 4: backfill any unassigned tiles with their first possible owner
        foreach (var pos in unassigned)
        {
            if (assignment.ContainsKey(pos)) continue;
            var possible = possibilities[pos];
            assignment[pos] = possible.Count > 0 ? possible.First() : TileOwner.Neutral;
        }

        return assignment;
    }

    private static int OwnerCount(OwnerCounts c, TileOwner o) => o switch
    {
        TileOwner.Player => c.Player,
        TileOwner.Rival => c.Rival,
        TileOwner.Neutral => c.Neutral,
        TileOwner.Noble => c.Noble,
        _ => 0
    };

    private static void SetOwnerCount(ref OwnerCounts c, TileOwner o, int v)
    {
        switch (o)
        {
            case TileOwner.Player: c.Player = v; break;
            case TileOwner.Rival: c.Rival = v; break;
            case TileOwner.Neutral: c.Neutral = v; break;
            case TileOwner.Noble: c.Noble = v; break;
        }
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
