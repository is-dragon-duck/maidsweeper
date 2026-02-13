namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class CampaignSystem
{
    /// <summary>
    /// Starts a new campaign from Level 1 with the starter deck.
    /// </summary>
    public static GameState StartCampaign(Random rng)
    {
        var starterDeck = CardDefinitions.CreateStarterDeck();
        var config = LevelConfigs.Level1;

        var state = CreateFloorState(config, starterDeck, rng);
        return state with
        {
            PersistentDeck = starterDeck,
            CurrentLevelId = config.LevelId,
            GamePhase = GamePhase.Playing
        };
    }

    /// <summary>
    /// Called when a floor is won. Transitions to CardReward phase or CampaignVictory.
    /// </summary>
    public static GameState CompleteFloor(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        var uponFinish = config?.UponFinish;

        if (uponFinish == null || (!uponFinish.CardReward && uponFinish.NextLevelId == null))
        {
            return state with { GamePhase = GamePhase.CampaignVictory };
        }

        if (uponFinish.CardReward)
        {
            var options = GenerateCardRewardOptions(rng);
            return state with
            {
                GamePhase = GamePhase.CardReward,
                CardRewardOptions = options
            };
        }

        // No reward, advance directly
        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// Generates 3 distinct card reward options from the reward pool.
    /// </summary>
    public static List<Card> GenerateCardRewardOptions(Random rng)
    {
        var pool = CardDefinitions.CreateRewardPool();
        var id = 0;
        string nextId() => $"reward_{id++}_{rng.Next(10000)}";

        // Shuffle and pick 3
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(3).Select(c => c with { Id = nextId() }).ToList();
    }

    /// <summary>
    /// Player selects a card reward — adds it to persistent deck and starts next floor.
    /// </summary>
    public static GameState SelectCardReward(GameState state, Card selected, Random rng)
    {
        var deck = state.PersistentDeck.ToList();
        deck.Add(selected);
        state = state with
        {
            PersistentDeck = deck,
            CardRewardOptions = null
        };

        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// Player skips the card reward — starts next floor without adding a card.
    /// </summary>
    public static GameState SkipCardReward(GameState state, Random rng)
    {
        state = state with { CardRewardOptions = null };
        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// Advances to the next floor based on the current level's uponFinish config.
    /// </summary>
    private static GameState AdvanceToNextFloor(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        var nextLevelId = config?.UponFinish?.NextLevelId;

        if (nextLevelId == null)
        {
            return state with { GamePhase = GamePhase.CampaignVictory };
        }

        var nextConfig = LevelConfigs.GetById(nextLevelId);
        if (nextConfig == null)
        {
            return state with { GamePhase = GamePhase.CampaignVictory };
        }

        return StartNextFloor(state, nextConfig, rng);
    }

    /// <summary>
    /// Creates new floor state: new board, shuffled persistent deck, fresh hand/spoons.
    /// </summary>
    public static GameState StartNextFloor(GameState state, LevelConfig nextLevel, Random rng)
    {
        var floorState = CreateFloorState(nextLevel, state.PersistentDeck.ToList(), rng);
        return floorState with
        {
            PersistentDeck = state.PersistentDeck,
            CurrentLevelId = nextLevel.LevelId,
            GamePhase = GamePhase.Playing,
            Copper = state.Copper
        };
    }

    /// <summary>
    /// Creates a fresh floor GameState from a level config and deck.
    /// </summary>
    private static GameState CreateFloorState(LevelConfig config, List<Card> deck, Random rng)
    {
        var board = BoardSystem.CreateBoard(config, rng);
        var shuffledDeck = DeckSystem.Shuffle(deck, rng);

        var state = new GameState
        {
            Board = board,
            DrawPile = shuffledDeck,
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1
        };

        return DeckSystem.DrawCards(state, 5, rng);
    }
}
