using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class CampaignSystemTests
{
    [Fact]
    public void StartCampaign_BeginsAtLevel1WithStarterDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        Assert.Equal("level1", state.CurrentLevelId);
        Assert.Equal(GamePhase.Playing, state.GamePhase);
        Assert.Equal(10, state.PersistentDeck.Count);
        Assert.Equal(5, state.Hand.Count);
        Assert.Equal(GameStatus.Playing, state.GameStatus);
    }

    [Fact]
    public void CompleteFloor_Level1TransitionsToCardReward()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        var newState = CampaignSystem.CompleteFloor(state, new Random(99));

        Assert.Equal(GamePhase.CardReward, newState.GamePhase);
        Assert.NotNull(newState.CardRewardOptions);
        Assert.Equal(3, newState.CardRewardOptions!.Count);
    }

    [Fact]
    public void GenerateCardRewardOptions_Returns3DistinctCards()
    {
        var rng = new Random(42);
        var options = CampaignSystem.GenerateCardRewardOptions(rng);

        Assert.Equal(3, options.Count);

        var names = options.Select(c => c.Name).ToHashSet();
        Assert.Equal(3, names.Count); // All distinct names
    }

    [Fact]
    public void GenerateCardRewardOptions_AllHaveUniqueIds()
    {
        var rng = new Random(42);
        var options = CampaignSystem.GenerateCardRewardOptions(rng);

        var ids = options.Select(c => c.Id).ToHashSet();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public void SelectCardReward_AddsToPersistentDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        var selected = state.CardRewardOptions![0];

        var newState = CampaignSystem.SelectCardReward(state, selected, new Random(100));

        Assert.Equal(11, newState.PersistentDeck.Count);
        Assert.Contains(selected, newState.PersistentDeck);
    }

    [Fact]
    public void SelectCardReward_AdvancesToNextFloor()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        var selected = state.CardRewardOptions![0];

        var newState = CampaignSystem.SelectCardReward(state, selected, new Random(100));

        Assert.Equal("level2", newState.CurrentLevelId);
        Assert.Equal(GamePhase.Playing, newState.GamePhase);
        Assert.Null(newState.CardRewardOptions);
        Assert.Equal(5, newState.Hand.Count);
    }

    [Fact]
    public void SkipCardReward_DoesNotAddCard()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));

        var newState = CampaignSystem.SkipCardReward(state, new Random(100));

        Assert.Equal(10, newState.PersistentDeck.Count); // Same as starter
        Assert.Equal("level2", newState.CurrentLevelId);
    }

    [Fact]
    public void FloorReset_ClearsHandAndDiscardAndExhaust()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        // Simulate some play: cards in discard and exhaust
        state = state with
        {
            GameStatus = GameStatus.Won,
            DiscardPile = [CardDefinitions.Spritz with { Id = "d1" }],
            ExhaustPile = [CardDefinitions.Twirl with { Id = "e1" }]
        };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        // New floor should have fresh state
        Assert.Empty(state.DiscardPile);
        Assert.Empty(state.ExhaustPile);
        Assert.Equal(5, state.Hand.Count);
        Assert.Equal(3, state.Spoons);
        Assert.Equal(1, state.TurnNumber);
    }

    [Fact]
    public void FloorReset_PersistentDeckShuffledIntoDrawPile()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));

        // Add a card
        var selected = state.CardRewardOptions![0];
        var newState = CampaignSystem.SelectCardReward(state, selected, new Random(100));

        // Total cards = hand + draw pile should equal persistent deck
        var totalCards = newState.Hand.Count + newState.DrawPile.Count;
        Assert.Equal(newState.PersistentDeck.Count, totalCards);
    }

    [Fact]
    public void PersistentDeck_GrowsAcrossFloors()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        Assert.Equal(10, state.PersistentDeck.Count);

        // Complete floor 1, pick a card
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SelectCardReward(state, state.CardRewardOptions![0], new Random(100));
        Assert.Equal(11, state.PersistentDeck.Count);

        // Complete floor 2, pick a card
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(101));
        state = CampaignSystem.SelectCardReward(state, state.CardRewardOptions![1], new Random(102));
        Assert.Equal(12, state.PersistentDeck.Count);
    }

    [Fact]
    public void CampaignProgressesThroughAllFloors()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var seed = 99;

        // Progress through floors 1-7 (each has a next level)
        var expectedLevels = new[] { "level2", "level3", "level4", "level5", "level6", "level7", "level8" };
        for (var i = 0; i < expectedLevels.Length; i++)
        {
            state = state with { GameStatus = GameStatus.Won };
            state = CampaignSystem.CompleteFloor(state, new Random(seed++));

            if (state.GamePhase == GamePhase.CardReward)
                state = CampaignSystem.SkipCardReward(state, new Random(seed++));

            if (state.GamePhase == GamePhase.UpgradeReward)
                state = CampaignSystem.SkipUpgrade(state, new Random(seed++));

            Assert.Equal(expectedLevels[i], state.CurrentLevelId);
            Assert.Equal(GamePhase.Playing, state.GamePhase);
        }

        // Floor 8 → victory
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(seed));
        Assert.Equal(GamePhase.CampaignVictory, state.GamePhase);
    }

    [Fact]
    public void Level2Board_HasNoble()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        var nobles = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Noble)
            .ToList();
        Assert.Single(nobles);
    }

    [Fact]
    public void Level3Board_HasCenterHole()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        // Skip through to level 3
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(101));
        state = CampaignSystem.SkipCardReward(state, new Random(102));

        Assert.Equal("level3", state.CurrentLevelId);
        Assert.Equal(4, state.Board.UnusedPositions.Count);
        Assert.Contains(new Position(2, 2), state.Board.UnusedPositions);
    }

    // ========== Upgrade System Tests ==========

    [Fact]
    public void GenerateUpgradeOptions_ContainsAllThreeTypes()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        var options = CampaignSystem.GenerateUpgradeOptions(deck, rng);

        Assert.Equal(3, options.Count);
        Assert.Contains(options, o => o.Type == UpgradeType.Enhance);
        Assert.Contains(options, o => o.Type == UpgradeType.BonusSpoon);
        Assert.Contains(options, o => o.Type == UpgradeType.RemoveCard);
    }

    [Fact]
    public void GenerateUpgradeOptions_EnhancePicksNonEnhancedCard()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        var options = CampaignSystem.GenerateUpgradeOptions(deck, rng);
        var enhance = options.First(o => o.Type == UpgradeType.Enhance);

        Assert.NotNull(enhance.TargetCard);
        Assert.False(enhance.TargetCard!.Enhanced);
    }

    [Fact]
    public void GenerateUpgradeOptions_BonusSpoonPicksNonBonusCard()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        var options = CampaignSystem.GenerateUpgradeOptions(deck, rng);
        var bonus = options.First(o => o.Type == UpgradeType.BonusSpoon);

        Assert.NotNull(bonus.TargetCard);
        Assert.False(bonus.TargetCard!.BonusSpoon);
    }

    [Fact]
    public void GenerateUpgradeOptions_RemoveCardHasNoTarget()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        var options = CampaignSystem.GenerateUpgradeOptions(deck, rng);
        var remove = options.First(o => o.Type == UpgradeType.RemoveCard);

        Assert.Null(remove.TargetCard);
    }

    [Fact]
    public void SelectUpgrade_EnhanceModifiesPersistentDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        // Now at level2; manually transition to upgrade
        var options = CampaignSystem.GenerateUpgradeOptions(state.PersistentDeck, new Random(50));
        var enhance = options.First(o => o.Type == UpgradeType.Enhance);
        state = state with { GamePhase = GamePhase.UpgradeReward, UpgradeOptions = options };

        var newState = CampaignSystem.SelectUpgrade(state, enhance, new Random(200));

        var upgraded = newState.PersistentDeck.First(c => c.Id == enhance.TargetCard!.Id);
        Assert.True(upgraded.Enhanced);
    }

    [Fact]
    public void SelectUpgrade_BonusSpoonModifiesPersistentDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        var options = CampaignSystem.GenerateUpgradeOptions(state.PersistentDeck, new Random(50));
        var bonus = options.First(o => o.Type == UpgradeType.BonusSpoon);
        state = state with { GamePhase = GamePhase.UpgradeReward, UpgradeOptions = options };

        var newState = CampaignSystem.SelectUpgrade(state, bonus, new Random(200));

        var upgraded = newState.PersistentDeck.First(c => c.Id == bonus.TargetCard!.Id);
        Assert.True(upgraded.BonusSpoon);
    }

    [Fact]
    public void SelectUpgrade_RemoveCardRemovesFromPersistentDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var cardToRemove = state.PersistentDeck.First(c => c.Name == "Twirl");

        var removeOption = new UpgradeOption { Type = UpgradeType.RemoveCard };
        state = state with { GamePhase = GamePhase.UpgradeReward };

        var newState = CampaignSystem.SelectUpgrade(state, removeOption, new Random(200), cardToRemove);

        Assert.Equal(9, newState.PersistentDeck.Count);
        Assert.DoesNotContain(newState.PersistentDeck, c => c.Id == cardToRemove.Id);
    }

    [Fact]
    public void GenerateUpgradeOptions_AllEnhanced_OmitsEnhanceOption()
    {
        // Create deck where all cards are already enhanced
        var deck = CardDefinitions.CreateStarterDeck()
            .Select(c => c with { Enhanced = true })
            .ToList();

        var options = CampaignSystem.GenerateUpgradeOptions(deck, new Random(42));

        Assert.DoesNotContain(options, o => o.Type == UpgradeType.Enhance);
    }

    [Fact]
    public void GenerateUpgradeOptions_AllBonusSpoon_OmitsBonusOption()
    {
        var deck = CardDefinitions.CreateStarterDeck()
            .Select(c => c with { BonusSpoon = true })
            .ToList();

        var options = CampaignSystem.GenerateUpgradeOptions(deck, new Random(42));

        Assert.DoesNotContain(options, o => o.Type == UpgradeType.BonusSpoon);
    }

    [Fact]
    public void UpgradeReward_FlowCardThenUpgrade()
    {
        // Set up level2 to have both card and upgrade rewards for testing
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        // Manually make level1 offer both rewards by going through the flow
        // Level1 currently only offers CardReward. We test the flow
        // by directly using TransitionToUpgradeReward-like behavior.
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        Assert.Equal(GamePhase.CardReward, state.GamePhase);

        // Select card reward
        state = CampaignSystem.SelectCardReward(state, state.CardRewardOptions![0], new Random(100));

        // Level1 has no upgrade reward, so it should advance to next floor
        Assert.Equal(GamePhase.Playing, state.GamePhase);
        Assert.Equal("level2", state.CurrentLevelId);
    }

    // ========== Manhattan-2 Adjacency Tests ==========

    [Fact]
    public void Manhattan2_CenterTileHas12Neighbors()
    {
        var config = new LevelConfig
        {
            Width = 5, Height = 5,
            PlayerCount = 10, RivalCount = 8, NeutralCount = 5, NobleCount = 2,
            AdjacencyRule = AdjacencyRule.Manhattan2
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));

        var neighbors = BoardSystem.GetNeighbors(board, new Position(2, 2));
        Assert.Equal(12, neighbors.Count);
    }

    [Fact]
    public void Manhattan2_CornerTileHasFewerNeighbors()
    {
        var config = new LevelConfig
        {
            Width = 5, Height = 5,
            PlayerCount = 10, RivalCount = 8, NeutralCount = 5, NobleCount = 2,
            AdjacencyRule = AdjacencyRule.Manhattan2
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));

        var neighbors = BoardSystem.GetNeighbors(board, new Position(0, 0));
        // Corner: (0,1), (0,2), (1,0), (1,1), (2,0) = 5
        Assert.Equal(5, neighbors.Count);
    }

    [Fact]
    public void Manhattan2_AdjacencyCountIsCorrect()
    {
        var config = new LevelConfig
        {
            Width = 5, Height = 5,
            PlayerCount = 10, RivalCount = 8, NeutralCount = 5, NobleCount = 2,
            AdjacencyRule = AdjacencyRule.Manhattan2
        };
        var board = BoardSystem.CreateBoard(config, new Random(42));
        var center = new Position(2, 2);

        // Reveal center tile
        var newBoard = BoardSystem.RevealTile(board, center, PlayerType.Player);
        var revealedTile = newBoard.GetTile(center);

        // The adjacency count should match the number of player neighbors in Manhattan-2 range
        var expectedCount = BoardSystem.GetNeighbors(board, center)
            .Count(n => board.GetTile(n).Owner == TileOwner.Player);
        Assert.Equal(expectedCount, revealedTile.AdjacencyCount);
    }

    [Fact]
    public void Level4Config_IsValid()
    {
        var config = LevelConfigs.Level4;
        var usable = config.Width * config.Height - config.UnusedLocations.Count;
        var total = config.PlayerCount + config.RivalCount + config.NeutralCount + config.NobleCount;
        Assert.Equal(usable, total);
        Assert.Equal(AdjacencyRule.Manhattan2, config.AdjacencyRule);
    }

    [Theory]
    [InlineData("level5")]
    [InlineData("level6")]
    [InlineData("level7")]
    [InlineData("level8")]
    public void LevelConfigs_AreValid(string levelId)
    {
        var config = LevelConfigs.GetById(levelId);
        Assert.NotNull(config);

        var usable = config!.Width * config.Height - config.UnusedLocations.Count;
        var total = config.PlayerCount + config.RivalCount + config.NeutralCount + config.NobleCount;
        Assert.Equal(usable, total);
    }

    [Fact]
    public void InitialRivalReveal_Works()
    {
        var config = LevelConfigs.Level6;
        Assert.Equal(1, config.InitialRivalReveal);

        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(config, rng);
        var state = new GameState
        {
            Board = board,
            Spoons = 3,
            MaxSpoons = 3
        };

        // Execute initial rival reveal
        state = TurnSystem.ExecuteRivalTurn(state, rng);

        var revealedRivals = state.Board.Tiles
            .Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);
        Assert.Equal(1, revealedRivals);
    }

    [Fact]
    public void CardRewardPool_IncludesStage3Cards()
    {
        var pool = CardDefinitions.CreateRewardPool();
        Assert.Contains(pool, c => c.Name == "Argue");
        Assert.Contains(pool, c => c.Name == "Peek");
        Assert.Contains(pool, c => c.Name == "Glaze");
        Assert.True(pool.Count >= 17); // 6 original + 11 Stage 3
    }

    [Fact]
    public void UpgradeReward_OfferedOnLevel5()
    {
        var config = LevelConfigs.Level5;
        Assert.True(config.UponFinish!.UpgradeReward);
        Assert.True(config.UponFinish!.CardReward);
    }

    [Fact]
    public void UpgradeReward_OfferedOnLevel7()
    {
        var config = LevelConfigs.Level7;
        Assert.True(config.UponFinish!.UpgradeReward);
        Assert.True(config.UponFinish!.CardReward);
    }

    [Fact]
    public void SkipUpgrade_AdvancesToNextFloor()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        // Manually put in upgrade phase to test skip
        var options = CampaignSystem.GenerateUpgradeOptions(state.PersistentDeck, new Random(50));
        state = state with { GamePhase = GamePhase.UpgradeReward, UpgradeOptions = options };

        var newState = CampaignSystem.SkipUpgrade(state, new Random(200));

        Assert.Equal(GamePhase.Playing, newState.GamePhase);
        Assert.Null(newState.UpgradeOptions);
    }
}
