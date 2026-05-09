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

        // Soirées spawn courtiers at the start of every rival turn
        state = state with { Board = BoardSystem.SpawnCourtiersFromSoirees(state.Board, rng) };

        // Choker: skip the rival's reveals when ≤5 unrevealed tiles remain.
        if (EquipmentSystem.ShouldChokerSuppressRivalTurn(state))
        {
            return state;
        }

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

            // Taunt: end the chain early if any active Taunt's threshold is met,
            // and consume the triggered Taunts so they don't fire again.
            if (TryConsumeTriggeredTaunts(state, out var afterTaunts))
            {
                state = afterTaunts;
                break;
            }
        }

        if (revealedPositions.Count > 0)
        {
            var decayed = IntentSystem.DecayIntent(state, revealedPositions);
            state = state with { RivalIntentPoints = decayed };
        }

        return state;
    }

    /// <summary>
    /// Returns true (and the updated state) if any active Taunt has met its
    /// required-reveals threshold. Triggered Taunts are removed from
    /// <see cref="GameState.ActiveTaunts"/> so they don't end every subsequent turn.
    /// </summary>
    private static bool TryConsumeTriggeredTaunts(GameState state, out GameState newState)
    {
        var triggered = new List<TauntEffect>();
        var remaining = new List<TauntEffect>();

        foreach (var taunt in state.ActiveTaunts)
        {
            var revealedByRival = taunt.Positions.Count(p =>
            {
                if (!state.Board.IsValidPosition(p)) return false;
                var tile = state.Board.GetTile(p);
                return tile.IsRevealed && tile.RevealedBy == PlayerType.Rival;
            });

            if (revealedByRival >= taunt.RequiredReveals)
                triggered.Add(taunt);
            else
                remaining.Add(taunt);
        }

        if (triggered.Count == 0)
        {
            newState = state;
            return false;
        }

        newState = state with { ActiveTaunts = remaining };
        return true;
    }

    /// <summary>
    /// Checks the game status based on current board state.
    /// Player-revealed unprotected noble (regular or lounging) → Lost.
    /// Rival-revealed unprotected noble → Won (floor completes in player's favor).
    /// All player tiles revealed → Won.
    /// </summary>
    public static GameStatus CheckGameStatus(GameState state)
    {
        var board = state.Board;

        // Look at every revealed tile that functions as a noble (regular or lounging).
        foreach (var t in board.Tiles)
        {
            if (!board.IsUsablePosition(t.Position)) continue;
            if (!t.IsRevealed || t.IsDestroyed) continue;
            if (t.ProtectedByExcuses || t.ProtectedByRivalMineProtection) continue;

            var functionsAsNoble = t.Owner == TileOwner.Noble || t.IsLoungingNoble;
            if (!functionsAsNoble) continue;

            return t.RevealedBy == PlayerType.Player
                ? GameStatus.Lost
                : GameStatus.Won;
        }

        // Check if all rival tiles were destroyed (loss) — only if some existed
        var rivalTiles = board.Tiles.Where(t => board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival).ToList();
        if (rivalTiles.Count > 0
            && rivalTiles.Any(t => t.IsDestroyed)
            && !rivalTiles.Any(t => !t.IsRevealed && !t.IsDestroyed))
        {
            return GameStatus.Lost;
        }

        // Check if all non-destroyed player tiles are revealed (win).
        // Destroyed player tiles count as "found".
        // Favor equipment relaxes the threshold by 1 (win with 1 player tile remaining).
        var playerTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Player && !t.IsDestroyed)
            .ToList();
        var revealedPlayerCount = playerTiles.Count(t => t.IsRevealed);
        var hasFavor = EquipmentSystem.HasEquipment(state, EquipmentEffectType.Favor);
        var winThreshold = hasFavor ? Math.Max(0, playerTiles.Count - 1) : playerTiles.Count;

        if (revealedPlayerCount >= winThreshold)
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
