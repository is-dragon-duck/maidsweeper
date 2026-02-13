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

        // Check game status
        var status = TurnSystem.CheckGameStatus(state);
        state = state with { GameStatus = status };

        if (status != GameStatus.Playing)
        {
            return new ActionResult { State = state, TurnEnded = true };
        }

        // Check if turn should end (non-player tile revealed)
        var revealedTile = newBoard.GetTile(pos);
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

        state = CardEffectSystem.PlayCard(state, card, targets, rng);

        // Check game status (Scurry can reveal tiles, potentially hitting a noble)
        var status = TurnSystem.CheckGameStatus(state);
        state = state with { GameStatus = status };

        if (status != GameStatus.Playing)
        {
            return new ActionResult { State = state, TurnEnded = true };
        }

        // Check if a Scurry reveal ended the turn
        // Scurry reveals a tile — if it was non-player, turn should end
        var turnEnded = false;
        if (card.EffectType == CardEffectType.Scurry && targets != null)
        {
            // Check which target was revealed by Scurry
            foreach (var target in targets)
            {
                var tile = state.Board.GetTile(target);
                if (tile.IsRevealed && tile.RevealedBy == PlayerType.Player && TurnSystem.ShouldEndTurn(tile))
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
