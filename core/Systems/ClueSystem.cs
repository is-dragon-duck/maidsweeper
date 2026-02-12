namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class ClueSystem
{
    /// <summary>
    /// Generates clue pip results for Imperious Instructions.
    ///
    /// Algorithm (matching alpha):
    /// 1. Pick 2 random unrevealed player tiles ("targets")
    /// 2. Pick 6 random other unrevealed tiles ("spoilers")
    /// 3. Build weighted bag: targets get 12 copies, spoilers get 4 (mines get 3, player-spoilers get 3)
    /// 4. Draw 10: first 2 guaranteed from targets, rest random without replacement
    /// 5. Validate: at least one target has max pip count (retry up to 10x)
    /// 6. Return pip counts per affected tile
    /// </summary>
    public static List<ClueResult> GenerateImperiousClue(GameState state, Random rng, bool enhanced = false)
    {
        var board = state.Board;

        // Get eligible unrevealed tiles (exclude tiles adjacent to 0-adjacency revealed player tiles)
        var excludedPositions = GetExcludedPositionsByAdjacency(board, TileOwner.Player);
        var unrevealed = board.Tiles
            .Where(t => !t.IsRevealed && !excludedPositions.Contains(t.Position))
            .ToList();
        var playerTiles = unrevealed.Where(t => t.Owner == TileOwner.Player).ToList();

        if (playerTiles.Count < 2)
        {
            // Not enough player tiles for a meaningful clue
            return [];
        }

        var clueId = Guid.NewGuid().ToString();

        // Try up to 10 times to generate a valid clue
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var result = TryGenerateClue(unrevealed, playerTiles, rng, enhanced, clueId);
            if (result != null)
                return result;
        }

        // Fallback: return whatever we get
        return TryGenerateClue(unrevealed, playerTiles, rng, enhanced, clueId, validate: false)!;
    }

    private static List<ClueResult>? TryGenerateClue(
        List<Tile> unrevealed, List<Tile> playerTiles, Random rng,
        bool enhanced, string clueId, bool validate = true)
    {
        // 1. Pick 2 random player tiles as targets
        var chosenTargets = SelectRandom(playerTiles, 2, rng);
        var targetPositions = chosenTargets.Select(t => t.Position).ToHashSet();

        // 2. Pick 6 random other tiles as spoilers
        var remaining = unrevealed
            .Where(t => !targetPositions.Contains(t.Position))
            .Where(t => !enhanced || t.Owner != TileOwner.Noble) // Enhanced: exclude mines
            .ToList();
        var spoilers = SelectRandom(remaining, 6, rng);

        // 3. Build weighted bag
        var bag = new List<Position>();

        // Target tiles: 12 copies each
        foreach (var target in chosenTargets)
        {
            for (var i = 0; i < 12; i++)
                bag.Add(target.Position);
        }

        // Spoiler tiles: 4 copies with adjustments
        foreach (var spoiler in spoilers)
        {
            var copies = 4;
            if (spoiler.Owner == TileOwner.Noble) copies--; // Mines: -1
            if (spoiler.Owner == TileOwner.Player) copies--; // Player spoilers: -1
            copies = Math.Max(0, copies);

            for (var i = 0; i < copies; i++)
                bag.Add(spoiler.Position);
        }

        if (bag.Count == 0)
            return validate ? null : [];

        // 4. Draw 10: first 2 guaranteed from targets, rest random without replacement
        var drawn = new List<Position>();

        // Guaranteed draws
        foreach (var target in chosenTargets)
            drawn.Add(target.Position);

        // Remove one instance of each guaranteed from bag
        var bagCopy = new List<Position>(bag);
        foreach (var target in chosenTargets)
        {
            var idx = bagCopy.IndexOf(target.Position);
            if (idx >= 0) bagCopy.RemoveAt(idx);
        }

        // Random draws for remaining
        var remainingDraws = 10 - drawn.Count;
        for (var i = 0; i < remainingDraws && bagCopy.Count > 0; i++)
        {
            var idx = rng.Next(bagCopy.Count);
            drawn.Add(bagCopy[idx]);
            bagCopy.RemoveAt(idx);
        }

        // 5. Count pips per position
        var pipCounts = new Dictionary<Position, int>();
        foreach (var pos in drawn)
        {
            pipCounts.TryGetValue(pos, out var current);
            pipCounts[pos] = current + 1;
        }

        // 6. Validate: at least one target has max pip count
        if (validate)
        {
            var maxPips = pipCounts.Values.Max();
            var targetHasMax = chosenTargets.Any(t =>
                pipCounts.TryGetValue(t.Position, out var pips) && pips == maxPips);

            if (!targetHasMax)
                return null; // Retry
        }

        // Build results
        var allAffected = pipCounts.Keys.ToList();
        var clueOrder = 0;
        return pipCounts
            .Select(kvp => new ClueResult
            {
                TilePosition = kvp.Key,
                PipStrength = kvp.Value,
                AllAffectedTiles = allAffected,
                ClueId = clueId,
                ClueRowPosition = clueOrder++
            })
            .ToList();
    }

    /// <summary>
    /// Finds positions that should be excluded from clue generation.
    /// Tiles adjacent to a revealed tile with adjacency count 0 (revealed by the target type)
    /// cannot be that type, so they're excluded from the pool.
    /// </summary>
    private static HashSet<Position> GetExcludedPositionsByAdjacency(Board board, TileOwner targetOwner)
    {
        var excluded = new HashSet<Position>();
        var revealedByType = targetOwner == TileOwner.Player ? PlayerType.Player : PlayerType.Rival;

        foreach (var tile in board.Tiles)
        {
            if (!tile.IsRevealed || tile.AdjacencyCount != 0 || tile.RevealedBy != revealedByType)
                continue;

            // All neighbors of a 0-adjacency tile cannot be the target owner type
            foreach (var neighbor in BoardSystem.GetNeighbors(board, tile.Position))
            {
                excluded.Add(neighbor);
            }
        }

        return excluded;
    }

    /// <summary>
    /// Selects up to N random tiles from a list without replacement.
    /// </summary>
    private static List<Tile> SelectRandom(List<Tile> tiles, int count, Random rng)
    {
        if (tiles.Count <= count)
            return tiles.ToList();

        var result = new List<Tile>(count);
        var indices = new HashSet<int>();

        while (result.Count < count)
        {
            var idx = rng.Next(tiles.Count);
            if (indices.Add(idx))
                result.Add(tiles[idx]);
        }

        return result;
    }
}
