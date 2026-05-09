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

        if (HasEquipment(state, EquipmentEffectType.Hyperfocus))
        {
            state = ApplyHyperfocus(state, rng);
        }

        if (HasEquipment(state, EquipmentEffectType.Mirror))
        {
            state = ApplyMirror(state, rng);
        }

        if (HasEquipment(state, EquipmentEffectType.BusyCanary))
        {
            state = ApplyBusyCanary(state, rng);
        }

        return state;
    }

    /// <summary>
    /// Hyperfocus: pull one random net-cost-0 card (Cost - BonusSpoon refund == 0) from
    /// the draw pile into the hand. No-op if no eligible card in draw pile.
    /// </summary>
    private static GameState ApplyHyperfocus(GameState state, Random rng)
    {
        var candidateIndices = Enumerable.Range(0, state.DrawPile.Count)
            .Where(i =>
            {
                var c = state.DrawPile[i];
                return c.Cost - (c.BonusSpoon ? 1 : 0) == 0;
            })
            .ToList();

        if (candidateIndices.Count == 0) return state;

        var idx = candidateIndices[rng.Next(candidateIndices.Count)];
        var picked = state.DrawPile[idx];
        var newDraw = state.DrawPile.ToList();
        newDraw.RemoveAt(idx);
        var newHand = state.Hand.ToList();
        newHand.Add(picked);
        return state with { DrawPile = newDraw, Hand = newHand };
    }

    /// <summary>
    /// Mirror: reveal one random unrevealed rival tile (by Rival, so it doesn't
    /// trigger the player's turn-end), then add player adjacency annotations to
    /// each of its unrevealed neighbors.
    /// </summary>
    private static GameState ApplyMirror(GameState state, Random rng)
    {
        var rivals = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed
                        && t.Owner == TileOwner.Rival)
            .ToList();
        if (rivals.Count == 0) return state;

        var pick = rivals[rng.Next(rivals.Count)];
        state = state with { Board = BoardSystem.RevealTile(state.Board, pick.Position, PlayerType.Rival) };

        foreach (var neighbor in BoardSystem.GetNeighbors(state.Board, pick.Position))
        {
            var nTile = state.Board.GetTile(neighbor);
            if (nTile.IsRevealed) continue;
            var playerCount = BoardSystem.CalculateAdjacency(state.Board, neighbor, PlayerType.Player);
            state = AnnotationSystem.AddAdjacencyInfo(state, neighbor,
                new AdjacencyInfo { PlayerCount = playerCount });
        }
        return state;
    }

    /// <summary>
    /// Busy Canary: run up to 2 cross-area Peek-style scans at random unrevealed positions.
    /// Each scan annotates nobles in the cross as `{Noble}` and other tiles as
    /// `{Player, Rival, Neutral}`.
    /// </summary>
    private static GameState ApplyBusyCanary(GameState state, Random rng)
    {
        var candidates = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed)
            .Select(t => t.Position)
            .ToList();
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var center in candidates.Take(2))
        {
            foreach (var tile in BoardSystem.GetTilesInCross(state.Board, center))
            {
                if (tile.IsRevealed) continue;
                if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, tile.Position)) continue;

                var subset = tile.Owner == TileOwner.Noble
                    ? new HashSet<TileOwner> { TileOwner.Noble }
                    : new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral };
                state = AnnotationSystem.AddOwnerSubset(state, tile.Position, subset);
            }
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
            var points = new Dictionary<Position, int>(state.RivalIntentPoints);
            IntentSystem.AddDistractionPoint(points, IntentSystem.GetExcludedPositions(state.Board), rng);
            state = state with { RivalIntentPoints = points };
        }

        if (HasEquipment(state, EquipmentEffectType.Glasses))
        {
            state = CardEffectSystem.ExecuteTingle(state, rng, CardDefinitions.Tingle);
        }

        if (HasEquipment(state, EquipmentEffectType.Espresso))
        {
            state = TriggerEspresso(state, rng);
        }

        return state;
    }

    /// <summary>
    /// Espresso: draw 1 extra card, then auto-play the cheapest non-targeting card
    /// that the player can afford. No-op if no eligible card or insufficient spoons.
    /// </summary>
    private static GameState TriggerEspresso(GameState state, Random rng)
    {
        state = DeckSystem.DrawCards(state, 1, rng);

        var pickable = state.Hand
            .Where(c => IsAutoPlayable(c.EffectType))
            .Where(c => DeckSystem.CanPlayCard(state, c))
            .OrderBy(c => c.Cost)
            .ToList();

        if (pickable.Count == 0) return state;

        try
        {
            state = CardEffectSystem.PlayCard(state, pickable[0], targets: null, rng);
        }
        catch
        {
            // Defensive: a card unexpectedly threw — leave state untouched
        }
        return state;
    }

    /// <summary>
    /// Returns true for card effects that don't require tile targeting or
    /// hand/exhaust card selection — eligible for Espresso auto-play.
    /// </summary>
    private static bool IsAutoPlayable(CardEffectType type) => type switch
    {
        CardEffectType.Recall => true,
        CardEffectType.RecallVague => true,
        CardEffectType.RecallSarcastic => true,
        CardEffectType.Tingle => true,
        CardEffectType.Twirl => true,
        CardEffectType.Caffeinate => true,
        CardEffectType.Breathe => true,
        CardEffectType.LockIn => true,
        CardEffectType.Rendezvous => true,
        CardEffectType.Ramble => true,
        CardEffectType.Glaze => true,
        CardEffectType.Mollify => true,
        CardEffectType.Read => true,
        CardEffectType.Hydrate => true,
        CardEffectType.Adopt => true,
        CardEffectType.Pose => true,
        _ => false
    };

    /// <summary>
    /// Mop: when a courtier is cleaned, draw 1 card. Call after BoardSystem.CleanCourtier
    /// (when the caller verified that a courtier was actually present).
    /// </summary>
    public static GameState OnCourtierCleaned(GameState state, Random rng)
    {
        if (HasEquipment(state, EquipmentEffectType.Mop))
        {
            state = DeckSystem.DrawCards(state, 1, rng);
        }
        return state;
    }

    /// <summary>
    /// Double Broom: when the player reveals a tile, Brush 2 random adjacent
    /// unrevealed tiles (each annotated with a random non-owner exclusion).
    /// Called by GameRunner.ProcessReveal after a successful tile reveal.
    /// </summary>
    public static GameState OnTileRevealedByPlayer(GameState state, Position revealedPos, Random rng)
    {
        if (!HasEquipment(state, EquipmentEffectType.DoubleBroom)) return state;

        var neighbors = BoardSystem.GetNeighbors(state.Board, revealedPos)
            .Where(n =>
            {
                var t = state.Board.GetTile(n);
                if (t.IsRevealed || t.IsDestroyed) return false;
                if (t.IsInner && !BoardSystem.CanReachInnerTile(state.Board, n)) return false;
                return true;
            })
            .ToList();

        for (var i = neighbors.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (neighbors[i], neighbors[j]) = (neighbors[j], neighbors[i]);
        }

        var allOwners = new[] { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble };
        foreach (var pos in neighbors.Take(2))
        {
            var tile = state.Board.GetTile(pos);
            var nonOwners = allOwners.Where(o => o != tile.Owner).ToList();
            if (nonOwners.Count == 0) continue;
            var excluded = nonOwners[rng.Next(nonOwners.Count)];
            var subset = new HashSet<TileOwner>(allOwners.Where(o => o != excluded));
            state = AnnotationSystem.AddOwnerSubset(state, pos, subset);
        }
        return state;
    }

    /// <summary>
    /// Choker: rival's turn ends early when 5 or fewer unrevealed (non-destroyed)
    /// tiles remain on the board.
    /// </summary>
    public static bool ShouldChokerSuppressRivalTurn(GameState state)
    {
        if (!HasEquipment(state, EquipmentEffectType.Choker)) return false;
        var unrevealed = state.Board.Tiles.Count(t =>
            state.Board.IsUsablePosition(t.Position)
            && !t.IsRevealed
            && !t.IsDestroyed);
        return unrevealed <= 5;
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
            EquipmentEffectType.BroomCloset => ApplyBroomCloset(state),
            EquipmentEffectType.Cocktail => ApplyCocktail(state, rng),
            EquipmentEffectType.Novel => ApplyNovel(state),
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
    /// Broom Closet: remove all Spritz cards from the persistent deck, add 3 Sweep cards.
    /// </summary>
    private static GameState ApplyBroomCloset(GameState state)
    {
        var deck = state.PersistentDeck
            .Where(c => c.EffectType != CardEffectType.Spritz)
            .ToList();
        for (var i = 0; i < 3; i++)
        {
            var sweep = CardDefinitions.Sweep with { Id = $"broomcloset_{Guid.NewGuid():N}" };
            // Bleach interaction: if owned, the new Sweep is auto-enhanced
            sweep = ApplyBleachToNewCard(state, sweep);
            deck.Add(sweep);
        }
        return state with { PersistentDeck = deck };
    }

    /// <summary>
    /// Cocktail: remove all Scurry cards from the persistent deck, add 2 random
    /// bonus-spoon cards drawn from the reward pool.
    /// </summary>
    private static GameState ApplyCocktail(GameState state, Random rng)
    {
        var deck = state.PersistentDeck
            .Where(c => c.EffectType != CardEffectType.Scurry)
            .ToList();
        var pool = CardDefinitions.CreateRewardPool();
        for (var i = 0; i < 2; i++)
        {
            var template = pool[rng.Next(pool.Count)];
            var card = template with
            {
                Id = $"cocktail_{Guid.NewGuid():N}",
                BonusSpoon = true
            };
            deck.Add(card);
        }
        return state with { PersistentDeck = deck };
    }

    /// <summary>
    /// Novel: replace every Recall card (Imperious / Vague / Sarcastic) in the
    /// persistent deck with a doubly-upgraded Sarcastic Recall.
    /// </summary>
    private static GameState ApplyNovel(GameState state)
    {
        var deck = state.PersistentDeck.ToList();
        for (var i = 0; i < deck.Count; i++)
        {
            var c = deck[i];
            var isRecall = c.EffectType == CardEffectType.Recall
                        || c.EffectType == CardEffectType.RecallVague
                        || c.EffectType == CardEffectType.RecallSarcastic;
            if (!isRecall) continue;

            deck[i] = CardDefinitions.RecallSarcastic with
            {
                Id = $"novel_{Guid.NewGuid():N}",
                Enhanced = true,
                BonusSpoon = true
            };
        }
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
