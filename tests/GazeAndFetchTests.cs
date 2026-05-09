using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M43: Directional cards Gaze (4 variants) and Fetch (4 variants).
/// Line-traversal mechanics, sanctum/inner blocking, reachability respected.
/// </summary>
public class GazeAndFetchTests
{
    /// <summary>
    /// Builds a 1×N or N×1 strip board for line-traversal tests. Owners listed in
    /// order from row=0,col=0 across.
    /// </summary>
    private static GameState BuildStripBoard(IReadOnlyList<TileOwner> owners, bool horizontal = true)
    {
        var tiles = new List<Tile>();
        if (horizontal)
        {
            for (var col = 0; col < owners.Count; col++)
                tiles.Add(new Tile { Position = new Position(0, col), Owner = owners[col] });
            return MakeState(new Board { Width = owners.Count, Height = 1, Tiles = tiles });
        }
        for (var row = 0; row < owners.Count; row++)
            tiles.Add(new Tile { Position = new Position(row, 0), Owner = owners[row] });
        return MakeState(new Board { Width = 1, Height = owners.Count, Tiles = tiles });
    }

    private static GameState MakeState(Board board) => new()
    {
        Board = board,
        CurrentLevelId = "level1",
        Hand = new List<Card>(),
        DrawPile = CardDefinitions.CreateStarterDeck(),
        Spoons = 3,
        MaxSpoons = 3,
        CurrentPlayer = PlayerType.Player
    };

    // ---------- Gaze: 4 directions, base behavior ----------

    [Theory]
    [InlineData(LineDirection.Right, "→")]
    [InlineData(LineDirection.Left, "←")]
    [InlineData(LineDirection.Up, "↑")]
    [InlineData(LineDirection.Down, "↓")]
    public void Gaze_FindsFirstRivalInEachDirection(LineDirection direction, string arrow)
    {
        // Build a 5-tile strip (horizontal or vertical depending on direction)
        // with a Rival at the appropriate end.
        // For Right: origin at col=0, rival at col=4
        // For Left: origin at col=4, rival at col=0
        // etc.
        var owners = new[] { TileOwner.Player, TileOwner.Neutral, TileOwner.Player, TileOwner.Neutral, TileOwner.Rival };
        bool horizontal = direction == LineDirection.Left || direction == LineDirection.Right;

        var state = BuildStripBoard(owners, horizontal);
        Position origin, rivalPos;
        if (direction == LineDirection.Right)
        {
            origin = new Position(0, 0);
            rivalPos = new Position(0, 4);
        }
        else if (direction == LineDirection.Left)
        {
            // Reverse: rival is at col 0, scan from col 4 going left
            state = BuildStripBoard(owners.Reverse().ToList(), horizontal: true);
            origin = new Position(0, 4);
            rivalPos = new Position(0, 0);
        }
        else if (direction == LineDirection.Down)
        {
            state = BuildStripBoard(owners, horizontal: false);
            origin = new Position(0, 0);
            rivalPos = new Position(4, 0);
        }
        else // Up
        {
            state = BuildStripBoard(owners.Reverse().ToList(), horizontal: false);
            origin = new Position(4, 0);
            rivalPos = new Position(0, 0);
        }

        var card = new Card
        {
            Id = "g1", Name = $"Gaze {arrow}", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = direction
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { origin }, card);

        var rivalAnnotation = newState.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(rivalAnnotation);
        Assert.Single(rivalAnnotation);
        Assert.Contains(TileOwner.Rival, rivalAnnotation);
    }

