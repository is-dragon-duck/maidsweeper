namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

/// <summary>
/// Between-floor shop. Generates 9 slots and processes purchases.
/// Pricing: ceil(baseCost * (1 + 0.1 * (visitCount - 1))).
/// </summary>
public static class ShopSystem
{
    // Base prices per slot (matching alpha layout)
    private const int RegularCardPrice = 5;
    private const int BonusSpoonCardPrice = 11;
    private const int EnhancedCardPrice = 10;
    private const int Equipment1Price = 19;
    private const int Equipment2Price = 23;
    private const int RemoveCardPrice = 14;
    private const int VisitingBunnyPrice = 4;
    private const int EnhancePrice = 9;

    /// <summary>
    /// Enters the shop phase: increments visit count, generates 9 slots, sets phase.
    /// </summary>
    public static GameState EnterShop(GameState state, Random rng)
    {
        var visitCount = state.ShopVisitCount + 1;
        var slots = GenerateSlots(state, visitCount, rng);
        return state with
        {
            ShopVisitCount = visitCount,
            GamePhase = GamePhase.Shop,
            ShopSlots = slots
        };
    }

    /// <summary>
    /// Exits the shop phase, clearing offerings.
    /// </summary>
    public static GameState ExitShop(GameState state)
    {
        return state with { ShopSlots = null };
    }

    /// <summary>
    /// Calculates a slot price for the given visit count.
    /// Price = ceil(baseCost * (1 + 0.1 * (visitCount - 1))).
    /// </summary>
    public static int CalculatePrice(int baseCost, int visitCount)
    {
        var multiplier = 1.0 + 0.1 * Math.Max(0, visitCount - 1);
        return (int)Math.Ceiling(baseCost * multiplier);
    }

    /// <summary>
    /// Builds the 9 shop slots. Card slots pull distinct names from the reward pool;
    /// equipment slots pull distinct un-owned items.
    /// </summary>
    public static List<ShopSlot> GenerateSlots(GameState state, int visitCount, Random rng)
    {
        var cardPool = CardDefinitions.CreateRewardPool();
        Shuffle(cardPool, rng);
        var cardPicks = cardPool.Take(4).ToList();

        var ownedTypes = state.Equipment.Select(e => e.EffectType).ToHashSet();
        var equipmentPool = EquipmentDefinitions.CreateOfferingPool()
            .Where(e => !ownedTypes.Contains(e.EffectType))
            .Where(e => e.Prereqs.All(p => ownedTypes.Contains(p)))
            .ToList();
        Shuffle(equipmentPool, rng);
        var equipmentPicks = equipmentPool.Take(2).ToList();

        string nextCardId(string tag) => $"shop_{tag}_{Guid.NewGuid():N}";
        string nextEquipmentId() => $"shop_eq_{Guid.NewGuid():N}";

        var slots = new List<ShopSlot>
        {
            new()
            {
                Index = 0,
                Kind = ShopSlotKind.RegularCard,
                Price = CalculatePrice(RegularCardPrice, visitCount),
                Card = cardPicks.ElementAtOrDefault(0) is { } c0 ? c0 with { Id = nextCardId("c0") } : null
            },
            new()
            {
                Index = 1,
                Kind = ShopSlotKind.RegularCard,
                Price = CalculatePrice(RegularCardPrice, visitCount),
                Card = cardPicks.ElementAtOrDefault(1) is { } c1 ? c1 with { Id = nextCardId("c1") } : null
            },
            new()
            {
                Index = 2,
                Kind = ShopSlotKind.BonusSpoonCard,
                Price = CalculatePrice(BonusSpoonCardPrice, visitCount),
                Card = cardPicks.ElementAtOrDefault(2) is { } c2 ? c2 with { Id = nextCardId("bs"), BonusSpoon = true } : null
            },
            new()
            {
                Index = 3,
                Kind = ShopSlotKind.EnhancedCard,
                Price = CalculatePrice(EnhancedCardPrice, visitCount),
                Card = cardPicks.ElementAtOrDefault(3) is { } c3 ? c3 with { Id = nextCardId("en"), Enhanced = true } : null
            },
            new()
            {
                Index = 4,
                Kind = ShopSlotKind.Equipment,
                Price = CalculatePrice(Equipment1Price, visitCount),
                Equipment = equipmentPicks.ElementAtOrDefault(0) is { } e0 ? e0 with { Id = nextEquipmentId() } : null
            },
            new()
            {
                Index = 5,
                Kind = ShopSlotKind.Equipment,
                Price = CalculatePrice(Equipment2Price, visitCount),
                Equipment = equipmentPicks.ElementAtOrDefault(1) is { } e1 ? e1 with { Id = nextEquipmentId() } : null
            },
            new()
            {
                Index = 6,
                Kind = ShopSlotKind.RemoveCard,
                Price = CalculatePrice(RemoveCardPrice, visitCount)
            },
            new()
            {
                Index = 7,
                Kind = ShopSlotKind.VisitingBunny,
                Price = CalculatePrice(VisitingBunnyPrice, visitCount)
            },
            new()
            {
                Index = 8,
                Kind = ShopSlotKind.Enhance,
                Price = CalculatePrice(EnhancePrice, visitCount)
            }
        };

        return slots;
    }

