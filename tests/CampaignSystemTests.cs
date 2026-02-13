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
    public void VictoryAfterLevel3()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        // Floor 1 → reward → Floor 2
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(99));
        state = CampaignSystem.SkipCardReward(state, new Random(100));
        Assert.Equal("level2", state.CurrentLevelId);

        // Floor 2 → reward → Floor 3
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(101));
        state = CampaignSystem.SkipCardReward(state, new Random(102));
        Assert.Equal("level3", state.CurrentLevelId);

        // Floor 3 → victory (no next level)
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(103));
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
}