    [Fact]
    public void Gaze_AnnotatesCheckedTilesAsNotRival()
    {
        // Origin at (0,0), Player, Neutral, Rival, Player. Gaze right.
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Neutral, TileOwner.Player, TileOwner.Rival, TileOwner.Player
        });
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        // Tiles 0, 1, 2 (before rival at 3) are annotated as NOT rival
        for (var col = 0; col <= 2; col++)
        {
            var ann = newState.Board.GetTile(new Position(0, col)).Annotations.OwnerSubset;
            Assert.NotNull(ann);
            Assert.DoesNotContain(TileOwner.Rival, ann);
            Assert.Contains(TileOwner.Player, ann);
        }
    }

    [Fact]
    public void Gaze_HandlesNoRivalInDirection_AnnotatesAllAsNotRival()
    {
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Neutral, TileOwner.Player
        });
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        // All 3 tiles annotated as not-rival
        for (var col = 0; col < 3; col++)
        {
            var ann = newState.Board.GetTile(new Position(0, col)).Annotations.OwnerSubset;
            Assert.NotNull(ann);
            Assert.DoesNotContain(TileOwner.Rival, ann);
        }
    }

    [Fact]
    public void Gaze_SkipsRevealedTiles_LineContinues()
    {
        // Strip: Player-revealed, Neutral-unrevealed, Rival.
        // Gaze right from (0,0): origin is revealed → skipped.
        // Continue → (0,1) Neutral → annotated not-rival.
        // Continue → (0,2) Rival → annotated {Rival}.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player,
                IsRevealed = true, RevealedBy = PlayerType.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival }
        };
        var state = MakeState(new Board { Width = 3, Height = 1, Tiles = tiles });
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        var rivalAnn = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset;
        Assert.NotNull(rivalAnn);
        Assert.Contains(TileOwner.Rival, rivalAnn);
    }

    [Fact]
    public void Gaze_BlockedByUnrevealedSanctum()
    {
        // Strip: Player, Sanctum (unrevealed), Rival.
        // Gaze right from (0,0): line stops at sanctum; rival behind it not annotated.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Sanctum },
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival }
        };
        var state = MakeState(new Board { Width = 3, Height = 1, Tiles = tiles });
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        // Origin annotated; rival behind sanctum NOT annotated
        Assert.NotNull(newState.Board.GetTile(new Position(0, 0)).Annotations.OwnerSubset);
        Assert.Null(newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset);
    }

    [Fact]
    public void Gaze_PassesThroughRevealedSanctum()
    {
        // Strip: Player, Sanctum (revealed), Rival.
        // Gaze right: line passes through revealed sanctum, finds rival.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Sanctum,
                IsRevealed = true, RevealedBy = PlayerType.Player },
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival }
        };
        var state = MakeState(new Board { Width = 3, Height = 1, Tiles = tiles });
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        var rivalAnn = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset;
        Assert.NotNull(rivalAnn);
        Assert.Contains(TileOwner.Rival, rivalAnn);
    }

    // ---------- Fetch: most-common owner reveals ----------

    [Fact]
    public void Fetch_RevealsMostCommonOwnerType()
    {
        // Strip: Player, Player, Rival, Player, Neutral. Fetch right from (0,0).
        // Most common = Player (3 tiles). All players revealed.
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Player, TileOwner.Rival, TileOwner.Player, TileOwner.Neutral
        });
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card);

        // Cols 0, 1, 3 are Player → all revealed
        Assert.True(newState.Board.GetTile(new Position(0, 0)).IsRevealed);
        Assert.True(newState.Board.GetTile(new Position(0, 1)).IsRevealed);
        Assert.True(newState.Board.GetTile(new Position(0, 3)).IsRevealed);
        // Cols 2 (Rival) and 4 (Neutral) NOT revealed
        Assert.False(newState.Board.GetTile(new Position(0, 2)).IsRevealed);
        Assert.False(newState.Board.GetTile(new Position(0, 4)).IsRevealed);
    }

    [Fact]
    public void Fetch_TieBrokenBySafetyOrder_PlayerOverNeutral()
    {
        // Strip: 1 Player, 1 Neutral. Tie → Player wins (safer).
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Neutral
        });
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card);

        Assert.True(newState.Board.GetTile(new Position(0, 0)).IsRevealed);
        Assert.False(newState.Board.GetTile(new Position(0, 1)).IsRevealed);
    }

    [Fact]
    public void Fetch_AnnotatesNonMajorityCheckedTiles()
    {
        // Strip: Player, Player, Rival. Most common = Player (2). Reveal both players.
        // Rival NOT revealed; should be annotated as "anything but player".
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Player, TileOwner.Rival
        });
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card);

        var ann = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset;
        Assert.NotNull(ann);
        Assert.DoesNotContain(TileOwner.Player, ann);
        Assert.Contains(TileOwner.Rival, ann);
    }

    [Theory]
    [InlineData(LineDirection.Right)]
    [InlineData(LineDirection.Left)]
    [InlineData(LineDirection.Up)]
    [InlineData(LineDirection.Down)]
    public void Fetch_WorksInEachDirection(LineDirection direction)
    {
        // Build a strip with all Player tiles. Fetch from one end → all revealed.
        bool horizontal = direction == LineDirection.Left || direction == LineDirection.Right;
        var state = BuildStripBoard(new[]
        {
            TileOwner.Player, TileOwner.Player, TileOwner.Player
        }, horizontal);

        Position origin = direction switch
        {
            LineDirection.Right => new Position(0, 0),
            LineDirection.Left => new Position(0, 2),
            LineDirection.Down => new Position(0, 0),
            LineDirection.Up => new Position(2, 0),
            _ => new Position(0, 0)
        };

        var card = new Card
        {
            Id = "f1", Name = $"Fetch", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = direction
        };
        var newState = CardEffectSystem.ExecuteFetch(state, new[] { origin }, card);

        // All 3 player tiles revealed
        var revealed = newState.Board.Tiles.Count(t => t.IsRevealed);
        Assert.Equal(3, revealed);
    }

    [Fact]
    public void Fetch_BlockedByUnrevealedSanctum()
    {
        // Strip: Player, Sanctum (unrevealed), Player. Fetch right from (0,0).
        // Line stops at sanctum → only the origin Player is checked → revealed.
        // (0,2) Player behind sanctum is NOT revealed.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Sanctum },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player }
        };
        var state = MakeState(new Board { Width = 3, Height = 1, Tiles = tiles });
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card);

        Assert.True(newState.Board.GetTile(new Position(0, 0)).IsRevealed);
        Assert.False(newState.Board.GetTile(new Position(0, 2)).IsRevealed);
    }

    // ---------- Reward pool ----------

    [Fact]
    public void RewardPool_IncludesAllDirectionalCards()
    {
        var pool = CardDefinitions.CreateRewardPool();
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Gaze && c.Direction == LineDirection.Up);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Gaze && c.Direction == LineDirection.Down);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Gaze && c.Direction == LineDirection.Left);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Gaze && c.Direction == LineDirection.Right);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Fetch && c.Direction == LineDirection.Up);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Fetch && c.Direction == LineDirection.Down);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Fetch && c.Direction == LineDirection.Left);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Fetch && c.Direction == LineDirection.Right);
    }
}
