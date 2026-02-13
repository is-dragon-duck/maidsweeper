using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class CardEffectTests
{
    /// <summary>
    /// Creates a small known board for testing card effects.
    /// 3x3 grid: positions are seeded so we know tile owners.
    /// </summary>
    private static GameState CreateTestGame(int seed = 42)
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var rng = new Random(seed);
        var board = BoardSystem.CreateBoard(config, rng);
        var deck = CardDefinitions.CreateStarterDeck();

        return new GameState
        {
            Board = board,
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            MaxSpoons = 3
        };
    }

    /// <summary>
    /// Creates a test game from the Level 1 config for more realistic tests.
    /// </summary>
    private static GameState CreateLevel1Game(int seed = 42)
    {
        var rng = new Random(seed);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var deck = CardDefinitions.CreateStarterDeck();
        deck = DeckSystem.Shuffle(deck, new Random(seed + 1));

        return new GameState
        {
            Board = board,
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            MaxSpoons = 3
        };
    }

    private static Position FindFirstUnrevealed(GameState state, TileOwner owner)
    {
        return state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == owner).Position;
    }

    // --- Spritz Tests ---

    [Fact]
    public void Spritz_SafeTileAnnotatedAsSafe()
    {
        var state = CreateLevel1Game();
        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var newState = CardEffectSystem.ExecuteSpritz(state, [playerPos], spritz);

        var annotation = newState.Board.GetTile(playerPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Contains(TileOwner.Player, annotation);
        Assert.Contains(TileOwner.Neutral, annotation);
        Assert.DoesNotContain(TileOwner.Rival, annotation);
        Assert.DoesNotContain(TileOwner.Noble, annotation);
    }

    [Fact]
    public void Spritz_NeutralTileAlsoAnnotatedAsSafe()
    {
        var state = CreateLevel1Game();
        var neutralPos = FindFirstUnrevealed(state, TileOwner.Neutral);

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var newState = CardEffectSystem.ExecuteSpritz(state, [neutralPos], spritz);

        var annotation = newState.Board.GetTile(neutralPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral }, annotation);
    }

    [Fact]
    public void Spritz_RivalTileAnnotatedAsDangerous()
    {
        var state = CreateLevel1Game();
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var newState = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz);

        var annotation = newState.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Noble }, annotation);
    }

    [Fact]
    public void Spritz_MultipleAnnotationSpritzIntersectsSubsets()
    {
        var state = CreateTestGame();
        // Find a rival tile
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        // Annotate as either rival or neutral
        var rivalOrNeutral = new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Neutral };
        state = AnnotationSystem.AddOwnerSubset(state, rivalPos, rivalOrNeutral);

        // Spritz: dangerous → {Rival, Mine}
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        state = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz);

        var annotation = state.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Single(annotation);
        Assert.Contains(TileOwner.Rival, annotation);
    }

    [Fact]
    public void Spritz_RemovesExtraDirty()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);
        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.Spritz with { Id = "s1" }],
            Spoons = 3,
            MaxSpoons = 3
        };

        var newState = CardEffectSystem.ExecuteSpritz(state, [dirtyTile.Position], state.Hand[0]);

        var tile = newState.Board.GetTile(dirtyTile.Position);
        Assert.False(tile.IsDirty, "Spritz should remove ExtraDirty");
        Assert.NotNull(tile.Annotations.OwnerSubset); // Also annotated
    }

    [Fact]
    public void Spritz_ThrowsOnRevealedTile()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        // Reveal the tile first
        state = state with { Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player) };

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, [pos], spritz));
    }

    [Fact]
    public void Spritz_ThrowsOnWrongTargetCount()
    {
        var state = CreateLevel1Game();
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, null, spritz));
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, [], spritz));
    }

    // --- Imperious Instructions Tests ---

    [Fact]
    public void Instructions_ProducesClueResults()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Recall);
        var newState = CardEffectSystem.ExecuteInstructions(state, rng, card);

        // At least some tiles should have clue results
        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        Assert.True(tilesWithClues > 0, "Instructions should produce clue results on tiles");
    }

    [Fact]
    public void Instructions_AffectsUpTo8Tiles()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Recall);
        var newState = CardEffectSystem.ExecuteInstructions(state, rng, card);

        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        // 2 targets + 6 spoilers = at most 8 tiles, but some bag entries may not get drawn
        Assert.InRange(tilesWithClues, 1, 8);
    }

    [Fact]
    public void Instructions_PlayerTileHasMaxPips()
    {
        // The algorithm guarantees (via retry validation) that a player tile has the max pip count
        var state = CreateLevel1Game(seed: 42);
        var rng = new Random(99);

        var clues = ClueSystem.GenerateImperiousClue(state, rng);
        Assert.NotEmpty(clues);

        var maxPips = clues.Max(c => c.PipStrength);
        var tilesWithMax = clues.Where(c => c.PipStrength == maxPips).ToList();

        Assert.True(
            tilesWithMax.Any(c => state.Board.GetTile(c.TilePosition).Owner == TileOwner.Player),
            $"Expected a player tile to have max pips ({maxPips}), but none did");
    }

    [Fact]
    public void Instructions_AllClueResultsShareSameId()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var clues = ClueSystem.GenerateImperiousClue(state, rng);
        if (clues.Count > 1)
        {
            var firstId = clues[0].ClueId;
            Assert.All(clues, c => Assert.Equal(firstId, c.ClueId));
        }
    }

    // --- Scurry Tests ---

    [Fact]
    public void Scurry_RevealsSaferTile()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Scurry);
        var newState = CardEffectSystem.ExecuteScurry(state, [playerPos, rivalPos], rng, card);

        // Player tile (safety 4) should be revealed
        Assert.True(newState.Board.GetTile(playerPos).IsRevealed);
        Assert.False(newState.Board.GetTile(rivalPos).IsRevealed);
    }

    [Fact]
    public void Scurry_AnnotatesNonRevealedTile()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Scurry);
        var newState = CardEffectSystem.ExecuteScurry(state, [playerPos, rivalPos], rng, card);

        // The rival tile should get an annotation: types at-most-as-safe-as Player (= all types)
        var annotation = newState.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Equal(4, annotation.Count); // All types possible when safest is Player
    }

    [Fact]
    public void Scurry_NeutralVsRival_RevealsNeutral()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var neutralPos = FindFirstUnrevealed(state, TileOwner.Neutral);
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Scurry);
        var newState = CardEffectSystem.ExecuteScurry(state, [neutralPos, rivalPos], rng, card);

        Assert.True(newState.Board.GetTile(neutralPos).IsRevealed);
        Assert.False(newState.Board.GetTile(rivalPos).IsRevealed);

        // Rival tile annotation: types at-most-as-safe-as Neutral = {Neutral, Rival, Mine}
        var annotation = newState.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Contains(TileOwner.Neutral, annotation);
        Assert.Contains(TileOwner.Rival, annotation);
        Assert.Contains(TileOwner.Noble, annotation);
        Assert.DoesNotContain(TileOwner.Player, annotation);
    }

    [Fact]
    public void Scurry_ExtraDirty_CleansInsteadOfRevealing()
    {
        // Create board with a dirty player tile
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);
        // Find a rival tile to pair with
        var rivalPos = board.Tiles.First(t => board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival).Position;

        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.Scurry with { Id = "sc1" }],
            Spoons = 3,
            MaxSpoons = 3
        };

        // Determine which tile is safer so we know which will be "revealed"
        var dirtySafety = dirtyTile.Owner == TileOwner.Player ? 4 : dirtyTile.Owner == TileOwner.Neutral ? 3 : 2;
        var rivalSafety = 2; // Rival is safety 2

        var rng = new Random(42);
        var newState = CardEffectSystem.ExecuteScurry(state, [dirtyTile.Position, rivalPos], rng, state.Hand[0]);

        if (dirtySafety >= rivalSafety)
        {
            // Dirty tile is safer (or equal) — should be cleaned, not revealed
            var tile = newState.Board.GetTile(dirtyTile.Position);
            Assert.False(tile.IsDirty, "Scurry should clean ExtraDirty from safer tile");
            Assert.False(tile.IsRevealed, "Scurry should NOT reveal an ExtraDirty tile");
            Assert.NotNull(tile.Annotations.OwnerSubset);
            Assert.Contains(dirtyTile.Owner, tile.Annotations.OwnerSubset);
            Assert.Single(tile.Annotations.OwnerSubset); // Exact owner annotation
        }
    }

    [Fact]
    public void Scurry_ThrowsOnWrongTargetCount()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);
        var card = state.Hand.First(c => c.EffectType == CardEffectType.Scurry);

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteScurry(state, [new Position(0, 0)], rng, card));
    }

    // --- Tingle Tests ---

    [Fact]
    public void Tingle_MarksRivalOrMineTile()
    {
        var state = CreateTestGame();
        var rng = new Random(42);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Tingle);
        var newState = CardEffectSystem.ExecuteTingle(state, rng, card);

        // Find the tile that got annotated
        var annotated = newState.Board.Tiles
            .Where(t => t.Annotations.OwnerSubset != null && t.Annotations.OwnerSubset.Count == 1)
            .ToList();

        Assert.NotEmpty(annotated);
        var tile = annotated.First();
        Assert.True(
            tile.Owner == TileOwner.Rival || tile.Owner == TileOwner.Noble,
            "Tingle should only target rival or mine tiles");
        Assert.Contains(tile.Owner, tile.Annotations.OwnerSubset!);
    }

    [Fact]
    public void Tingle_PrefersAmbiguousTiles()
    {
        var state = CreateTestGame();
        var rng = new Random(42);

        // Mark all but one rival tile as already known
        var firstRival = FindFirstUnrevealed(state, TileOwner.Rival);
        foreach (var tile in state.Board.Tiles)
        {
            if (tile.Position == firstRival)
            {
                continue;
            }
            state = AnnotationSystem.AddOwnerSubset(state, tile.Position, new HashSet<TileOwner> { tile.Owner });
        }

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Tingle);
        var newState = CardEffectSystem.ExecuteTingle(state, new Random(), card);

        Assert.Equal(newState.Board.GetTile(firstRival).Annotations.OwnerSubset, new HashSet<TileOwner> { TileOwner.Rival });
    }

    [Fact]
    public void Tingle_NoTargetsReturnsUnchangedState()
    {
        // Board with only player and neutral tiles
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 2, RivalCount = 0, NeutralCount = 2, NobleCount = 0
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.Tingle with { Id = "t1" }],
            Spoons = 3
        };

        var rng = new Random(42);
        var newState = CardEffectSystem.ExecuteTingle(state, rng, state.Hand[0]);

        // No tiles should have owner subset annotations
        Assert.All(newState.Board.Tiles, t => Assert.Null(t.Annotations.OwnerSubset));
    }

    // --- Twirl Tests ---

    [Fact]
    public void Twirl_Gains3Copper()
    {
        var state = CreateLevel1Game();
        var card = CardDefinitions.Twirl with { Id = "tw1" };

        var newState = CardEffectSystem.ExecuteTwirl(state, card);

        Assert.Equal(3, newState.Copper);
    }

    [Fact]
    public void Twirl_EnhancedGains5Copper()
    {
        var state = CreateLevel1Game();
        var card = CardDefinitions.Twirl with { Id = "tw1", Enhanced = true };

        var newState = CardEffectSystem.ExecuteTwirl(state, card);

        Assert.Equal(5, newState.Copper);
    }

    // --- PlayCard Integration Tests ---

    [Fact]
    public void PlayCard_DeductsSpoonsAndDiscardsCard()
    {
        var state = CreateLevel1Game();
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);

        var newState = CardEffectSystem.PlayCard(state, spritz, [playerPos], new Random(42));

        Assert.Equal(2, newState.Spoons); // 3 - 1 cost
        Assert.DoesNotContain(spritz, newState.Hand);
        Assert.Contains(spritz, newState.DiscardPile);
    }

    [Fact]
    public void PlayCard_ExhaustCardGoesToExhaustPile()
    {
        var state = CreateLevel1Game() with { Spoons = 3 };
        var twirl = CardDefinitions.Twirl with { Id = "tw_test" };
        state = state with { Hand = state.Hand.ToList().Append(twirl).ToList() };

        var newState = CardEffectSystem.PlayCard(state, twirl, null, new Random(42));

        Assert.Contains(twirl, newState.ExhaustPile);
        Assert.DoesNotContain(twirl, newState.DiscardPile);
    }

    [Fact]
    public void PlayCard_ThrowsWhenInsufficientSpoons()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);

        Assert.Throws<InvalidOperationException>(() =>
            CardEffectSystem.PlayCard(state, spritz, [playerPos], new Random(42)));
    }

    // --- Edge Case Tests ---

    [Fact]
    public void Instructions_FewPlayerTilesReturnsEmpty()
    {
        // Board with only 1 player tile — not enough for Instructions
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 1, RivalCount = 2, NeutralCount = 1, NobleCount = 0
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.RecallImperious with { Id = "r1" }],
            Spoons = 3
        };

        var rng = new Random(99);
        var newState = CardEffectSystem.ExecuteInstructions(state, rng, state.Hand[0]);

        // Should produce no clues (not crash)
        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        Assert.Equal(0, tilesWithClues);
    }

    [Fact]
    public void Scurry_ThrowsOnRevealedTarget()
    {
        var state = CreateLevel1Game();
        var pos1 = FindFirstUnrevealed(state, TileOwner.Player);
        var pos2 = FindFirstUnrevealed(state, TileOwner.Rival);

        // Reveal pos1 first
        state = state with { Board = BoardSystem.RevealTile(state.Board, pos1, PlayerType.Player) };

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Scurry);
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteScurry(state, [pos1, pos2], new Random(42), card));
    }

    [Fact]
    public void PlayCard_LastCardInHandLeavesEmptyHand()
    {
        var card = CardDefinitions.Tingle with { Id = "t_last" };
        var state = CreateLevel1Game() with
        {
            Hand = new List<Card> { card },
            Spoons = 1
        };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Empty(newState.Hand);
    }

    // --- Brush Tests ---

    [Fact]
    public void Brush_AnnotatesTilesIn3x3()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var center = new Position(2, 3);
        var newState = CardEffectSystem.ExecuteBrush(state, [center], rng);

        // All unrevealed tiles in 3x3 should have owner subset annotation
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        var annotated = tilesInArea
            .Where(t => !t.IsRevealed)
            .Select(t => newState.Board.GetTile(t.Position))
            .Where(t => t.Annotations.OwnerSubset != null)
            .ToList();

        Assert.True(annotated.Count > 0, "Brush should annotate tiles");
    }

    [Fact]
    public void Brush_ExcludesANonOwner()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var center = new Position(2, 3);
        var newState = CardEffectSystem.ExecuteBrush(state, [center], rng);

        // Each annotated tile should have subset of size 3 (4 owners - 1 excluded)
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        foreach (var origTile in tilesInArea)
        {
            if (origTile.IsRevealed) continue;
            var tile = newState.Board.GetTile(origTile.Position);
            if (tile.Annotations.OwnerSubset != null)
            {
                // Subset should contain the tile's real owner
                Assert.Contains(origTile.Owner, tile.Annotations.OwnerSubset);
                // Subset should have at most 3 elements (one was excluded)
                Assert.True(tile.Annotations.OwnerSubset.Count <= 3);
            }
        }
    }

    [Fact]
    public void Brush_RespectsEdges()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        // Corner position — should only annotate 4 tiles
        var corner = new Position(0, 0);
        var newState = CardEffectSystem.ExecuteBrush(state, [corner], rng);

        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, corner, 1);
        Assert.Equal(4, tilesInArea.Count);
    }

    // --- Sweep Tests ---

    [Fact]
    public void Sweep_RemovesDirtIn5x5()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.Sweep with { Id = "sw1" }],
            Spoons = 3,
            MaxSpoons = 3
        };

        // Find the dirty tile
        var dirtyTile = board.Tiles.First(t => t.IsDirty);

        // Use Sweep centered on dirty tile
        var newState = CardEffectSystem.ExecuteSweep(state, [dirtyTile.Position]);

        var tile = newState.Board.GetTile(dirtyTile.Position);
        Assert.False(tile.IsDirty);
    }

    [Fact]
    public void Sweep_DoesNotAffectNonDirtyTiles()
    {
        var state = CreateLevel1Game();
        var center = new Position(2, 3);

        // No dirty tiles on Level 1 — Sweep should return same state
        var newState = CardEffectSystem.ExecuteSweep(state, [center]);
        Assert.Same(state, newState);
    }

    // --- Caffeinate Tests ---

    [Fact]
    public void Caffeinate_Gains2Spoons()
    {
        var state = CreateLevel1Game();
        var newState = CardEffectSystem.ExecuteCaffeinate(state);

        Assert.Equal(state.Spoons + 2, newState.Spoons);
    }

    [Fact]
    public void PlayCard_CaffeinateExhausts()
    {
        var state = CreateLevel1Game();
        var caffCard = CardDefinitions.Caffeinate with { Id = "caff_test" };
        state = state with { Hand = state.Hand.ToList().Append(caffCard).ToList() };

        var newState = CardEffectSystem.PlayCard(state, caffCard, null, new Random(42));

        Assert.Contains(caffCard, newState.ExhaustPile);
        Assert.DoesNotContain(caffCard, newState.DiscardPile);
    }

    // --- Breathe Tests ---

    [Fact]
    public void Breathe_Draws3Cards()
    {
        var state = CreateLevel1Game();
        var initialHandCount = state.Hand.Count;
        // Make sure draw pile has cards by putting extras there
        var draw = Enumerable.Range(0, 5).Select(i =>
            CardDefinitions.Spritz with { Id = $"extra_{i}" }).ToList();
        state = state with { DrawPile = draw };

        var newState = CardEffectSystem.ExecuteBreathe(state, new Random(42));

        Assert.Equal(initialHandCount + 3, newState.Hand.Count);
    }

    [Fact]
    public void Breathe_HandlesSmallDeck()
    {
        var state = CreateLevel1Game() with
        {
            DrawPile = new List<Card> { CardDefinitions.Spritz with { Id = "last" } },
            DiscardPile = new List<Card>()
        };
        var initialHand = state.Hand.Count;

        var newState = CardEffectSystem.ExecuteBreathe(state, new Random(42));

        // Only 1 card available, should draw 1
        Assert.Equal(initialHand + 1, newState.Hand.Count);
    }

    // --- Lock In Tests ---

    [Fact]
    public void LockIn_Draws2Cards()
    {
        var state = CreateLevel1Game();
        var draw = Enumerable.Range(0, 5).Select(i =>
            CardDefinitions.Spritz with { Id = $"extra_{i}" }).ToList();
        state = state with { DrawPile = draw };
        var initialHandCount = state.Hand.Count;

        var newState = CardEffectSystem.ExecuteLockIn(state, new Random(42));

        Assert.Equal(initialHandCount + 2, newState.Hand.Count);
    }

    [Fact]
    public void PlayCard_LockInCosts0AndExhausts()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var lockCard = CardDefinitions.LockIn with { Id = "lock_test" };
        var draw = Enumerable.Range(0, 5).Select(i =>
            CardDefinitions.Spritz with { Id = $"draw_{i}" }).ToList();
        state = state with
        {
            Hand = new List<Card> { lockCard },
            DrawPile = draw
        };

        var newState = CardEffectSystem.PlayCard(state, lockCard, null, new Random(42));

        Assert.Equal(0, newState.Spoons); // Still 0 — cost 0
        Assert.Contains(lockCard, newState.ExhaustPile);
    }

    // --- Rendezvous Tests ---

    [Fact]
    public void Rendezvous_Reveals1PlayerAnd1RivalTile()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var playerBefore = state.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        var rivalBefore = state.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);

        var newState = CardEffectSystem.ExecuteRendezvous(state, rng);

        var playerAfter = newState.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        var rivalAfter = newState.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);

        Assert.Equal(playerBefore + 1, playerAfter);
        Assert.Equal(rivalBefore + 1, rivalAfter);
    }

    [Fact]
    public void Rendezvous_SwapsAdjacencyPerspective()
    {
        var state = CreateLevel1Game();
        var rng = new Random(42);

        var newState = CardEffectSystem.ExecuteRendezvous(state, rng);

        // The newly revealed player tile should have rival adjacency
        var revealedPlayer = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Player && !state.Board.GetTile(t.Position).IsRevealed);
        var expectedRivalAdj = BoardSystem.CalculateAdjacency(state.Board, revealedPlayer.Position, PlayerType.Rival);
        Assert.Equal(expectedRivalAdj, revealedPlayer.AdjacencyCount);

        // The newly revealed rival tile should have player adjacency
        var revealedRival = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Rival && !state.Board.GetTile(t.Position).IsRevealed);
        // Need to check against updated board (after player tile was revealed)
        var boardAfterPlayerReveal = newState.Board;
        var expectedPlayerAdj = BoardSystem.CalculateAdjacency(state.Board, revealedRival.Position, PlayerType.Player);
        Assert.Equal(expectedPlayerAdj, revealedRival.AdjacencyCount);
    }

    [Fact]
    public void Rendezvous_NoTargets_ReturnsUnchanged()
    {
        // Board with no rival tiles
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 2, RivalCount = 0, NeutralCount = 2, NobleCount = 0
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = [CardDefinitions.Rendezvous with { Id = "r1" }],
            Spoons = 3
        };

        var rng = new Random(42);
        var newState = CardEffectSystem.ExecuteRendezvous(state, rng);

        // Player tile revealed, but no rival tile available
        var playerRevealed = newState.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(1, playerRevealed);
    }
}
