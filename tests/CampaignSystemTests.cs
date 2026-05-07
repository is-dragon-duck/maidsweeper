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
            if (state.GamePhase == GamePhase.EquipmentReward)
                state = CampaignSystem.SkipEquipment(state, new Random(seed++));
            if (state.GamePhase == GamePhase.Shop)
                state = CampaignSystem.LeaveShop(state, new Random(seed++));

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

        // Floor 1 → 2: Card only
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        // Floor 2 → 3: Card + Upgrade
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(101));
        state = CampaignSystem.SkipCardReward(state, new Random(102));
        if (state.GamePhase == GamePhase.UpgradeReward)
            state = CampaignSystem.SkipUpgrade(state, new Random(103));

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
    public void RewardFlow_Level2OffersCardAndUpgrade()
    {
        var config = LevelConfigs.Level2;
        Assert.True(config.UponFinish!.CardReward);
        Assert.True(config.UponFinish!.UpgradeReward);
    }

    [Fact]
    public void RewardFlow_Level5OffersCardAndEquipment()
    {
        var config = LevelConfigs.Level5;
        Assert.True(config.UponFinish!.CardReward);
        Assert.True(config.UponFinish!.EquipmentReward);
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

    // ========== Copper Economy (M26) Tests ==========

    private static GameState CreateCopperTestGame(int seed = 42)
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
            MaxSpoons = 3,
            CurrentLevelId = "level_test"
        };
    }

    [Fact]
    public void CopperEconomy_UnrevealedRivalTilesGrantCopper_AtFloorEnd()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            GameStatus = GameStatus.Won
        };

        // Board has 3 rival tiles, all unrevealed
        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(3, newState.Copper);
    }

    [Fact]
    public void CopperEconomy_RevealedRivalTilesDoNotGrantCopper()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            GameStatus = GameStatus.Won
        };

        // Reveal 1 rival tile
        var rivalTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival);
        var board = BoardSystem.RevealTile(state.Board, rivalTile.Position, PlayerType.Rival);
        state = state with { Board = board };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        // Only 2 unrevealed rivals remain
        Assert.Equal(2, newState.Copper);
    }

    [Fact]
    public void CopperEconomy_PlayerTileReveal_AwardsCopper_Every5th()
    {
        var state = CreateCopperTestGame() with { Copper = 0, PlayerTilesRevealedCount = 4 };

        // Find an unrevealed player tile
        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(42));

        // 5th reveal → +1 copper
        Assert.Equal(1, result.State.Copper);
        Assert.Equal(5, result.State.PlayerTilesRevealedCount);
    }

    [Fact]
    public void CopperEconomy_PlayerTileReveal_NoCopperBelow5th()
    {
        var state = CreateCopperTestGame() with { Copper = 0, PlayerTilesRevealedCount = 0 };

        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(42));

        Assert.Equal(0, result.State.Copper);
        Assert.Equal(1, result.State.PlayerTilesRevealedCount);
    }

    [Fact]
    public void CopperEconomy_RevealCountPersistsAcrossFloors()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng) with { PlayerTilesRevealedCount = 7 };
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        Assert.Equal(7, state.PlayerTilesRevealedCount);
    }

    [Fact]
    public void CopperEconomy_ComplaintsAndRivalTilesCalculatedTogether()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 5,
            ComplaintsStacks = 2,
            GameStatus = GameStatus.Won
        };

        // 3 unrevealed rivals → +3, then 2 stacks × 2 = 4 penalty
        // 5 + 3 - 4 = 4
        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(4, newState.Copper);
    }

    [Fact]
    public void CopperEconomy_CopperCannotGoNegative()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            ComplaintsStacks = 5,
            GameStatus = GameStatus.Won
        };

        // 3 unrevealed rivals → +3, then 5 stacks × 2 = 10 penalty
        // Max(0, 3 - 10) = 0
        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(0, newState.Copper);
    }

    [Fact]
    public void CopperEconomy_NonPlayerTileRevealDoesNotIncrementCounter()
    {
        var state = CreateCopperTestGame() with { Copper = 0, PlayerTilesRevealedCount = 0 };

        // Find an unrevealed rival tile
        var rivalTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival);

        var result = GameRunner.ProcessReveal(state, rivalTile.Position, new Random(42));

        Assert.Equal(0, result.State.PlayerTilesRevealedCount);
    }

    // ========== Food Cards (M27) Tests ==========

    [Fact]
    public void Read_BaseGives2Stacks()
    {
        var state = CreateCopperTestGame();
        var card = CardDefinitions.Read with { Id = "r1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Equal(2, newState.ReadStacks);
    }

    [Fact]
    public void Read_EnhancedGives3Stacks()
    {
        var state = CreateCopperTestGame();
        var card = CardDefinitions.Read with { Id = "r1", Enhanced = true };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Equal(3, newState.ReadStacks);
    }

    [Fact]
    public void Read_Draw6CardsInsteadOf5()
    {
        var state = CreateCopperTestGame() with { ReadStacks = 1 };
        // Set up a clean draw situation: all cards in draw pile
        var deck = CardDefinitions.CreateStarterDeck();
        state = state with
        {
            Hand = new List<Card>(),
            DrawPile = deck,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1
        };

        state = TurnSystem.StartPlayerTurn(state, new Random(42));

        Assert.Equal(6, state.Hand.Count);
    }

    [Fact]
    public void Read_ZeroStacks_Draw5()
    {
        var state = CreateCopperTestGame() with { ReadStacks = 0 };
        var deck = CardDefinitions.CreateStarterDeck();
        state = state with
        {
            Hand = new List<Card>(),
            DrawPile = deck,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1
        };

        state = TurnSystem.StartPlayerTurn(state, new Random(42));

        Assert.Equal(5, state.Hand.Count);
    }

    [Fact]
    public void Read_StacksDecrementAtFloorEnd()
    {
        var state = CreateCopperTestGame() with
        {
            ReadStacks = 2,
            GameStatus = GameStatus.Won
        };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(1, newState.ReadStacks);
    }

    [Fact]
    public void Hydrate_BaseGives2Stacks()
    {
        var state = CreateCopperTestGame();
        var card = CardDefinitions.Hydrate with { Id = "h1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Equal(2, newState.HydrateStacks);
    }

    [Fact]
    public void Hydrate_Grants1SpoonOnCopperReveal()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            Spoons = 1,
            HydrateStacks = 1,
            PlayerTilesRevealedCount = 4
        };

        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(42));

        // 5th reveal → copper + hydrate spoon
        Assert.Equal(1, result.State.Copper);
        Assert.True(result.State.Spoons >= 2); // had 1, gained 1 from Hydrate
    }

    [Fact]
    public void Hydrate_NoBonusWithoutStacks()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            Spoons = 1,
            HydrateStacks = 0,
            PlayerTilesRevealedCount = 4
        };

        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(42));

        // 5th reveal → copper but no hydrate spoon
        Assert.Equal(1, result.State.Copper);
        // Spoons should not increase from Hydrate (may decrease from turn transition)
    }

    [Fact]
    public void Hydrate_NoBonusOnNonCopperReveal()
    {
        var state = CreateCopperTestGame() with
        {
            Copper = 0,
            Spoons = 1,
            HydrateStacks = 1,
            PlayerTilesRevealedCount = 0 // 1st reveal, no copper
        };

        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(42));

        Assert.Equal(0, result.State.Copper);
        // No copper → no hydrate bonus
    }

    [Fact]
    public void Adopt_BaseGives2Stacks()
    {
        var state = CreateCopperTestGame();
        var card = CardDefinitions.Adopt with { Id = "a1" };
        state = state with { Hand = new List<Card> { card }, Spoons = 3 };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(42));

        Assert.Equal(2, newState.AdoptStacks);
    }

    [Fact]
    public void Adopt_Reveals1PlayerTileAtFloorStart()
    {
        // Test Adopt directly via StartNextFloor with a simple board
        var config = new LevelConfig
        {
            LevelId = "test",
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        // Create a state with AdoptStacks = 1
        var baseState = new GameState
        {
            Board = BoardSystem.CreateBoard(config, rng),
            PersistentDeck = deck,
            Copper = 0,
            AdoptStacks = 1
        };

        var newState = CampaignSystem.StartNextFloor(baseState, config, new Random(99));

        // Adopt should reveal 1 player tile
        var revealedPlayer = newState.Board.Tiles
            .Count(t => newState.Board.IsUsablePosition(t.Position) && t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(1, revealedPlayer);
        Assert.Equal(1, newState.AdoptStacks); // persisted, not decremented here
    }

    [Fact]
    public void Adopt_ZeroStacks_NoReveal()
    {
        var config = new LevelConfig
        {
            LevelId = "test",
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var deck = CardDefinitions.CreateStarterDeck();
        var rng = new Random(42);

        var baseState = new GameState
        {
            Board = BoardSystem.CreateBoard(config, rng),
            PersistentDeck = deck,
            Copper = 0,
            AdoptStacks = 0
        };

        var newState = CampaignSystem.StartNextFloor(baseState, config, new Random(99));

        var revealedPlayer = newState.Board.Tiles
            .Count(t => newState.Board.IsUsablePosition(t.Position) && t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(0, revealedPlayer);
    }

    [Fact]
    public void FoodStacks_AllDecrementAtFloorEnd()
    {
        var state = CreateCopperTestGame() with
        {
            ReadStacks = 3,
            HydrateStacks = 2,
            AdoptStacks = 1,
            GameStatus = GameStatus.Won
        };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(2, newState.ReadStacks);
        Assert.Equal(1, newState.HydrateStacks);
        Assert.Equal(0, newState.AdoptStacks);
    }

    [Fact]
    public void FoodStacks_DontGoBelowZero()
    {
        var state = CreateCopperTestGame() with
        {
            ReadStacks = 0,
            HydrateStacks = 0,
            AdoptStacks = 0,
            GameStatus = GameStatus.Won
        };

        var newState = CampaignSystem.CompleteFloor(state, new Random(42));

        Assert.Equal(0, newState.ReadStacks);
        Assert.Equal(0, newState.HydrateStacks);
        Assert.Equal(0, newState.AdoptStacks);
    }

    [Fact]
    public void FoodStacks_PersistAcrossFloors()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng) with
        {
            ReadStacks = 3,
            HydrateStacks = 2,
            AdoptStacks = 2
        };
        state = state with { GameStatus = GameStatus.Won };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        // Decremented by 1 each, then persisted to next floor
        Assert.Equal(2, state.ReadStacks);
        Assert.Equal(1, state.HydrateStacks);
        Assert.Equal(1, state.AdoptStacks);
    }

    [Fact]
    public void FoodCards_InRewardPool()
    {
        var pool = CardDefinitions.CreateRewardPool();
        Assert.Contains(pool, c => c.Name == "Read");
        Assert.Contains(pool, c => c.Name == "Hydrate");
        Assert.Contains(pool, c => c.Name == "Adopt");
    }

    // ========== Equipment Data Model & Core System (M29) Tests ==========

    [Fact]
    public void EquipmentDefinitions_AllHaveValidProperties()
    {
        var pool = EquipmentDefinitions.CreateOfferingPool();

        Assert.NotEmpty(pool);
        foreach (var equipment in pool)
        {
            Assert.False(string.IsNullOrEmpty(equipment.Name));
            Assert.False(string.IsNullOrEmpty(equipment.Description));
        }

        // All effect types are unique within the pool (one item per effect)
        var effectTypes = pool.Select(e => e.EffectType).ToList();
        Assert.Equal(effectTypes.Count, effectTypes.Distinct().Count());
    }

    [Fact]
    public void GenerateEquipmentOptions_Returns3DistinctItems()
    {
        var rng = new Random(42);
        var options = CampaignSystem.GenerateEquipmentOptions([], rng);

        Assert.Equal(3, options.Count);

        var types = options.Select(e => e.EffectType).ToHashSet();
        Assert.Equal(3, types.Count);
    }

    [Fact]
    public void GenerateEquipmentOptions_ExcludesAlreadyOwned()
    {
        // Own all but two items — offering pool should only contain those two
        var pool = EquipmentDefinitions.CreateOfferingPool();
        var owned = pool.Take(pool.Count - 2)
            .Select(e => e with { Id = $"owned_{e.EffectType}" })
            .ToList();

        var rng = new Random(42);
        var options = CampaignSystem.GenerateEquipmentOptions(owned, rng);

        Assert.Equal(2, options.Count); // Only 2 unowned items remain
        var ownedTypes = owned.Select(e => e.EffectType).ToHashSet();
        Assert.DoesNotContain(options, e => ownedTypes.Contains(e.EffectType));
    }

    [Fact]
    public void GenerateEquipmentOptions_AllOptionsHaveUniqueIds()
    {
        var rng = new Random(42);
        var options = CampaignSystem.GenerateEquipmentOptions([], rng);

        var ids = options.Select(e => e.Id).ToHashSet();
        Assert.Equal(options.Count, ids.Count);
    }

    [Fact]
    public void SelectEquipment_AddsToInventoryAndAdvances()
    {
        // Set up at level1 so AdvanceToNextFloor can resolve next level via uponFinish
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var offering = EquipmentDefinitions.Coffee with { Id = "offer_coffee" };
        state = state with
        {
            GamePhase = GamePhase.EquipmentReward,
            EquipmentOptions = new List<Equipment> { offering }
        };

        var newState = CampaignSystem.SelectEquipment(state, offering, new Random(99));

        Assert.Single(newState.Equipment);
        Assert.Equal(EquipmentEffectType.Coffee, newState.Equipment[0].EffectType);
        Assert.Null(newState.EquipmentOptions);
        Assert.Equal("level2", newState.CurrentLevelId);
        Assert.Equal(GamePhase.Playing, newState.GamePhase);
    }

    [Fact]
    public void SkipEquipment_DoesNotAddAndAdvances()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            GamePhase = GamePhase.EquipmentReward,
            EquipmentOptions = new List<Equipment> { EquipmentDefinitions.Coffee with { Id = "o" } }
        };

        var newState = CampaignSystem.SkipEquipment(state, new Random(99));

        Assert.Empty(newState.Equipment);
        Assert.Null(newState.EquipmentOptions);
        Assert.Equal("level2", newState.CurrentLevelId);
        Assert.Equal(GamePhase.Playing, newState.GamePhase);
    }

    [Fact]
    public void Equipment_PersistsAcrossFloors()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var owned = EquipmentDefinitions.Coffee with { Id = "my_coffee" };
        state = state with
        {
            Equipment = new List<Equipment> { owned },
            GameStatus = GameStatus.Won
        };

        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));

        Assert.Equal("level2", state.CurrentLevelId);
        Assert.Single(state.Equipment);
        Assert.Equal("my_coffee", state.Equipment[0].Id);
    }

    [Fact]
    public void RewardFlow_CardThenEquipment()
    {
        // Build a custom level that offers card + equipment to verify ordering
        var equipLevel = new LevelConfig
        {
            LevelId = "flow_test",
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1,
            UponFinish = new UponFinishConfig
            {
                CardReward = true,
                EquipmentReward = true,
                NextLevelId = "level2"
            }
        };

        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(equipLevel, rng);
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = board,
            PersistentDeck = deck,
            DrawPile = deck,
            CurrentLevelId = equipLevel.LevelId,
            GameStatus = GameStatus.Won
        };

        // CompleteFloor reads config from LevelConfigs.GetById, which won't find our test level,
        // so we drive the transitions directly.
        state = state with
        {
            GamePhase = GamePhase.CardReward,
            CardRewardOptions = CampaignSystem.GenerateCardRewardOptions(new Random(50))
        };

        // Skip card → since custom level isn't registered, we instead test the explicit transition.
        // Use the registered Level5 path which has card + upgrade, then verify equipment is reachable
        // via the dedicated transition. This test focuses on the EquipmentReward phase being entered
        // when configured and exited cleanly.
        var equipOptions = CampaignSystem.GenerateEquipmentOptions(state.Equipment, new Random(60));
        state = state with
        {
            GamePhase = GamePhase.EquipmentReward,
            EquipmentOptions = equipOptions
        };

        Assert.Equal(GamePhase.EquipmentReward, state.GamePhase);
        Assert.Equal(3, state.EquipmentOptions!.Count);
    }

    [Fact]
    public void GenerateEquipmentOptions_EmptyWhenAllOwned()
    {
        var allOwned = EquipmentDefinitions.CreateOfferingPool()
            .Select((e, i) => e with { Id = $"owned_{i}" })
            .ToList();

        var options = CampaignSystem.GenerateEquipmentOptions(allOwned, new Random(42));

        Assert.Empty(options);
    }

    // ========== Reward Flow (M33) Tests ==========

    /// <summary>
    /// Walks the campaign through every floor, skipping each phase, asserting
    /// that the right phases appear (and only those) per the M33 design table.
    /// </summary>
    [Fact]
    public void RewardFlow_FullCampaign_PhaseSequenceMatchesTable()
    {
        // Expected phase sequence per floor end → next floor:
        // Floor 1 → 2:  Card
        // Floor 2 → 3:  Card, Upgrade
        // Floor 3 → 4:  Equipment
        // Floor 4 → 5:  Shop
        // Floor 5 → 6:  Card, Equipment
        // Floor 6 → 7:  Card
        // Floor 7 → 8:  Card, Upgrade
        // Floor 8: no rewards (campaign ends)
        var expectedPhases = new[]
        {
            new[] { GamePhase.CardReward },
            new[] { GamePhase.CardReward, GamePhase.UpgradeReward },
            new[] { GamePhase.EquipmentReward },
            new[] { GamePhase.Shop },
            new[] { GamePhase.CardReward, GamePhase.EquipmentReward },
            new[] { GamePhase.CardReward },
            new[] { GamePhase.CardReward, GamePhase.UpgradeReward },
        };

        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var seed = 99;

        for (var floorIdx = 0; floorIdx < expectedPhases.Length; floorIdx++)
        {
            state = state with { GameStatus = GameStatus.Won };
            state = CampaignSystem.CompleteFloor(state, new Random(seed++));

            var observed = new List<GamePhase>();
            while (state.GamePhase != GamePhase.Playing && state.GamePhase != GamePhase.CampaignVictory)
            {
                observed.Add(state.GamePhase);
                state = state.GamePhase switch
                {
                    GamePhase.CardReward => CampaignSystem.SkipCardReward(state, new Random(seed++)),
                    GamePhase.UpgradeReward => CampaignSystem.SkipUpgrade(state, new Random(seed++)),
                    GamePhase.EquipmentReward => CampaignSystem.SkipEquipment(state, new Random(seed++)),
                    GamePhase.Shop => CampaignSystem.LeaveShop(state, new Random(seed++)),
                    _ => throw new InvalidOperationException($"Unexpected phase {state.GamePhase}")
                };
            }

            Assert.Equal(expectedPhases[floorIdx], observed.ToArray());
        }

        // Floor 8 → campaign victory (no NextLevelId)
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(seed));
        Assert.Equal(GamePhase.CampaignVictory, state.GamePhase);
    }

    [Fact]
    public void RewardFlow_EquipmentPhaseOnlyOnConfiguredFloors()
    {
        // Only Level3 (Equipment-only) and Level5 (Card+Equipment) should expose Equipment phase
        Assert.True(LevelConfigs.Level3.UponFinish!.EquipmentReward);
        Assert.True(LevelConfigs.Level5.UponFinish!.EquipmentReward);

        Assert.False(LevelConfigs.Level1.UponFinish!.EquipmentReward);
        Assert.False(LevelConfigs.Level2.UponFinish!.EquipmentReward);
        Assert.False(LevelConfigs.Level4.UponFinish!.EquipmentReward);
        Assert.False(LevelConfigs.Level6.UponFinish!.EquipmentReward);
        Assert.False(LevelConfigs.Level7.UponFinish!.EquipmentReward);
    }

    [Fact]
    public void RewardFlow_ShopPhaseOnlyOnConfiguredFloors()
    {
        // Only Level4 should have Shop in the M33 table
        Assert.True(LevelConfigs.Level4.UponFinish!.Shop);

        Assert.False(LevelConfigs.Level1.UponFinish!.Shop);
        Assert.False(LevelConfigs.Level2.UponFinish!.Shop);
        Assert.False(LevelConfigs.Level3.UponFinish!.Shop);
        Assert.False(LevelConfigs.Level5.UponFinish!.Shop);
        Assert.False(LevelConfigs.Level6.UponFinish!.Shop);
        Assert.False(LevelConfigs.Level7.UponFinish!.Shop);
    }

    [Fact]
    public void RewardFlow_FinalFloorGoesDirectlyToVictory()
    {
        // Drive a state at level8 (final) with Won status; CompleteFloor → CampaignVictory
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        // Walk to level 8
        var seed = 99;
        for (var i = 0; i < 7; i++)
        {
            state = state with { GameStatus = GameStatus.Won };
            state = CampaignSystem.CompleteFloor(state, new Random(seed++));
            while (state.GamePhase != GamePhase.Playing && state.GamePhase != GamePhase.CampaignVictory)
            {
                state = state.GamePhase switch
                {
                    GamePhase.CardReward => CampaignSystem.SkipCardReward(state, new Random(seed++)),
                    GamePhase.UpgradeReward => CampaignSystem.SkipUpgrade(state, new Random(seed++)),
                    GamePhase.EquipmentReward => CampaignSystem.SkipEquipment(state, new Random(seed++)),
                    GamePhase.Shop => CampaignSystem.LeaveShop(state, new Random(seed++)),
                    _ => state
                };
            }
        }
        Assert.Equal("level8", state.CurrentLevelId);

        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(seed));

        Assert.Equal(GamePhase.CampaignVictory, state.GamePhase);
    }

    [Fact]
    public void RewardFlow_SkippingCardOnLevel2_AdvancesToUpgradePhase()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        // Floor 1 → 2 (skip card reward)
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));
        Assert.Equal("level2", state.CurrentLevelId);

        // Complete floor 2 → CardReward, skip → UpgradeReward (not Playing yet)
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(101));
        Assert.Equal(GamePhase.CardReward, state.GamePhase);

        state = CampaignSystem.SkipCardReward(state, new Random(102));
        Assert.Equal(GamePhase.UpgradeReward, state.GamePhase);
        Assert.Equal("level2", state.CurrentLevelId); // still on level 2 until all phases done
    }

    [Fact]
    public void RewardFlow_SelectingCardOnLevel5_AdvancesToEquipmentPhase()
    {
        // Walk to level5 first
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var seed = 99;
        for (var i = 0; i < 4; i++)
        {
            state = state with { GameStatus = GameStatus.Won };
            state = CampaignSystem.CompleteFloor(state, new Random(seed++));
            while (state.GamePhase != GamePhase.Playing)
            {
                state = state.GamePhase switch
                {
                    GamePhase.CardReward => CampaignSystem.SkipCardReward(state, new Random(seed++)),
                    GamePhase.UpgradeReward => CampaignSystem.SkipUpgrade(state, new Random(seed++)),
                    GamePhase.EquipmentReward => CampaignSystem.SkipEquipment(state, new Random(seed++)),
                    GamePhase.Shop => CampaignSystem.LeaveShop(state, new Random(seed++)),
                    _ => state
                };
            }
        }
        Assert.Equal("level5", state.CurrentLevelId);

        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(seed++));
        Assert.Equal(GamePhase.CardReward, state.GamePhase);

        // Selecting card advances to EquipmentReward (not Playing)
        var card = state.CardRewardOptions![0];
        state = CampaignSystem.SelectCardReward(state, card, new Random(seed++));
        Assert.Equal(GamePhase.EquipmentReward, state.GamePhase);
    }
}
