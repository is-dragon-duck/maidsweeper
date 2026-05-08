namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems.AI;

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
    /// Executes the rival's turn: dispatches to the level's configured AI, which
    /// returns a list of positions to reveal. Reveals them in order, stopping at
    /// the first non-rival reveal (which ends the rival's turn). Decays intent
    /// points after all reveals complete.
    /// Falls back to a random rival tile if the AI returns no picks (e.g., on
    /// InitialRivalReveal before any intent points have been generated).
    /// </summary>
    public static GameState ExecuteRivalTurn(GameState state, Random rng)
    {
        // Reset Distraction stacks at start of rival turn (legacy, kept until full migration)
        state = state with { DistractionStacks = 0 };

        var levelConfig = LevelConfigs.GetById(state.CurrentLevelId);
        var aiType = levelConfig?.RivalAi ?? AiType.Random;
        var ai = AiRegistry.Get(aiType);
        var context = new AiContext { LevelConfig = levelConfig };

        var picks = ai.SelectTilesToReveal(state, state.RivalIntentPoints, context, rng);

        if (picks.Count == 0)
        {
            // Fallback: pick a random rival tile (used at floor start before any
            // player turn has generated intent points).
            var rivalTiles = state.Board.Tiles
                .Where(t => state.Board.IsUsablePosition(t.Position)
                            && !t.IsRevealed && !t.IsDestroyed
                            && t.Owner == TileOwner.Rival)
                .ToList();
            if (rivalTiles.Count > 0)
            {
                picks = new[] { rivalTiles[rng.Next(rivalTiles.Count)].Position };
            }
        }

        var revealedPositions = new List<Position>();
        foreach (var pos in picks)
        {
            state = state with
            {
                Board = BoardSystem.RevealTile(state.Board, pos, PlayerType.Rival)
            };
            revealedPositions.Add(pos);

            var revealedTile = state.Board.GetTile(pos);
            if (revealedTile.Owner != TileOwner.Rival) break;
        }

        if (revealedPositions.Count > 0)
        {
            var decayed = IntentSystem.DecayIntent(state, revealedPositions);
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
