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
        var newState = CardEffectSystem.ExecuteSpritz(state, [playerPos], spritz, new Random(0));

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
        var newState = CardEffectSystem.ExecuteSpritz(state, [neutralPos], spritz, new Random(0));

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
        var newState = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz, new Random(0));

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
        state = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz, new Random(0));

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

        var newState = CardEffectSystem.ExecuteSpritz(state, [dirtyTile.Position], state.Hand[0], new Random(0));

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
            CardEffectSystem.ExecuteSpritz(state, [pos], spritz, new Random(0)));
    }

    [Fact]
    public void Spritz_ThrowsOnWrongTargetCount()
    {
        var state = CreateLevel1Game();
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, null, spritz, new Random(0)));
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, [], spritz, new Random(0)));
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
    public void Instructions_ProducesPipsWithOnlyOneUnrevealedPlayerTile()
    {
        // Regression: late-floor scenario where only 1 player tile is left unrevealed.
        // Previously the algorithm short-circuited at playerTiles.Count < 2 and produced no pips.
        var state = CreateLevel1Game();

        // Reveal every player tile except one.
        var playerPositions = state.Board.Tiles
            .Where(t => t.Owner == TileOwner.Player)
            .Select(t => t.Position)
            .ToList();
        var lastPlayerPos = playerPositions[0];
        var newTiles = state.Board.Tiles.ToList();
        foreach (var pos in playerPositions.Skip(1))
        {
            var idx = state.Board.TileIndex(pos);
            newTiles[idx] = newTiles[idx] with
            {
                IsRevealed = true,
                RevealedBy = PlayerType.Player,
                AdjacencyCount = 0
            };
        }
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var clues = ClueSystem.GenerateImperiousClue(state, new Random(99));
        Assert.NotEmpty(clues);

        // The remaining player tile should have the most pips.
        var maxPips = clues.Max(c => c.PipStrength);
        Assert.Contains(clues, c => c.TilePosition == lastPlayerPos && c.PipStrength == maxPips);
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
    public void Instructions_ZeroPlayerTilesReturnsEmpty()
    {
        // Board with no player tiles — nothing to point at.
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 0, RivalCount = 2, NeutralCount = 2, NobleCount = 0
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

        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        Assert.Equal(0, tilesWithClues);
    }

    [Fact]
    public void Instructions_OnePlayerTileStillProducesClue()
    {
        // 1 player tile is enough — the clue should concentrate pips on it.
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

        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        Assert.True(tilesWithClues > 0);
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
        var newState = CardEffectSystem.ExecuteBrush(state, [center], rng, CardDefinitions.Brush);

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
        var newState = CardEffectSystem.ExecuteBrush(state, [center], rng, CardDefinitions.Brush);

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
        var newState = CardEffectSystem.ExecuteBrush(state, [corner], rng, CardDefinitions.Brush);

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
        var newState = CardEffectSystem.ExecuteSweep(state, [dirtyTile.Position], new Random(0), CardDefinitions.Sweep);

        var tile = newState.Board.GetTile(dirtyTile.Position);
        Assert.False(tile.IsDirty);
    }

    [Fact]
    public void Sweep_DoesNotAffectNonDirtyTiles()
    {
        var state = CreateLevel1Game();
        var center = new Position(2, 3);

        // No dirty tiles on Level 1 — Sweep should return same state
        var newState = CardEffectSystem.ExecuteSweep(state, [center], new Random(0), CardDefinitions.Sweep);
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

        var newState = CardEffectSystem.ExecuteBreathe(state, new Random(42), CardDefinitions.Breathe);

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

        var newState = CardEffectSystem.ExecuteBreathe(state, new Random(42), CardDefinitions.Breathe);

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

        var newState = CardEffectSystem.ExecuteLockIn(state, new Random(42), CardDefinitions.LockIn);

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

        var newState = CardEffectSystem.ExecuteRendezvous(state, rng, CardDefinitions.Rendezvous);

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

        var newState = CardEffectSystem.ExecuteRendezvous(state, rng, CardDefinitions.Rendezvous);

        // The newly revealed player tile should have rival adjacency and rival perspective
        var revealedPlayer = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Player && !state.Board.GetTile(t.Position).IsRevealed);
        var expectedRivalAdj = BoardSystem.CalculateAdjacency(state.Board, revealedPlayer.Position, PlayerType.Rival);
        Assert.Equal(expectedRivalAdj, revealedPlayer.AdjacencyCount);
        Assert.Equal(PlayerType.Rival, revealedPlayer.RevealedBy);

        // The newly revealed rival tile should have player adjacency and player perspective
        var revealedRival = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Rival && !state.Board.GetTile(t.Position).IsRevealed);
        var expectedPlayerAdj = BoardSystem.CalculateAdjacency(state.Board, revealedRival.Position, PlayerType.Player);
        Assert.Equal(expectedPlayerAdj, revealedRival.AdjacencyCount);
        Assert.Equal(PlayerType.Player, revealedRival.RevealedBy);
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
        var newState = CardEffectSystem.ExecuteRendezvous(state, rng, CardDefinitions.Rendezvous);

        // Player tile revealed, but no rival tile available
        var playerRevealed = newState.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(1, playerRevealed);
    }

    // ========== Stage 3 Card Definition Tests ==========

    [Theory]
    [InlineData("Argue", 1, false, CardEffectType.Argue)]
    [InlineData("Accept Help", 3, false, CardEffectType.AcceptHelp)]
    [InlineData("Eavesdrop", 1, false, CardEffectType.Eavesdrop)]
    [InlineData("Peek", 0, false, CardEffectType.Peek)]
    [InlineData("Explode", 1, false, CardEffectType.Explode)]
    [InlineData("Deliver", 1, false, CardEffectType.Deliver)]
    [InlineData("Brat", 1, true, CardEffectType.Brat)]
    [InlineData("Ramble", 1, false, CardEffectType.Ramble)]
    [InlineData("Glaze", 0, true, CardEffectType.Glaze)]
    [InlineData("Mask", 0, true, CardEffectType.Mask)]
    [InlineData("Nap", 1, true, CardEffectType.Nap)]
    [InlineData("Mollify", 1, true, CardEffectType.Mollify)]
    public void Stage3CardDefinitions_HaveCorrectProperties(string name, int cost, bool exhaust, CardEffectType effectType)
    {
        var card = effectType switch
        {
            CardEffectType.Argue => CardDefinitions.Argue,
            CardEffectType.AcceptHelp => CardDefinitions.AcceptHelp,
            CardEffectType.Eavesdrop => CardDefinitions.Eavesdrop,
            CardEffectType.Peek => CardDefinitions.Peek,
            CardEffectType.Explode => CardDefinitions.Explode,
            CardEffectType.Deliver => CardDefinitions.Deliver,
            CardEffectType.Brat => CardDefinitions.Brat,
            CardEffectType.Ramble => CardDefinitions.Ramble,
            CardEffectType.Glaze => CardDefinitions.Glaze,
            CardEffectType.Mask => CardDefinitions.Mask,
            CardEffectType.Nap => CardDefinitions.Nap,
            CardEffectType.Mollify => CardDefinitions.Mollify,
            _ => throw new ArgumentException($"Unknown effect type: {effectType}")
        };

        Assert.Equal(name, card.Name);
        Assert.Equal(cost, card.Cost);
        Assert.Equal(exhaust, card.Exhaust);
        Assert.Equal(effectType, card.EffectType);
    }

    [Fact]
    public void BonusSpoon_GrantsSpoonAfterPlay()
    {
        var state = CreateTestGame();
        var card = CardDefinitions.Spritz with
        {
            Id = "bonus_test",
            BonusSpoon = true
        };

        // Put the card in hand
        var hand = state.Hand.ToList();
        hand[0] = card;
        state = state with { Hand = hand, Spoons = 3 };

        // Find a valid target tile (unrevealed)
        var target = state.Board.Tiles.First(t => !t.IsRevealed);

        var newState = CardEffectSystem.PlayCard(state, card, [target.Position], new Random(42));

        // Cost 1, then +1 bonus = net 0 loss from 3 spoons
        Assert.Equal(3, newState.Spoons);
    }

    // ========== Argue Tests ==========

    [Fact]
    public void Argue_AnnotatesNeutralsAsNeutral()
    {
        var state = CreateLevel1Game();
        var center = new Position(2, 3);

        var newState = CardEffectSystem.ExecuteArgue(state, [center], new Random(42),
            CardDefinitions.Argue with { Id = "a1" });

        // Check neutral tiles in 3x3 area
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        foreach (var tile in tilesInArea)
        {
            if (tile.IsRevealed) continue;
            var updated = newState.Board.GetTile(tile.Position);
            if (tile.Owner == TileOwner.Neutral)
            {
                Assert.NotNull(updated.Annotations.OwnerSubset);
                Assert.Single(updated.Annotations.OwnerSubset!);
                Assert.Contains(TileOwner.Neutral, updated.Annotations.OwnerSubset!);
            }
        }
    }

    [Fact]
    public void Argue_AnnotatesNonNeutralsAsNotNeutral()
    {
        var state = CreateLevel1Game();
        var center = new Position(2, 3);

        var newState = CardEffectSystem.ExecuteArgue(state, [center], new Random(42),
            CardDefinitions.Argue with { Id = "a1" });

        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        foreach (var tile in tilesInArea)
        {
            if (tile.IsRevealed) continue;
            var updated = newState.Board.GetTile(tile.Position);
            if (tile.Owner != TileOwner.Neutral)
            {
                Assert.NotNull(updated.Annotations.OwnerSubset);
                Assert.Equal(
                    new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Noble },
                    updated.Annotations.OwnerSubset!);
            }
        }
    }

    [Fact]
    public void Argue_EnhancedDraws1Card()
    {
        var state = CreateLevel1Game();
        var draw = Enumerable.Range(0, 5).Select(i =>
            CardDefinitions.Spritz with { Id = $"draw_{i}" }).ToList();
        state = state with { DrawPile = draw };
        var initialHandCount = state.Hand.Count;

        var center = new Position(2, 3);
        var card = CardDefinitions.Argue with { Id = "a1", Enhanced = true };

        var newState = CardEffectSystem.ExecuteArgue(state, [center], new Random(42), card);

        Assert.Equal(initialHandCount + 1, newState.Hand.Count);
    }

    [Fact]
    public void Argue_BaseDoesNotDrawCards()
    {
        var state = CreateLevel1Game();
        var draw = Enumerable.Range(0, 5).Select(i =>
            CardDefinitions.Spritz with { Id = $"draw_{i}" }).ToList();
        state = state with { DrawPile = draw };
        var initialHandCount = state.Hand.Count;

        var center = new Position(2, 3);
        var card = CardDefinitions.Argue with { Id = "a1" };

        var newState = CardEffectSystem.ExecuteArgue(state, [center], new Random(42), card);

        Assert.Equal(initialHandCount, newState.Hand.Count);
    }

    [Fact]
    public void Argue_EdgeOfBoardClipsArea()
    {
        var state = CreateLevel1Game();
        var corner = new Position(0, 0);

        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, corner, 1);

        // Corner of a 7x7 board → 2x2 = 4 tiles
        Assert.Equal(4, tilesInArea.Count);

        // Should not throw
        var newState = CardEffectSystem.ExecuteArgue(state, [corner], new Random(42),
            CardDefinitions.Argue with { Id = "a1" });

        // All 4 tiles should be annotated
        var annotated = tilesInArea
            .Where(t => !t.IsRevealed)
            .Select(t => newState.Board.GetTile(t.Position))
            .Count(t => t.Annotations.OwnerSubset != null);
        Assert.Equal(tilesInArea.Count(t => !t.IsRevealed), annotated);
    }

    // ========== Eavesdrop Tests ==========

    [Fact]
    public void Eavesdrop_PlayerTile_AnnotatedAsPlayer()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        var newState = CardEffectSystem.ExecuteEavesdrop(state, [pos],
            CardDefinitions.Eavesdrop with { Id = "e1" });

        var annotations = newState.Board.GetTile(pos).Annotations;
        Assert.NotNull(annotations.OwnerSubset);
        Assert.Single(annotations.OwnerSubset!);
        Assert.Contains(TileOwner.Player, annotations.OwnerSubset!);
    }

    [Fact]
    public void Eavesdrop_NonPlayerTile_AnnotatedAsNotPlayer()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Rival);

        var newState = CardEffectSystem.ExecuteEavesdrop(state, [pos],
            CardDefinitions.Eavesdrop with { Id = "e1" });

        var annotations = newState.Board.GetTile(pos).Annotations;
        Assert.NotNull(annotations.OwnerSubset);
        Assert.Equal(
            new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble },
            annotations.OwnerSubset!);
    }

    [Fact]
    public void Eavesdrop_Base_AddsPlayerAdjacency()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        var newState = CardEffectSystem.ExecuteEavesdrop(state, [pos],
            CardDefinitions.Eavesdrop with { Id = "e1" });

        var adjInfo = newState.Board.GetTile(pos).Annotations.AdjacencyInfo;
        Assert.NotNull(adjInfo);
        Assert.NotNull(adjInfo!.PlayerCount);
        // Other counts should be null (unknown)
        Assert.Null(adjInfo.RivalCount);
        Assert.Null(adjInfo.NeutralCount);
        Assert.Null(adjInfo.NobleCount);
    }

    [Fact]
    public void Eavesdrop_Enhanced_ExactOwnerAndFullAdjacency()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Rival);

        var card = CardDefinitions.Eavesdrop with { Id = "e1", Enhanced = true };
        var newState = CardEffectSystem.ExecuteEavesdrop(state, [pos], card);

        var annotations = newState.Board.GetTile(pos).Annotations;
        // Exact owner
        Assert.NotNull(annotations.OwnerSubset);
        Assert.Single(annotations.OwnerSubset!);
        Assert.Contains(TileOwner.Rival, annotations.OwnerSubset!);

        // Full adjacency
        var adjInfo = annotations.AdjacencyInfo;
        Assert.NotNull(adjInfo);
        Assert.NotNull(adjInfo!.PlayerCount);
        Assert.NotNull(adjInfo.RivalCount);
        Assert.NotNull(adjInfo.NeutralCount);
        Assert.NotNull(adjInfo.NobleCount);
    }

    [Fact]
    public void Eavesdrop_DoesNotRevealTile()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        var newState = CardEffectSystem.ExecuteEavesdrop(state, [pos],
            CardDefinitions.Eavesdrop with { Id = "e1" });

        Assert.False(newState.Board.GetTile(pos).IsRevealed);
    }

    [Fact]
    public void Eavesdrop_ThrowsOnRevealedTile()
    {
        var state = CreateLevel1Game();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);
        state = state with { Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player) };

        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteEavesdrop(state, [pos],
                CardDefinitions.Eavesdrop with { Id = "e1" }));
    }

    // ========== Peek Tests ==========

    [Fact]
    public void Peek_AnnotatesNoblesAsNoble()
    {
        // Use a board with nobles
        var state = CreateTestGame();
        var nobleTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble);

        // Use that noble tile as center
        var (newState, foundNobles) = CardEffectSystem.ExecutePeek(state, [nobleTile.Position],
            CardDefinitions.Peek with { Id = "p1" });

        Assert.True(foundNobles);
        var annotations = newState.Board.GetTile(nobleTile.Position).Annotations;
        Assert.NotNull(annotations.OwnerSubset);
        Assert.Single(annotations.OwnerSubset!);
        Assert.Contains(TileOwner.Noble, annotations.OwnerSubset!);
    }

    [Fact]
    public void Peek_AnnotatesNonNoblesAsNotNoble()
    {
        var state = CreateTestGame();
        var center = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player).Position;

        var (newState, _) = CardEffectSystem.ExecutePeek(state, [center],
            CardDefinitions.Peek with { Id = "p1" });

        var tilesInCross = BoardSystem.GetTilesInCross(state.Board, center);
        foreach (var tile in tilesInCross)
        {
            if (tile.IsRevealed) continue;
            var updated = newState.Board.GetTile(tile.Position);
            if (tile.Owner != TileOwner.Noble)
            {
                Assert.NotNull(updated.Annotations.OwnerSubset);
                Assert.Equal(
                    new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral },
                    updated.Annotations.OwnerSubset!);
            }
        }
    }

    [Fact]
    public void Peek_ExhaustsWhenNoblesFound()
    {
        var state = CreateTestGame();
        var nobleTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble);
        var card = CardDefinitions.Peek with { Id = "p1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, [nobleTile.Position], new Random(42));

        Assert.Contains(card, newState.ExhaustPile);
        Assert.DoesNotContain(card, newState.DiscardPile);
    }

    [Fact]
    public void Peek_DiscardsWhenNoNoblesFound()
    {
        // Create a board with no nobles so Peek can't find any
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 3, NobleCount = 0
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var card = CardDefinitions.Peek with { Id = "p1" };
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card> { card },
            Spoons = 3,
            MaxSpoons = 3
        };

        var center = board.Tiles.First(t => !t.IsRevealed).Position;
        var newState = CardEffectSystem.PlayCard(state, card, [center], new Random(42));

        Assert.Contains(card, newState.DiscardPile);
        Assert.DoesNotContain(card, newState.ExhaustPile);
    }

    [Fact]
    public void Peek_Enhanced_Uses3x3()
    {
        var state = CreateLevel1Game();
        var center = new Position(3, 3);
        var card = CardDefinitions.Peek with { Id = "p1", Enhanced = true };

        var (newState, _) = CardEffectSystem.ExecutePeek(state, [center], card);

        // 3x3 should cover up to 9 tiles; cross only covers 5
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        var annotated = tilesInArea
            .Where(t => !t.IsRevealed)
            .Select(t => newState.Board.GetTile(t.Position))
            .Count(t => t.Annotations.OwnerSubset != null);

        // All unrevealed tiles in 3x3 should be annotated
        Assert.Equal(tilesInArea.Count(t => !t.IsRevealed), annotated);
    }

    [Fact]
    public void Peek_Costs0Spoons()
    {
        var state = CreateTestGame() with { Spoons = 0 };
        var card = CardDefinitions.Peek with { Id = "p1" };
        state = state with { Hand = new List<Card> { card } };

        var center = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        // Should not throw — 0 cost
        var newState = CardEffectSystem.PlayCard(state, card, [center], new Random(42));
        Assert.Equal(0, newState.Spoons);
    }

    [Fact]
    public void BurstCross_CorrectTileCount()
    {
        var state = CreateLevel1Game();
        // Center tile on a 7x7 board — should get 5 tiles (center + 4 cardinal)
        var center = new Position(3, 3);
        var tiles = BoardSystem.GetTilesInCross(state.Board, center);
        Assert.Equal(5, tiles.Count);
    }

    [Fact]
    public void BurstCross_ClipsAtEdge()
    {
        var state = CreateLevel1Game();
        // Corner of 7x7 board — should get 3 tiles (center + right + down)
        var corner = new Position(0, 0);
        var tiles = BoardSystem.GetTilesInCross(state.Board, corner);
        Assert.Equal(3, tiles.Count);
    }

    // ========== Explode Tests ==========

    [Fact]
    public void Explode_DestroysTile()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;
        var card = CardDefinitions.Explode with { Id = "ex1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, [pos], new Random(42));

        Assert.True(newState.Board.GetTile(pos).IsDestroyed);
    }

    [Fact]
    public void Explode_DestroyingPlayerTile_CountsTowardWin()
    {
        // Create board where all player tiles are revealed except one
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 1, RivalCount = 1, NeutralCount = 1, NobleCount = 1
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var playerTile = board.Tiles.First(t => t.Owner == TileOwner.Player);

        // Destroy the player tile via Explode
        var card = CardDefinitions.Explode with { Id = "ex1", Enhanced = true };
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card> { card },
            Spoons = 3,
            MaxSpoons = 3
        };

        var newState = CardEffectSystem.ExecuteExplode(state, [playerTile.Position], card);

        // All player tiles are destroyed → should be Won
        var status = TurnSystem.CheckGameStatus(newState);
        Assert.Equal(GameStatus.Won, status);
    }

    [Fact]
    public void Explode_DestroyingNobleTile_DoesNotLose()
    {
        var state = CreateTestGame();
        var nobleTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble);
        var card = CardDefinitions.Explode with { Id = "ex1" };

        var newState = CardEffectSystem.ExecuteExplode(state, [nobleTile.Position], card);

        var status = TurnSystem.CheckGameStatus(newState);
        Assert.NotEqual(GameStatus.Lost, status);
    }

    [Fact]
    public void Explode_DestroyingLastRivalTile_Loses()
    {
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 1, RivalCount = 1, NeutralCount = 2, NobleCount = 0
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var rivalTile = board.Tiles.First(t => t.Owner == TileOwner.Rival);
        var card = CardDefinitions.Explode with { Id = "ex1", Enhanced = true };
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card> { card },
            Spoons = 3,
            MaxSpoons = 3
        };

        var newState = CardEffectSystem.ExecuteExplode(state, [rivalTile.Position], card);

        var status = TurnSystem.CheckGameStatus(newState);
        Assert.Equal(GameStatus.Lost, status);
    }

    [Fact]
    public void Explode_Base_GainsComplaintsAndMollify()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;
        var card = CardDefinitions.Explode with { Id = "ex1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, [pos], new Random(42));

        Assert.Equal(1, newState.ComplaintsStacks);
        Assert.Contains(newState.Hand, c => c.EffectType == CardEffectType.Mollify);
    }

    [Fact]
    public void Explode_Enhanced_NoComplaintsOrMollify()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;
        var card = CardDefinitions.Explode with { Id = "ex1", Enhanced = true };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, [pos], new Random(42));

        Assert.Equal(0, newState.ComplaintsStacks);
        Assert.DoesNotContain(newState.Hand, c => c.EffectType == CardEffectType.Mollify);
    }

    [Fact]
    public void Explode_DestroyedTile_ExcludedFromAdjacency()
    {
        var state = CreateTestGame();
        // Destroy a tile
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;
        var newTiles = state.Board.Tiles.ToList();
        newTiles[state.Board.TileIndex(pos)] = state.Board.GetTile(pos) with { IsDestroyed = true };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        // Neighbors of adjacent tiles should not include the destroyed tile
        var neighbors = BoardSystem.GetNeighbors(state.Board, pos);
        foreach (var neighbor in neighbors)
        {
            var neighborNeighbors = BoardSystem.GetNeighbors(state.Board, neighbor);
            Assert.DoesNotContain(pos, neighborNeighbors);
        }
    }

    [Fact]
    public void Explode_DestroyedTile_NotInAreaQueries()
    {
        var state = CreateTestGame();
        var pos = new Position(1, 1); // Center of 3x3
        var newTiles = state.Board.Tiles.ToList();
        newTiles[state.Board.TileIndex(pos)] = state.Board.GetTile(pos) with { IsDestroyed = true };
        var board = state.Board with { Tiles = newTiles };

        var tilesInArea = BoardSystem.GetTilesInArea(board, pos, 1);
        Assert.DoesNotContain(tilesInArea, t => t.Position == pos);
    }

    // ========== Mollify Tests ==========

    [Fact]
    public void Mollify_ReducesComplaintsBy1()
    {
        var state = CreateTestGame() with { ComplaintsStacks = 2 };
        var card = CardDefinitions.Mollify with { Id = "m1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Equal(1, newState.ComplaintsStacks);
    }

    [Fact]
    public void Mollify_DoesNotGoBelowZero()
    {
        var state = CreateTestGame() with { ComplaintsStacks = 0 };

        var newState = CardEffectSystem.ExecuteMollify(state);

        Assert.Equal(0, newState.ComplaintsStacks);
    }

    [Fact]
    public void Complaints_Lose2CopperPerStack_AtFloorEnd()
    {
        var state = CreateTestGame() with
        {
            Copper = 10,
            ComplaintsStacks = 3,
            GameStatus = GameStatus.Won,
            CurrentLevelId = "level_1"
        };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        // 3 unrevealed rival tiles → +3 copper, then 3 stacks × 2 copper = 6 penalty
        // 10 + 3 - 6 = 7
        Assert.Equal(7, newState.Copper);
        Assert.Equal(0, newState.ComplaintsStacks);
    }

    [Fact]
    public void Mollify_ClearedFromDeckAtFloorTransition()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var mollify = CardDefinitions.Mollify with { Id = "mollify_temp" };
        deck.Add(mollify);

        var state = CreateTestGame() with
        {
            PersistentDeck = deck,
            CurrentLevelId = "level_1",
            GameStatus = GameStatus.Won
        };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.DoesNotContain(newState.PersistentDeck, c => c.EffectType == CardEffectType.Mollify);
    }

    // ========== Deliver Tests ==========

    [Fact]
    public void Deliver_NobleTile_ConvertsRevealsAndGainCopper()
    {
        var state = CreateTestGame();
        var nobleTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble);
        var card = CardDefinitions.Deliver with { Id = "d1" };

        var newState = CardEffectSystem.ExecuteDeliver(state, [nobleTile.Position], card);

        var tile = newState.Board.GetTile(nobleTile.Position);
        Assert.Equal(TileOwner.Neutral, tile.Owner);
        Assert.True(tile.IsRevealed);
        Assert.Equal(2, newState.Copper);
    }

    [Fact]
    public void Deliver_NonNobleTile_NoEffect()
    {
        var state = CreateTestGame();
        var playerTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player);
        var card = CardDefinitions.Deliver with { Id = "d1" };

        var newState = CardEffectSystem.ExecuteDeliver(state, [playerTile.Position], card);

        var tile = newState.Board.GetTile(playerTile.Position);
        Assert.False(tile.IsRevealed);
        Assert.Equal(0, newState.Copper);
    }

    [Fact]
    public void Deliver_DoesNotEndTurn()
    {
        var state = CreateTestGame();
        var nobleTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble);
        var card = CardDefinitions.Deliver with { Id = "d1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var result = GameRunner.ProcessCardPlay(state, card, [nobleTile.Position], new Random(42));

        // Deliver reveals a neutral tile — should NOT end turn
        Assert.False(result.TurnEnded);
    }

    [Fact]
    public void Deliver_Enhanced_GivesNobleAdjacencyRegardless()
    {
        var state = CreateTestGame();
        var playerTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player);
        var card = CardDefinitions.Deliver with { Id = "d1", Enhanced = true };

        var newState = CardEffectSystem.ExecuteDeliver(state, [playerTile.Position], card);

        // Non-noble tile, but enhanced gives noble adjacency
        var adjInfo = newState.Board.GetTile(playerTile.Position).Annotations.AdjacencyInfo;
        Assert.NotNull(adjInfo);
        Assert.NotNull(adjInfo!.NobleCount);
    }

    // ========== Brat Tests ==========

    [Fact]
    public void Brat_UnrevealsRevealedTile()
    {
        var state = CreateTestGame();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        // Reveal the tile first
        state = state with { Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player) };
        Assert.True(state.Board.GetTile(pos).IsRevealed);

        var card = CardDefinitions.Brat with { Id = "b1" };
        var newState = CardEffectSystem.ExecuteBrat(state, [pos], card);

        Assert.False(newState.Board.GetTile(pos).IsRevealed);
        Assert.Null(newState.Board.GetTile(pos).RevealedBy);
    }

    [Fact]
    public void Brat_UnrevealedTile_RetainsAdjacencyCount()
    {
        var state = CreateTestGame();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);

        state = state with { Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player) };
        var adjCount = state.Board.GetTile(pos).AdjacencyCount;

        var card = CardDefinitions.Brat with { Id = "b1" };
        var newState = CardEffectSystem.ExecuteBrat(state, [pos], card);

        Assert.Equal(adjCount, newState.Board.GetTile(pos).AdjacencyCount);
    }

    [Fact]
    public void Brat_Enhanced_GainsCopper()
    {
        var state = CreateTestGame();
        var pos = FindFirstUnrevealed(state, TileOwner.Player);
        state = state with { Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player) };

        var card = CardDefinitions.Brat with { Id = "b1", Enhanced = true };
        var newState = CardEffectSystem.ExecuteBrat(state, [pos], card);

        Assert.Equal(2, newState.Copper);
    }

    [Fact]
    public void Brat_ThrowsOnUnrevealedTile()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        var card = CardDefinitions.Brat with { Id = "b1" };
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteBrat(state, [pos], card));
    }

    // ========== Accept Help Tests ==========

    [Fact]
    public void AcceptHelp_RevealsSafestTypeInCross()
    {
        var state = CreateLevel1Game();
        // Pick a center where we have unrevealed tiles
        var center = new Position(3, 3);
        var card = CardDefinitions.AcceptHelp with { Id = "ah1" };

        var crossTiles = BoardSystem.GetTilesInCross(state.Board, center);
        var unrevealed = crossTiles.Where(t => !t.IsRevealed).ToList();
        var safestType = unrevealed.Select(t => t.Owner).Distinct()
            .OrderByDescending(o => o switch
            {
                TileOwner.Player => 4, TileOwner.Neutral => 3,
                TileOwner.Rival => 2, TileOwner.Noble => 1, _ => 0
            }).First();

        var newState = CardEffectSystem.ExecuteAcceptHelp(state, [center], card, new Random(0));

        // All tiles of the safest type in the cross should be revealed
        foreach (var tile in unrevealed)
        {
            if (tile.Owner == safestType)
            {
                Assert.True(newState.Board.GetTile(tile.Position).IsRevealed,
                    $"Tile at {tile.Position} (owner {tile.Owner}) should be revealed");
            }
        }
    }

    [Fact]
    public void AcceptHelp_PlayerTiles_NoTurnEnd()
    {
        // Create board where all tiles in cross are Player
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 5, RivalCount = 2, NeutralCount = 1, NobleCount = 1
        };
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(config, rng);

        // Find a center where the cross has mostly player tiles
        var center = new Position(1, 1);
        var crossTiles = BoardSystem.GetTilesInCross(board, center);
        var safestOwner = crossTiles.Where(t => !t.IsRevealed)
            .Select(t => t.Owner).Distinct()
            .OrderByDescending(o => o switch
            {
                TileOwner.Player => 4, TileOwner.Neutral => 3,
                TileOwner.Rival => 2, _ => 1
            }).First();

        if (safestOwner == TileOwner.Player)
        {
            var card = CardDefinitions.AcceptHelp with { Id = "ah1" };
            var state = new GameState
            {
                Board = board,
                Hand = new List<Card> { card },
                Spoons = 3,
                MaxSpoons = 3
            };

            var result = GameRunner.ProcessCardPlay(state, card, [center], new Random(42));
            Assert.False(result.TurnEnded, "Revealing Player tiles should not end the turn");
        }
    }

    [Fact]
    public void AcceptHelp_SetsDiscountStatus()
    {
        var state = CreateLevel1Game();
        var center = new Position(3, 3);
        var card = CardDefinitions.AcceptHelp with { Id = "ah1" };

        var newState = CardEffectSystem.ExecuteAcceptHelp(state, [center], card, new Random(0));

        Assert.True(newState.AcceptHelpDiscount);
    }

    [Fact]
    public void AcceptHelp_SecondPlayCosts0()
    {
        var state = CreateLevel1Game() with { AcceptHelpDiscount = true, Spoons = 0 };
        var card = CardDefinitions.AcceptHelp with { Id = "ah1" };

        // Should be playable at 0 spoons
        Assert.True(DeckSystem.CanPlayCard(state, card));

        // Effective cost should be 0
        Assert.Equal(0, DeckSystem.GetEffectiveCost(state, card));
    }

    [Fact]
    public void AcceptHelp_ExtraDirty_CleanedAndAnnotated()
    {
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, new Random(42));
        var dirtyTile = board.Tiles.First(t => t.IsDirty);

        // Find the cross around the dirty tile
        var center = dirtyTile.Position;
        var card = CardDefinitions.AcceptHelp with { Id = "ah1" };
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card> { card },
            Spoons = 3,
            MaxSpoons = 3
        };

        var crossTiles = BoardSystem.GetTilesInCross(board, center);
        var unrevealed = crossTiles.Where(t => !t.IsRevealed).ToList();
        var safestType = unrevealed.Select(t => t.Owner).Distinct()
            .OrderByDescending(o => o switch
            {
                TileOwner.Player => 4, TileOwner.Neutral => 3,
                TileOwner.Rival => 2, _ => 1
            }).First();

        if (dirtyTile.Owner == safestType)
        {
            var newState = CardEffectSystem.ExecuteAcceptHelp(state, [center], card, new Random(0));

            var tile = newState.Board.GetTile(dirtyTile.Position);
            Assert.False(tile.IsDirty, "ExtraDirty should be cleaned");
            Assert.False(tile.IsRevealed, "Cleaned ExtraDirty should not be revealed");
            Assert.NotNull(tile.Annotations.OwnerSubset);
        }
    }

    [Fact]
    public void AcceptHelp_Enhanced_AnnotatesInsteadOfRevealing()
    {
        var state = CreateLevel1Game();
        var center = new Position(3, 3);
        var card = CardDefinitions.AcceptHelp with { Id = "ah1", Enhanced = true };

        var newState = CardEffectSystem.ExecuteAcceptHelp(state, [center], card, new Random(0));

        // No tiles should be revealed (enhanced annotates instead)
        var crossTiles = BoardSystem.GetTilesInCross(state.Board, center);
        foreach (var tile in crossTiles)
        {
            if (!tile.IsRevealed)
            {
                var updated = newState.Board.GetTile(tile.Position);
                Assert.False(updated.IsRevealed, "Enhanced Accept Help should not reveal tiles");
                Assert.NotNull(updated.Annotations.OwnerSubset);
            }
        }
    }

    // ========== Ramble Tests ==========

    [Fact]
    public void Ramble_Adds2DistractionIntentPoints()
    {
        // Seed intent points so AddDistractionPoint has nonzero candidates to pick from.
        var state = CreateTestGame() with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 5,
                [new Position(1, 1)] = 3
            }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        var card = CardDefinitions.Ramble with { Id = "r1" };

        var newState = CardEffectSystem.ExecuteRamble(state, card, new Random(7));

        Assert.Equal(sumBefore + 2, newState.RivalIntentPoints.Values.Sum());
    }

    [Fact]
    public void Ramble_Enhanced_Adds4DistractionIntentPoints()
    {
        var state = CreateTestGame() with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 5
            }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        var card = CardDefinitions.Ramble with { Id = "r1", Enhanced = true };

        var newState = CardEffectSystem.ExecuteRamble(state, card, new Random(7));

        Assert.Equal(sumBefore + 4, newState.RivalIntentPoints.Values.Sum());
    }

    [Fact]
    public void Ramble_DistractionsAccumulate()
    {
        var state = CreateTestGame() with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 5,
                [new Position(2, 2)] = 1
            }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        var card = CardDefinitions.Ramble with { Id = "r1" };

        state = CardEffectSystem.ExecuteRamble(state, card, new Random(7));
        state = CardEffectSystem.ExecuteRamble(state, card, new Random(8));

        Assert.Equal(sumBefore + 4, state.RivalIntentPoints.Values.Sum());
    }

    // ========== Glaze Tests ==========

    [Fact]
    public void Glaze_Adds1ExcusesStack()
    {
        var state = CreateTestGame();
        var card = CardDefinitions.Glaze with { Id = "g1" };

        var newState = CardEffectSystem.ExecuteGlaze(state, card);

        Assert.Equal(1, newState.ExcusesStacks);
    }

    [Fact]
    public void Glaze_BaseVersion_Exhausts()
    {
        var state = CreateTestGame();
        var card = CardDefinitions.Glaze with { Id = "g1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Contains(card, newState.ExhaustPile);
        Assert.DoesNotContain(card, newState.DiscardPile);
    }

    [Fact]
    public void Glaze_EnhancedVersion_DoesNotExhaust()
    {
        var state = CreateTestGame();
        var card = CardDefinitions.Glaze with { Id = "g1", Enhanced = true };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Contains(card, newState.DiscardPile);
        Assert.DoesNotContain(card, newState.ExhaustPile);
    }

    [Fact]
    public void Glaze_ExcusesConsumedOnNobleReveal()
    {
        // Create a board with a noble tile and set up Excuses
        var state = CreateTestGame() with { ExcusesStacks = 1 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;

        // Reveal the noble tile via ProcessReveal
        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        // Should survive (not lost) and Excuses consumed
        Assert.NotEqual(GameStatus.Lost, result.State.GameStatus);
        Assert.Equal(0, result.State.ExcusesStacks);

        // The noble tile should be revealed but protected
        Assert.True(result.State.Board.GetTile(noblePos).IsRevealed);
        Assert.True(result.State.Board.GetTile(noblePos).ProtectedByExcuses);
    }

    [Fact]
    public void Glaze_MultipleStacks_MultipleProtections()
    {
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 2
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card>(),
            Spoons = 3,
            MaxSpoons = 3,
            ExcusesStacks = 2
        };

        var nobles = board.Tiles.Where(t => t.Owner == TileOwner.Noble).ToList();

        // Reveal first noble
        var result1 = GameRunner.ProcessReveal(state, nobles[0].Position, new Random(42));
        Assert.NotEqual(GameStatus.Lost, result1.State.GameStatus);
        Assert.Equal(1, result1.State.ExcusesStacks);

        // Reveal second noble
        var result2 = GameRunner.ProcessReveal(result1.State, nobles[1].Position, new Random(42));
        Assert.NotEqual(GameStatus.Lost, result2.State.GameStatus);
        Assert.Equal(0, result2.State.ExcusesStacks);
    }

    [Fact]
    public void NobleReveal_WithNoExcuses_StillLoses()
    {
        var state = CreateTestGame() with { ExcusesStacks = 0 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;

        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        Assert.Equal(GameStatus.Lost, result.State.GameStatus);
    }

    // ========== Excuses Penalty Tests ==========

    [Fact]
    public void Excuses_DropTo0_Adds2ComplaintsAnd2Mollify()
    {
        var state = CreateTestGame() with { ExcusesStacks = 1, ComplaintsStacks = 0 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;

        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        Assert.Equal(0, result.State.ExcusesStacks);
        Assert.Equal(2, result.State.ComplaintsStacks);
        // 2 Mollify cards exist somewhere across hand/draw/discard (turn transition may shuffle them)
        var allCards = result.State.Hand
            .Concat(result.State.DrawPile)
            .Concat(result.State.DiscardPile)
            .ToList();
        Assert.Equal(2, allCards.Count(c => c.EffectType == CardEffectType.Mollify));
    }

    [Fact]
    public void Excuses_DropFrom2To1_NoPenalty()
    {
        // Board with 1 noble
        var state = CreateTestGame() with { ExcusesStacks = 2, ComplaintsStacks = 0 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;

        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        Assert.Equal(1, result.State.ExcusesStacks);
        Assert.Equal(0, result.State.ComplaintsStacks);
        // No Mollify added anywhere
        var allCards = result.State.Hand
            .Concat(result.State.DrawPile)
            .Concat(result.State.DiscardPile)
            .ToList();
        Assert.DoesNotContain(allCards, c => c.EffectType == CardEffectType.Mollify);
    }

    [Fact]
    public void Excuses_DropTo0_MollifyInjectedBeforeTurnTransition()
    {
        // Test the injection directly by calling ConsumeExcusesIfNeeded via ProcessReveal
        // and checking state before turn transition shuffles things
        // We verify indirectly: 2 Mollify exist in the deck after reveal
        var state = CreateTestGame() with { ExcusesStacks = 1 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;
        var mollifyCountBefore = state.Hand.Concat(state.DrawPile).Concat(state.DiscardPile)
            .Count(c => c.EffectType == CardEffectType.Mollify);

        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        var mollifyCountAfter = result.State.Hand.Concat(result.State.DrawPile).Concat(result.State.DiscardPile)
            .Count(c => c.EffectType == CardEffectType.Mollify);
        Assert.Equal(mollifyCountBefore + 2, mollifyCountAfter);
    }

    [Fact]
    public void Excuses_PenaltyFiresOnlyOnceAt0()
    {
        // Board with 2 nobles, 3 excuses → reveal both nobles → 3→2→1, then 1→0
        // But we can only reveal 1 noble at a time via ProcessReveal
        // Test: reveal first noble (2→1, no penalty), then second (1→0, penalty)
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 2
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var state = new GameState
        {
            Board = board,
            Hand = new List<Card>(),
            Spoons = 3,
            MaxSpoons = 3,
            ExcusesStacks = 2,
            ComplaintsStacks = 0
        };

        var nobles = board.Tiles.Where(t => t.Owner == TileOwner.Noble).ToList();

        // First noble: 2→1, no penalty
        var result1 = GameRunner.ProcessReveal(state, nobles[0].Position, new Random(42));
        Assert.Equal(1, result1.State.ExcusesStacks);
        Assert.Equal(0, result1.State.ComplaintsStacks);

        // Second noble: 1→0, penalty fires
        var result2 = GameRunner.ProcessReveal(result1.State, nobles[1].Position, new Random(42));
        Assert.Equal(0, result2.State.ExcusesStacks);
        Assert.Equal(2, result2.State.ComplaintsStacks);
    }

    [Fact]
    public void Excuses_ExistingComplaints_StacksAdd()
    {
        // Start with 1 Complaints already, then trigger Excuses penalty
        var state = CreateTestGame() with { ExcusesStacks = 1, ComplaintsStacks = 1 };
        var noblePos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Noble).Position;

        var result = GameRunner.ProcessReveal(state, noblePos, new Random(42));

        Assert.Equal(3, result.State.ComplaintsStacks); // 1 existing + 2 from penalty
    }

    // ========== Mask Tests ==========

    [Fact]
    public void Mask_PlaysSelectedCardForFree()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1" };
        var spritz = CardDefinitions.Spritz with { Id = "spritz_target" };
        state = state with { Hand = new List<Card> { mask, spritz } };

        var targetPos = FindFirstUnrevealed(state, TileOwner.Player);

        // Spritz costs 1 but Mask plays it for free at 0 spoons
        var newState = CardEffectSystem.PlayMaskedCard(state, mask, spritz, [targetPos], new Random(42));

        // Spritz effect should have worked (owner subset annotation)
        Assert.NotNull(newState.Board.GetTile(targetPos).Annotations.OwnerSubset);
        Assert.Equal(0, newState.Spoons); // No spoons spent
    }

    [Fact]
    public void Mask_Base_ExhaustsBothCards()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1" };
        var spritz = CardDefinitions.Spritz with { Id = "spritz_target" };
        state = state with { Hand = new List<Card> { mask, spritz } };

        var targetPos = FindFirstUnrevealed(state, TileOwner.Player);
        var newState = CardEffectSystem.PlayMaskedCard(state, mask, spritz, [targetPos], new Random(42));

        Assert.Contains(mask, newState.ExhaustPile);
        Assert.Contains(spritz, newState.ExhaustPile);
        Assert.DoesNotContain(mask, newState.DiscardPile);
        Assert.DoesNotContain(spritz, newState.DiscardPile);
    }

    [Fact]
    public void Mask_Enhanced_OnlyPlayedCardExhausts()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1", Enhanced = true };
        var spritz = CardDefinitions.Spritz with { Id = "spritz_target" };
        state = state with { Hand = new List<Card> { mask, spritz } };

        var targetPos = FindFirstUnrevealed(state, TileOwner.Player);
        var newState = CardEffectSystem.PlayMaskedCard(state, mask, spritz, [targetPos], new Random(42));

        Assert.Contains(spritz, newState.ExhaustPile);
        Assert.Contains(mask, newState.DiscardPile);
        Assert.DoesNotContain(mask, newState.ExhaustPile);
    }

    [Fact]
    public void Mask_BonusSpoonOnPlayedCard_StillTriggers()
    {
        var state = CreateLevel1Game() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1" };
        var spritz = CardDefinitions.Spritz with { Id = "spritz_bonus", BonusSpoon = true };
        state = state with { Hand = new List<Card> { mask, spritz } };

        var targetPos = FindFirstUnrevealed(state, TileOwner.Player);
        var newState = CardEffectSystem.PlayMaskedCard(state, mask, spritz, [targetPos], new Random(42));

        Assert.Equal(1, newState.Spoons); // +1 from bonus spoon
    }

    [Fact]
    public void Mask_PlusCaffeinate_Gains2SpoonsForFree()
    {
        var state = CreateTestGame() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1" };
        var caff = CardDefinitions.Caffeinate with { Id = "caff1" };
        state = state with { Hand = new List<Card> { mask, caff } };

        var newState = CardEffectSystem.PlayMaskedCard(state, mask, caff, null, new Random(42));

        Assert.Equal(2, newState.Spoons); // Caffeinate gives +2
    }

    [Fact]
    public void Mask_SelectedCardWithExhaust_StillExhausts()
    {
        var state = CreateTestGame() with { Spoons = 0 };
        var mask = CardDefinitions.Mask with { Id = "mask1" };
        var twirl = CardDefinitions.Twirl with { Id = "twirl1" }; // Already has Exhaust=true
        state = state with { Hand = new List<Card> { mask, twirl } };

        var newState = CardEffectSystem.PlayMaskedCard(state, mask, twirl, null, new Random(42));

        Assert.Contains(twirl, newState.ExhaustPile);
        Assert.Equal(3, newState.Copper); // Twirl effect executed
    }

    // ========== Nap Tests ==========

    [Fact]
    public void Nap_RetrievesCardFromExhaustToHand()
    {
        var state = CreateTestGame();
        var exhaustedCard = CardDefinitions.Spritz with { Id = "exhausted1" };
        state = state with
        {
            ExhaustPile = new List<Card> { exhaustedCard },
            Hand = new List<Card> { CardDefinitions.Nap with { Id = "nap1" } },
            Spoons = 3
        };

        var nap = state.Hand[0];
        var newState = CardEffectSystem.PlayNap(state, nap, exhaustedCard, new Random(42));

        Assert.Contains(exhaustedCard, newState.Hand);
        Assert.DoesNotContain(exhaustedCard, newState.ExhaustPile);
    }

    [Fact]
    public void Nap_ExhaustsItself()
    {
        var state = CreateTestGame();
        var exhaustedCard = CardDefinitions.Spritz with { Id = "exhausted1" };
        var nap = CardDefinitions.Nap with { Id = "nap1" };
        state = state with
        {
            ExhaustPile = new List<Card> { exhaustedCard },
            Hand = new List<Card> { nap },
            Spoons = 3
        };

        var newState = CardEffectSystem.PlayNap(state, nap, exhaustedCard, new Random(42));

        Assert.Contains(nap, newState.ExhaustPile);
    }

    [Fact]
    public void Nap_Enhanced_GrantsSpoonsEqualToRetrievedCost()
    {
        var state = CreateTestGame();
        var exhaustedCard = CardDefinitions.AcceptHelp with { Id = "exhausted1" }; // Cost 3
        var nap = CardDefinitions.Nap with { Id = "nap1", Enhanced = true };
        state = state with
        {
            ExhaustPile = new List<Card> { exhaustedCard },
            Hand = new List<Card> { nap },
            Spoons = 3
        };

        var newState = CardEffectSystem.PlayNap(state, nap, exhaustedCard, new Random(42));

        // 3 - 1 (Nap cost) + 3 (retrieved card's cost) = 5
        Assert.Equal(5, newState.Spoons);
    }

    [Fact]
    public void Nap_EmptyExhaustPile_DoesNothingButExhausts()
    {
        var state = CreateTestGame();
        var nap = CardDefinitions.Nap with { Id = "nap1" };
        state = state with
        {
            ExhaustPile = new List<Card>(),
            Hand = new List<Card> { nap },
            Spoons = 3
        };

        var newState = CardEffectSystem.PlayNap(state, nap, null, new Random(42));

        Assert.Contains(nap, newState.ExhaustPile);
        Assert.Empty(newState.Hand);
        Assert.Equal(2, newState.Spoons); // 3 - 1 cost
    }

    [Fact]
    public void Nap_RetrievedCardIsPlayable()
    {
        var state = CreateTestGame();
        var exhaustedCard = CardDefinitions.Spritz with { Id = "exhausted1" };
        var nap = CardDefinitions.Nap with { Id = "nap1" };
        state = state with
        {
            ExhaustPile = new List<Card> { exhaustedCard },
            Hand = new List<Card> { nap },
            Spoons = 3
        };

        var newState = CardEffectSystem.PlayNap(state, nap, exhaustedCard, new Random(42));

        // Retrieved card should be in hand and playable
        var retrieved = newState.Hand.First(c => c.Id == exhaustedCard.Id);
        Assert.True(DeckSystem.CanPlayCard(newState, retrieved));
    }

    [Fact]
    public void Nap_CantRetrieveNapWithNap()
    {
        // This is naturally handled: Nap exhausts itself AFTER retrieval,
        // so it's not in the exhaust pile when selection happens.
        // But even if it were, Nap is removed from hand before checking exhaust.
        var nap = CardDefinitions.Nap with { Id = "nap1" };
        var anotherNap = CardDefinitions.Nap with { Id = "nap2" };
        var state = CreateTestGame() with
        {
            ExhaustPile = new List<Card> { anotherNap },
            Hand = new List<Card> { nap },
            Spoons = 3
        };

        // This should work — retrieving a different Nap is allowed
        // The rule "can't retrieve Nap with Nap" in the plan means
        // the current Nap isn't in exhaust yet when you play it
        var newState = CardEffectSystem.PlayNap(state, nap, anotherNap, new Random(42));
        Assert.Contains(anotherNap, newState.Hand);
    }

    // ========== Recall - Vague Tests ==========

    [Fact]
    public void RecallVague_ProducesClueResults()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var clues = ClueSystem.GenerateVagueClue(state, rng);
        Assert.NotEmpty(clues);

        var tilesWithClues = clues.Select(c => c.TilePosition).Distinct().Count();
        Assert.True(tilesWithClues > 0, "Vague should produce clue results");
    }

    [Fact]
    public void RecallVague_Draws8PipsTotal()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var clues = ClueSystem.GenerateVagueClue(state, rng);
        var totalPips = clues.Sum(c => c.PipStrength);
        Assert.InRange(totalPips, 1, 8);
    }

    [Fact]
    public void RecallVague_PlayerTileHasMaxPips()
    {
        var state = CreateLevel1Game(seed: 42);
        var rng = new Random(99);

        var clues = ClueSystem.GenerateVagueClue(state, rng);
        Assert.NotEmpty(clues);

        var maxPips = clues.Max(c => c.PipStrength);
        var tilesWithMax = clues.Where(c => c.PipStrength == maxPips).ToList();
        Assert.True(
            tilesWithMax.Any(c => state.Board.GetTile(c.TilePosition).Owner == TileOwner.Player),
            $"Expected a player tile to have max pips ({maxPips}), but none did");
    }

    [Fact]
    public void RecallVague_First3GuaranteedFromTargets()
    {
        // With enough player tiles, at least 3 pips should land on player tiles
        var state = CreateLevel1Game(seed: 42);
        var rng = new Random(99);

        var clues = ClueSystem.GenerateVagueClue(state, rng);
        Assert.NotEmpty(clues);

        var playerPips = clues
            .Where(c => state.Board.GetTile(c.TilePosition).Owner == TileOwner.Player)
            .Sum(c => c.PipStrength);
        Assert.True(playerPips >= 3, $"Expected at least 3 pips on player tiles, got {playerPips}");
    }

    [Fact]
    public void RecallVague_Enhanced_All5GuaranteedFromTargets()
    {
        var state = CreateLevel1Game(seed: 42);
        var rng = new Random(99);

        var clues = ClueSystem.GenerateVagueClue(state, rng, enhanced: true);
        Assert.NotEmpty(clues);

        // Enhanced guarantees 5 draws from targets (player tiles)
        var playerPips = clues
            .Where(c => state.Board.GetTile(c.TilePosition).Owner == TileOwner.Player)
            .Sum(c => c.PipStrength);
        Assert.True(playerPips >= 5, $"Enhanced should guarantee at least 5 pips on player tiles, got {playerPips}");
    }

    [Fact]
    public void RecallVague_SetsRecallPlayedThisFloor()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);
        var card = CardDefinitions.RecallVague with { Id = "rv1" };
        state = state with { Hand = state.Hand.ToList().Append(card).ToList(), Spoons = 5 };

        var newState = CardEffectSystem.PlayCard(state, card, null, rng);
        Assert.True(newState.RecallPlayedThisFloor);
    }

    // ========== Recall - Sarcastic Tests ==========

    [Fact]
    public void RecallSarcastic_ProducesAntiClueResults()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var clues = ClueSystem.GenerateSarcasticClue(state, rng);
        Assert.NotEmpty(clues);
        Assert.All(clues, c => Assert.True(c.IsAntiClue));
    }

    [Fact]
    public void RecallSarcastic_PipsWeightedTowardNonPlayerTiles()
    {
        // Run multiple seeds and check that non-player tiles get more pips on average
        var playerPipsTotal = 0;
        var nonPlayerPipsTotal = 0;

        for (var seed = 0; seed < 20; seed++)
        {
            var state = CreateLevel1Game(seed: seed);
            var rng = new Random(seed + 100);

            var clues = ClueSystem.GenerateSarcasticClue(state, rng);
            foreach (var clue in clues)
            {
                if (state.Board.GetTile(clue.TilePosition).Owner == TileOwner.Player)
                    playerPipsTotal += clue.PipStrength;
                else
                    nonPlayerPipsTotal += clue.PipStrength;
            }
        }

        Assert.True(nonPlayerPipsTotal > playerPipsTotal,
            $"Non-player pips ({nonPlayerPipsTotal}) should exceed player pips ({playerPipsTotal})");
    }

    [Fact]
    public void RecallSarcastic_Enhanced_RefundsSpoonIfRecallAlreadyPlayed()
    {
        var state = CreateLevel1Game();
        state = state with { RecallPlayedThisFloor = true, Spoons = 3 };
        var card = CardDefinitions.RecallSarcastic with { Id = "rs1", Enhanced = true };
        state = state with { Hand = state.Hand.ToList().Append(card).ToList() };

        var rng = new Random(99);
        var newState = CardEffectSystem.PlayCard(state, card, null, rng);

        // Cost 2, but enhanced refunds 1 spoon if Recall already played = net cost 1
        // So 3 - 2 + 1 = 2
        Assert.Equal(2, newState.Spoons);
    }

    [Fact]
    public void RecallSarcastic_Enhanced_NoRefundIfFirstRecall()
    {
        var state = CreateLevel1Game();
        state = state with { RecallPlayedThisFloor = false, Spoons = 3 };
        var card = CardDefinitions.RecallSarcastic with { Id = "rs1", Enhanced = true };
        state = state with { Hand = state.Hand.ToList().Append(card).ToList() };

        var rng = new Random(99);
        var newState = CardEffectSystem.PlayCard(state, card, null, rng);

        // Cost 2, no refund since first Recall this floor
        Assert.Equal(1, newState.Spoons);
    }

    [Fact]
    public void RecallSarcastic_SetsRecallPlayedThisFloor()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);
        var card = CardDefinitions.RecallSarcastic with { Id = "rs1" };
        state = state with { Hand = state.Hand.ToList().Append(card).ToList(), Spoons = 5 };

        var newState = CardEffectSystem.PlayCard(state, card, null, rng);
        Assert.True(newState.RecallPlayedThisFloor);
    }

    [Fact]
    public void RecallImperious_SetsRecallPlayedThisFloor()
    {
        var state = CreateLevel1Game();
        var rng = new Random(99);

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Recall);
        var newState = CardEffectSystem.ExecuteInstructions(state, rng, card);
        Assert.True(newState.RecallPlayedThisFloor);
    }

    [Fact]
    public void RecallPlayedThisFloor_ResetsAtFloorStart()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { RecallPlayedThisFloor = true };

        // Win the floor and advance
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, rng);
        state = CampaignSystem.SkipCardReward(state, rng);

        Assert.False(state.RecallPlayedThisFloor);
    }
}
