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

        // Generate this turn's intent points and combine with carry-over.
        var newIntent = IntentSystem.GenerateTurnIntent(state, rng);
        var combined = IntentSystem.Combine(state.RivalIntentPoints, newIntent);
        state = state with { RivalIntentPoints = combined };

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
    /// Executes the rival's turn: reveals 1 tile chosen by intent-weighted preference,
    /// then decays intent points. Falls back to a random rival tile if intent points
    /// are empty (e.g., on InitialRivalReveal before the first player turn).
    /// </summary>
    public static GameState ExecuteRivalTurn(GameState state, Random rng)
    {
        // Reset Distraction stacks at start of rival turn (legacy, kept until full migration)
        state = state with { DistractionStacks = 0 };

        Position? target = null;

        if (state.RivalIntentPoints.Count > 0)
        {
            // Restrict to currently-revealable tiles (skip already-revealed/destroyed/etc.)
            var eligible = state.RivalIntentPoints
                .Where(kv => state.Board.IsUsablePosition(kv.Key)
                             && !state.Board.GetTile(kv.Key).IsRevealed
                             && !state.Board.GetTile(kv.Key).IsDestroyed)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            target = IntentSystem.PickHighestPoints(eligible, rng);
        }

        if (target == null)
        {
            // Fallback: random rival tile (used at floor start before any player turn)
            var rivalTiles = state.Board.Tiles
                .Where(t => state.Board.IsUsablePosition(t.Position)
                            && !t.IsRevealed && !t.IsDestroyed
                            && t.Owner == TileOwner.Rival)
                .ToList();
            if (rivalTiles.Count > 0)
            {
                target = rivalTiles[rng.Next(rivalTiles.Count)].Position;
            }
        }

        if (target.HasValue)
        {
            state = state with
            {
                Board = BoardSystem.RevealTile(state.Board, target.Value, PlayerType.Rival)
            };

            // Decay intent points after the reveal
            var decayed = IntentSystem.DecayIntent(state, new[] { target.Value });
            state = state with { RivalIntentPoints = decayed };
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
