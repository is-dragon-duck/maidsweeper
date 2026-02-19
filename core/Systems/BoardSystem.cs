namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class BoardSystem
{
    /// <summary>
    /// Creates a board by shuffling tile owners among usable positions.
    /// Unused positions get inert placeholder tiles. Special tiles are assigned after owner placement.
    /// </summary>
    public static Board CreateBoard(LevelConfig config, Random rng)
    {
        var totalTiles = config.Width * config.Height;
        var unusedSet = new HashSet<Position>(config.UnusedLocations);
        var usableCount = totalTiles - unusedSet.Count;
        var expectedTiles = config.PlayerCount + config.RivalCount + config.NeutralCount + config.NobleCount;

        if (expectedTiles != usableCount)
        {
            throw new ArgumentException(
                $"Tile counts ({expectedTiles}) don't match usable grid size ({usableCount})");
        }

        // Build flat list of owners for usable positions, then shuffle
        var owners = new List<TileOwner>(usableCount);
        owners.AddRange(Enumerable.Repeat(TileOwner.Player, config.PlayerCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Rival, config.RivalCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Neutral, config.NeutralCount));
        owners.AddRange(Enumerable.Repeat(TileOwner.Noble, config.NobleCount));

        Shuffle(owners, rng);

        // Assign to grid positions (row-major order)
        var tiles = new List<Tile>(totalTiles);
        var ownerIndex = 0;
        for (var row = 0; row < config.Height; row++)
        {
            for (var col = 0; col < config.Width; col++)
            {
                var pos = new Position(row, col);
                if (unusedSet.Contains(pos))
                {
                    // Placeholder tile at unused position
                    tiles.Add(new Tile { Position = pos, Owner = TileOwner.Neutral });
                }
                else
                {
                    tiles.Add(new Tile { Position = pos, Owner = owners[ownerIndex++] });
                }
            }
        }

        var board = new Board
        {
            Width = config.Width,
            Height = config.Height,
            Tiles = tiles,
            UnusedPositions = unusedSet,
            AdjacencyRule = config.AdjacencyRule
        };

        // Assign special tiles
        foreach (var specialConfig in config.SpecialTiles)
        {
            board = PlaceSpecialTiles(board, specialConfig, rng);
        }

        return board;
    }

    /// <summary>
    /// Randomly assigns a special tile type to eligible usable tiles.
    /// </summary>
    private static Board PlaceSpecialTiles(Board board, SpecialTileConfig config, Random rng)
    {
        var eligibleOwners = new HashSet<TileOwner>(config.EligibleOwners);
        var candidates = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position)
                        && !t.IsRevealed
                        && t.SpecialTile == null
                        && eligibleOwners.Contains(t.Owner))
            .ToList();

        Shuffle(candidates, rng);

        var count = Math.Min(config.Count, candidates.Count);
        var newTiles = board.Tiles.ToList();

        for (var i = 0; i < count; i++)
        {
            var tile = candidates[i];
            var idx = board.TileIndex(tile.Position);
            newTiles[idx] = tile with { SpecialTile = config.Type };
        }

        return board with { Tiles = newTiles };
    }

    /// <summary>
    /// Returns valid neighbor positions using king adjacency (8-directional).
    /// Filters to usable positions within board bounds (excludes unused positions).
    /// </summary>
    public static List<Position> GetNeighbors(Board board, Position pos)
    {
        var offsets = board.AdjacencyRule == AdjacencyRule.Manhattan2
            ? Position.Manhattan2Offsets
            : Position.KingOffsets;
        var neighbors = new List<Position>(offsets.Length);

        foreach (var (dRow, dCol) in offsets)
        {
            var neighbor = new Position(pos.Row + dRow, pos.Col + dCol);
            if (board.IsUsablePosition(neighbor) && !board.GetTile(neighbor).IsDestroyed)
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
    /// If the tile is ExtraDirty and revealed by the Player, it cleans the dirt instead of revealing.
    /// Rival reveals ignore ExtraDirty (reveal normally).
    /// Returns a new board with the updated tile (immutable pattern).
    /// </summary>
    public static Board RevealTile(Board board, Position pos, PlayerType revealedBy)
    {
        var tile = board.GetTile(pos);
        if (tile.IsRevealed)
            return board;

        // ExtraDirty: player click cleans instead of revealing
        if (tile.IsDirty && revealedBy == PlayerType.Player)
        {
            var cleanedTile = tile with { SpecialTile = null };
            var cleanedTiles = board.Tiles.ToList();
            cleanedTiles[board.TileIndex(pos)] = cleanedTile;
            return board with { Tiles = cleanedTiles };
        }

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

    /// <summary>
    /// Returns all usable tiles within a rectangular area centered on the given position.
    /// Radius 1 = 3x3, radius 2 = 5x5, etc.
    /// </summary>
    public static List<Tile> GetTilesInArea(Board board, Position center, int radius)
    {
        var tiles = new List<Tile>();

        for (var row = center.Row - radius; row <= center.Row + radius; row++)
        {
            for (var col = center.Col - radius; col <= center.Col + radius; col++)
            {
                var pos = new Position(row, col);
                if (board.IsUsablePosition(pos) && !board.GetTile(pos).IsDestroyed)
                {
                    tiles.Add(board.GetTile(pos));
                }
            }
        }

        return tiles;
    }

    /// <summary>
    /// Returns all usable tiles in a burst-1-cross area: center + 4 cardinal neighbors.
    /// </summary>
    public static List<Tile> GetTilesInCross(Board board, Position center)
    {
        var tiles = new List<Tile>();
        var offsets = new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) };

        foreach (var (dRow, dCol) in offsets)
        {
            var pos = new Position(center.Row + dRow, center.Col + dCol);
            if (board.IsUsablePosition(pos) && !board.GetTile(pos).IsDestroyed)
            {
                tiles.Add(board.GetTile(pos));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Calculates full adjacency info (counts of each owner type) for a tile's neighbors.
    /// </summary>
    public static AdjacencyInfo CalculateFullAdjacency(Board board, Position pos)
    {
        var neighbors = GetNeighbors(board, pos);
        return new AdjacencyInfo
        {
            PlayerCount = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Player),
            RivalCount = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Rival),
            NeutralCount = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Neutral),
            NobleCount = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Noble)
        };
    }

    /// <summary>
    /// Calculates only the player adjacency count for a tile's neighbors.
    /// Other owner counts are left as null (unknown).
    /// </summary>
    public static AdjacencyInfo CalculatePlayerAdjacency(Board board, Position pos)
    {
        var neighbors = GetNeighbors(board, pos);
        return new AdjacencyInfo
        {
            PlayerCount = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Player)
        };
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
