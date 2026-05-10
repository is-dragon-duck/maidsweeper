using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// M48: L1–8 configs ported verbatim from the alpha's <c>levels-config.json</c>.
/// Each test pins one slice of that file (dimensions, tile counts, holes, special
/// tiles, behaviors, reward flow). If the alpha config changes, these tests are
/// the single point of update.
/// </summary>
public class AlphaLevelConfigTests
{
    public static IEnumerable<object[]> AllLevels =>
        new[]
        {
            new object[] { "level1" }, new object[] { "level2" },
            new object[] { "level3" }, new object[] { "level4" },
            new object[] { "level5" }, new object[] { "level6" },
            new object[] { "level7" }, new object[] { "level8" }
        };

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Level_TileCountsSumToUsableArea(string levelId)
    {
        var c = LevelConfigs.GetById(levelId);
        Assert.NotNull(c);
        var usable = c!.Width * c.Height - c.UnusedLocations.Count;
        var sum = c.PlayerCount + c.RivalCount + c.NeutralCount + c.NobleCount;
        Assert.Equal(usable, sum);
    }

    // --- Per-level dimension + tile-count pins ---

    [Theory]
    [InlineData("level1", 6, 5, 12, 10, 8, 0)]
    [InlineData("level2", 6, 5, 10, 9, 8, 1)]
    [InlineData("level3", 6, 6, 11, 10, 8, 3)]
    [InlineData("level4", 9, 9, 14, 12, 11, 3)]
    [InlineData("level5", 7, 7, 12, 11, 9, 4)]
    [InlineData("level6", 9, 9, 15, 13, 11, 3)]
    [InlineData("level7", 7, 7, 13, 11, 9, 3)]
    [InlineData("level8", 7, 7, 17, 15, 12, 4)]
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

    // --- Hole counts (the actual position layouts are pinned elsewhere) ---

