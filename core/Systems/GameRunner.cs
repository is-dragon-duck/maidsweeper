namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

/// <summary>
/// Result of processing a player action (reveal or card play).
/// Indicates what happened and whether the turn ended.
/// </summary>
public record ActionResult
{
    public required GameState State { get; init; }
    public bool TurnEnded { get; init; }
    public bool GameOver => State.GameStatus != GameStatus.Playing;
}

public static class GameRunner
{
    /// <summary>
    /// Creates a new game: board from config, shuffled starter deck, draw initial hand.
    /// Optionally takes a persistent deck (for campaign continuation).
    /// </summary>
    public static GameState CreateGame(LevelConfig config, Random rng, List<Card>? persistentDeck = null)
    {
        var board = BoardSystem.CreateBoard(config, rng);
        var deck = persistentDeck ?? CardDefinitions.CreateStarterDeck();
        deck = DeckSystem.Shuffle(deck, rng);

        var state = new GameState
        {
            Board = board,
            DrawPile = deck,
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1,
            PersistentDeck = persistentDeck ?? CardDefinitions.CreateStarterDeck(),
            CurrentLevelId = config.LevelId
        };

        // Draw initial hand of 5
        state = DeckSystem.DrawCards(state, 5, rng);

        // Initial rival reveal (some floors start with rival tiles pre-revealed)
        for (var i = 0; i < config.InitialRivalReveal; i++)
        {
            state = TurnSystem.ExecuteRivalTurn(state, rng);
        }

        return state;
    }

