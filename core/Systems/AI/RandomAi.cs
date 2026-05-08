namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// RandomAi: weighted random selection by intent points. A tile with 5 points is
/// 5× more likely to be picked than a tile with 1 point. Zero-point tiles are
/// never picked. Continues revealing while it keeps hitting rivals (chains until
/// it reveals a non-rival, which ends the rival's turn).
/// </summary>
public sealed class RandomAi : IRivalAi
{
    public AiType Type => AiType.Random;

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
            // Filter to currently-revealable, positive-points tiles
            DropIneligible(state, pool);
            if (pool.Count == 0) break;

            var pick = WeightedRandom(pool, rng);
            if (pick == null) break;

            picks.Add(pick.Value);
            pool.Remove(pick.Value);

            // Chain only if the revealed tile is rival-owned; otherwise stop here
            // (caller still reveals this last tile but that reveal ends the rival's turn)
            var tile = state.Board.GetTile(pick.Value);
            if (tile.Owner != TileOwner.Rival) break;
        }

        return picks;
    }

    /// <summary>
    /// Drops positions from the pool that are no longer eligible (revealed,
    /// destroyed, unused, or zero-points).
    /// </summary>
    private static void DropIneligible(GameState state, Dictionary<Position, int> pool)
    {
        var keys = pool.Keys.ToList();
        foreach (var key in keys)
        {
            if (pool[key] <= 0
                || !state.Board.IsUsablePosition(key)
                || state.Board.GetTile(key).IsRevealed
                || state.Board.GetTile(key).IsDestroyed)
            {
                pool.Remove(key);
            }
        }
    }

    /// <summary>
    /// Picks a position weighted by its point count.
    /// </summary>
    private static Position? WeightedRandom(Dictionary<Position, int> pool, Random rng)
    {
        var total = pool.Values.Sum();
        if (total <= 0) return null;

        var roll = rng.Next(total);
        var cumulative = 0;
        foreach (var (pos, weight) in pool)
        {
            cumulative += weight;
            if (roll < cumulative) return pos;
        }
        return pool.Keys.Last(); // shouldn't reach (numerical guard)
    }
}
