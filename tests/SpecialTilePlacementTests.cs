using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M38: special-tile placement strategies (Owners / Random / NonMine / Empty / Explicit).
/// </summary>
public class SpecialTilePlacementTests
{
    /// <summary>
    /// Counts the number of tiles whose Specials includes the given flag.
    /// </summary>
    private static int CountWithSpecial(Board board, SpecialTileType flag) =>
        board.Tiles.Count(t => t.Specials.HasFlag(flag));

    // ---------- Owners (existing behavior) ----------

    [Fact]
    public void OwnersStrategy_RestrictsToEligibleOwners()
    {
        var config = new LevelConfig
        {
            Width = 4, Height = 3,
            PlayerCount = 4, RivalCount = 4, NeutralCount = 4, NobleCount = 0,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.ExtraDirty,
                    Count = 4,
                    Strategy = PlacementStrategy.Owners,
                    EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        Assert.Equal(4, CountWithSpecial(board, SpecialTileType.ExtraDirty));
        // Every dirty tile must be Player or Neutral
        foreach (var t in board.Tiles.Where(t => t.IsDirty))
        {
            Assert.Contains(t.Owner, new[] { TileOwner.Player, TileOwner.Neutral });
        }
    }

    // ---------- Random ----------

    [Fact]
    public void RandomStrategy_PlacesOnAnyUsableTile()
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 2,
            PlayerCount = 2, RivalCount = 2, NeutralCount = 1, NobleCount = 1,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.ExtraDirty,
                    Count = 3,
                    Strategy = PlacementStrategy.Random
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        Assert.Equal(3, CountWithSpecial(board, SpecialTileType.ExtraDirty));
        // No restriction on owner — could land on any of the 4 owner types
        foreach (var t in board.Tiles.Where(t => t.IsDirty))
        {
            Assert.True(board.IsUsablePosition(t.Position));
        }
    }

    // ---------- NonMine ----------

    [Fact]
    public void NonMineStrategy_NeverPlacesOnNobles()
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 2, NeutralCount = 2, NobleCount = 2,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Courtier,
                    Count = 5,
                    Strategy = PlacementStrategy.NonMine
                }
            ]
        };

        for (var seed = 0; seed < 10; seed++)
        {
            var board = BoardSystem.CreateBoard(config, new Random(seed));
            Assert.Equal(5, CountWithSpecial(board, SpecialTileType.Courtier));
            foreach (var t in board.Tiles.Where(t => t.IsCourtier))
            {
                Assert.NotEqual(TileOwner.Noble, t.Owner);
            }
        }
    }

    // ---------- Empty ----------

    [Fact]
    public void EmptyStrategy_OnlyUsesUnusedPositions()
    {
        var unused = new List<Position> { new(0, 0), new(0, 1), new(2, 2) };
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 2, NeutralCount = 1, NobleCount = 0,
            UnusedLocations = unused,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Soiree,
                    Count = 2,
                    Strategy = PlacementStrategy.Empty
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        Assert.Equal(2, CountWithSpecial(board, SpecialTileType.Soiree));
        foreach (var t in board.Tiles.Where(t => t.IsSoiree))
        {
            Assert.Contains(t.Position, unused);
        }
    }

    [Fact]
    public void EmptyStrategy_DoesNotAffectTileCountTotals()
    {
        // Total usable = 9 - 4 unused = 5; tile counts must sum to 5 even with 2 soirées on unused positions.
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 2, RivalCount = 2, NeutralCount = 1, NobleCount = 0,
            UnusedLocations = [new(0, 0), new(0, 1), new(2, 2), new(1, 1)],
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Soiree,
                    Count = 2,
                    Strategy = PlacementStrategy.Empty
                }
            ]
        };

        // CreateBoard validates expectedTiles == usableCount internally; this should not throw.
        var board = BoardSystem.CreateBoard(config, new Random(42));

        Assert.Equal(2, CountWithSpecial(board, SpecialTileType.Soiree));
        Assert.Equal(5, board.Tiles.Count(t => board.IsUsablePosition(t.Position)));
    }

    // ---------- Explicit ----------

    [Fact]
    public void ExplicitStrategy_HonorsExactPositions()
    {
        var spots = new List<Position> { new(0, 0), new(1, 2) };
        var config = new LevelConfig
        {
            Width = 3, Height = 2,
            PlayerCount = 2, RivalCount = 2, NeutralCount = 2, NobleCount = 0,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Sanctum,
                    Count = 2, // ignored when Strategy = Explicit
                    Strategy = PlacementStrategy.Explicit,
                    ExplicitPositions = spots
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        foreach (var pos in spots)
        {
            Assert.True(board.GetTile(pos).IsSanctum,
                $"Expected sanctum at {pos}");
        }
        // No other tile should have the sanctum flag
        Assert.Equal(2, CountWithSpecial(board, SpecialTileType.Sanctum));
    }

    // ---------- Combinations / flags ----------

    [Fact]
    public void MultipleSpecialFlagsCanCoexist()
    {
        // Place ExtraDirty via Owners on player/neutral, then place Courtier via Random
        // — overlap is allowed because they're independent flags.
        var config = new LevelConfig
        {
            Width = 3, Height = 2,
            PlayerCount = 3, RivalCount = 2, NeutralCount = 1, NobleCount = 0,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.ExtraDirty,
                    Count = 4,
                    Strategy = PlacementStrategy.Owners,
                    EligibleOwners = [TileOwner.Player, TileOwner.Neutral, TileOwner.Rival]
                },
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Courtier,
                    Count = 3,
                    Strategy = PlacementStrategy.Random
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        Assert.Equal(4, CountWithSpecial(board, SpecialTileType.ExtraDirty));
        Assert.Equal(3, CountWithSpecial(board, SpecialTileType.Courtier));

        // At least one tile should have both flags (4 + 3 = 7 placements over 6 tiles → pigeonhole)
        var bothFlags = board.Tiles
            .Count(t => t.IsDirty && t.IsCourtier);
        Assert.True(bothFlags >= 1,
            "expected at least one tile with both ExtraDirty and Courtier (4+3 placements over 6 tiles)");
    }

    [Fact]
    public void Tile_WithSpecialAndWithoutSpecial_ManageFlagsImmutably()
    {
        var tile = new Tile { Position = new Position(0, 0), Owner = TileOwner.Player };

        var dirty = tile.WithSpecial(SpecialTileType.ExtraDirty);
        Assert.True(dirty.IsDirty);
        Assert.False(tile.IsDirty); // original unchanged

        var dirtyAndCourtier = dirty.WithSpecial(SpecialTileType.Courtier);
        Assert.True(dirtyAndCourtier.IsDirty);
        Assert.True(dirtyAndCourtier.IsCourtier);

        var clean = dirtyAndCourtier.WithoutSpecial(SpecialTileType.ExtraDirty);
        Assert.False(clean.IsDirty);
        Assert.True(clean.IsCourtier); // courtier still present
    }
}
