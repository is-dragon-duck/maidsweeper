using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M39: Courtiers (move when interacted with) + Soirées (spawn courtiers each rival turn).
/// </summary>
public class CourtierAndSoireeTests
{
    /// <summary>
    /// Builds a manual 3×3 board where (1,1) is a courtier with a predetermined target,
    /// or no courtier if `placeCourtier` is false.
    /// </summary>
    private static GameState BuildCourtierBoard(Position courtierPos, Position? moveTarget,
        bool isPlayerTurn = true)
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var pos = new Position(row, col);
                var owner = TileOwner.Player;
                if (pos == courtierPos)
                {
                    tiles.Add(new Tile
                    {
                        Position = pos,
                        Owner = TileOwner.Player,
                        Specials = SpecialTileType.Courtier,
                        CourtierMoveTarget = moveTarget
                    });
                }
                else
                {
                    tiles.Add(new Tile { Position = pos, Owner = owner });
                }
            }
        }
        var board = new Board { Width = 3, Height = 3, Tiles = tiles };
        var deck = CardDefinitions.CreateStarterDeck();
        return new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = isPlayerTurn ? PlayerType.Player : PlayerType.Rival,
            TurnNumber = 1,
            // Pre-populate intent so the rival turn doesn't fall back to its random branch
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 1 }
        };
    }

    // ---------- CleanCourtier basic mechanics ----------

    [Fact]
    public void CleanCourtier_MovesCourtierToTarget_AndAssignsNewTarget()
    {
        var origin = new Position(1, 1);
        var target = new Position(1, 2);
        var state = BuildCourtierBoard(origin, target);

        var newBoard = BoardSystem.CleanCourtier(state.Board, origin, new Random(7));

        Assert.False(newBoard.GetTile(origin).IsCourtier);
        Assert.Null(newBoard.GetTile(origin).CourtierMoveTarget);
        Assert.True(newBoard.GetTile(target).IsCourtier);
        // New move target must exist (target has eligible neighbors on a 3x3)
        Assert.NotNull(newBoard.GetTile(target).CourtierMoveTarget);
    }

    [Fact]
    public void CleanCourtier_Collision_IncomingDisappears()
    {
        // Build a board where (0,0) is a courtier whose target is (0,1), which already has a courtier.
        var tiles = new List<Tile>();
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                var pos = new Position(row, col);
                if (pos == new Position(0, 0))
                {
                    tiles.Add(new Tile
                    {
                        Position = pos, Owner = TileOwner.Player,
                        Specials = SpecialTileType.Courtier,
                        CourtierMoveTarget = new Position(0, 1)
                    });
                }
                else if (pos == new Position(0, 1))
                {
                    tiles.Add(new Tile
                    {
                        Position = pos, Owner = TileOwner.Player,
                        Specials = SpecialTileType.Courtier,
                        CourtierMoveTarget = new Position(1, 1)
                    });
                }
                else
                {
                    tiles.Add(new Tile { Position = pos, Owner = TileOwner.Player });
                }
            }
        }
        var board = new Board { Width = 2, Height = 2, Tiles = tiles };

        var newBoard = BoardSystem.CleanCourtier(board, new Position(0, 0), new Random(7));

        // Origin lost its courtier (always)
        Assert.False(newBoard.GetTile(new Position(0, 0)).IsCourtier);
        // Target retains its existing courtier (incoming merges/disappears)
        Assert.True(newBoard.GetTile(new Position(0, 1)).IsCourtier);
        // Target's MoveTarget unchanged
        Assert.Equal(new Position(1, 1), newBoard.GetTile(new Position(0, 1)).CourtierMoveTarget);
    }

    [Fact]
    public void CleanCourtier_NoOpOnTileWithoutCourtier()
    {
        var state = BuildCourtierBoard(new Position(1, 1), new Position(1, 2));
        var nonCourtier = new Position(0, 0);

        var newBoard = BoardSystem.CleanCourtier(state.Board, nonCourtier, new Random(7));

        Assert.Equal(state.Board, newBoard);
    }

    // ---------- Click → courtier movement + turn-end ----------

    [Fact]
    public void ClickingCourtier_MovesCourtier_AndEndsTurn()
    {
        var origin = new Position(1, 1);
        var target = new Position(1, 2);
        var state = BuildCourtierBoard(origin, target);

        var result = GameRunner.ProcessReveal(state, origin, new Random(7));

        Assert.True(result.TurnEnded);
        Assert.False(result.State.Board.GetTile(origin).IsCourtier);
        // Original tile NOT revealed (cleaning is not a reveal)
        Assert.False(result.State.Board.GetTile(origin).IsRevealed);
        // Courtier moved to target
        Assert.True(result.State.Board.GetTile(target).IsCourtier);
    }

    // ---------- Card targeting on courtier ----------

    [Fact]
    public void Spritz_OnCourtier_MovesCourtier_AndAnnotates_WithoutEndingTurn()
    {
        var origin = new Position(1, 1);
        var target = new Position(1, 2);
        var state = BuildCourtierBoard(origin, target);
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);

        var result = GameRunner.ProcessCardPlay(state, spritz, new[] { origin }, new Random(7));

        Assert.False(result.TurnEnded);
        // Courtier moved off origin
        Assert.False(result.State.Board.GetTile(origin).IsCourtier);
        Assert.True(result.State.Board.GetTile(target).IsCourtier);
        // Origin was annotated by Spritz (player owner → safe annotation)
        var annotation = result.State.Board.GetTile(origin).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Contains(TileOwner.Player, annotation);
    }

    [Fact]
    public void Eavesdrop_OnCourtier_DoesNotMoveCourtier()
    {
        var origin = new Position(1, 1);
        var target = new Position(1, 2);
        var state = BuildCourtierBoard(origin, target);
        // Replace one of the hand cards with Eavesdrop so we can play it
        var eavesdrop = CardDefinitions.Eavesdrop with { Id = "ed1" };
        state = state with { Hand = new List<Card> { eavesdrop }, Spoons = 3 };

        var result = GameRunner.ProcessCardPlay(state, eavesdrop, new[] { origin }, new Random(7));

        // Courtier still on origin
        Assert.True(result.State.Board.GetTile(origin).IsCourtier);
        Assert.False(result.State.Board.GetTile(target).IsCourtier);
    }

    [Fact]
    public void Tingle_DoesNotMoveCourtiers()
    {
        // Build a board where Tingle MUST land on the rival courtier (only rival on board)
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                Specials = SpecialTileType.Courtier,
                CourtierMoveTarget = new Position(0, 1) },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var tingle = CardDefinitions.Tingle with { Id = "t1" };
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Hand = new List<Card> { tingle },
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var result = GameRunner.ProcessCardPlay(state, tingle, null, new Random(7));

        // Courtier still in place
        Assert.True(result.State.Board.GetTile(new Position(0, 0)).IsCourtier);
    }

    // ---------- Soirée spawning ----------

    [Fact]
    public void SoireeSpawn_PlacesCourtierOnAdjacentTile()
    {
        // 2x2 board with soirée at (0,0); the other 3 tiles are eligible spawn destinations.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Soiree },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player },
            new() { Position = new Position(1, 0), Owner = TileOwner.Player },
            new() { Position = new Position(1, 1), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 2, Height = 2, Tiles = tiles };

        var newBoard = BoardSystem.SpawnCourtiersFromSoirees(board, new Random(7));

        var courtierCount = newBoard.Tiles.Count(t => t.IsCourtier);
        Assert.Equal(1, courtierCount);
        var courtier = newBoard.Tiles.First(t => t.IsCourtier);
        Assert.NotEqual(new Position(0, 0), courtier.Position); // not on the soirée itself
    }

    [Fact]
    public void SoireeSpawn_NoOpWhenAllAdjacentAlreadyCourtier()
    {
        // 2x2 board: soirée at (0,0); all neighbors already have courtier flag.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Soiree },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player,
                Specials = SpecialTileType.Courtier,
                CourtierMoveTarget = new Position(1, 1) },
            new() { Position = new Position(1, 0), Owner = TileOwner.Player,
                Specials = SpecialTileType.Courtier,
                CourtierMoveTarget = new Position(1, 1) },
            new() { Position = new Position(1, 1), Owner = TileOwner.Player,
                Specials = SpecialTileType.Courtier,
                CourtierMoveTarget = null }
        };
        var board = new Board { Width = 2, Height = 2, Tiles = tiles };

        var newBoard = BoardSystem.SpawnCourtiersFromSoirees(board, new Random(7));

        // Same 3 courtier tiles, no new ones
        Assert.Equal(3, newBoard.Tiles.Count(t => t.IsCourtier));
    }

    [Fact]
    public void MultipleSoireesSpawnIndependently()
    {
        // 1x4 board: soirées at (0,0) and (0,3); neighbors (0,1) and (0,2) are eligible.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Soiree },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player },
            new() { Position = new Position(0, 3), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Soiree }
        };
        var board = new Board { Width = 4, Height = 1, Tiles = tiles };

        var newBoard = BoardSystem.SpawnCourtiersFromSoirees(board, new Random(7));

        // Both soirées spawn → both inner tiles end up with courtiers
        Assert.True(newBoard.GetTile(new Position(0, 1)).IsCourtier);
        Assert.True(newBoard.GetTile(new Position(0, 2)).IsCourtier);
    }

    [Fact]
    public void RivalTurnTriggersSoireeSpawning()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Soiree },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player },
            new() { Position = new Position(1, 0), Owner = TileOwner.Player },
            new() { Position = new Position(1, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 2, Tiles = tiles };
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            CurrentPlayer = PlayerType.Rival
        };

        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(7));

        // Soirée spawned at least 1 courtier on a neighbor
        var courtierCount = newState.Board.Tiles.Count(t => t.IsCourtier);
        Assert.True(courtierCount >= 1, "soirée should have spawned at least 1 courtier on rival turn");
    }

    // ---------- Initial placement: CourtierMoveTarget assigned ----------

    [Fact]
    public void CreateBoard_AssignsMoveTargetsToInitialCourtiers()
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 4, RivalCount = 2, NeutralCount = 2, NobleCount = 1,
            SpecialTiles =
            [
                new SpecialTileConfig
                {
                    Type = SpecialTileType.Courtier,
                    Count = 2,
                    Strategy = PlacementStrategy.NonMine
                }
            ]
        };

        var board = BoardSystem.CreateBoard(config, new Random(42));

        var courtiers = board.Tiles.Where(t => t.IsCourtier).ToList();
        Assert.Equal(2, courtiers.Count);
        // Each courtier has a valid MoveTarget (3x3 board has eligible neighbors for any tile)
        foreach (var c in courtiers)
        {
            Assert.NotNull(c.CourtierMoveTarget);
        }
    }
}
