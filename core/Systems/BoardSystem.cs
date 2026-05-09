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

        // Initialize courtier MoveTargets for any courtier flags placed above
        board = InitializeCourtierTargets(board, rng);

        return board;
    }

    /// <summary>
    /// Places special-tile flags onto tiles per the configured PlacementStrategy.
    /// Multiple flags can coexist on a single tile; the strategy filters which positions
    /// are eligible to receive this particular flag.
    /// </summary>
    private static Board PlaceSpecialTiles(Board board, SpecialTileConfig config, Random rng)
    {
        var newTiles = board.Tiles.ToList();
        var picks = SelectPlacementPositions(board, config, rng);

        foreach (var pos in picks)
        {
            var idx = board.TileIndex(pos);
            newTiles[idx] = newTiles[idx].WithSpecial(config.Type);
        }

        return board with { Tiles = newTiles };
    }

    private static List<Position> SelectPlacementPositions(
        Board board,
        SpecialTileConfig config,
        Random rng)
    {
        switch (config.Strategy)
        {
            case PlacementStrategy.Explicit:
                // Use exact positions; no shuffling, no count limit beyond list length.
                return (config.ExplicitPositions ?? Array.Empty<Position>()).ToList();

            case PlacementStrategy.Empty:
            {
                // Pick from UnusedPositions only.
                var pool = board.UnusedPositions.ToList();
                return ShuffleAndTake(pool, config.Count, rng);
            }

            case PlacementStrategy.NonMine:
            {
                // Any usable, unrevealed, non-noble tile that doesn't already have this flag.
                var pool = board.Tiles
                    .Where(t => board.IsUsablePosition(t.Position)
                                && !t.IsRevealed
                                && !t.Specials.HasFlag(config.Type)
                                && t.Owner != TileOwner.Noble)
                    .Select(t => t.Position)
                    .ToList();
                return ShuffleAndTake(pool, config.Count, rng);
            }

            case PlacementStrategy.Random:
            {
                // Any usable, unrevealed tile that doesn't already have this flag.
                var pool = board.Tiles
                    .Where(t => board.IsUsablePosition(t.Position)
                                && !t.IsRevealed
                                && !t.Specials.HasFlag(config.Type))
                    .Select(t => t.Position)
                    .ToList();
                return ShuffleAndTake(pool, config.Count, rng);
            }

            case PlacementStrategy.Owners:
            default:
            {
                // Restrict to specific owner types (existing behavior).
                var eligibleOwners = new HashSet<TileOwner>(config.EligibleOwners);
                var pool = board.Tiles
                    .Where(t => board.IsUsablePosition(t.Position)
                                && !t.IsRevealed
                                && !t.Specials.HasFlag(config.Type)
                                && eligibleOwners.Contains(t.Owner))
                    .Select(t => t.Position)
                    .ToList();
                return ShuffleAndTake(pool, config.Count, rng);
            }
        }
    }

    private static List<Position> ShuffleAndTake(List<Position> pool, int count, Random rng)
    {
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(Math.Min(count, pool.Count)).ToList();
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
            var cleanedTile = tile.WithoutSpecial(SpecialTileType.ExtraDirty);
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

    /// <summary>
    /// Checks whether a revealed tile's adjacency count is fully satisfied by
    /// adjacent revealed tiles of the matching owner type.
    /// E.g., a tile with RevealedBy=Player and AdjacencyCount=3 is saturated
    /// when exactly 3 adjacent revealed tiles are Player-owned.
    /// </summary>
    public static bool IsSaturated(Board board, Position pos)
    {
        var tile = board.GetTile(pos);
        if (!tile.IsRevealed || tile.IsDestroyed) return false;
        if (tile.RevealedBy == null) return false;

        var targetOwner = tile.RevealedBy == PlayerType.Player ? TileOwner.Player : TileOwner.Rival;
        var neighbors = GetNeighbors(board, pos);
        var revealedMatchCount = neighbors.Count(n =>
        {
            var neighbor = board.GetTile(n);
            return neighbor.IsRevealed && neighbor.Owner == targetOwner;
        });

        return revealedMatchCount >= tile.AdjacencyCount;
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

    // ─────────────────────────────────────────────────────────────
    // Courtiers + Soirées (M39)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Picks a random adjacent eligible position for a courtier to move to.
    /// Eligible = usable + unrevealed + not destroyed + not already a courtier.
    /// Returns null if no eligible neighbor exists.
    /// </summary>
    public static Position? SelectCourtierTarget(Board board, Position from, Random rng)
    {
        var candidates = GetNeighbors(board, from)
            .Where(n => board.IsUsablePosition(n))
            .Where(n =>
            {
                var t = board.GetTile(n);
                return !t.IsRevealed && !t.IsDestroyed && !t.IsCourtier;
            })
            .ToList();

        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    /// <summary>
    /// Sets MoveTargets for all courtier-flagged tiles that don't yet have one.
    /// Called once after PlaceSpecialTiles during board creation.
    /// </summary>
    private static Board InitializeCourtierTargets(Board board, Random rng)
    {
        var positions = board.Tiles
            .Where(t => t.IsCourtier && t.CourtierMoveTarget == null)
            .Select(t => t.Position)
            .ToList();

        foreach (var pos in positions)
        {
            var target = SelectCourtierTarget(board, pos, rng);
            var newTiles = board.Tiles.ToList();
            newTiles[board.TileIndex(pos)] = board.GetTile(pos) with { CourtierMoveTarget = target };
            board = board with { Tiles = newTiles };
        }
        return board;
    }

    /// <summary>
    /// Removes the courtier flag from `from` and moves the courtier to its
    /// MoveTarget. Handles collision (incoming courtier disappears if target
    /// already has one) and target invalidation (destroyed/revealed/etc.).
    /// </summary>
    public static Board CleanCourtier(Board board, Position from, Random rng)
    {
        var origin = board.GetTile(from);
        if (!origin.IsCourtier) return board;

        // Remove courtier flag from origin (always)
        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(from)] = origin
            .WithoutSpecial(SpecialTileType.Courtier) with { CourtierMoveTarget = null };
        board = board with { Tiles = newTiles };

        var target = origin.CourtierMoveTarget;
        if (target == null) return board;
        if (!board.IsValidPosition(target.Value)) return board;
        if (!board.IsUsablePosition(target.Value)) return board;

        var targetTile = board.GetTile(target.Value);
        if (targetTile.IsRevealed || targetTile.IsDestroyed) return board;

        // Collision: target already has a courtier — incoming courtier merges (disappears)
        if (targetTile.IsCourtier) return board;

        // Move courtier to target with a fresh MoveTarget
        var newMoveTarget = SelectCourtierTarget(board, target.Value, rng);
        var moved = targetTile
            .WithSpecial(SpecialTileType.Courtier) with { CourtierMoveTarget = newMoveTarget };
        newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(target.Value)] = moved;
        return board with { Tiles = newTiles };
    }

    /// <summary>
    /// For each soirée tile, spawns 1 courtier on a random adjacent eligible tile
    /// (per SelectCourtierTarget rules). No-op when a soirée has no eligible neighbor.
    /// Called at the start of each rival turn.
    /// </summary>
    public static Board SpawnCourtiersFromSoirees(Board board, Random rng)
    {
        var soireePositions = board.Tiles
            .Where(t => t.IsSoiree && !t.IsDestroyed)
            .Select(t => t.Position)
            .ToList();

        foreach (var soireePos in soireePositions)
        {
            var spawnAt = SelectCourtierTarget(board, soireePos, rng);
            if (spawnAt == null) continue;

            var newMoveTarget = SelectCourtierTarget(board, spawnAt.Value, rng);
            var spawnTile = board.GetTile(spawnAt.Value);
            var spawned = spawnTile
                .WithSpecial(SpecialTileType.Courtier) with { CourtierMoveTarget = newMoveTarget };

            var newTiles = board.Tiles.ToList();
            newTiles[board.TileIndex(spawnAt.Value)] = spawned;
            board = board with { Tiles = newTiles };
        }

        return board;
    }
}
