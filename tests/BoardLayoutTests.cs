using Maidsweeper.Core.Models;

namespace Maidsweeper.Tests;

/// <summary>
/// Verifies every configured level fits within the BoardLayout's reserved area.
/// If you add a larger level and these fail, either shrink the level or bump
/// BoardLayout.MaxGrid* (and update Main.tscn's BoardMargin custom_minimum_size to match).
/// </summary>
public class BoardLayoutTests
{
    [Theory]
    [InlineData("level1")]
    [InlineData("level2")]
    [InlineData("level3")]
    [InlineData("level4")]
    [InlineData("level5")]
    [InlineData("level6")]
    [InlineData("level7")]
    [InlineData("level8")]
    public void Level_FitsWithinBoardArea(string levelId)
    {
        var config = LevelConfigs.GetById(levelId);
        Assert.NotNull(config);

        var widthPx = BoardLayout.RequiredWidth(config!.Width);
        var heightPx = BoardLayout.RequiredHeight(config.Height);

        Assert.True(widthPx <= BoardLayout.MaxGridWidthPx,
            $"{levelId}: grid width {widthPx}px exceeds reserved {BoardLayout.MaxGridWidthPx}px " +
            $"({config.Width} cols)");
        Assert.True(heightPx <= BoardLayout.MaxGridHeightPx,
            $"{levelId}: grid height {heightPx}px exceeds reserved {BoardLayout.MaxGridHeightPx}px " +
            $"({config.Height} rows)");
    }

    [Fact]
    public void RequiredWidth_AccountsForGapsBetweenTiles()
    {
        // 1 tile = TileSize (no gap)
        Assert.Equal(BoardLayout.TileSize, BoardLayout.RequiredWidth(1));
        // 3 tiles = 3*TileSize + 2*TileGap
        Assert.Equal(3 * BoardLayout.TileSize + 2 * BoardLayout.TileGap,
            BoardLayout.RequiredWidth(3));
    }

    [Fact]
    public void RequiredHeight_AccountsForGapsBetweenTiles()
    {
        Assert.Equal(BoardLayout.TileSize, BoardLayout.RequiredHeight(1));
        Assert.Equal(5 * BoardLayout.TileSize + 4 * BoardLayout.TileGap,
            BoardLayout.RequiredHeight(5));
    }

    [Fact]
    public void RequiredDimensions_ZeroForEmpty()
    {
        Assert.Equal(0, BoardLayout.RequiredWidth(0));
        Assert.Equal(0, BoardLayout.RequiredHeight(0));
    }
}