    [Theory]
    [InlineData("level1", 0)]
    [InlineData("level2", 2)]
    [InlineData("level3", 4)]
    [InlineData("level4", 41)]   // checkerboard, 5+4+5+4+5+4+5+4+5
    [InlineData("level5", 13)]   // diamond + cross
    [InlineData("level6", 39)]   // checkerboard + extra mid-row gaps
    [InlineData("level7", 13)]   // same pattern as L5
    [InlineData("level8", 1)]
    public void Level_HoleCountMatchesAlpha(string levelId, int expectedHoles)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expectedHoles, c.UnusedLocations.Count);
    }

    // --- Adjacency rules ---

    [Theory]
    [InlineData("level1", AdjacencyRule.King)]
    [InlineData("level2", AdjacencyRule.King)]
    [InlineData("level3", AdjacencyRule.King)]
    [InlineData("level4", AdjacencyRule.Manhattan2)]
    [InlineData("level5", AdjacencyRule.King)]
    [InlineData("level6", AdjacencyRule.Manhattan2)]
    [InlineData("level7", AdjacencyRule.Manhattan2)]
    [InlineData("level8", AdjacencyRule.King)]
    public void Level_AdjacencyMatchesAlpha(string levelId, AdjacencyRule expected)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expected, c.AdjacencyRule);
    }

    // --- Rival AI assignments ---

    [Theory]
    [InlineData("level1", AiType.Random)]
    [InlineData("level2", AiType.Random)]
    [InlineData("level3", AiType.Random)]
    [InlineData("level4", AiType.Random)]
    [InlineData("level5", AiType.Conservative)]
    [InlineData("level6", AiType.Conservative)]
    [InlineData("level7", AiType.Conservative)]
    [InlineData("level8", AiType.Conservative)]
    public void Level_RivalAiMatchesAlpha(string levelId, AiType expected)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expected, c.RivalAi);
    }

    // --- Initial rival reveal (alpha L1-L5 = 0; L6-L8 = 1) ---

    [Theory]
    [InlineData("level1", 0)]
    [InlineData("level2", 0)]
    [InlineData("level3", 0)]
    [InlineData("level4", 0)]
    [InlineData("level5", 0)]
    [InlineData("level6", 1)]
    [InlineData("level7", 1)]
    [InlineData("level8", 1)]
    public void Level_InitialRivalRevealMatchesAlpha(string levelId, int expected)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.Equal(expected, c.InitialRivalReveal);
    }

    // --- RivalNeverNobles (only L5 in our 1-8 range) ---

    [Fact]
    public void Level5_RivalNeverNobles()
    {
        Assert.True(LevelConfigs.Level5.RivalNeverNobles);
    }

    [Theory]
    [InlineData("level1")] [InlineData("level2")] [InlineData("level3")]
    [InlineData("level4")] [InlineData("level6")] [InlineData("level7")]
    [InlineData("level8")]
    public void OtherLevels_DoNotForbidNobles(string levelId)
    {
        var c = LevelConfigs.GetById(levelId)!;
        Assert.False(c.RivalNeverNobles);
    }

    // --- Reward flow per uponFinish (alpha M33 table) ---

    [Theory]
    [InlineData("level1", true,  false, false, false, "level2")]
    [InlineData("level2", true,  true,  false, false, "level3")]
    [InlineData("level3", false, false, true,  false, "level4")]
    [InlineData("level4", false, false, false, true,  "level5")]
    [InlineData("level5", true,  false, true,  false, "level6")]
    [InlineData("level6", true,  false, false, false, "level7")]
    [InlineData("level7", true,  true,  false, false, "level8")]
    [InlineData("level8", false, false, false, true,  "level9")]
    public void Level_UponFinishMatchesAlpha(
        string levelId, bool card, bool upgrade, bool equipment, bool shop, string nextId)
    {
        var c = LevelConfigs.GetById(levelId)!;
        var f = c.UponFinish!;
        Assert.Equal(card, f.CardReward);
        Assert.Equal(upgrade, f.UpgradeReward);
        Assert.Equal(equipment, f.EquipmentReward);
        Assert.Equal(shop, f.Shop);
        Assert.Equal(nextId, f.NextLevelId);
    }

    // --- L4 / L6 checkerboard hole patterns ---

    [Fact]
    public void Level4_Holes_FormCheckerboard()
    {
        // Alpha L4: 9×9 with all (col, row) where (col+row) is even removed.
        var holes = LevelConfigs.Level4.UnusedLocations.ToHashSet();
        for (var row = 0; row < 9; row++)
        for (var col = 0; col < 9; col++)
        {
            var pos = new Position(row, col);
            var shouldBeHole = (row + col) % 2 == 0;
            Assert.Equal(shouldBeHole, holes.Contains(pos));
        }
    }

    [Fact]
    public void Level6_Holes_OmitColumns0_3_6_8_OnRows1_2_4_5_7_8()
    {
        // Spot-check the L6 pattern: rows 0/3/6 have holes at even cols (0,2,4,6,8);
        // rows 1/2/4/5/7/8 have holes at odd cols (1,3,5,7).
        var holes = LevelConfigs.Level6.UnusedLocations.ToHashSet();
        Assert.Contains(new Position(0, 0), holes);
        Assert.Contains(new Position(0, 8), holes);
        Assert.Contains(new Position(3, 4), holes);
        Assert.Contains(new Position(6, 6), holes);
        Assert.Contains(new Position(2, 1), holes);
        Assert.Contains(new Position(8, 7), holes);
        Assert.DoesNotContain(new Position(0, 1), holes); // row 0, odd col
        Assert.DoesNotContain(new Position(1, 0), holes); // row 1, even col
    }

    // --- L5 / L7 diamond+cross hole layout ---

    [Theory]
    [InlineData("level5")]
    [InlineData("level7")]
    public void Levels5And7_ShareDiamondCrossHoles(string levelId)
    {
        var c = LevelConfigs.GetById(levelId)!;
        var holes = c.UnusedLocations.ToHashSet();
        Assert.Contains(new Position(1, 3), holes);  // top of diamond
        Assert.Contains(new Position(3, 3), holes);  // center
        Assert.Contains(new Position(5, 3), holes);  // bottom
        Assert.Contains(new Position(3, 1), holes);  // left
        Assert.Contains(new Position(3, 5), holes);  // right
        Assert.Equal(13, holes.Count);
    }

    // --- L8 specials: courtier + soirée + extraDirty ---

    [Fact]
    public void Level8_HasCourtierSoireeExtraDirty()
    {
        var c = LevelConfigs.Level8;
        Assert.Equal(3, c.SpecialTiles.Count);
        Assert.Contains(c.SpecialTiles, s => s.Type == SpecialTileType.Courtier && s.Count == 1
                                             && s.Strategy == PlacementStrategy.NonMine);
        Assert.Contains(c.SpecialTiles, s => s.Type == SpecialTileType.Soiree && s.Count == 1
                                             && s.Strategy == PlacementStrategy.Empty);
        Assert.Contains(c.SpecialTiles, s => s.Type == SpecialTileType.ExtraDirty && s.Count == 4
                                             && s.Strategy == PlacementStrategy.Random);
    }

    [Fact]
    public void Level8_Board_PlacesCourtierAndSoiree()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level8, new Random(42));
        var courtierCount = board.Tiles.Count(t => t.Specials.HasFlag(SpecialTileType.Courtier));
        var soireeCount = board.Tiles.Count(t => t.Specials.HasFlag(SpecialTileType.Soiree));
        Assert.Equal(1, courtierCount);
        Assert.Equal(1, soireeCount);

        // The soiree should sit on the [3,3] hole (Empty placement).
        Assert.True(board.GetTile(new Position(3, 3)).Specials.HasFlag(SpecialTileType.Soiree));
    }

    // --- ExtraDirty placement strategy switch ---

    [Theory]
    [InlineData("level2", 1)]
    [InlineData("level3", 3)]
    [InlineData("level4", 3)]
    [InlineData("level5", 6)]
    [InlineData("level6", 5)]
    [InlineData("level7", 4)]
    [InlineData("level8", 4)]
    public void Level_ExtraDirtyCountMatchesAlpha(string levelId, int expectedCount)
    {
        var c = LevelConfigs.GetById(levelId)!;
        var dirty = c.SpecialTiles.FirstOrDefault(s => s.Type == SpecialTileType.ExtraDirty);
        Assert.NotNull(dirty);
        Assert.Equal(expectedCount, dirty!.Count);
    }
}
