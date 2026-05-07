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

    // ===========================================================
    // M31: Set 2 — Deck Modifiers (on-acquisition + Tiara passive)
    // ===========================================================

    /// <summary>
    /// One-shot deck modification when the player picks deck-modifying equipment.
    /// Called from CampaignSystem.SelectEquipment after the equipment is added to inventory.
    /// </summary>
    public static GameState ApplyOnAcquisition(GameState state, Equipment equipment, Random rng)
    {
        return equipment.EffectType switch
        {
            EquipmentEffectType.Bleach => ApplyBleach(state),
            EquipmentEffectType.Estrogen => ApplyEstrogen(state, rng),
            EquipmentEffectType.Progesterone => ApplyProgesterone(state, rng),
            EquipmentEffectType.CrystalBall => ApplyCrystalBall(state, rng),
            EquipmentEffectType.Boots => ApplyBoots(state, rng),
            _ => state
        };
    }

    /// <summary>
    /// Bleach: enhance every Spritz, Sweep, and Brush already in the persistent deck.
    /// </summary>
    private static GameState ApplyBleach(GameState state)
    {
        var deck = state.PersistentDeck.Select(c =>
            IsBleachableEffect(c.EffectType) && !c.Enhanced
                ? c with { Enhanced = true }
                : c
        ).ToList();
        return state with { PersistentDeck = deck };
    }

    private static bool IsBleachableEffect(CardEffectType effect) =>
        effect == CardEffectType.Spritz
        || effect == CardEffectType.Sweep
        || effect == CardEffectType.Brush;

    /// <summary>
    /// Estrogen: pick 3 random non-enhanced cards in the persistent deck and grant BonusSpoon.
    /// Eligibility filter: cards that are not Enhanced (so this doesn't pile onto already-enhanced ones).
    /// </summary>
    private static GameState ApplyEstrogen(GameState state, Random rng)
    {
        return UpgradeRandomCards(state, c => !c.Enhanced, c => c with { BonusSpoon = true }, 3, rng);
    }

    /// <summary>
    /// Progesterone: pick 3 random non-bonus-spoon cards in the persistent deck and grant Enhanced.
    /// </summary>
    private static GameState ApplyProgesterone(GameState state, Random rng)
    {
        return UpgradeRandomCards(state, c => !c.BonusSpoon, c => c with { Enhanced = true }, 3, rng);
    }

    private static GameState UpgradeRandomCards(GameState state, Func<Card, bool> eligible,
        Func<Card, Card> upgrade, int count, Random rng)
    {
        var deck = state.PersistentDeck.ToList();
        var indices = Enumerable.Range(0, deck.Count).Where(i => eligible(deck[i])).ToList();

        // Shuffle eligible indices and take up to `count`
        for (var i = indices.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        foreach (var idx in indices.Take(count))
        {
            deck[idx] = upgrade(deck[idx]);
        }

        return state with { PersistentDeck = deck };
    }

    /// <summary>
    /// Crystal Ball: add 3 doubly-upgraded (Enhanced + BonusSpoon) Tingle cards to the persistent deck.
    /// </summary>
    private static GameState ApplyCrystalBall(GameState state, Random rng)
    {
        var deck = state.PersistentDeck.ToList();
        for (var i = 0; i < 3; i++)
        {
            var tingle = CardDefinitions.Tingle with
            {
                Id = $"crystalball_{Guid.NewGuid():N}",
                Enhanced = true,
                BonusSpoon = true
            };
            deck.Add(tingle);
        }
        return state with { PersistentDeck = deck };
    }

    /// <summary>
    /// Boots: remove 1 random card from the persistent deck and add a doubly-upgraded
    /// random card from the reward pool.
    /// </summary>
    private static GameState ApplyBoots(GameState state, Random rng)
    {
        var deck = state.PersistentDeck.ToList();
        if (deck.Count == 0) return state;

        var idx = rng.Next(deck.Count);
        deck.RemoveAt(idx);

        var pool = CardDefinitions.CreateRewardPool();
        var template = pool[rng.Next(pool.Count)];
        var newCard = template with
        {
            Id = $"boots_{Guid.NewGuid():N}",
            Enhanced = true,
            BonusSpoon = true
        };
        deck.Add(newCard);

        return state with { PersistentDeck = deck };
    }

    /// <summary>
    /// Bleach ongoing effect: when adding a Spritz/Sweep/Brush to the persistent deck,
    /// auto-enhance it if Bleach is owned.
    /// </summary>
    public static Card ApplyBleachToNewCard(GameState state, Card card)
    {
        if (!HasEquipment(state, EquipmentEffectType.Bleach)) return card;
        if (!IsBleachableEffect(card.EffectType)) return card;
        if (card.Enhanced) return card;
        return card with { Enhanced = true };
    }

    /// <summary>
    /// Tiara: doubles copper rewards. Returns the multiplier (1 or 2) based on equipment.
    /// </summary>
    public static int CopperMultiplier(GameState state) =>
        HasEquipment(state, EquipmentEffectType.Tiara) ? 2 : 1;
}
