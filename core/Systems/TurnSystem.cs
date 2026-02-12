namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class TurnSystem
{
    /// <summary>
    /// Starts a new player turn: discard hand, draw 5, reset energy, increment turn.
    /// </summary>
    public static GameState StartPlayerTurn(GameState state, Random rng)
    {
        state = DeckSystem.DiscardHand(state);
        state = DeckSystem.DrawCards(state, 5, rng);

        return state with
        {
            Energy = state.MaxEnergy,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = state.TurnNumber + 1
        };
    }

    /// <summary>
    /// Ends the player turn and transitions to rival turn.
    /// </summary>
    public static GameState EndPlayerTurn(GameState state)
    {
        return state with { CurrentPlayer = PlayerType.Rival };
    }

    /// <summary>
    /// Executes the rival's turn: reveals 1 random unrevealed rival tile.
    /// Then transitions back to player.
    /// </summary>
    public static GameState ExecuteRivalTurn(GameState state, Random rng)
    {
        var rivalTiles = state.Board.Tiles
            .Where(t => !t.IsRevealed && t.Owner == TileOwner.Rival)
            .ToList();

        if (rivalTiles.Count > 0)
        {
            var target = rivalTiles[rng.Next(rivalTiles.Count)];
            state = state with
            {
                Board = BoardSystem.RevealTile(state.Board, target.Position, PlayerType.Rival)
            };
        }

        return state;
    }

    /// <summary>
    /// Checks the game status based on current board state.
    /// Won: all player tiles revealed.
    /// Lost: any mine tile revealed.
    /// </summary>
    public static GameStatus CheckGameStatus(GameState state)
    {
        var board = state.Board;

        // Check for mine reveal (loss)
        if (board.Tiles.Any(t => t.IsRevealed && t.Owner == TileOwner.Mine))
            return GameStatus.Lost;

        // Check if all player tiles revealed (win)
        var allPlayerRevealed = board.Tiles
            .Where(t => t.Owner == TileOwner.Player)
            .All(t => t.IsRevealed);

        if (allPlayerRevealed)
            return GameStatus.Won;

        return GameStatus.Playing;
    }

    /// <summary>
    /// Determines if a tile reveal should end the player's turn.
    /// Revealing a non-player tile (rival, neutral, mine) ends the turn.
    /// </summary>
    public static bool ShouldEndTurn(Tile revealedTile)
    {
        return revealedTile.Owner != TileOwner.Player;
    }
}
