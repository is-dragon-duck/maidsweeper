namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

/// <summary>
/// Applies equipment passive effects at well-defined trigger points.
/// Stage 4 Set 1: Coffee, Frilly Dress, Dust Bunny, Handbag, Eyeshadow, Glasses.
/// </summary>
public static class EquipmentSystem
{
    public static bool HasEquipment(GameState state, EquipmentEffectType type) =>
        state.Equipment.Any(e => e.EffectType == type);

    /// <summary>
    /// Returns the draw count for a non-initial player turn (called from StartPlayerTurn).
    /// Coffee reduces draws by 1 on turns 2+. Read adds 1 if any stacks remain.
    /// </summary>
    public static int GetTurnDrawCount(GameState state)
    {
        var drawCount = 5 + (state.ReadStacks > 0 ? 1 : 0);
        if (HasEquipment(state, EquipmentEffectType.Coffee))
            drawCount -= 1;
        return Math.Max(0, drawCount);
    }

    /// <summary>
    /// Floor-start equipment effects. Called once per floor after Equipment is propagated.
    /// Coffee: +1 MaxSpoons (and refill). Handbag: draw 2 extra cards.
    /// Dust Bunny: reveal 1 random player tile.
    /// </summary>
    public static GameState ApplyOnFloorStart(GameState state, Random rng)
    {
        if (HasEquipment(state, EquipmentEffectType.Coffee))
        {
            var newMax = state.MaxSpoons + 1;
            state = state with { MaxSpoons = newMax, Spoons = newMax };
        }

        if (HasEquipment(state, EquipmentEffectType.Handbag))
        {
            state = DeckSystem.DrawCards(state, 2, rng);
        }

        if (HasEquipment(state, EquipmentEffectType.DustBunny))
        {
            state = RevealRandomPlayerTile(state, rng);
        }

        return state;
    }

    /// <summary>
    /// Turn-start equipment effects. Called for turn 1 (from floor start) and every
    /// subsequent turn (from StartPlayerTurn after the new turn state is in place).
    /// Eyeshadow: +1 Distraction stack. Glasses: free Tingle effect.
    /// </summary>
    public static GameState ApplyOnTurnStart(GameState state, Random rng)
    {
        if (HasEquipment(state, EquipmentEffectType.Eyeshadow))
        {
            state = state with { DistractionStacks = state.DistractionStacks + 1 };
        }

        if (HasEquipment(state, EquipmentEffectType.Glasses))
        {
            state = CardEffectSystem.ExecuteTingle(state, rng, CardDefinitions.Tingle);
        }

        return state;
    }

    /// <summary>
    /// Frilly Dress: suppress turn end for the first 4 neutral reveals on turn 1.
    /// Returns the updated state and whether the turn end was suppressed.
    /// </summary>
    public static (GameState state, bool suppressed) ApplyFrillyDress(GameState state, Tile revealedTile)
    {
        if (revealedTile.Owner != TileOwner.Neutral) return (state, false);
        if (state.TurnNumber != 1) return (state, false);
        if (!HasEquipment(state, EquipmentEffectType.FrillyDress)) return (state, false);
        if (state.Turn1NeutralReveals >= 4) return (state, false);

        state = state with { Turn1NeutralReveals = state.Turn1NeutralReveals + 1 };
        return (state, true);
    }

    /// <summary>
    /// Reveals 1 random unrevealed player tile (used by Dust Bunny floor-start hook).
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
}
