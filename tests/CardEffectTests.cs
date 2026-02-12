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
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, MineCount = 1
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

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
        var newState = CardEffectSystem.ExecuteSpritz(state, [playerPos], spritz);

        var annotation = newState.Board.GetTile(playerPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Contains(TileOwner.Player, annotation);
        Assert.Contains(TileOwner.Neutral, annotation);
        Assert.DoesNotContain(TileOwner.Rival, annotation);
        Assert.DoesNotContain(TileOwner.Mine, annotation);
    }

    [Fact]
    public void Spritz_NeutralTileAlsoAnnotatedAsSafe()
    {
        var state = CreateLevel1Game();
        var neutralPos = FindFirstUnrevealed(state, TileOwner.Neutral);

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
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

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
        var newState = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz);

        var annotation = newState.Board.GetTile(rivalPos).Annotations.OwnerSubset;
        Assert.NotNull(annotation);
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Mine }, annotation);
    }

    [Fact]
    public void Spritz_MultipleSpritzIntersectsSubsets()
    {
        var state = CreateTestGame();
        // Find a rival tile
        var rivalPos = FindFirstUnrevealed(state, TileOwner.Rival);

        // First Spritz: dangerous → {Rival, Mine}
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
        state = CardEffectSystem.ExecuteSpritz(state, [rivalPos], spritz);

        // Tingle the same tile to narrow to {Rival}
        var exactRival = new HashSet<TileOwner> { TileOwner.Rival };
        state = AnnotationSystem.AddOwnerSubset(state, rivalPos, exactRival);

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

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteSpritz(state, [pos], spritz));
    }

    [Fact]
    public void Spritz_ThrowsOnWrongTargetCount()
    {
        var state = CreateLevel1Game();
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);

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

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Instructions);
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

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Instructions);
        var newState = CardEffectSystem.ExecuteInstructions(state, rng, card);

        var tilesWithClues = newState.Board.Tiles.Count(t => t.Annotations.ClueResults.Count > 0);
        // 2 targets + 6 spoilers = at most 8 tiles, but some bag entries may not get drawn
        Assert.InRange(tilesWithClues, 1, 8);
    }

    [Fact]
    public void Instructions_PlayerTilesTendToHaveHighestPips()
    {
        // Statistical test: over many runs, player tiles should have max pips most of the time
        var playerHasMax = 0;
        var trials = 100;

        for (var i = 0; i < trials; i++)
        {
            var state = CreateLevel1Game(seed: i);
            var rng = new Random(i * 1000);

            var clues = ClueSystem.GenerateImperiousClue(state, rng);
            if (clues.Count == 0) continue;

            var maxPips = clues.Max(c => c.PipStrength);
            var maxPositions = clues.Where(c => c.PipStrength == maxPips).Select(c => c.TilePosition);

            if (maxPositions.Any(p => state.Board.GetTile(p).Owner == TileOwner.Player))
                playerHasMax++;
        }

        // Due to validation, player should have max pips in nearly all valid trials
        Assert.True(playerHasMax >= trials * 0.8,
            $"Player tiles had max pips in {playerHasMax}/{trials} trials, expected >= 80%");
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
        Assert.Contains(TileOwner.Mine, annotation);
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
            tile.Owner == TileOwner.Rival || tile.Owner == TileOwner.Mine,
            "Tingle should only target rival or mine tiles");
        Assert.Contains(tile.Owner, tile.Annotations.OwnerSubset!);
    }

    [Fact]
    public void Tingle_PrefersAmbiguousTiles()
    {
        var state = CreateTestGame();
        var rng = new Random(42);

        // Mark one rival tile as already known
        var firstRival = FindFirstUnrevealed(state, TileOwner.Rival);
        state = AnnotationSystem.AddOwnerSubset(state, firstRival, new HashSet<TileOwner> { TileOwner.Rival });

        var card = state.Hand.First(c => c.EffectType == CardEffectType.Tingle);

        // Run many times to check preference
        var hitAlreadyKnown = 0;
        for (var i = 0; i < 50; i++)
        {
            var newState = CardEffectSystem.ExecuteTingle(state, new Random(i), card);

            // Check if the already-known tile was changed (it shouldn't be targeted when ambiguous tiles exist)
            var newAnnotated = newState.Board.Tiles
                .Where(t => t.Annotations.OwnerSubset?.Count == 1 && t.Position != firstRival)
                .ToList();

            // If there are other candidates with single-owner now, Tingle targeted a new tile
            if (newAnnotated.Any())
                continue;

            hitAlreadyKnown++;
        }

        // Should rarely hit the already-known tile when ambiguous alternatives exist
        Assert.True(hitAlreadyKnown < 25,
            $"Tingle hit already-known tile {hitAlreadyKnown}/50 times, should prefer ambiguous");
    }

    [Fact]
    public void Tingle_NoTargetsReturnsUnchangedState()
    {
        // Board with only player and neutral tiles
        var config = new LevelConfig
        {
            Width = 2, Height = 2,
            PlayerCount = 2, RivalCount = 0, NeutralCount = 2, MineCount = 0
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
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
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
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Scout);
        var playerPos = FindFirstUnrevealed(state, TileOwner.Player);

        Assert.Throws<InvalidOperationException>(() =>
            CardEffectSystem.PlayCard(state, spritz, [playerPos], new Random(42)));
    }
}
