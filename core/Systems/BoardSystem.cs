namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class BoardSystem
{
    /// <summary>
    /// Creates a board by shuffling tile owners and placing them on the grid.
    /// No unused locations or special tiles for Stage 1.
    /// </summary>
    public static Board CreateBoard(LevelConfig config, Random rng)
    {
        var totalTiles = config.Width * config.Height;
        var expectedTiles = config.PlayerCount + config.RivalCount + config.NeutralCount + config.NobleCount;

        if (expectedTiles != totalTiles)
        {
            throw new ArgumentException(
                $"Tile counts ({expectedTiles}) don't match grid size ({config.Width}x{config.Height} = {totalTiles})");
        }

        // Build flat list of owners, then shuffle
        var owners = new List<TileOwner>(totalTiles);
        owners.AddRange(Enumerable.Repeat(TileOwner.Player, config.PlayerCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Rival, config.RivalCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Neutral, config.NeutralCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Noble, config.NobleCount));

        Shuffle(owners, rng);

        // Assign to grid positions (row-major order)
        var tiles = new List<Tile>(totalTiles);
        var index = 0;
        for (var row = 0; row < config.Height; row++)
        {
            for (var col = 0; col < config.Width; col++)
            {
                tiles.Add(new Tile
                {
                    Position = new Position(row, col),
                    Owner = owners[index++]
                });
            }
        }

        return new Board
        {
            Width = config.Width,
            Height = config.Height,
            Tiles = tiles
        };
    }

    /// <summary>
    /// Returns valid neighbor positions using king adjacency (8-directional).
    /// Filters to positions within board bounds.
    /// </summary>
    public static List<Position> GetNeighbors(Board board, Position pos)
    {
        var neighbors = new List<Position>(8);

        foreach (var (dRow, dCol) in Position.KingOffsets)
        {
            var neighbor = new Position(pos.Row + dRow, pos.Col + dCol);
            if (board.IsValidPosition(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Calculates adjacency count: number of neighbors matching the revealer's owner type.
    /// When the player reveals a tile, count = number of neighboring player tiles.
    /// When the rival reveals a tile, count = number of neighboring rival tiles.
    /// </summary>
    public static int CalculateAdjacency(Board board, Position pos, PlayerType revealedBy)
    {
        var targetOwner = revealedBy == PlayerType.Player ? TileOwner.Player : TileOwner.Rival;
        var neighbors = GetNeighbors(board, pos);

        return neighbors.Count(n => board.GetTile(n).Owner == targetOwner);
    }

    /// <summary>
    /// Reveals a tile, setting its revealedBy and calculating adjacency.
    /// Returns a new board with the updated tile (immutable pattern).
    /// </summary>
    public static Board RevealTile(Board board, Position pos, PlayerType revealedBy)
    {
        var tile = board.GetTile(pos);
        if (tile.IsRevealed)
            return board;

        var adjacency = CalculateAdjacency(board, pos, revealedBy);
        var revealedTile = tile with
        {
            IsRevealed = true,
            RevealedBy = revealedBy,
            AdjacencyCount = adjacency
        };

        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = revealedTile;

        return board with { Tiles = newTiles };
    }

    /// <summary>Fisher-Yates shuffle.</summary>
    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
