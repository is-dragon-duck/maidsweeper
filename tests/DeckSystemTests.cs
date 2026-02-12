using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class DeckSystemTests
{
    private static GameState CreateTestState(List<Card>? drawPile = null, List<Card>? hand = null,
        List<Card>? discardPile = null, int energy = 3)
    {
        return new GameState
        {
            Board = new Board { Width = 1, Height = 1, Tiles = [new Tile { Position = new Position(0, 0), Owner = TileOwner.Player }] },
            DrawPile = drawPile ?? [],
            Hand = hand ?? [],
            DiscardPile = discardPile ?? [],
            Energy = energy
        };
    }

    private static Card MakeCard(string id, string name = "Test", int cost = 1, bool exhaust = false)
    {
        return new Card { Id = id, Name = name, Cost = cost, EffectType = CardEffectType.Spritz, Exhaust = exhaust };
    }

    [Fact]
    public void StarterDeck_Has10Cards()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        Assert.Equal(10, deck.Count);
    }

    [Fact]
    public void StarterDeck_HasCorrectDistribution()
    {
        var deck = CardDefinitions.CreateStarterDeck();

        Assert.Single(deck.Where(c => c.EffectType == CardEffectType.Recall));
        Assert.Equal(3, deck.Count(c => c.EffectType == CardEffectType.Spritz));
        Assert.Equal(3, deck.Count(c => c.EffectType == CardEffectType.Tingle));
        Assert.Equal(2, deck.Count(c => c.EffectType == CardEffectType.Scurry));
        Assert.Single(deck.Where(c => c.EffectType == CardEffectType.Twirl));
    }

    [Fact]
    public void StarterDeck_TwirlCosts3AndExhausts()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var twirl = deck.First(c => c.EffectType == CardEffectType.Twirl);

        Assert.Equal(3, twirl.Cost);
        Assert.True(twirl.Exhaust);
    }

    [Fact]
    public void StarterDeck_AllCardsHaveUniqueIds()
    {
        var deck = CardDefinitions.CreateStarterDeck();
        var ids = deck.Select(c => c.Id).ToHashSet();

        Assert.Equal(deck.Count, ids.Count);
    }

    [Fact]
    public void DrawCards_DrawsFromEndOfPile()
    {
        var cards = Enumerable.Range(0, 5).Select(i => MakeCard($"c{i}")).ToList();
        var state = CreateTestState(drawPile: cards);

        var newState = DeckSystem.DrawCards(state, 2, new Random(42));

        Assert.Equal(2, newState.Hand.Count);
        Assert.Equal(3, newState.DrawPile.Count);
        // Cards drawn from end: c4, c3
        Assert.Equal("c4", newState.Hand[0].Id);
        Assert.Equal("c3", newState.Hand[1].Id);
    }

    [Fact]
    public void DrawCards_Draw5From10Leaves5()
    {
        var cards = Enumerable.Range(0, 10).Select(i => MakeCard($"c{i}")).ToList();
        var state = CreateTestState(drawPile: cards);

        var newState = DeckSystem.DrawCards(state, 5, new Random(42));

        Assert.Equal(5, newState.Hand.Count);
        Assert.Equal(5, newState.DrawPile.Count);
    }

    [Fact]
    public void DrawCards_ShufflesDiscardWhenDrawPileEmpty()
    {
        var discardCards = Enumerable.Range(0, 5).Select(i => MakeCard($"d{i}")).ToList();
        var state = CreateTestState(discardPile: discardCards);

        var newState = DeckSystem.DrawCards(state, 3, new Random(42));

        Assert.Equal(3, newState.Hand.Count);
        Assert.Equal(2, newState.DrawPile.Count);
        Assert.Empty(newState.DiscardPile);
    }

    [Fact]
    public void DrawCards_StopsWhenBothPilesEmpty()
    {
        var drawCards = new List<Card> { MakeCard("c0"), MakeCard("c1") };
        var state = CreateTestState(drawPile: drawCards);

        var newState = DeckSystem.DrawCards(state, 5, new Random(42));

        Assert.Equal(2, newState.Hand.Count);
        Assert.Empty(newState.DrawPile);
    }

    [Fact]
    public void DrawCards_DrawPileEmptyThenDiscardShuffled()
    {
        // 2 in draw pile, 3 in discard, draw 4
        var draw = new List<Card> { MakeCard("d0"), MakeCard("d1") };
        var discard = new List<Card> { MakeCard("x0"), MakeCard("x1"), MakeCard("x2") };
        var state = CreateTestState(drawPile: draw, discardPile: discard);

        var newState = DeckSystem.DrawCards(state, 4, new Random(42));

        Assert.Equal(4, newState.Hand.Count);
        Assert.Single(newState.DrawPile);
        Assert.Empty(newState.DiscardPile);
    }

    [Fact]
    public void DrawCards_DoesNotMutateOriginalState()
    {
        var cards = Enumerable.Range(0, 5).Select(i => MakeCard($"c{i}")).ToList();
        var state = CreateTestState(drawPile: cards);

        var newState = DeckSystem.DrawCards(state, 3, new Random(42));

        Assert.Empty(state.Hand);
        Assert.Equal(5, state.DrawPile.Count);
        Assert.Equal(3, newState.Hand.Count);
    }

    [Fact]
    public void DiscardHand_MovesAllCardsToDiscard()
    {
        var hand = new List<Card> { MakeCard("h0"), MakeCard("h1"), MakeCard("h2") };
        var state = CreateTestState(hand: hand);

        var newState = DeckSystem.DiscardHand(state);

        Assert.Empty(newState.Hand);
        Assert.Equal(3, newState.DiscardPile.Count);
    }

    [Fact]
    public void DiscardHand_AppendsToExistingDiscard()
    {
        var hand = new List<Card> { MakeCard("h0") };
        var discard = new List<Card> { MakeCard("d0"), MakeCard("d1") };
        var state = CreateTestState(hand: hand, discardPile: discard);

        var newState = DeckSystem.DiscardHand(state);

        Assert.Equal(3, newState.DiscardPile.Count);
    }

    [Fact]
    public void DiscardHand_EmptyHandReturnsSameState()
    {
        var state = CreateTestState();

        var newState = DeckSystem.DiscardHand(state);

        Assert.Same(state, newState);
    }

    [Fact]
    public void DiscardCard_RemovesFromHandAddsToDiscard()
    {
        var card = MakeCard("h0");
        var hand = new List<Card> { card, MakeCard("h1") };
        var state = CreateTestState(hand: hand);

        var newState = DeckSystem.DiscardCard(state, card);

        Assert.Single(newState.Hand);
        Assert.Equal("h1", newState.Hand[0].Id);
        Assert.Single(newState.DiscardPile);
        Assert.Equal("h0", newState.DiscardPile[0].Id);
    }

    [Fact]
    public void DiscardCard_ThrowsWhenCardNotInHand()
    {
        var state = CreateTestState(hand: [MakeCard("h0")]);
        var notInHand = MakeCard("missing");

        Assert.Throws<InvalidOperationException>(() => DeckSystem.DiscardCard(state, notInHand));
    }

    [Fact]
    public void ExhaustCard_AddsToExhaustPile()
    {
        var card = MakeCard("e0", exhaust: true);
        var state = CreateTestState();

        var newState = DeckSystem.ExhaustCard(state, card);

        Assert.Single(newState.ExhaustPile);
        Assert.Equal("e0", newState.ExhaustPile[0].Id);
    }

    [Fact]
    public void CanPlayCard_TrueWhenEnoughEnergy()
    {
        var state = CreateTestState(energy: 3);
        var card = MakeCard("c", cost: 2);

        Assert.True(DeckSystem.CanPlayCard(state, card));
    }

    [Fact]
    public void CanPlayCard_TrueWhenExactEnergy()
    {
        var state = CreateTestState(energy: 1);
        var card = MakeCard("c", cost: 1);

        Assert.True(DeckSystem.CanPlayCard(state, card));
    }

    [Fact]
    public void CanPlayCard_FalseWhenNotEnoughEnergy()
    {
        var state = CreateTestState(energy: 1);
        var card = MakeCard("c", cost: 2);

        Assert.False(DeckSystem.CanPlayCard(state, card));
    }

    [Fact]
    public void CanPlayCard_ZeroCostAlwaysPlayable()
    {
        var state = CreateTestState(energy: 0);
        var card = MakeCard("c", cost: 0);

        Assert.True(DeckSystem.CanPlayCard(state, card));
    }

    [Fact]
    public void SpendEnergy_DeductsCardCost()
    {
        var state = CreateTestState(energy: 3);
        var card = MakeCard("c", cost: 2);

        var newState = DeckSystem.SpendEnergy(state, card);

        Assert.Equal(1, newState.Energy);
    }

    [Fact]
    public void SpendEnergy_EnergyReducedRefunds1()
    {
        var state = CreateTestState(energy: 3);
        var card = new Card { Id = "er", Name = "Reduced", Cost = 2, EffectType = CardEffectType.Spritz, EnergyReduced = true };

        var newState = DeckSystem.SpendEnergy(state, card);

        // Cost 2, refund 1 = net cost 1
        Assert.Equal(2, newState.Energy);
    }

    [Fact]
    public void SpendEnergy_ThrowsWhenInsufficientEnergy()
    {
        var state = CreateTestState(energy: 1);
        var card = MakeCard("c", cost: 3);

        Assert.Throws<InvalidOperationException>(() => DeckSystem.SpendEnergy(state, card));
    }

    [Fact]
    public void Shuffle_ReturnsDifferentOrderWithDifferentSeeds()
    {
        var cards = Enumerable.Range(0, 10).Select(i => MakeCard($"c{i}")).ToList();

        var shuffled1 = DeckSystem.Shuffle(cards, new Random(1));
        var shuffled2 = DeckSystem.Shuffle(cards, new Random(2));

        Assert.False(shuffled1.Select(c => c.Id).SequenceEqual(shuffled2.Select(c => c.Id)));
    }

    [Fact]
    public void Shuffle_SameSeedProducesSameOrder()
    {
        var cards = Enumerable.Range(0, 10).Select(i => MakeCard($"c{i}")).ToList();

        var shuffled1 = DeckSystem.Shuffle(cards, new Random(42));
        var shuffled2 = DeckSystem.Shuffle(cards, new Random(42));

        Assert.True(shuffled1.Select(c => c.Id).SequenceEqual(shuffled2.Select(c => c.Id)));
    }

    [Fact]
    public void Shuffle_DoesNotMutateInput()
    {
        var cards = Enumerable.Range(0, 5).Select(i => MakeCard($"c{i}")).ToList();
        var originalIds = cards.Select(c => c.Id).ToList();

        DeckSystem.Shuffle(cards, new Random(42));

        Assert.True(cards.Select(c => c.Id).SequenceEqual(originalIds));
    }
}
