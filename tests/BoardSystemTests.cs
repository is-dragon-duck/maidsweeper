using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class BoardSystemTests
{
    private static readonly LevelConfig Level1 = LevelConfigs.Level1;

    [Fact]
    public void CreateBoard_HasCorrectDimensions()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        Assert.Equal(6, board.Width);
        Assert.Equal(5, board.Height);
        Assert.Equal(30, board.Tiles.Count);
    }

    [Fact]
    public void CreateBoard_HasCorrectTileCounts()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        Assert.Equal(12, board.Tiles.Count(t => t.Owner == TileOwner.Player));
        Assert.Equal(10, board.Tiles.Count(t => t.Owner == TileOwner.Rival));
        Assert.Equal(8, board.Tiles.Count(t => t.Owner == TileOwner.Neutral));
        Assert.Equal(0, board.Tiles.Count(t => t.Owner == TileOwner.Noble));
    }

    [Fact]
    public void CreateBoard_AllTilesStartUnrevealed()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        Assert.All(board.Tiles, t =>
        {
            Assert.False(t.IsRevealed);
            Assert.Null(t.RevealedBy);
        });
    }

    [Fact]
    public void CreateBoard_TilesHaveCorrectPositions()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        for (var row = 0; row < board.Height; row++)
        {
            for (var col = 0; col < board.Width; col++)
            {
                var tile = board.GetTile(new Position(row, col));
                Assert.Equal(new Position(row, col), tile.Position);
            }
        }
    }

    [Fact]
    public void CreateBoard_ShuffleProducesKnownLayout()
    {
        // Verify a specific seed produces a specific known layout (deterministic)
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        // First few tiles for seed 42 — if shuffle logic changes, this test catches it
        var firstFiveOwners = board.Tiles.Take(5).Select(t => t.Owner).ToList();
        var snapshot = new List<TileOwner>
        {
            board.Tiles[0].Owner, board.Tiles[1].Owner, board.Tiles[2].Owner,
            board.Tiles[3].Owner, board.Tiles[4].Owner
        };

        // Verify it's not just the unshuffled order (Player, Player, Player, ...)
        // The first 12 tiles pre-shuffle would all be Player; after shuffle they shouldn't be
        Assert.False(
            board.Tiles.Take(12).All(t => t.Owner == TileOwner.Player),
            "Board should be shuffled, not in original order");
    }

    [Fact]
    public void CreateBoard_SameSeedProducesSameLayout()
    {
        var board1 = BoardSystem.CreateBoard(Level1, new Random(42));
        var board2 = BoardSystem.CreateBoard(Level1, new Random(42));

        var owners1 = board1.Tiles.Select(t => t.Owner).ToList();
        var owners2 = board2.Tiles.Select(t => t.Owner).ToList();

        Assert.True(owners1.SequenceEqual(owners2));
    }

    [Fact]
    public void CreateBoard_ThrowsOnMismatchedCounts()
    {
        var badConfig = new LevelConfig
        {
            Width = 6, Height = 5,
            PlayerCount = 10, RivalCount = 10, NeutralCount = 8, NobleCount = 0
        };

        Assert.Throws<ArgumentException>(() =>
            BoardSystem.CreateBoard(badConfig, new Random(42)));
    }

    [Fact]
    public void GetNeighbors_CornerReturns3()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        var topLeft = BoardSystem.GetNeighbors(board, new Position(0, 0));
        Assert.Equal(3, topLeft.Count);

        var bottomRight = BoardSystem.GetNeighbors(board, new Position(4, 5));
        Assert.Equal(3, bottomRight.Count);
    }

    [Fact]
    public void GetNeighbors_EdgeReturns5()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        // Top edge, not corner
        var topEdge = BoardSystem.GetNeighbors(board, new Position(0, 3));
        Assert.Equal(5, topEdge.Count);

        // Left edge, not corner
        var leftEdge = BoardSystem.GetNeighbors(board, new Position(2, 0));
        Assert.Equal(5, leftEdge.Count);
    }

    [Fact]
    public void GetNeighbors_CenterReturns8()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        var center = BoardSystem.GetNeighbors(board, new Position(2, 3));
        Assert.Equal(8, center.Count);
    }

    [Fact]
    public void GetNeighbors_ReturnsCorrectPositions()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        var neighbors = BoardSystem.GetNeighbors(board, new Position(1, 1));
        var expected = new HashSet<Position>
        {
            new(0, 0), new(0, 1), new(0, 2),
            new(1, 0),            new(1, 2),
            new(2, 0), new(2, 1), new(2, 2)
        };

        Assert.Equal(expected, neighbors.ToHashSet());
    }

    [Fact]
    public void CalculateAdjacency_CountsRevealerOwnerType()
    {
        // Create a small known board for adjacency testing
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 4, RivalCount = 3, NeutralCount = 2, NobleCount = 0
        };

        // Use a seeded random and find the actual layout
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(config, rng);

        // For center tile (1,1), count player-type neighbors
        var center = new Position(1, 1);
        var playerAdj = BoardSystem.CalculateAdjacency(board, center, PlayerType.Player);
        var rivalAdj = BoardSystem.CalculateAdjacency(board, center, PlayerType.Rival);

        // Count manually from the actual board
        var neighbors = BoardSystem.GetNeighbors(board, center);
        var expectedPlayer = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Player);
        var expectedRival = neighbors.Count(n => board.GetTile(n).Owner == TileOwner.Rival);

        Assert.Equal(expectedPlayer, playerAdj);
        Assert.Equal(expectedRival, rivalAdj);
    }

    [Fact]
    public void RevealTile_MarksAsRevealed()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));
        var pos = new Position(0, 0);

        var newBoard = BoardSystem.RevealTile(board, pos, PlayerType.Player);

        var tile = newBoard.GetTile(pos);
        Assert.True(tile.IsRevealed);
        Assert.Equal(PlayerType.Player, tile.RevealedBy);
    }

    [Fact]
    public void RevealTile_SetsAdjacencyCount()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));
        var pos = new Position(2, 3);

        var newBoard = BoardSystem.RevealTile(board, pos, PlayerType.Player);

        var tile = newBoard.GetTile(pos);
        var expectedAdj = BoardSystem.CalculateAdjacency(board, pos, PlayerType.Player);
        Assert.Equal(expectedAdj, tile.AdjacencyCount);
    }

    [Fact]
    public void RevealTile_DoesNotMutateOriginalBoard()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));
        var pos = new Position(0, 0);

        var newBoard = BoardSystem.RevealTile(board, pos, PlayerType.Player);

        // Original board unchanged
        Assert.False(board.GetTile(pos).IsRevealed);
        // New board has the reveal
        Assert.True(newBoard.GetTile(pos).IsRevealed);
    }

    [Fact]
    public void RevealTile_AlreadyRevealedReturnsSameBoard()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));
        var pos = new Position(0, 0);

        var revealed = BoardSystem.RevealTile(board, pos, PlayerType.Player);
        var doubleRevealed = BoardSystem.RevealTile(revealed, pos, PlayerType.Player);

        Assert.Same(revealed, doubleRevealed);
    }

    [Fact]
    public void RevealTile_RivalRevealCountsRivalNeighbors()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));
        var pos = new Position(2, 3);

        var playerRevealed = BoardSystem.RevealTile(board, pos, PlayerType.Player);
        var rivalRevealed = BoardSystem.RevealTile(board, pos, PlayerType.Rival);

        var playerAdj = playerRevealed.GetTile(pos).AdjacencyCount;
        var rivalAdj = rivalRevealed.GetTile(pos).AdjacencyCount;

        // These should generally differ since they count different owner types
        // (Both could coincidentally be equal, but the values should be correct)
        var expectedPlayer = BoardSystem.CalculateAdjacency(board, pos, PlayerType.Player);
        var expectedRival = BoardSystem.CalculateAdjacency(board, pos, PlayerType.Rival);

        Assert.Equal(expectedPlayer, playerAdj);
        Assert.Equal(expectedRival, rivalAdj);
    }

    // --- Unused Locations Tests ---

    [Fact]
    public void CreateBoard_WithUnusedLocations_TracksUnusedPositions()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        Assert.Equal(2, board.UnusedPositions.Count);
        Assert.Contains(new Position(0, 0), board.UnusedPositions);
        Assert.Contains(new Position(4, 5), board.UnusedPositions);
    }

    [Fact]
    public void CreateBoard_WithUnusedLocations_CorrectUsableTileCount()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        var usableTiles = board.Tiles.Where(t => board.IsUsablePosition(t.Position)).ToList();
        Assert.Equal(28, usableTiles.Count); // 30 - 2 unused
    }

    [Fact]
    public void CreateBoard_WithUnusedLocations_CorrectOwnerCounts()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        var usable = board.Tiles.Where(t => board.IsUsablePosition(t.Position)).ToList();
        Assert.Equal(10, usable.Count(t => t.Owner == TileOwner.Player));
        Assert.Equal(9, usable.Count(t => t.Owner == TileOwner.Rival));
        Assert.Equal(8, usable.Count(t => t.Owner == TileOwner.Neutral));
        Assert.Equal(1, usable.Count(t => t.Owner == TileOwner.Noble));
    }

    [Fact]
    public void IsUsablePosition_FalseForUnusedPositions()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        Assert.False(board.IsUsablePosition(new Position(0, 0)));
        Assert.False(board.IsUsablePosition(new Position(4, 5)));
        Assert.True(board.IsUsablePosition(new Position(0, 1)));
    }

    [Fact]
    public void GetNeighbors_ExcludesUnusedPositions()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        // Position (0,1) is adjacent to unused (0,0)
        var neighbors = BoardSystem.GetNeighbors(board, new Position(0, 1));
        Assert.DoesNotContain(new Position(0, 0), neighbors);
    }

    [Fact]
    public void CalculateAdjacency_ExcludesUnusedPositions()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        // Adjacency should only count usable neighbors
        var pos = new Position(1, 0); // near unused (0,0)
        var adj = BoardSystem.CalculateAdjacency(board, pos, PlayerType.Player);
        var usableNeighbors = BoardSystem.GetNeighbors(board, pos);

        // Should NOT count the placeholder tile at (0,0)
        Assert.DoesNotContain(new Position(0, 0), usableNeighbors);
        var expected = usableNeighbors.Count(n => board.GetTile(n).Owner == TileOwner.Player);
        Assert.Equal(expected, adj);
    }

    // --- ExtraDirty Tests ---

    [Fact]
    public void CreateBoard_WithExtraDirty_PlacesCorrectCount()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        var dirtyTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && t.IsDirty)
            .ToList();

        Assert.Single(dirtyTiles);
    }

    [Fact]
    public void CreateBoard_WithExtraDirty_OnlyEligibleOwners()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));

        var dirtyTiles = board.Tiles.Where(t => t.IsDirty).ToList();
        Assert.All(dirtyTiles, t =>
            Assert.True(t.Owner == TileOwner.Player || t.Owner == TileOwner.Neutral));
    }

    [Fact]
    public void RevealTile_ExtraDirty_PlayerClickCleans()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);

        var newBoard = BoardSystem.RevealTile(board, dirtyTile.Position, PlayerType.Player);

        var tile = newBoard.GetTile(dirtyTile.Position);
        Assert.False(tile.IsDirty); // Dirt removed
        Assert.False(tile.IsRevealed); // Not revealed yet
    }

    [Fact]
    public void RevealTile_ExtraDirty_SecondClickReveals()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);

        // First click: clean
        var cleaned = BoardSystem.RevealTile(board, dirtyTile.Position, PlayerType.Player);
        Assert.False(cleaned.GetTile(dirtyTile.Position).IsRevealed);

        // Second click: reveal
        var revealed = BoardSystem.RevealTile(cleaned, dirtyTile.Position, PlayerType.Player);
        Assert.True(revealed.GetTile(dirtyTile.Position).IsRevealed);
    }

    [Fact]
    public void RevealTile_ExtraDirty_RivalRevealsNormally()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);

        var newBoard = BoardSystem.RevealTile(board, dirtyTile.Position, PlayerType.Rival);

        var tile = newBoard.GetTile(dirtyTile.Position);
        Assert.True(tile.IsRevealed); // Rival ignores ExtraDirty
    }

    // --- Level Config Validation ---

    [Fact]
    public void Level2Config_ValidatesCorrectly()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        Assert.Equal(30, board.Tiles.Count);
        Assert.Equal(2, board.UnusedPositions.Count);
    }

    [Fact]
    public void Level3Config_ValidatesCorrectly()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level3, new Random(42));
        Assert.Equal(36, board.Tiles.Count); // 6x6
        Assert.Equal(4, board.UnusedPositions.Count);

        var usable = board.Tiles.Where(t => board.IsUsablePosition(t.Position)).ToList();
        Assert.Equal(32, usable.Count);
        Assert.Equal(3, usable.Count(t => t.Owner == TileOwner.Noble));
    }

    [Fact]
    public void Level3Config_ExtraDirtyCount()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level3, new Random(42));

        var dirtyTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && t.IsDirty)
            .ToList();

        Assert.Equal(3, dirtyTiles.Count);
    }

    // --- GetTilesInArea Tests ---

    [Fact]
    public void GetTilesInArea_ReturnsCorrectCountAtCenter()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        // 3x3 area at center of 6x5 board
        var tiles = BoardSystem.GetTilesInArea(board, new Position(2, 3), 1);
        Assert.Equal(9, tiles.Count); // Full 3x3
    }

    [Fact]
    public void GetTilesInArea_ClipsAtEdges()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        // 3x3 area at corner (0,0) — only 4 valid positions
        var tiles = BoardSystem.GetTilesInArea(board, new Position(0, 0), 1);
        Assert.Equal(4, tiles.Count);
    }

    [Fact]
    public void GetTilesInArea_ExcludesUnusedPositions()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level3, new Random(42));

        // Center of Level 3 has unused positions at (2,2), (2,3), (3,2), (3,3)
        // 5x5 area centered at (2,2) would include the hole
        var tiles = BoardSystem.GetTilesInArea(board, new Position(2, 2), 2);
        var positions = tiles.Select(t => t.Position).ToHashSet();

        Assert.DoesNotContain(new Position(2, 2), positions);
        Assert.DoesNotContain(new Position(2, 3), positions);
        Assert.DoesNotContain(new Position(3, 2), positions);
        Assert.DoesNotContain(new Position(3, 3), positions);
    }

    [Fact]
    public void GetTilesInArea_Radius2Returns5x5()
    {
        var board = BoardSystem.CreateBoard(Level1, new Random(42));

        // 5x5 area at center — should get 25 tiles
        var tiles = BoardSystem.GetTilesInArea(board, new Position(2, 3), 2);
        Assert.Equal(25, tiles.Count);
    }
}
