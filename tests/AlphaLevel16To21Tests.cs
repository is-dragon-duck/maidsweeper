using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// M50: L16–L21 configs ported verbatim from the alpha. Sanctums introduced at
/// L16; L21 is the final boss with <c>winTheGame=true</c>.
/// </summary>
public class AlphaLevel16To21Tests
{
    public static IEnumerable<object[]> SanctumLevels =>
        new[]
        {
            new object[] { "level16" }, new object[] { "level17" },
            new object[] { "level18" }, new object[] { "level19" },
            new object[] { "level20" }, new object[] { "level21" }
        };

    [Theory]
    [MemberData(nameof(SanctumLevels))]
    public void Level_TileCountsSumToUsableArea(string levelId)
    {
        var c = LevelConfigs.GetById(levelId);
        Assert.NotNull(c);
        var holes = c!.UnusedLocations.Count > 0 ? c.UnusedLocations.Count : c.RandomUnusedCount;
        var usable = c.Width * c.Height - holes;
        var sum = c.PlayerCount + c.RivalCount + c.NeutralCount + c.NobleCount;
        Assert.Equal(usable, sum);
    }

    // --- Dimensions + tile counts ---

    [Theory]
    [InlineData("level16", 7, 7, 15, 13, 10, 7)]
    [InlineData("level17", 9, 9, 21, 19, 14, 7)]
    [InlineData("level18", 10, 10, 24, 21, 16, 9)]
    [InlineData("level19", 8, 8, 20, 18, 15, 8)]
    [InlineData("level20", 10, 10, 24, 22, 19, 5)]
    [InlineData("level21", 10, 10, 24, 22, 19, 10)]
    public void Level_DimensionsAndCountsMatchAlpha(
        string levelId, int width, int height,
        int player, int rival, int neutral, int noble)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(width, c.Width);
        Assert.Equal(height, c.Height);
        Assert.Equal(player, c.PlayerCount);
        Assert.Equal(rival, c.RivalCount);
        Assert.Equal(neutral, c.NeutralCount);
        Assert.Equal(noble, c.NobleCount);
    }

    // --- All Stage 5 final-segment levels run on Reasoning ---

    [Theory]
    [MemberData(nameof(SanctumLevels))]
    public void Level_RunsReasoningAi(string levelId)
    {
        Assert.Equal(AiType.Reasoning, LevelConfigs.GetById(levelId)!.RivalAi);
    }

    // --- Adjacency ---

    [Theory]
    [InlineData("level16", AdjacencyRule.King)]
    [InlineData("level17", AdjacencyRule.King)]
    [InlineData("level18", AdjacencyRule.Manhattan2)]
    [InlineData("level19", AdjacencyRule.King)]
    [InlineData("level20", AdjacencyRule.Manhattan2)]
    [InlineData("level21", AdjacencyRule.Manhattan2)]
    public void Level_Adjacency(string levelId, AdjacencyRule expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.AdjacencyRule);
    }

    // --- Initial rival reveal ---

    [Theory]
    [InlineData("level16", 2)]
    [InlineData("level17", 3)]
    [InlineData("level18", 3)]
    [InlineData("level19", 3)]
    [InlineData("level20", 2)]
    [InlineData("level21", 4)]
    public void Level_InitialRivalReveal(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.InitialRivalReveal);
    }

    // --- Mine protection / rival places mines / never nobles ---

    [Theory]
    [InlineData("level16", 2)]
    [InlineData("level17", 0)]
    [InlineData("level18", 2)]
    [InlineData("level19", 3)]
    [InlineData("level20", 3)]
    [InlineData("level21", 0)]
    public void Level_RivalMineProtection(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalMineProtection);
    }

    [Theory]
    [InlineData("level17", 3)]
    [InlineData("level19", 3)]
    [InlineData("level20", 1)]
    [InlineData("level21", 3)]
    public void Level_RivalPlacesMines(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalPlacesMines);
    }

    [Theory]
    [InlineData("level17")]
    [InlineData("level21")]
    public void Level_RivalNeverNobles_Set(string levelId)
    {
        Assert.True(LevelConfigs.GetById(levelId)!.RivalNeverNobles);
    }

    // --- Reward flow per uponFinish ---

    [Theory]
    [InlineData("level16", false, false, false, true,  "level17")]
    [InlineData("level17", true,  false, true,  false, "level18")]
    [InlineData("level18", true,  false, false, false, "level19")]
    [InlineData("level19", true,  true,  false, false, "level20")]
    [InlineData("level20", false, false, false, true,  "level21")]
    public void Level_UponFinishMatchesAlpha(
        string levelId, bool card, bool upgrade, bool equipment, bool shop, string nextId)
    {
        var f = LevelConfigs.GetById(levelId)!.UponFinish!;
        Assert.Equal(card, f.CardReward);
        Assert.Equal(upgrade, f.UpgradeReward);
        Assert.Equal(equipment, f.EquipmentReward);
        Assert.Equal(shop, f.Shop);
        Assert.Equal(nextId, f.NextLevelId);
    }

    [Fact]
    public void Level21_HasNoRewardsAndNoNextLevel()
    {
        // alpha L21: winTheGame = true, nextLevel: [""]
        var f = LevelConfigs.Level21.UponFinish!;
        Assert.False(f.CardReward);
        Assert.False(f.UpgradeReward);
        Assert.False(f.EquipmentReward);
        Assert.False(f.Shop);
        Assert.Null(f.NextLevelId);
    }

    [Fact]
    public void Level21_CompleteFloor_TransitionsDirectlyToCampaignVictory()
    {
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level21, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level21",
            GameStatus = GameStatus.Won
        };
        var result = CampaignSystem.CompleteFloor(state, new Random(123));
        Assert.Equal(GamePhase.CampaignVictory, result.GamePhase);
    }

    // --- Special tile counts ---

    [Theory]
    [InlineData("level16", SpecialTileType.ExtraDirty, 4)]
    [InlineData("level16", SpecialTileType.Sanctum, 1)]
    [InlineData("level16", SpecialTileType.Courtier, 1)]
    [InlineData("level16", SpecialTileType.Soiree, 1)]
    [InlineData("level17", SpecialTileType.LoungingNoble, 3)]
    [InlineData("level17", SpecialTileType.Courtier, 3)]
    [InlineData("level17", SpecialTileType.Soiree, 3)]
    [InlineData("level18", SpecialTileType.Sanctum, 6)]
    [InlineData("level18", SpecialTileType.Courtier, 3)]
    [InlineData("level19", SpecialTileType.Sanctum, 3)]
    [InlineData("level19", SpecialTileType.LoungingNoble, 3)]
    [InlineData("level19", SpecialTileType.Courtier, 3)]
    [InlineData("level19", SpecialTileType.Soiree, 3)]
    [InlineData("level20", SpecialTileType.Sanctum, 5)]
    [InlineData("level20", SpecialTileType.LoungingNoble, 1)]
    [InlineData("level21", SpecialTileType.Sanctum, 7)]
    [InlineData("level21", SpecialTileType.Courtier, 5)]
    [InlineData("level21", SpecialTileType.Soiree, 5)]
    [InlineData("level21", SpecialTileType.LoungingNoble, 5)]
    public void Level_SpecialTileCount(string levelId, SpecialTileType type, int expected)
    {
        var s = LevelConfigs.GetById(levelId)!.SpecialTiles
            .FirstOrDefault(x => x.Type == type);
        Assert.NotNull(s);
        Assert.Equal(expected, s!.Count);
    }

    // --- Sanctums always land on neutral or noble ---

    [Fact]
    public void Sanctum_PlacedOnNeutralOrNoble()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var board = BoardSystem.CreateBoard(LevelConfigs.Level18, new Random(seed));
            var sanctums = board.Tiles.Where(t => t.IsSanctum).ToList();
            Assert.Equal(6, sanctums.Count);
            Assert.All(sanctums, t =>
                Assert.True(t.Owner == TileOwner.Neutral || t.Owner == TileOwner.Noble,
                    $"Sanctum at {t.Position} has owner {t.Owner}"));
        }
    }

    // --- Inner tile generation: sanctums create inner tiles around them ---

    [Fact]
    public void InnerTiles_GeneratedForLevelsWithSanctums()
    {
        // L18 has 6 sanctums + Manhattan-2 adjacency → many inner tiles around them.
        var board = BoardSystem.CreateBoard(LevelConfigs.Level18, new Random(42));
        var innerCount = board.Tiles.Count(t => t.IsInner);
        Assert.True(innerCount > 0, "L18 should have at least some inner tiles");

        // Every inner tile must sit next to at least one sanctum (raw spatial neighbor).
        foreach (var inner in board.Tiles.Where(t => t.IsInner))
        {
            var hasSanctumNeighbor = false;
            for (var dRow = -1; dRow <= 1 && !hasSanctumNeighbor; dRow++)
            for (var dCol = -1; dCol <= 1 && !hasSanctumNeighbor; dCol++)
            {
                if (dRow == 0 && dCol == 0) continue;
                var n = new Position(inner.Position.Row + dRow, inner.Position.Col + dCol);
                if (!board.IsValidPosition(n)) continue;
                if (board.GetTile(n).IsSanctum) hasSanctumNeighbor = true;
            }
            // Manhattan-2 also includes (±2,0) and (0,±2)
            for (var d = -2; d <= 2 && !hasSanctumNeighbor; d += 4)
            {
                var ns = new[]
                {
                    new Position(inner.Position.Row + d, inner.Position.Col),
                    new Position(inner.Position.Row, inner.Position.Col + d)
                };
                foreach (var n in ns)
                {
                    if (!board.IsValidPosition(n)) continue;
                    if (board.GetTile(n).IsSanctum) hasSanctumNeighbor = true;
                }
            }
            Assert.True(hasSanctumNeighbor,
                $"Inner tile at {inner.Position} has no sanctum neighbor");
        }
    }

    [Fact]
    public void InnerTiles_NeverPlacedOnSanctumsOrEmpties()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level21, new Random(7));
        foreach (var inner in board.Tiles.Where(t => t.IsInner))
        {
            Assert.False(inner.IsSanctum,
                $"Inner tile at {inner.Position} should not be a sanctum");
            Assert.True(board.IsUsablePosition(inner.Position),
                $"Inner tile at {inner.Position} should not be on an empty position");
        }
    }

    [Fact]
    public void NoSanctums_NoInnerTiles()
    {
        // Pre-Stage-5 levels have no sanctum specials → no inner tiles.
        var board = BoardSystem.CreateBoard(LevelConfigs.Level10, new Random(7));
        Assert.DoesNotContain(board.Tiles, t => t.IsInner);
    }

    // --- RandomUnusedCount honored ---

    [Theory]
    [InlineData("level16", 4)]
    [InlineData("level17", 20)]
    [InlineData("level18", 30)]
    [InlineData("level19", 3)]
    [InlineData("level20", 30)]
    [InlineData("level21", 25)]
    public void Level_RandomUnusedCountProducesExactlyNHoles(string levelId, int expected)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expected, c.RandomUnusedCount);
        var board = BoardSystem.CreateBoard(c, new Random(42));
        Assert.Equal(expected, board.UnusedPositions.Count);
    }

    // --- Surface mine ↔ courtier mutual exclusion under heavy mix ---

    [Fact]
    public void Level21_SurfaceMineAndCourtier_NeverShareTile()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var board = BoardSystem.CreateBoard(LevelConfigs.Level21, new Random(seed));
            foreach (var t in board.Tiles)
            {
                Assert.False(
                    t.Specials.HasFlag(SpecialTileType.LoungingNoble) &&
                    t.Specials.HasFlag(SpecialTileType.Courtier),
                    $"Seed {seed}: tile at {t.Position} has both LoungingNoble and Courtier");
            }
        }
    }
}
