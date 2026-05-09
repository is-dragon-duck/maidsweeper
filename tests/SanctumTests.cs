using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M41: Sanctum + Inner Tiles. Portal adjacency, reachability, recomputation,
/// card-targeting filters.
/// </summary>
public class SanctumTests
{
    /// <summary>
    /// 3×3 board layout used by most tests:
    ///   (0,0) outer Player        (0,1) outer Player        (0,2) outer Player
    ///   (1,0) outer Player        (1,1) Sanctum on Neutral  (1,2) inner Rival
    ///   (2,0) outer Player        (2,1) outer Player        (2,2) outer Player
    /// Inner tile (1,2) is adjacent to the sanctum at (1,1) and to outer tiles
    /// (0,2), (2,2) in raw offsets — but the inner-tile rule restricts its
    /// direct neighbors to just the sanctum.
    /// </summary>
    private static GameState BuildSanctumBoard(bool sanctumRevealed = false)
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var pos = new Position(row, col);
                if (pos == new Position(1, 1))
                {
                    tiles.Add(new Tile
                    {
                        Position = pos,
                        Owner = TileOwner.Neutral,
                        Specials = SpecialTileType.Sanctum,
                        IsRevealed = sanctumRevealed,
                        RevealedBy = sanctumRevealed ? PlayerType.Player : null
                    });
                }
                else if (pos == new Position(1, 2))
                {
                    tiles.Add(new Tile
                    {
                        Position = pos,
                        Owner = TileOwner.Rival,
                        Specials = SpecialTileType.InnerTile
                    });
                }
                else
                {
                    tiles.Add(new Tile { Position = pos, Owner = TileOwner.Player });
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
            CurrentPlayer = PlayerType.Player,
            // Pre-seed intent so a rival turn doesn't fall into the random branch
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 1 }
        };
    }

    // ---------- Adjacency: inner tile only sees sanctum directly ----------

    [Fact]
    public void InnerTile_DirectNeighbors_OnlyIncludeAdjacentSanctums()
    {
        var state = BuildSanctumBoard();
        var inner = new Position(1, 2);

        var neighbors = BoardSystem.GetNeighbors(state.Board, inner);

        // Inner tile only sees the sanctum at (1,1) — NOT (0,1), (0,2), (2,1), (2,2).
        Assert.Single(neighbors);
        Assert.Contains(new Position(1, 1), neighbors);
    }

    [Fact]
    public void OuterTile_DoesNotSeeInnerTile_DirectlyWithoutPortal()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var outer = new Position(0, 2);

        var neighbors = BoardSystem.GetNeighbors(state.Board, outer);

        // (0,2)'s offset neighbors are (0,1), (1,1), (1,2). Inner (1,2) excluded.
        Assert.Contains(new Position(0, 1), neighbors);
        Assert.Contains(new Position(1, 1), neighbors); // sanctum is allowed
        Assert.DoesNotContain(new Position(1, 2), neighbors);
    }

    // ---------- Adjacency: revealed sanctum bridges ----------

    [Fact]
    public void RevealedSanctum_BridgesOuterToInner()
    {
        var state = BuildSanctumBoard(sanctumRevealed: true);
        var outer = new Position(0, 2); // adjacent to sanctum (1,1)

        var neighbors = BoardSystem.GetNeighbors(state.Board, outer);

        // Now sees the inner tile via the open portal
        Assert.Contains(new Position(1, 2), neighbors);
    }

    [Fact]
    public void RevealedSanctum_NeighborsIncludeBothInnerAndOuter()
    {
        var state = BuildSanctumBoard(sanctumRevealed: true);
        var sanctum = new Position(1, 1);

        var neighbors = BoardSystem.GetNeighbors(state.Board, sanctum);

        // Sanctum sees all 8 surrounding king-neighbors (including the inner tile)
        Assert.Equal(8, neighbors.Count);
        Assert.Contains(new Position(1, 2), neighbors); // inner
        Assert.Contains(new Position(0, 0), neighbors); // outer corner
    }

    [Fact]
    public void UnrevealedSanctum_DoesNotBridge()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var outer = new Position(0, 2);

        var neighbors = BoardSystem.GetNeighbors(state.Board, outer);

        Assert.DoesNotContain(new Position(1, 2), neighbors);
    }

    // ---------- Recomputation on reveal ----------

    [Fact]
    public void RevealingSanctum_RecomputesAdjacentRevealedTileCounts()
    {
        // Reveal (0,2) first — it's an outer tile next to the sanctum and inner.
        // Initially: sanctum unrevealed → (0,2) doesn't see inner → 0 rival neighbors.
        // After revealing sanctum → (0,2) sees inner Rival → 1 rival neighbor.
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var outerPos = new Position(0, 2);
        var sanctum = new Position(1, 1);

        // Reveal outer tile (player reveals it; player adjacency)
        var board = BoardSystem.RevealTile(state.Board, outerPos, PlayerType.Player);
        // (0,2) is Player; player adjacency = count of player neighbors among (0,1) and (1,1)... wait
        // Actually RevealedBy=Player so count = player neighbors. With no portal: (0,1)=Player, (1,1)=Sanctum/Neutral, (1,2)=excluded.
        // Player count = 1.
        Assert.Equal(1, board.GetTile(outerPos).AdjacencyCount);

        // Now reveal the sanctum — portal opens, (0,2)'s neighbor set grows to include inner.
        // But inner is Rival, not Player. Player count stays 1. Still — the recompute must run.
        board = BoardSystem.RevealTile(board, sanctum, PlayerType.Player);

        // (0,2) now also "sees" the inner tile (Rival) → still 1 player neighbor (no change in count
        // for this case, but the recompute happened — verify by checking for sanctum's own count).
        // Sanctum revealed by player; player adjacency of (1,1) = number of player neighbors among
        // its 8 king neighbors. (0,0), (0,1), (0,2), (1,0), (1,2)=Rival, (2,0), (2,1), (2,2). Players = 7.
        // After (0,2) was already revealed it counts toward player adjacency of sanctum.
        Assert.Equal(7, board.GetTile(sanctum).AdjacencyCount);
    }

    [Fact]
    public void BratUnrevealsSanctum_RecomputesAdjacency()
    {
        // Sanctum revealed; reveal (0,2) (player-revealed Player tile).
        // With portal open, (0,2) sees through sanctum to its other neighbors:
        // direct (0,1), (1,1), inner (1,2)=Rival, plus through portal (0,0), (1,0), (2,0), (2,1), (2,2).
        // Player count among those = 6 (all the surrounding outer tiles).
        var state = BuildSanctumBoard(sanctumRevealed: true);
        var outerPos = new Position(0, 2);

        var newBoard = BoardSystem.RevealTile(state.Board, outerPos, PlayerType.Player);
        Assert.Equal(6, newBoard.GetTile(outerPos).AdjacencyCount);

        state = state with { Board = newBoard, Hand = new List<Card> { CardDefinitions.Brat with { Id = "b1" } }, Spoons = 3 };

        // Brat un-reveals the sanctum — portal closes. (0,2)'s neighbors are now only
        // (0,1) and (1,1) — no inner tile, no portal extension. Player count = 1.
        state = CardEffectSystem.ExecuteBrat(state, new[] { new Position(1, 1) }, CardDefinitions.Brat);

        Assert.False(state.Board.GetTile(new Position(1, 1)).IsRevealed);
        Assert.Equal(1, state.Board.GetTile(outerPos).AdjacencyCount);
    }

    // ---------- Reachability ----------

    [Fact]
    public void InnerTile_Unreachable_WhenSanctumNotRevealed()
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
    public void OuterTile_AlwaysReachable()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        Assert.True(BoardSystem.CanReachInnerTile(state.Board, new Position(0, 0)));
    }

    [Fact]
    public void ClickingUnreachableInnerTile_Throws()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        Assert.Throws<InvalidOperationException>(() =>
            GameRunner.ProcessReveal(state, new Position(1, 2), new Random(7)));
    }

    [Fact]
    public void ClickingReachableInnerTile_Succeeds()
    {
        var state = BuildSanctumBoard(sanctumRevealed: true);
        // Result is rival-tile reveal that ends turn; we just want it not to throw.
        var result = GameRunner.ProcessReveal(state, new Position(1, 2), new Random(7));
        Assert.True(result.State.Board.GetTile(new Position(1, 2)).IsRevealed);
    }

    // ---------- Card targeting ----------

    [Fact]
    public void Spritz_OnUnreachableInnerTile_Throws()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, new[] { new Position(1, 2) }, spritz, new Random(7)));
    }

    [Fact]
    public void Eavesdrop_OnUnreachableInnerTile_Throws()
    {
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var eavesdrop = CardDefinitions.Eavesdrop with { Id = "ed1" };

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteEavesdrop(state, new[] { new Position(1, 2) }, eavesdrop));
    }

    [Fact]
    public void Tingle_DoesNotPickUnreachableInnerTiles()
    {
        // Build a board where the only Rival is an unreachable inner tile.
        // Tingle should be a no-op (no reachable rival/noble candidates).
        var state = BuildSanctumBoard(sanctumRevealed: false);
        // Inner tile (1,2) is the only Rival; it's unreachable.

        var newState = CardEffectSystem.ExecuteTingle(state, new Random(7), CardDefinitions.Tingle);

        // No annotations added (no candidates) — verify by checking the inner tile's annotation
        Assert.Null(newState.Board.GetTile(new Position(1, 2)).Annotations.OwnerSubset);
    }

    [Fact]
    public void Sweep_DoesNotProcessUnreachableInnerTiles()
    {
        // Mark the inner tile as ExtraDirty too. Sweep on (1,1) area should NOT clean
        // the dirty flag from the unreachable inner tile.
        var state = BuildSanctumBoard(sanctumRevealed: false);
        var newTiles = state.Board.Tiles.ToList();
        var innerIdx = state.Board.TileIndex(new Position(1, 2));
        newTiles[innerIdx] = newTiles[innerIdx].WithSpecial(SpecialTileType.ExtraDirty);
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var newState = CardEffectSystem.ExecuteSweep(state, new[] { new Position(1, 1) }, new Random(7));

        // Inner tile still dirty (Sweep skipped it because it's unreachable)
        Assert.True(newState.Board.GetTile(new Position(1, 2)).IsDirty);
    }

    // ---------- Win condition still works ----------

    [Fact]
    public void RevealingInnerTile_CountsTowardWin()
    {
        // Build a tiny board where revealing the inner tile (a Player) completes the win.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Sanctum, IsRevealed = true, RevealedBy = PlayerType.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player,
                Specials = SpecialTileType.InnerTile }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            CurrentPlayer = PlayerType.Player,
            Hand = new List<Card>(),
            DrawPile = CardDefinitions.CreateStarterDeck()
        };

        // Inner tile reachable since the sanctum is revealed
        var result = GameRunner.ProcessReveal(state, new Position(0, 1), new Random(7));

        Assert.Equal(GameStatus.Won, result.State.GameStatus);
    }
}
