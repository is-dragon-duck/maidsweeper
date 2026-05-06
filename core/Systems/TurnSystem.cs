namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class TurnSystem
{
    /// <summary>
    /// Starts a new player turn: discard hand, draw 5, reset spoons, increment turn.
    /// </summary>
    public static GameState StartPlayerTurn(GameState state, Random rng)
    {
        state = DeckSystem.DiscardHand(state);
        var drawCount = EquipmentSystem.GetTurnDrawCount(state);
        state = DeckSystem.DrawCards(state, drawCount, rng);

        state = state with
        {
            Spoons = state.MaxSpoons,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = state.TurnNumber + 1
        };

        return EquipmentSystem.ApplyOnTurnStart(state, rng);
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
        // Reset Distraction stacks at start of rival turn (consumed by AI)
        state = state with { DistractionStacks = 0 };

        var rivalTiles = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && !t.IsDestroyed && t.Owner == TileOwner.Rival)
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
    /// Lost: any noble tile revealed.
    /// </summary>
    public static GameStatus CheckGameStatus(GameState state)
    {
        var board = state.Board;

        // Check for noble reveal (loss) — destroyed and Excuses-protected nobles don't count
        if (board.Tiles.Any(t => board.IsUsablePosition(t.Position) && t.IsRevealed && !t.IsDestroyed
                                 && t.Owner == TileOwner.Noble && !t.ProtectedByExcuses))
            return GameStatus.Lost;

        // Check if all rival tiles were destroyed (loss) — only if some existed
        var rivalTiles = board.Tiles.Where(t => board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival).ToList();
        if (rivalTiles.Count > 0
            && rivalTiles.Any(t => t.IsDestroyed)
            && !rivalTiles.Any(t => !t.IsRevealed && !t.IsDestroyed))
        {
            return GameStatus.Lost;
        }

        // Check if all non-destroyed player tiles are revealed (win)
        // Destroyed player tiles count as "found"
        var allPlayerDone = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Player && !t.IsDestroyed)
            .All(t => t.IsRevealed);

        if (allPlayerDone)
            return GameStatus.Won;

        return GameStatus.Playing;
    }

    /// <summary>
    /// Determines if a tile reveal should end the player's turn.
    /// Revealing a non-player tile (rival, neutral, noble) ends the turn.
    /// </summary>
    public static bool ShouldEndTurn(Tile revealedTile)
    {
        return revealedTile.Owner != TileOwner.Player;
    }
}
