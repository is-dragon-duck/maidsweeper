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
            GamePhase = GamePhase.Playing,
            ExcusesStacks = 1
        };
    }

    /// <summary>
    /// Called when a floor is won. Transitions to CardReward phase or CampaignVictory.
    /// </summary>
    public static GameState CompleteFloor(GameState state, Random rng)
    {
        // Apply Complaints copper penalty: lose 2 copper per stack
        if (state.ComplaintsStacks > 0)
        {
            var penalty = state.ComplaintsStacks * 2;
            state = state with
            {
                Copper = Math.Max(0, state.Copper - penalty),
                ComplaintsStacks = 0
            };
        }

        // Remove Mollify cards from persistent deck (they don't persist between floors)
        var cleanedDeck = state.PersistentDeck.Where(c => c.EffectType != CardEffectType.Mollify).ToList();
        state = state with { PersistentDeck = cleanedDeck };

        var config = LevelConfigs.GetById(state.CurrentLevelId);
        var uponFinish = config?.UponFinish;

        if (uponFinish == null || (!uponFinish.CardReward && !uponFinish.UpgradeReward && uponFinish.NextLevelId == null))
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

        if (uponFinish.UpgradeReward)
        {
            return TransitionToUpgradeReward(state, rng);
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
    /// Player selects a card reward — adds it to persistent deck.
    /// Then transitions to upgrade reward if configured, otherwise advances.
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

        return TransitionAfterCardReward(state, rng);
    }

    /// <summary>
    /// Player skips the card reward.
    /// Then transitions to upgrade reward if configured, otherwise advances.
    /// </summary>
    public static GameState SkipCardReward(GameState state, Random rng)
    {
        state = state with { CardRewardOptions = null };
        return TransitionAfterCardReward(state, rng);
    }

    /// <summary>
    /// After card reward (selected or skipped), check if upgrade reward follows.
    /// </summary>
    private static GameState TransitionAfterCardReward(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        if (config?.UponFinish?.UpgradeReward == true)
        {
            return TransitionToUpgradeReward(state, rng);
        }

        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// Transitions to the upgrade reward phase with 3 generated options.
    /// </summary>
    private static GameState TransitionToUpgradeReward(GameState state, Random rng)
    {
        var options = GenerateUpgradeOptions(state.PersistentDeck, rng);
        return state with
        {
            GamePhase = GamePhase.UpgradeReward,
            UpgradeOptions = options
        };
    }

    /// <summary>
    /// Generates 3 upgrade options: Enhance (random card), BonusSpoon (random card), RemoveCard.
    /// If no eligible cards exist for Enhance or BonusSpoon, those options are omitted.
    /// </summary>
    public static List<UpgradeOption> GenerateUpgradeOptions(IReadOnlyList<Card> persistentDeck, Random rng)
    {
        var options = new List<UpgradeOption>();

        // Enhance: pick a random non-enhanced card
        var enhanceable = persistentDeck.Where(c => !c.Enhanced).ToList();
        if (enhanceable.Count > 0)
        {
            var target = enhanceable[rng.Next(enhanceable.Count)];
            options.Add(new UpgradeOption { Type = UpgradeType.Enhance, TargetCard = target });
        }

        // BonusSpoon: pick a random card without bonus spoon
        var bonusable = persistentDeck.Where(c => !c.BonusSpoon).ToList();
        if (bonusable.Count > 0)
        {
            var target = bonusable[rng.Next(bonusable.Count)];
            options.Add(new UpgradeOption { Type = UpgradeType.BonusSpoon, TargetCard = target });
        }

        // RemoveCard: always available (player picks which card)
        options.Add(new UpgradeOption { Type = UpgradeType.RemoveCard });

        return options;
    }

    /// <summary>
    /// Player selects an upgrade option. For RemoveCard, cardToRemove must be provided.
    /// </summary>
    public static GameState SelectUpgrade(GameState state, UpgradeOption selected, Random rng, Card? cardToRemove = null)
    {
        var deck = state.PersistentDeck.ToList();

        switch (selected.Type)
        {
            case UpgradeType.Enhance:
            {
                var idx = deck.FindIndex(c => c.Id == selected.TargetCard!.Id);
                if (idx >= 0)
                    deck[idx] = deck[idx] with { Enhanced = true };
                break;
            }
            case UpgradeType.BonusSpoon:
            {
                var idx = deck.FindIndex(c => c.Id == selected.TargetCard!.Id);
                if (idx >= 0)
                    deck[idx] = deck[idx] with { BonusSpoon = true };
                break;
            }
            case UpgradeType.RemoveCard:
            {
                if (cardToRemove != null)
                    deck.RemoveAll(c => c.Id == cardToRemove.Id);
                break;
            }
        }

        state = state with
        {
            PersistentDeck = deck,
            UpgradeOptions = null
        };

        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// Player skips the upgrade reward.
    /// </summary>
    public static GameState SkipUpgrade(GameState state, Random rng)
    {
        state = state with { UpgradeOptions = null };
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
            Copper = state.Copper,
            // Reset per-floor status effects
            AcceptHelpDiscount = false,
            DistractionStacks = 0,
            ExcusesStacks = 1,
            ComplaintsStacks = 0
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

        state = DeckSystem.DrawCards(state, 5, rng);

        // Initial rival reveal
        for (var i = 0; i < config.InitialRivalReveal; i++)
        {
            state = TurnSystem.ExecuteRivalTurn(state, rng);
        }

        return state;
    }
}
