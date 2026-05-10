using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M51: validates the pure-C# data inputs that the Stage 5 UI reads — primarily
/// <see cref="BoardSystem.CanReachInnerTile"/>, which drives the inner-tile
/// padlock overlay in TileView. The Godot rendering itself isn't unit-tested,
/// but the data flowing into the UI is.
/// </summary>
public class Stage5UiDataTests
{
    /// <summary>
    /// Builds a tiny 3×3 board with one sanctum at the center and one
    /// inner tile next to it.  The remaining tiles are simple owners.
    /// </summary>
    private static GameState BuildSanctumBoard(bool sanctumRevealed)
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
        {
            var pos = new Position(row, col);
            Tile tile;
            if (row == 1 && col == 1)
            {
                tile = new Tile
                {
                    Position = pos,
                    Owner = TileOwner.Neutral,
                    Specials = SpecialTileType.Sanctum,
                    IsRevealed = sanctumRevealed,
                    RevealedBy = sanctumRevealed ? PlayerType.Player : null
                };
            }
            else if (row == 1 && col == 2)
            {
                tile = new Tile
                {
                    Position = pos,
                    Owner = TileOwner.Rival,
                    Specials = SpecialTileType.InnerTile
                };
            }
            else
            {
                tile = new Tile { Position = pos, Owner = TileOwner.Player };
            }
            tiles.Add(tile);
        }
        var board = new Board { Width = 3, Height = 3, Tiles = tiles };
        return new GameState { Board = board, CurrentLevelId = "level16" };
    }

    [Fact]
    public void InnerTile_NotReachable_WhenAdjacentSanctumUnrevealed()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        Assert.False(BoardSystem.CanReachInnerTile(state.Board, new Position(1, 2)));
    }

    [Fact]
    public void InnerTile_Reachable_WhenAdjacentSanctumRevealed()
    {
        var state = BuildSanctumBoard(sanctumRevealed: true);
        Assert.True(BoardSystem.CanReachInnerTile(state.Board, new Position(1, 2)));
    }

    [Fact]
    public void NonInnerTile_AlwaysReachable()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        Assert.True(BoardSystem.CanReachInnerTile(state.Board, new Position(0, 0)));
    }

    /// <summary>
    /// HUD parses "level1".."level21" → 1..21. Verifies the parse the HUD does
    /// matches what's now in <see cref="LevelConfigs.GetById"/>.
    /// </summary>
    [Theory]
    [InlineData("level1", 1)]
    [InlineData("level9", 9)]
    [InlineData("level21", 21)]
    public void HudFloorParse_RecognizesAllImplementedLevels(string levelId, int expected)
    {
        Assert.NotNull(LevelConfigs.GetById(levelId));
        // Same parse the HUD does:
        Assert.StartsWith("level", levelId);
        Assert.True(int.TryParse(levelId.Substring(5), out var num));
        Assert.Equal(expected, num);
    }
}