    /// <summary>
    /// Processes a player clicking a tile to reveal it.
    /// Returns ActionResult with updated state and whether the turn ended.
    /// </summary>
    public static ActionResult ProcessReveal(GameState state, Position pos, Random rng)
    {
        if (state.CurrentPlayer != PlayerType.Player)
            throw new InvalidOperationException("Not the player's turn");

        if (state.GameStatus != GameStatus.Playing)
            throw new InvalidOperationException("Game is not in progress");

        var tile = state.Board.GetTile(pos);
        if (tile.IsRevealed)
            throw new InvalidOperationException("Tile is already revealed");

        // Inner tiles are clickable only when at least one adjacent sanctum is revealed.
        if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, pos))
            throw new InvalidOperationException("Inner tile is unreachable (no adjacent sanctum revealed)");

        // Courtier: clicking moves the courtier and "cleans" (no reveal). Click counts as
        // attempting to reveal, so the player's turn still ends.
        if (tile.IsCourtier)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, pos, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
            state = ProcessTurnTransition(state, rng);
            return new ActionResult { State = state, TurnEnded = true };
        }

        var wasDirty = tile.IsDirty;

        // Reveal the tile (or clean if ExtraDirty)
        var newBoard = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player);
        state = state with { Board = newBoard };

        // If tile was dirty and got cleaned (not revealed), end the turn
        var cleanedTile = newBoard.GetTile(pos);
        if (wasDirty && !cleanedTile.IsRevealed)
        {
            state = ProcessTurnTransition(state, rng);
            return new ActionResult { State = state, TurnEnded = true };
        }

        // Track player tile reveals for copper economy
        var revealedTile = state.Board.GetTile(pos);
        if (revealedTile.IsRevealed && revealedTile.Owner == TileOwner.Player)
        {
            state = TrackPlayerTileReveal(state);
        }

        // Consume Excuses if a noble was revealed
        state = ConsumeExcusesIfNeeded(state);

        // Check game status
        var status = TurnSystem.CheckGameStatus(state);
        state = state with { GameStatus = status };

        if (status != GameStatus.Playing)
        {
            return new ActionResult { State = state, TurnEnded = true };
        }

        // Check if turn should end (non-player tile revealed)
        var turnEnded = TurnSystem.ShouldEndTurn(revealedTile);

        // Frilly Dress: suppress turn end for first 4 neutral reveals on turn 1
        if (turnEnded)
        {
            var (frillyState, suppressed) = EquipmentSystem.ApplyFrillyDress(state, revealedTile);
            state = frillyState;
            if (suppressed) turnEnded = false;
        }

        if (turnEnded)
        {
            state = ProcessTurnTransition(state, rng);
        }

        return new ActionResult { State = state, TurnEnded = turnEnded };
    }

    /// <summary>
    /// Processes playing a card from hand.
    /// Returns ActionResult with updated state.
    /// Cards that cause reveals (Scurry) may end the turn.
    /// </summary>
    public static ActionResult ProcessCardPlay(GameState state, Card card, Position[]? targets, Random rng)
    {
        if (state.CurrentPlayer != PlayerType.Player)
            throw new InvalidOperationException("Not the player's turn");

        if (state.GameStatus != GameStatus.Playing)
            throw new InvalidOperationException("Game is not in progress");

        var boardBefore = state.Board;
        state = CardEffectSystem.PlayCard(state, card, targets, rng);

        // Track player tiles newly revealed by the card
        foreach (var tile in state.Board.Tiles)
        {
            if (!state.Board.IsUsablePosition(tile.Position)) continue;
            var before = boardBefore.GetTile(tile.Position);
            if (!before.IsRevealed && tile.IsRevealed && tile.Owner == TileOwner.Player)
            {
                state = TrackPlayerTileReveal(state);
            }
        }

        // Consume Excuses if any noble was revealed by the card
        state = ConsumeExcusesIfNeeded(state);

        // Check game status
        var status = TurnSystem.CheckGameStatus(state);
        state = state with { GameStatus = status };

        if (status != GameStatus.Playing)
        {
            return new ActionResult { State = state, TurnEnded = true };
        }

        // Check if a revealing card ended the turn (newly revealed non-Player tile)
        var turnEnded = false;
        if (card.EffectType == CardEffectType.Scurry
            || card.EffectType == CardEffectType.AcceptHelp
            || card.EffectType == CardEffectType.Fetch)
        {
            foreach (var tile in state.Board.Tiles)
            {
                if (!state.Board.IsUsablePosition(tile.Position)) continue;
                var before = boardBefore.GetTile(tile.Position);
                if (!before.IsRevealed && tile.IsRevealed && TurnSystem.ShouldEndTurn(tile))
                {
                    var (frillyState, suppressed) = EquipmentSystem.ApplyFrillyDress(state, tile);
                    state = frillyState;
                    if (!suppressed)
                    {
                        turnEnded = true;
                        break;
                    }
                }
            }
        }

        if (turnEnded)
        {
            state = ProcessTurnTransition(state, rng);
        }

        return new ActionResult { State = state, TurnEnded = turnEnded };
    }

    /// <summary>
    /// Processes the player manually ending their turn (without revealing a non-player tile).
    /// </summary>
    public static ActionResult ProcessEndTurn(GameState state, Random rng)
    {
        if (state.CurrentPlayer != PlayerType.Player)
            throw new InvalidOperationException("Not the player's turn");

        if (state.GameStatus != GameStatus.Playing)
            throw new InvalidOperationException("Game is not in progress");

        state = ProcessTurnTransition(state, rng);

        return new ActionResult { State = state, TurnEnded = true };
    }

    /// <summary>
    /// Tracks a player tile reveal. Awards 1 copper on every 5th cumulative reveal.
    /// </summary>
    private static GameState TrackPlayerTileReveal(GameState state)
    {
        var newCount = state.PlayerTilesRevealedCount + 1;
        var copperGain = newCount % 5 == 0 ? 1 * EquipmentSystem.CopperMultiplier(state) : 0;
        var hydrateSpoon = copperGain > 0 && state.HydrateStacks > 0 ? 1 : 0;
        return state with
        {
            PlayerTilesRevealedCount = newCount,
            Copper = state.Copper + copperGain,
            Spoons = state.Spoons + hydrateSpoon
        };
    }

    /// <summary>
    /// Consumes Excuses stacks for any newly **player-revealed** noble tiles
    /// (regular or lounging). Rival-revealed nobles are handled by the rival
    /// mine protection mechanic (see <see cref="ConsumeRivalMineProtectionIfNeeded"/>),
    /// not Excuses.
    /// When Excuses drops to 0: adds 2 Complaints stacks, 1 Mollify to discard, 1 Mollify to top of draw.
    /// </summary>
    private static GameState ConsumeExcusesIfNeeded(GameState state)
    {
        if (state.ExcusesStacks <= 0)
            return state;

        var board = state.Board;
        var newTiles = board.Tiles.ToList();
        var excusesLeft = state.ExcusesStacks;
        var changed = false;

        for (var i = 0; i < newTiles.Count; i++)
        {
            var tile = newTiles[i];
            if (!board.IsUsablePosition(tile.Position)) continue;
            if (!tile.IsRevealed || tile.IsDestroyed) continue;
            if (tile.RevealedBy != PlayerType.Player) continue;
            if (tile.ProtectedByExcuses) continue;

            var functionsAsNoble = tile.Owner == TileOwner.Noble || tile.IsLoungingNoble;
            if (!functionsAsNoble) continue;

            if (excusesLeft <= 0) break;

            newTiles[i] = tile with { ProtectedByExcuses = true };
            excusesLeft--;
            changed = true;
        }

        if (!changed) return state;

        state = state with
        {
            Board = board with { Tiles = newTiles },
            ExcusesStacks = excusesLeft
        };

        // Penalty when Excuses drops to 0: +2 Complaints, +2 Mollify (1 discard, 1 draw top)
        if (excusesLeft == 0)
        {
            state = state with { ComplaintsStacks = state.ComplaintsStacks + 2 };

            var mollifyDiscard = CardDefinitions.Mollify with { Id = $"mollify_{Guid.NewGuid():N}" };
            var discardPile = state.DiscardPile.ToList();
            discardPile.Add(mollifyDiscard);

            var mollifyDraw = CardDefinitions.Mollify with { Id = $"mollify_{Guid.NewGuid():N}" };
            var drawPile = state.DrawPile.ToList();
            drawPile.Add(mollifyDraw); // top of draw pile = end of list (drawn first)

            state = state with
            {
                DiscardPile = discardPile,
                DrawPile = drawPile
            };
        }

        return state;
    }

    /// <summary>
    /// Consumes RivalMineProtectionCount for any newly **rival-revealed** noble tiles
    /// (regular or lounging). Each protected reveal awards 5 copper (× Tiara) and marks
    /// the tile so CheckGameStatus doesn't treat it as a floor-win trigger.
    /// </summary>
    private static GameState ConsumeRivalMineProtectionIfNeeded(GameState state)
    {
        if (state.RivalMineProtectionCount <= 0) return state;

        var board = state.Board;
        var newTiles = board.Tiles.ToList();
        var protectionLeft = state.RivalMineProtectionCount;
        var copperGained = 0;
        var changed = false;

        for (var i = 0; i < newTiles.Count; i++)
        {
            var tile = newTiles[i];
            if (!board.IsUsablePosition(tile.Position)) continue;
            if (!tile.IsRevealed || tile.IsDestroyed) continue;
            if (tile.RevealedBy != PlayerType.Rival) continue;
            if (tile.ProtectedByRivalMineProtection) continue;

            var functionsAsNoble = tile.Owner == TileOwner.Noble || tile.IsLoungingNoble;
            if (!functionsAsNoble) continue;

            if (protectionLeft <= 0) break;

            newTiles[i] = tile with { ProtectedByRivalMineProtection = true };
            protectionLeft--;
            copperGained += 5 * EquipmentSystem.CopperMultiplier(state);
            changed = true;
        }

        if (!changed) return state;

        return state with
        {
            Board = board with { Tiles = newTiles },
            RivalMineProtectionCount = protectionLeft,
            Copper = state.Copper + copperGained
        };
    }

    /// <summary>
    /// Handles the turn transition: end player turn → rival turn → start new player turn.
    /// </summary>
    private static GameState ProcessTurnTransition(GameState state, Random rng)
    {
        // End player turn
        state = TurnSystem.EndPlayerTurn(state);

        // Rival turn
        state = TurnSystem.ExecuteRivalTurn(state, rng);

        // Absorb any rival-revealed nobles via mine protection (before win/loss check)
        state = ConsumeRivalMineProtectionIfNeeded(state);

        // Place rival lounging nobles (per RivalPlacesMines) on player/neutral tiles
        var levelConfig = LevelConfigs.GetById(state.CurrentLevelId);
        if (levelConfig != null && levelConfig.RivalPlacesMines > 0)
        {
            state = state with
            {
                Board = BoardSystem.PlaceRivalLoungingNobles(state.Board, levelConfig.RivalPlacesMines, rng)
            };
        }

        // Check if rival reveal ended the game
        var status = TurnSystem.CheckGameStatus(state);
        state = state with { GameStatus = status };

        if (status != GameStatus.Playing)
            return state;

        // Start new player turn
        state = TurnSystem.StartPlayerTurn(state, rng);

        return state;
    }
}
