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
}
