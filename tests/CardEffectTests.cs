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
            Energy = 3,
            MaxEnergy = 3
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
            Energy = 3,
            MaxEnergy = 3
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
            Energy = 3
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
    public void PlayCard_DeductsEnergyAndDiscardsCard()
    {
        var state = CreateLevel1Game();
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);

        var newState = CardEffectSystem.PlayCard(state, spritz, [playerPos], new Random(42));

        Assert.Equal(2, newState.Energy); // 3 - 1 cost
        Assert.DoesNotContain(spritz, newState.Hand);
        Assert.Contains(spritz, newState.DiscardPile);
    }

    [Fact]
    public void PlayCard_ExhaustCardGoesToExhaustPile()
    {
        var state = CreateLevel1Game() with { Energy = 3 };
        var twirl = CardDefinitions.Twirl with { Id = "tw_test" };
        state = state with { Hand = state.Hand.ToList().Append(twirl).ToList() };

        var newState = CardEffectSystem.PlayCard(state, twirl, null, new Random(42));

        Assert.Contains(twirl, newState.ExhaustPile);
        Assert.DoesNotContain(twirl, newState.DiscardPile);
    }

    [Fact]
    public void PlayCard_ThrowsWhenInsufficientEnergy()
    {
        var state = CreateLevel1Game() with { Energy = 0 };
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
            Energy = 3
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
            Energy = 1
        };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Empty(newState.Hand);
    }
}
