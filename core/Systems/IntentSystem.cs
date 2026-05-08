namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

/// <summary>
/// Per-tile intent points that drive rival reveals. Mirrors the alpha's
/// `clueSystem.generateRivalIntentPoints` and `decayRivalIntentPoints`.
///
/// Generation (each player turn): pick 2 random rivals + 6 random others,
/// stable-sort by safety (Rival > Neutral > Player > Noble), assign points
/// [5, 3, 3, 3, 3, 1, 1, 1], then add 4 baseline distractions.
///
/// Decay (after rival reveals): remove revealed positions; for any rival
/// reveal with adjacencyCount=0, also remove its neighbors; decrement all
/// remaining points by 1; drop zeros.
/// </summary>
public static class IntentSystem
{
    private static readonly int[] BasePoints = { 5, 3, 3, 3, 3, 1, 1, 1 };
    private const int BaselineDistractions = 4;

    /// <summary>
    /// Returns positions that should be excluded from intent generation because
    /// they have already been deduced via revealed adjacency information.
    /// (Mirrors the alpha's `getExcludedPositionsByAdjacency`.)
    ///
    /// Implementation note: in this initial port we treat *no* positions as excluded.
    /// The exclusion set will grow as we wire deduction-aware AI in M36/M37/M42.
    /// </summary>
    public static HashSet<Position> GetExcludedPositions(Board board) => new();

    /// <summary>
    /// Generate one turn's worth of intent points from scratch (does NOT carry over).
    /// Caller is responsible for combining this with previous points in StartPlayerTurn.
    /// </summary>
    public static Dictionary<Position, int> GenerateTurnIntent(GameState state, Random rng)
    {
        var board = state.Board;
        var excluded = GetExcludedPositions(board);

        var unrevealed = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position)
                        && !t.IsRevealed
                        && !t.IsDestroyed
                        && !excluded.Contains(t.Position))
            .ToList();

        var rivalTiles = unrevealed.Where(t => t.Owner == TileOwner.Rival).ToList();

        var chosenRivals = SelectRandom(rivalTiles, 2, rng);

        var remaining = unrevealed
            .Where(t => !chosenRivals.Any(r => r.Position == t.Position))
            .ToList();
        var chosenOthers = SelectRandom(remaining, 6, rng);

        // Combine and stable-sort by safety (Rival > Neutral > Player > Noble).
        // C# OrderBy is stable, so ties preserve insertion order.
        var combined = chosenRivals.Concat(chosenOthers)
            .OrderBy(t => SafetyOrder(t.Owner))
            .ToList();

        var points = new Dictionary<Position, int>();
        for (var i = 0; i < combined.Count && i < BasePoints.Length; i++)
        {
            points[combined[i].Position] = BasePoints[i];
        }

        for (var i = 0; i < BaselineDistractions; i++)
        {
            AddDistractionPoint(points, excluded, rng);
        }

        return points;
    }

    /// <summary>
    /// Adds +1 to a random tile that already has nonzero points (and isn't excluded).
    /// No-op if no eligible tiles exist.
    /// </summary>
    public static void AddDistractionPoint(
        Dictionary<Position, int> points,
        HashSet<Position> excluded,
        Random rng)
    {
        var candidates = points.Keys.Where(p => !excluded.Contains(p) && points[p] > 0).ToList();
        if (candidates.Count == 0) return;

        var target = candidates[rng.Next(candidates.Count)];
        points[target] = points[target] + 1;
    }

    /// <summary>
    /// Combines existing carry-over points with newly-generated ones (sums).
    /// </summary>
    public static Dictionary<Position, int> Combine(
        IReadOnlyDictionary<Position, int> existing,
        IReadOnlyDictionary<Position, int> incoming)
    {
        var combined = new Dictionary<Position, int>(existing);
        foreach (var (pos, pts) in incoming)
        {
            combined[pos] = combined.TryGetValue(pos, out var prev) ? prev + pts : pts;
        }
        return combined;
    }

    /// <summary>
    /// Decays intent points after rival reveals.
    /// 1. Remove points for revealed positions.
    /// 2. For any rival-revealed tile with adjacencyCount=0, remove points from its neighbors.
    /// 3. Decrement all remaining points by 1, drop zeros.
    /// </summary>
    public static Dictionary<Position, int> DecayIntent(
        GameState state,
        IReadOnlyList<Position> revealedPositions)
    {
        var board = state.Board;
        var points = new Dictionary<Position, int>(state.RivalIntentPoints);

        // Step 1: remove revealed
        foreach (var pos in revealedPositions)
        {
            points.Remove(pos);
        }

        // Step 2: for rival-revealed 0-adjacency tiles, also drop neighbors
        foreach (var pos in revealedPositions)
        {
            if (!board.IsValidPosition(pos)) continue;
            var tile = board.GetTile(pos);
            if (!tile.IsRevealed) continue;
            if (tile.RevealedBy != PlayerType.Rival) continue;
            if (tile.AdjacencyCount != 0) continue;

            foreach (var neighbor in BoardSystem.GetNeighbors(board, pos))
            {
                points.Remove(neighbor);
            }
        }

        // Step 3: decrement, drop zeros
        var keys = points.Keys.ToList();
        foreach (var key in keys)
        {
            var newVal = points[key] - 1;
            if (newVal <= 0) points.Remove(key);
            else points[key] = newVal;
        }

        return points;
    }

    /// <summary>
    /// Picks the highest-points tile (random among ties) — used by the temporary
    /// pre-AI-framework rival turn implementation in M35. Real AI types in M36+
    /// use the points dictionary as a weight for their own selection logic.
    /// </summary>
    public static Position? PickHighestPoints(
        IReadOnlyDictionary<Position, int> points,
        Random rng)
    {
        if (points.Count == 0) return null;

        var max = points.Values.Max();
        var top = points.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
        return top[rng.Next(top.Count)];
    }

    // ---------- helpers ----------

    private static int SafetyOrder(TileOwner owner) => owner switch
    {
        TileOwner.Rival => 0,
        TileOwner.Neutral => 1,
        TileOwner.Player => 2,
        TileOwner.Noble => 3,
        _ => 4
    };

    private static List<Tile> SelectRandom(IReadOnlyList<Tile> source, int count, Random rng)
    {
        if (count <= 0 || source.Count == 0) return new List<Tile>();
        var pool = source.ToList();
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }
}