    /// <summary>
    /// Returns true if the slot is purchasable: not already bought, has content
    /// (cards/equipment slots only), and the player has enough copper.
    /// </summary>
    public static bool CanPurchase(GameState state, int slotIndex)
    {
        if (state.ShopSlots == null || slotIndex < 0 || slotIndex >= state.ShopSlots.Count)
            return false;

        var slot = state.ShopSlots[slotIndex];
        if (slot.IsPurchased) return false;
        if (state.Copper < slot.Price) return false;

        return slot.Kind switch
        {
            ShopSlotKind.RegularCard or ShopSlotKind.BonusSpoonCard or ShopSlotKind.EnhancedCard
                => slot.Card != null,
            ShopSlotKind.Equipment => slot.Equipment != null,
            ShopSlotKind.RemoveCard => state.PersistentDeck.Count > 0,
            ShopSlotKind.Enhance => state.PersistentDeck.Any(c => !c.Enhanced),
            ShopSlotKind.VisitingBunny => true,
            _ => false
        };
    }

    /// <summary>
    /// Purchases a shop slot. Validates affordability and eligibility.
    /// For RemoveCard, cardToRemove must be supplied.
    /// Returns the updated state with the slot marked purchased and effect applied.
    /// </summary>
    public static GameState Purchase(GameState state, int slotIndex, Random rng, Card? cardToRemove = null)
    {
        if (state.ShopSlots == null)
            throw new InvalidOperationException("Not in shop phase");
        if (slotIndex < 0 || slotIndex >= state.ShopSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        var slots = state.ShopSlots.ToList();
        var slot = slots[slotIndex];

        if (slot.IsPurchased)
            throw new InvalidOperationException("Slot already purchased");
        if (state.Copper < slot.Price)
            throw new InvalidOperationException("Not enough copper");

        switch (slot.Kind)
        {
            case ShopSlotKind.RegularCard:
            case ShopSlotKind.BonusSpoonCard:
            case ShopSlotKind.EnhancedCard:
            {
                if (slot.Card == null)
                    throw new InvalidOperationException("Slot has no card");
                var card = EquipmentSystem.ApplyBleachToNewCard(state, slot.Card);
                var deck = state.PersistentDeck.ToList();
                deck.Add(card);
                state = state with { PersistentDeck = deck };
                break;
            }
            case ShopSlotKind.Equipment:
            {
                if (slot.Equipment == null)
                    throw new InvalidOperationException("Slot has no equipment");
                var equipment = state.Equipment.ToList();
                equipment.Add(slot.Equipment);
                state = state with { Equipment = equipment };
                state = EquipmentSystem.ApplyOnAcquisition(state, slot.Equipment, rng);
                break;
            }
            case ShopSlotKind.RemoveCard:
            {
                if (cardToRemove == null)
                    throw new ArgumentNullException(nameof(cardToRemove), "RemoveCard requires a card");
                var deck = state.PersistentDeck.ToList();
                var removed = deck.RemoveAll(c => c.Id == cardToRemove.Id);
                if (removed == 0)
                    throw new InvalidOperationException("Card not in persistent deck");
                state = state with { PersistentDeck = deck };
                break;
            }
            case ShopSlotKind.VisitingBunny:
            {
                state = state with
                {
                    VisitingBunnyPendingReveals = state.VisitingBunnyPendingReveals + 1
                };
                break;
            }
            case ShopSlotKind.Enhance:
            {
                var deck = state.PersistentDeck.ToList();
                var enhanceable = Enumerable.Range(0, deck.Count)
                    .Where(i => !deck[i].Enhanced)
                    .ToList();
                if (enhanceable.Count == 0)
                    throw new InvalidOperationException("No enhanceable cards");

                var idx = enhanceable[rng.Next(enhanceable.Count)];
                deck[idx] = deck[idx] with { Enhanced = true };
                state = state with { PersistentDeck = deck };
                break;
            }
        }

        // Deduct copper and mark slot purchased
        slots[slotIndex] = slot with { IsPurchased = true };
        return state with
        {
            Copper = state.Copper - slot.Price,
            ShopSlots = slots
        };
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
