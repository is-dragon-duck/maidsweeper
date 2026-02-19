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
        var revealedTile = state.Board.GetTile(pos);
        var turnEnded = TurnSystem.ShouldEndTurn(revealedTile);

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
        if (card.EffectType == CardEffectType.Scurry || card.EffectType == CardEffectType.AcceptHelp)
        {
            foreach (var tile in state.Board.Tiles)
            {
                if (!state.Board.IsUsablePosition(tile.Position)) continue;
                var before = boardBefore.GetTile(tile.Position);
                if (!before.IsRevealed && tile.IsRevealed && TurnSystem.ShouldEndTurn(tile))
                {
                    turnEnded = true;
                    break;
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
    /// Consumes Excuses stacks for any newly revealed noble tiles.
    /// Marks protected nobles so CheckGameStatus won't treat them as losses.
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
            if (board.IsUsablePosition(tile.Position) && tile.IsRevealed && !tile.IsDestroyed
                && tile.Owner == TileOwner.Noble && !tile.ProtectedByExcuses
                && excusesLeft > 0)
            {
                newTiles[i] = tile with { ProtectedByExcuses = true };
                excusesLeft--;
                changed = true;
            }
        }

        if (!changed) return state;

        return state with
        {
            Board = board with { Tiles = newTiles },
            ExcusesStacks = excusesLeft
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
