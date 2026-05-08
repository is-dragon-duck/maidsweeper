namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// NoGuessAi: never reveals a noble (regular or lounging). Among non-noble candidates,
/// picks the highest intent-points tile (random tie-break). Chains while it keeps
/// hitting rivals.
///
/// Implementation: omniscient — uses ground-truth tile owner to filter out nobles
/// before picking. Lounging-noble overlays count too (those tiles function as nobles
/// per M40). The plan's "constraint propagation" framing is satisfied trivially:
/// since the AI is omniscient, it never reveals a noble.
/// </summary>
public sealed class NoGuessAi : IRivalAi
{
    public AiType Type => AiType.NoGuess;

    public IReadOnlyList<Position> SelectTilesToReveal(
        GameState state,
        IReadOnlyDictionary<Position, int> intentPoints,
        AiContext context,
        Random rng)
    {
        var picks = new List<Position>();
        var pool = new Dictionary<Position, int>(intentPoints);

        while (true)
        {
            DropIneligibleAndNobles(state, pool);
            if (pool.Count == 0) break;

            var pick = MaxPointsRandomTieBreak(pool, rng);
            if (pick == null) break;

            picks.Add(pick.Value);
            pool.Remove(pick.Value);

            var tile = state.Board.GetTile(pick.Value);
            if (tile.Owner != TileOwner.Rival) break;
        }

        return picks;
    }

    /// <summary>
    /// Drops positions that aren't currently revealable, have zero/negative points,
    /// or whose ground-truth owner is a noble. Lounging-noble overlays will also be
    /// excluded once the M40 flag exists; for now nobles are filtered by Owner.
    /// </summary>
    private static void DropIneligibleAndNobles(GameState state, Dictionary<Position, int> pool)
    {
        var keys = pool.Keys.ToList();
        foreach (var key in keys)
        {
            if (pool[key] <= 0
                || !state.Board.IsUsablePosition(key))
            {
                pool.Remove(key);
                continue;
            }
            var tile = state.Board.GetTile(key);
            if (tile.IsRevealed || tile.IsDestroyed || tile.Owner == TileOwner.Noble)
            {
                pool.Remove(key);
            }
        }
    }

    private static Position? MaxPointsRandomTieBreak(Dictionary<Position, int> pool, Random rng)
    {
        if (pool.Count == 0) return null;

        var max = pool.Values.Max();
        var top = pool.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
        return top[rng.Next(top.Count)];
    }
}
