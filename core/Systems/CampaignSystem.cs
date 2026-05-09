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
        state = state with
        {
            PersistentDeck = starterDeck,
            CurrentLevelId = config.LevelId,
            GamePhase = GamePhase.Playing,
            ExcusesStacks = 1,
            RivalMineProtectionCount = config.RivalMineProtection
        };

        // Initial rival intent points for turn 1 (Level 1 has no equipment yet)
        var initialIntent = IntentSystem.GenerateTurnIntent(state, rng);
        return state with { RivalIntentPoints = initialIntent };
    }

    /// <summary>
    /// Called when a floor is won. Transitions to CardReward phase or CampaignVictory.
    /// </summary>
    public static GameState CompleteFloor(GameState state, Random rng)
    {
        // Award copper from unrevealed rival tiles (1 per tile, x2 with Tiara)
        var unrevealedRivals = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed
                        && t.Owner == TileOwner.Rival);
        if (unrevealedRivals > 0)
        {
            state = state with { Copper = state.Copper + unrevealedRivals * EquipmentSystem.CopperMultiplier(state) };
        }

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

        // Decrement food stacks at floor end
        state = state with
        {
            ReadStacks = Math.Max(0, state.ReadStacks - 1),
            HydrateStacks = Math.Max(0, state.HydrateStacks - 1),
            AdoptStacks = Math.Max(0, state.AdoptStacks - 1)
        };

        // Remove Mollify cards from persistent deck (they don't persist between floors)
        var cleanedDeck = state.PersistentDeck.Where(c => c.EffectType != CardEffectType.Mollify).ToList();
        state = state with { PersistentDeck = cleanedDeck };

        var config = LevelConfigs.GetById(state.CurrentLevelId);
        var uponFinish = config?.UponFinish;

        if (uponFinish == null || (!uponFinish.CardReward && !uponFinish.UpgradeReward
                                   && !uponFinish.EquipmentReward && !uponFinish.Shop
                                   && uponFinish.NextLevelId == null))
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

        if (uponFinish.EquipmentReward)
        {
            return TransitionToEquipmentReward(state, rng);
        }

        if (uponFinish.Shop)
        {
            return ShopSystem.EnterShop(state, rng);
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
        // Bleach ongoing effect: future Spritz/Sweep/Brush added to deck are auto-enhanced
        var cardToAdd = EquipmentSystem.ApplyBleachToNewCard(state, selected);

        var deck = state.PersistentDeck.ToList();
        deck.Add(cardToAdd);
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
    /// After card reward (selected or skipped), check if upgrade, equipment, or shop follows.
    /// </summary>
    private static GameState TransitionAfterCardReward(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        if (config?.UponFinish?.UpgradeReward == true)
        {
            return TransitionToUpgradeReward(state, rng);
        }

        if (config?.UponFinish?.EquipmentReward == true)
        {
            return TransitionToEquipmentReward(state, rng);
        }

        if (config?.UponFinish?.Shop == true)
        {
            return ShopSystem.EnterShop(state, rng);
        }

        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// After upgrade reward (selected or skipped), check if equipment or shop follows.
    /// </summary>
    private static GameState TransitionAfterUpgradeReward(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        if (config?.UponFinish?.EquipmentReward == true)
        {
            return TransitionToEquipmentReward(state, rng);
        }

        if (config?.UponFinish?.Shop == true)
        {
            return ShopSystem.EnterShop(state, rng);
        }

        return AdvanceToNextFloor(state, rng);
    }

    /// <summary>
    /// After equipment reward (selected or skipped), check if shop follows.
    /// </summary>
    private static GameState TransitionAfterEquipmentReward(GameState state, Random rng)
    {
        var config = LevelConfigs.GetById(state.CurrentLevelId);
        if (config?.UponFinish?.Shop == true)
        {
            return ShopSystem.EnterShop(state, rng);
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

        return TransitionAfterUpgradeReward(state, rng);
    }

    /// <summary>
    /// Player skips the upgrade reward.
    /// </summary>
    public static GameState SkipUpgrade(GameState state, Random rng)
    {
        state = state with { UpgradeOptions = null };
        return TransitionAfterUpgradeReward(state, rng);
    }

    /// <summary>
    /// Transitions to the equipment reward phase with 3 generated options.
    /// </summary>
    private static GameState TransitionToEquipmentReward(GameState state, Random rng)
    {
        var options = GenerateEquipmentOptions(state.Equipment, rng);
        if (options.Count == 0)
        {
            // No equipment available — skip phase entirely
            return AdvanceToNextFloor(state, rng);
        }

        return state with
        {
            GamePhase = GamePhase.EquipmentReward,
            EquipmentOptions = options
        };
    }

    /// <summary>
    /// Generates up to 3 equipment offerings, excluding already-owned items.
    /// </summary>
    public static List<Equipment> GenerateEquipmentOptions(IReadOnlyList<Equipment> owned, Random rng)
    {
        var ownedTypes = owned.Select(e => e.EffectType).ToHashSet();
        var pool = EquipmentDefinitions.CreateOfferingPool()
            .Where(e => !ownedTypes.Contains(e.EffectType))
            .Where(e => e.Prereqs.All(p => ownedTypes.Contains(p)))
            .ToList();

        var id = 0;
        string nextId() => $"equipment_{id++}_{rng.Next(10000)}";

        // Shuffle and pick up to 3
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.Take(3).Select(e => e with { Id = nextId() }).ToList();
    }

    /// <summary>
    /// Player selects an equipment offering — adds it to their inventory.
    /// Applies any one-shot deck-modifying effects from the new equipment.
    /// </summary>
    public static GameState SelectEquipment(GameState state, Equipment selected, Random rng)
    {
        var equipment = state.Equipment.ToList();
        equipment.Add(selected);
        state = state with
        {
            Equipment = equipment,
            EquipmentOptions = null
        };

        // One-shot deck modifications from this equipment (Bleach, Estrogen, etc.)
        state = EquipmentSystem.ApplyOnAcquisition(state, selected, rng);

        return TransitionAfterEquipmentReward(state, rng);
    }

    /// <summary>
    /// Player skips the equipment reward.
    /// </summary>
    public static GameState SkipEquipment(GameState state, Random rng)
    {
        state = state with { EquipmentOptions = null };
        return TransitionAfterEquipmentReward(state, rng);
    }

    /// <summary>
    /// Player leaves the shop (after spending however much copper they wanted).
    /// </summary>
    public static GameState LeaveShop(GameState state, Random rng)
    {
        state = ShopSystem.ExitShop(state);
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
        floorState = floorState with
        {
            PersistentDeck = state.PersistentDeck,
            Equipment = state.Equipment,
            CurrentLevelId = nextLevel.LevelId,
            GamePhase = GamePhase.Playing,
            Copper = state.Copper,
            PlayerTilesRevealedCount = state.PlayerTilesRevealedCount,
            // Reset per-floor status effects
            AcceptHelpDiscount = false,
            DistractionStacks = 0,
            ExcusesStacks = 1,
            ComplaintsStacks = 0,
            RecallPlayedThisFloor = false,
            // Reset per-floor rival mine protection from this level's config
            RivalMineProtectionCount = nextLevel.RivalMineProtection,
            // Reset per-floor Taunt effects
            ActiveTaunts = new List<TauntEffect>(),
            // Persist multi-floor food stacks
            ReadStacks = state.ReadStacks,
            HydrateStacks = state.HydrateStacks,
            AdoptStacks = state.AdoptStacks,
            // Persist shop state
            ShopVisitCount = state.ShopVisitCount,
            VisitingBunnyPendingReveals = state.VisitingBunnyPendingReveals,
            // Reset per-floor intent points
            RivalIntentPoints = new Dictionary<Position, int>()
        };

        // Equipment floor-start hooks (Coffee, Handbag, Dust Bunny)
        floorState = EquipmentSystem.ApplyOnFloorStart(floorState, rng);

        // Generate initial intent before turn-start hooks so Eyeshadow can add a distraction.
        var initialIntent = IntentSystem.GenerateTurnIntent(floorState, rng);
        floorState = floorState with { RivalIntentPoints = initialIntent };

        // Equipment turn-start hooks for turn 1 (Eyeshadow, Glasses)
        floorState = EquipmentSystem.ApplyOnTurnStart(floorState, rng);

        // Adopt: reveal 1 random player tile at floor start
        if (floorState.AdoptStacks > 0)
        {
            floorState = RevealRandomPlayerTile(floorState, rng);
        }

        // Visiting Bunny: reveal one player tile per pending reveal (purchased last shop)
        if (floorState.VisitingBunnyPendingReveals > 0)
        {
            var pending = floorState.VisitingBunnyPendingReveals;
            for (var i = 0; i < pending; i++)
            {
                floorState = RevealRandomPlayerTile(floorState, rng);
            }
            floorState = floorState with { VisitingBunnyPendingReveals = 0 };
        }

        return floorState;
    }

    /// <summary>
    /// Reveals 1 random unrevealed player tile on the board.
    /// </summary>
    private static GameState RevealRandomPlayerTile(GameState state, Random rng)
    {
        var unrevealed = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed
                        && t.Owner == TileOwner.Player)
            .ToList();

        if (unrevealed.Count == 0) return state;

        var target = unrevealed[rng.Next(unrevealed.Count)];
        var newBoard = BoardSystem.RevealTile(state.Board, target.Position, PlayerType.Player);
        return state with { Board = newBoard };
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
