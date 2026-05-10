using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// M49: L9–L15 configs ported from the alpha. Validates dimensions, tile counts,
/// AI assignments, special-tile counts, RandomUnusedCount handling, Explicit
/// shuffle-and-truncate behavior, and surface-mine ↔ courtier mutual exclusion.
/// </summary>
public class AlphaLevel9To15Tests
{
    public static IEnumerable<object[]> AllStage5Levels =>
        new[]
        {
            new object[] { "level9" }, new object[] { "level10" },
            new object[] { "level11" }, new object[] { "level12" },
            new object[] { "level13" }, new object[] { "level14" },
            new object[] { "level15" }
        };

    [Theory]
    [MemberData(nameof(AllStage5Levels))]
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
    [InlineData("level9",  7, 6, 13, 11, 8,  6)]
    [InlineData("level10", 10, 9, 18, 15, 13, 6)]
    [InlineData("level11", 8, 8, 22, 17, 15, 6)]
    [InlineData("level12", 9, 9, 21, 18, 15, 7)]
    [InlineData("level13", 10, 10, 19, 18, 14, 9)]
    [InlineData("level14", 9, 9, 21, 19, 15, 6)]
    [InlineData("level15", 9, 9, 21, 19, 14, 7)]
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

    // --- AI assignments ---

