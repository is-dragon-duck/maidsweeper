namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class DeckSystem
{
    /// <summary>
    /// Shuffles a list of cards using Fisher-Yates.
    /// Returns a new shuffled list (does not mutate input).
    /// </summary>
    public static List<Card> Shuffle(IReadOnlyList<Card> cards, Random rng)
    {
        var shuffled = cards.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }

    /// <summary>
    /// Draws N cards from the draw pile into hand.
    /// If draw pile runs out, shuffles discard into draw pile and continues.
    /// Returns new GameState with updated hand, draw pile, and discard pile.
    /// </summary>
    public static GameState DrawCards(GameState state, int count, Random rng)
    {
        var hand = state.Hand.ToList();
        var drawPile = state.DrawPile.ToList();
        var discardPile = state.DiscardPile.ToList();

        for (var i = 0; i < count; i++)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                    break; // Nothing left to draw

                // Shuffle discard into draw pile
                drawPile = Shuffle(discardPile, rng);
                discardPile.Clear();
            }

            // Draw from end of list (pop)
            var card = drawPile[^1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
        }

        return state with
        {
            Hand = hand,
            DrawPile = drawPile,
            DiscardPile = discardPile
        };
    }

    /// <summary>
    /// Moves all cards from hand to discard pile.
    /// </summary>
    public static GameState DiscardHand(GameState state)
    {
        if (state.Hand.Count == 0)
            return state;

        var discardPile = state.DiscardPile.ToList();
        discardPile.AddRange(state.Hand);

        return state with
        {
            Hand = Array.Empty<Card>(),
            DiscardPile = discardPile
        };
    }

    /// <summary>
    /// Moves a specific card from hand to discard pile.
    /// </summary>
    public static GameState DiscardCard(GameState state, Card card)
    {
        var hand = state.Hand.ToList();
        var removed = hand.Remove(card);
        if (!removed)
            throw new InvalidOperationException($"Card '{card.Name}' ({card.Id}) not found in hand");

        var discardPile = state.DiscardPile.ToList();
        discardPile.Add(card);

        return state with
        {
            Hand = hand,
            DiscardPile = discardPile
        };
    }

    /// <summary>
    /// Moves a specific card to the exhaust pile (removed from play for this floor).
    /// </summary>
    public static GameState ExhaustCard(GameState state, Card card)
    {
        var exhaustPile = state.ExhaustPile.ToList();
        exhaustPile.Add(card);

        return state with { ExhaustPile = exhaustPile };
    }

    /// <summary>
    /// Returns the effective cost of a card, accounting for status effects.
    /// Accept Help costs 0 when AcceptHelpDiscount is active.
    /// </summary>
    public static int GetEffectiveCost(GameState state, Card card)
    {
        if (card.EffectType == CardEffectType.AcceptHelp && state.AcceptHelpDiscount)
            return 0;
        return card.Cost;
    }

    /// <summary>
    /// Checks if a card can be played given current spoons.
    /// </summary>
    public static bool CanPlayCard(GameState state, Card card)
    {
        return state.Spoons >= GetEffectiveCost(state, card);
    }

    /// <summary>
    /// Deducts spoons for playing a card. Full cost is always paid upfront.
    /// BonusSpoon is applied separately after the card effect executes.
    /// </summary>
    public static GameState SpendSpoons(GameState state, Card card)
    {
        var newSpoons = state.Spoons - GetEffectiveCost(state, card);

        if (newSpoons < 0)
            throw new InvalidOperationException(
                $"Not enough spoons to play '{card.Name}' (cost {card.Cost}, have {state.Spoons})");

        return state with { Spoons = newSpoons };
    }
}
