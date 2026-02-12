namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class CardEffectSystem
{
    /// <summary>
    /// Plays a card: spend energy, execute effect, discard/exhaust card.
    /// </summary>
    public static GameState PlayCard(GameState state, Card card, Position[]? targets, Random rng)
    {
        if (!DeckSystem.CanPlayCard(state, card))
            throw new InvalidOperationException(
                $"Not enough energy to play '{card.Name}' (cost {card.Cost}, have {state.Energy})");

        // Spend energy
        state = DeckSystem.SpendEnergy(state, card);

        // Remove card from hand
        var hand = state.Hand.ToList();
        hand.Remove(card);
        state = state with { Hand = hand };

        // Execute effect
        state = card.EffectType switch
        {
            CardEffectType.Spritz => ExecuteSpritz(state, targets, card),
            CardEffectType.Recall => ExecuteInstructions(state, rng, card),
            CardEffectType.Scurry => ExecuteScurry(state, targets, rng, card),
            CardEffectType.Tingle => ExecuteTingle(state, rng, card),
            CardEffectType.Twirl => ExecuteTwirl(state, card),
            _ => throw new ArgumentException($"Unknown card effect type: {card.EffectType}")
        };

        // Discard or exhaust
        if (card.Exhaust)
        {
            state = DeckSystem.ExhaustCard(state, card);
        }
        else
        {
            var discardPile = state.DiscardPile.ToList();
            discardPile.Add(card);
            state = state with { DiscardPile = discardPile };
        }

        return state;
    }

    /// <summary>
    /// Spritz: Target 1 unrevealed tile.
    /// If Player or Neutral → safe: annotate {Player, Neutral}
    /// If Rival or Mine → dangerous: annotate {Rival, Mine}
    /// </summary>
    public static GameState ExecuteSpritz(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Spritz requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (tile.IsRevealed)
            throw new ArgumentException("Cannot Spritz a revealed tile");

        var isSafe = tile.Owner == TileOwner.Player || tile.Owner == TileOwner.Neutral;
        var subset = isSafe
            ? new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral }
            : new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Noble };

        return AnnotationSystem.AddOwnerSubset(state, pos, subset);
    }

    /// <summary>
    /// Recall - Imperious: No targeting. Distributes clue pips via bag draw.
    /// </summary>
    public static GameState ExecuteInstructions(GameState state, Random rng, Card card)
    {
        var clueResults = ClueSystem.GenerateImperiousClue(state, rng, card.Enhanced);

        foreach (var result in clueResults)
        {
            state = AnnotationSystem.AddClueResult(state, result.TilePosition, result);
        }

        return state;
    }

    /// <summary>
    /// Scurry: Target 2 unrevealed tiles. Reveals the safer one.
    /// Safety: Player(4) > Neutral(3) > Rival(2) > Mine(1).
    /// Non-revealed tiles get an ownerSubset annotation of types at-most-as-safe as the revealed tile.
    /// </summary>
    public static GameState ExecuteScurry(GameState state, Position[]? targets, Random rng, Card card)
    {
        if (targets == null || targets.Length != 2)
            throw new ArgumentException("Scurry requires exactly 2 target tiles");

        var tiles = targets.Select(p => (pos: p, tile: state.Board.GetTile(p))).ToList();

        if (tiles.Any(t => t.tile.IsRevealed))
            throw new ArgumentException("Cannot Scurry revealed tiles");

        // Sort by safety descending; break ties randomly
        var sorted = tiles.OrderByDescending(t => GetSafety(t.tile.Owner)).ToList();

        // Break ties with random
        if (sorted.Count == 2 && GetSafety(sorted[0].tile.Owner) == GetSafety(sorted[1].tile.Owner))
        {
            if (rng.Next(2) == 1)
                (sorted[0], sorted[1]) = (sorted[1], sorted[0]);
        }

        var safest = sorted[0];
        var other = sorted[1];

        // Reveal the safer tile (player reveals)
        state = BoardSystem.RevealTile(state.Board, safest.pos, PlayerType.Player) is var newBoard
            ? state with { Board = newBoard }
            : state;

        // Annotate the other tile: types at-most-as-safe as what was revealed
        var revealedSafety = GetSafety(safest.tile.Owner);
        var possibleOwners = new HashSet<TileOwner>();
        foreach (TileOwner owner in Enum.GetValues<TileOwner>())
        {
            if (GetSafety(owner) <= revealedSafety)
                possibleOwners.Add(owner);
        }

        state = AnnotationSystem.AddOwnerSubset(state, other.pos, possibleOwners);

        return state;
    }

    /// <summary>
    /// Tingle: No targeting. Picks 1 random unrevealed rival/noble tile
    /// and annotates it with its exact owner type.
    /// Prefers "ambiguous" tiles (no single-owner annotation yet).
    /// </summary>
    public static GameState ExecuteTingle(GameState state, Random rng, Card card)
    {
        var candidates = state.Board.Tiles
            .Where(t => !t.IsRevealed && (t.Owner == TileOwner.Rival || t.Owner == TileOwner.Noble))
            .ToList();

        if (candidates.Count == 0)
            return state; // No valid targets

        // Prefer ambiguous tiles (no single-owner annotation)
        var ambiguous = candidates
            .Where(t => t.Annotations.OwnerSubset == null || t.Annotations.OwnerSubset.Count != 1)
            .ToList();

        var pool = ambiguous.Count > 0 ? ambiguous : candidates;
        var target = pool[rng.Next(pool.Count)];

        var exactOwner = new HashSet<TileOwner> { target.Owner };
        return AnnotationSystem.AddOwnerSubset(state, target.Position, exactOwner);
    }

    /// <summary>
    /// Twirl: Gain 3 copper (5 if enhanced). Exhausts.
    /// </summary>
    public static GameState ExecuteTwirl(GameState state, Card card)
    {
        var copperGain = card.Enhanced ? 5 : 3;
        return state with { Copper = state.Copper + copperGain };
    }

    /// <summary>
    /// Safety ranking for Scurry: Player(4) > Neutral(3) > Rival(2) > Mine(1).
    /// </summary>
    private static int GetSafety(TileOwner owner) => owner switch
    {
        TileOwner.Player => 4,
        TileOwner.Neutral => 3,
        TileOwner.Rival => 2,
        TileOwner.Noble => 1,
        _ => 0
    };
}