    [Theory]
    [InlineData("level9", AiType.Conservative)]
    [InlineData("level10", AiType.Conservative)]
    [InlineData("level11", AiType.Conservative)]
    [InlineData("level12", AiType.Conservative)]
    [InlineData("level13", AiType.Conservative)]
    [InlineData("level14", AiType.Reasoning)]
    [InlineData("level15", AiType.Reasoning)]
    public void Level_RivalAi(string levelId, AiType expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalAi);
    }

    // --- Adjacency ---

    [Theory]
    [InlineData("level9",  AdjacencyRule.Manhattan2)]
    [InlineData("level10", AdjacencyRule.King)]
    [InlineData("level11", AdjacencyRule.King)]
    [InlineData("level12", AdjacencyRule.King)]
    [InlineData("level13", AdjacencyRule.Manhattan2)]
    [InlineData("level14", AdjacencyRule.King)]
    [InlineData("level15", AdjacencyRule.King)]
    public void Level_Adjacency(string levelId, AdjacencyRule expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.AdjacencyRule);
    }

    // --- Initial rival reveal ---

    [Theory]
    [InlineData("level9", 2)]
    [InlineData("level10", 1)]
    [InlineData("level11", 1)]
    [InlineData("level12", 1)]
    [InlineData("level13", 1)]
    [InlineData("level14", 2)]
    [InlineData("level15", 2)]
    public void Level_InitialRivalReveal(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.InitialRivalReveal);
    }

    // --- Mine protection ---

    [Theory]
    [InlineData("level9", 0)]
    [InlineData("level10", 0)]
    [InlineData("level11", 0)]
    [InlineData("level12", 1)]
    [InlineData("level13", 0)]
    [InlineData("level14", 1)]
    [InlineData("level15", 1)]
    public void Level_RivalMineProtection(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalMineProtection);
    }

    // --- Rival places mines ---

    [Theory]
    [InlineData("level14", 1)]
    [InlineData("level15", 2)]
    public void Level_RivalPlacesMines(string levelId, int expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalPlacesMines);
    }

    [Theory]
    [InlineData("level9")]
    [InlineData("level10")]
    [InlineData("level11")]
    [InlineData("level12")]
    [InlineData("level13")]
    public void Level_DoesNotPlaceMines(string levelId)
    {
        Assert.Equal(0, LevelConfigs.GetById(levelId)!.RivalPlacesMines);
    }

    // --- RivalNeverNobles (alpha "rivalNeverMines") ---

    [Theory]
    [InlineData("level9", true)]
    [InlineData("level13", true)]
    public void Level_RivalNeverNobles_Set(string levelId, bool expected)
    {
        Assert.Equal(expected, LevelConfigs.GetById(levelId)!.RivalNeverNobles);
    }

    [Theory]
    [InlineData("level10")]
    [InlineData("level11")]
    [InlineData("level12")]
    [InlineData("level14")]
    [InlineData("level15")]
    public void Level_RivalNeverNobles_NotSet(string levelId)
    {
        Assert.False(LevelConfigs.GetById(levelId)!.RivalNeverNobles);
    }

    // --- Reward flow per uponFinish ---

    [Theory]
    [InlineData("level9",  true,  false, true,  false, "level10")]
    [InlineData("level10", true,  false, false, false, "level11")]
    [InlineData("level11", true,  true,  false, false, "level12")]
    [InlineData("level12", false, false, false, true,  "level13")]
    [InlineData("level13", true,  false, true,  false, "level14")]
    [InlineData("level14", true,  false, false, false, "level15")]
    [InlineData("level15", true,  true,  false, false, "level16")]
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

    // --- Special tile counts ---

    [Theory]
    [InlineData("level9",  SpecialTileType.ExtraDirty, 6)]
    [InlineData("level10", SpecialTileType.ExtraDirty, 5)]
    [InlineData("level10", SpecialTileType.Courtier,   2)]
    [InlineData("level10", SpecialTileType.Soiree,     4)]
    [InlineData("level11", SpecialTileType.ExtraDirty, 4)]
    [InlineData("level11", SpecialTileType.Soiree,     4)]
    [InlineData("level12", SpecialTileType.ExtraDirty, 5)]
    [InlineData("level12", SpecialTileType.Courtier,   1)]
    [InlineData("level12", SpecialTileType.Soiree,     1)]
    [InlineData("level12", SpecialTileType.LoungingNoble, 4)]
    [InlineData("level13", SpecialTileType.ExtraDirty, 4)]
    [InlineData("level13", SpecialTileType.Courtier,   2)]
    [InlineData("level13", SpecialTileType.Soiree,     4)]
    [InlineData("level14", SpecialTileType.ExtraDirty, 5)]
    [InlineData("level14", SpecialTileType.LoungingNoble, 2)]
    [InlineData("level15", SpecialTileType.ExtraDirty, 6)]
    [InlineData("level15", SpecialTileType.LoungingNoble, 3)]
    [InlineData("level15", SpecialTileType.Courtier, 2)]
    [InlineData("level15", SpecialTileType.Soiree, 2)]
    public void Level_SpecialTileCount(string levelId, SpecialTileType type, int expected)
    {
        var s = LevelConfigs.GetById(levelId)!.SpecialTiles
            .FirstOrDefault(x => x.Type == type);
        Assert.NotNull(s);
        Assert.Equal(expected, s!.Count);
    }

    // --- RandomUnusedCount honored at board creation ---

    [Theory]
    [InlineData("level12", 20)]
    [InlineData("level14", 20)]
    [InlineData("level15", 20)]
    public void Level_RandomUnusedCountProducesExactlyNHoles(string levelId, int expected)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expected, c.RandomUnusedCount);
        Assert.Empty(c.UnusedLocations);

        var board = BoardSystem.CreateBoard(c, new Random(42));
        Assert.Equal(expected, board.UnusedPositions.Count);
    }

    [Fact]
    public void RandomUnusedCount_DifferentSeedsProduceDifferentHoles()
    {
        var b1 = BoardSystem.CreateBoard(LevelConfigs.Level12, new Random(1));
        var b2 = BoardSystem.CreateBoard(LevelConfigs.Level12, new Random(2));
        Assert.NotEqual(b1.UnusedPositions, b2.UnusedPositions);
    }

    // --- Explicit placement: shuffle + take Count ---

    [Fact]
    public void Level13_LairPlacement_PicksFourFromEightCandidates()
    {
        var soireeConfig = LevelConfigs.Level13.SpecialTiles
            .First(s => s.Type == SpecialTileType.Soiree);
        Assert.Equal(PlacementStrategy.Explicit, soireeConfig.Strategy);
        Assert.Equal(4, soireeConfig.Count);
        Assert.Equal(8, soireeConfig.ExplicitPositions.Count);

        var board = BoardSystem.CreateBoard(LevelConfigs.Level13, new Random(42));
        var soirees = board.Tiles.Count(t => t.Specials.HasFlag(SpecialTileType.Soiree));
        Assert.Equal(4, soirees);

        // Every soirée landed on one of the eight candidates
        var allowed = soireeConfig.ExplicitPositions.ToHashSet();
        foreach (var t in board.Tiles.Where(x => x.Specials.HasFlag(SpecialTileType.Soiree)))
            Assert.Contains(t.Position, allowed);
    }

    [Fact]
    public void Level13_LairPlacement_DifferentSeedsPickDifferentSubsets()
    {
        var b1 = BoardSystem.CreateBoard(LevelConfigs.Level13, new Random(1));
        var b2 = BoardSystem.CreateBoard(LevelConfigs.Level13, new Random(99));
        var s1 = b1.Tiles.Where(t => t.Specials.HasFlag(SpecialTileType.Soiree))
            .Select(t => t.Position).ToHashSet();
        var s2 = b2.Tiles.Where(t => t.Specials.HasFlag(SpecialTileType.Soiree))
            .Select(t => t.Position).ToHashSet();
        Assert.NotEqual(s1, s2);
    }

    // --- Surface mine ↔ courtier mutual exclusion ---

    [Fact]
    public void SurfaceMine_AndCourtier_NeverShareTile()
    {
        // L15 has both: 3 surfaceMines and 2 courtiers. Run multiple seeds.
        for (var seed = 0; seed < 30; seed++)
        {
            var board = BoardSystem.CreateBoard(LevelConfigs.Level15, new Random(seed));
            foreach (var t in board.Tiles)
            {
                if (t.Specials.HasFlag(SpecialTileType.LoungingNoble)
                    && t.Specials.HasFlag(SpecialTileType.Courtier))
                {
                    Assert.Fail($"Seed {seed}: tile at {t.Position} has both LoungingNoble and Courtier");
                }
            }
        }
    }

    // --- L10 corner-soirée placement (Explicit on holes) ---

    [Fact]
    public void Level10_SoireesAtCorners()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level10, new Random(42));
        var soireeCount = board.Tiles.Count(t => t.Specials.HasFlag(SpecialTileType.Soiree));
        Assert.Equal(4, soireeCount);

        var corners = new[]
        {
            new Position(0, 0), new Position(0, 9),
            new Position(8, 0), new Position(8, 9)
        };
        foreach (var corner in corners)
            Assert.True(board.GetTile(corner).Specials.HasFlag(SpecialTileType.Soiree),
                $"Corner {corner} should have Soiree");
    }

    // --- L12 lounging nobles land on player/neutral only ---

    [Fact]
    public void Level12_LoungingNoblesOnPlayerOrNeutralOnly()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var board = BoardSystem.CreateBoard(LevelConfigs.Level12, new Random(seed));
            var lounging = board.Tiles
                .Where(t => t.Specials.HasFlag(SpecialTileType.LoungingNoble))
                .ToList();
            Assert.Equal(4, lounging.Count);
            Assert.All(lounging, t =>
                Assert.True(t.Owner == TileOwner.Player || t.Owner == TileOwner.Neutral));
        }
    }
}
