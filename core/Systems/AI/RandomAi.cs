namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// RandomAi: weighted random selection by intent points. A tile with 5 points is
/// 5× more likely to be picked than a tile with 1 point. Zero-point tiles are
/// never picked. Continues revealing while it keeps hitting rivals (chains until
/// it reveals a non-rival, which ends the rival's turn). When `RivalNeverNobles`
/// is set on the level, nobles (regular or lounging) are filtered out of the pool.
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
        var rivalNeverNobles = context.LevelConfig?.RivalNeverNobles ?? false;

        while (true)
        {
            // Filter to currently-revealable, positive-points tiles
            DropIneligible(state, pool, rivalNeverNobles);
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
    /// destroyed, unused, zero-points, or forbidden nobles when the level sets
    /// RivalNeverNobles).
    /// </summary>
    private static void DropIneligible(GameState state, Dictionary<Position, int> pool, bool rivalNeverNobles)
    {
        var keys = pool.Keys.ToList();
        foreach (var key in keys)
        {
            if (pool[key] <= 0
                || !state.Board.IsUsablePosition(key)
                || state.Board.GetTile(key).IsRevealed
                || state.Board.GetTile(key).IsDestroyed
                || IsForbiddenNoble(state, key, rivalNeverNobles))
            {
                pool.Remove(key);
            }
        }
    }

    private static bool IsForbiddenNoble(GameState state, Position pos, bool rivalNeverNobles)
    {
        if (!rivalNeverNobles) return false;
        var tile = state.Board.GetTile(pos);
        return tile.Owner == TileOwner.Noble || tile.IsLoungingNoble;
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
