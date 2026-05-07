using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// Tests for the between-floor shop (M32):
/// 9 slots, progressive pricing, purchase paths, equipment exclusion, Visiting Bunny.
/// </summary>
public class ShopSystemTests
{
    private static GameState BuildShopState(int copper = 200, IReadOnlyList<Equipment>? equipment = null,
        IReadOnlyList<Card>? deck = null, int shopVisitCount = 0)
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState
        {
            Board = board,
            PersistentDeck = deck ?? CardDefinitions.CreateStarterDeck(),
            Equipment = equipment ?? new List<Equipment>(),
            CurrentLevelId = "level1",
            Copper = copper,
            ShopVisitCount = shopVisitCount
        };
    }

    [Fact]
    public void EnterShop_Generates9Slots()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));

        Assert.Equal(GamePhase.Shop, state.GamePhase);
        Assert.NotNull(state.ShopSlots);
        Assert.Equal(9, state.ShopSlots!.Count);
    }

    [Fact]
    public void EnterShop_IncrementsVisitCount()
    {
        var state = BuildShopState();
        Assert.Equal(0, state.ShopVisitCount);

        state = ShopSystem.EnterShop(state, new Random(42));
        Assert.Equal(1, state.ShopVisitCount);

        state = ShopSystem.ExitShop(state);
        state = ShopSystem.EnterShop(state, new Random(43));
        Assert.Equal(2, state.ShopVisitCount);
    }

    [Fact]
    public void CalculatePrice_FirstVisit_BaseCost()
    {
        Assert.Equal(5, ShopSystem.CalculatePrice(5, 1));
        Assert.Equal(11, ShopSystem.CalculatePrice(11, 1));
    }

    [Fact]
    public void CalculatePrice_ProgressiveBy10PercentPerVisit()
    {
        // visit 2: ceil(5 * 1.1) = 6, ceil(10 * 1.1) = 11
        Assert.Equal(6, ShopSystem.CalculatePrice(5, 2));
        Assert.Equal(11, ShopSystem.CalculatePrice(10, 2));

        // visit 3: ceil(10 * 1.2) = 12
        Assert.Equal(12, ShopSystem.CalculatePrice(10, 3));
    }

    [Fact]
    public void GenerateSlots_HasCorrectKindsInOrder()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));
        var slots = state.ShopSlots!;

        Assert.Equal(ShopSlotKind.RegularCard, slots[0].Kind);
        Assert.Equal(ShopSlotKind.RegularCard, slots[1].Kind);
        Assert.Equal(ShopSlotKind.BonusSpoonCard, slots[2].Kind);
        Assert.Equal(ShopSlotKind.EnhancedCard, slots[3].Kind);
        Assert.Equal(ShopSlotKind.Equipment, slots[4].Kind);
        Assert.Equal(ShopSlotKind.Equipment, slots[5].Kind);
        Assert.Equal(ShopSlotKind.RemoveCard, slots[6].Kind);
        Assert.Equal(ShopSlotKind.VisitingBunny, slots[7].Kind);
        Assert.Equal(ShopSlotKind.Enhance, slots[8].Kind);
    }

    [Fact]
    public void GenerateSlots_BonusSpoonAndEnhancedCardsHaveModifiers()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));

        Assert.True(state.ShopSlots![2].Card!.BonusSpoon);
        Assert.True(state.ShopSlots[3].Card!.Enhanced);
    }

    [Fact]
    public void GenerateSlots_CardSlotsHaveDistinctNames()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));

        var names = new[] { 0, 1, 2, 3 }
            .Select(i => state.ShopSlots![i].Card!.Name)
            .ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void GenerateSlots_EquipmentSlots_ExcludeAlreadyOwned()
    {
        // Own all but two equipment items — both equipment slots should fill from those two
        var pool = EquipmentDefinitions.CreateOfferingPool();
        var owned = pool.Take(pool.Count - 2)
            .Select(e => e with { Id = $"owned_{e.EffectType}" })
            .ToList();

        var state = BuildShopState(equipment: owned);
        state = ShopSystem.EnterShop(state, new Random(42));

        var ownedTypes = owned.Select(e => e.EffectType).ToHashSet();
        Assert.NotNull(state.ShopSlots![4].Equipment);
        Assert.NotNull(state.ShopSlots[5].Equipment);
        Assert.DoesNotContain(state.ShopSlots[4].Equipment!.EffectType, ownedTypes);
        Assert.DoesNotContain(state.ShopSlots[5].Equipment!.EffectType, ownedTypes);
    }

    [Fact]
    public void Purchase_RegularCard_AddsToPersistentDeck()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));
        var startSize = state.PersistentDeck.Count;
        var card = state.ShopSlots![0].Card!;

        var newState = ShopSystem.Purchase(state, 0, new Random(99));

        Assert.Equal(startSize + 1, newState.PersistentDeck.Count);
        Assert.Contains(newState.PersistentDeck, c => c.Id == card.Id);
        Assert.True(newState.ShopSlots![0].IsPurchased);
        Assert.Equal(state.Copper - state.ShopSlots![0].Price, newState.Copper);
    }

    [Fact]
    public void Purchase_Equipment_AddsToInventory_AndAppliesAcquisition()
    {
        // Force the first equipment slot to be Bleach by owning everything else
        var pool = EquipmentDefinitions.CreateOfferingPool();
        var owned = pool
            .Where(e => e.EffectType != EquipmentEffectType.Bleach)
            .Select(e => e with { Id = $"owned_{e.EffectType}" })
            .ToList();

        // Deck with bleachable cards so Bleach's ApplyOnAcquisition has something to do
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "s1" },
            CardDefinitions.Sweep with { Id = "sw1" },
            CardDefinitions.Brush with { Id = "br1" },
            CardDefinitions.Tingle with { Id = "t1" }
        };
        var state = BuildShopState(equipment: owned, deck: deck);
        state = ShopSystem.EnterShop(state, new Random(42));

        // The single un-owned equipment is Bleach; it'll be in slot 4 (only one available).
        Assert.NotNull(state.ShopSlots![4].Equipment);
        Assert.Equal(EquipmentEffectType.Bleach, state.ShopSlots[4].Equipment!.EffectType);

        var newState = ShopSystem.Purchase(state, 4, new Random(99));

        Assert.Contains(newState.Equipment, e => e.EffectType == EquipmentEffectType.Bleach);
        // Bleach's on-acquisition applied: Spritz/Sweep/Brush enhanced
        Assert.True(newState.PersistentDeck.First(c => c.Id == "s1").Enhanced);
        Assert.True(newState.PersistentDeck.First(c => c.Id == "sw1").Enhanced);
        Assert.True(newState.PersistentDeck.First(c => c.Id == "br1").Enhanced);
    }

    [Fact]
    public void Purchase_RemoveCard_RemovesFromDeck()
    {
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "keep_1" },
            CardDefinitions.Spritz with { Id = "trash" },
            CardDefinitions.Spritz with { Id = "keep_2" }
        };
        var state = BuildShopState(deck: deck);
        state = ShopSystem.EnterShop(state, new Random(42));

        var trashCard = state.PersistentDeck.First(c => c.Id == "trash");
        var newState = ShopSystem.Purchase(state, 6, new Random(99), cardToRemove: trashCard);

        Assert.Equal(2, newState.PersistentDeck.Count);
        Assert.DoesNotContain(newState.PersistentDeck, c => c.Id == "trash");
    }

    [Fact]
    public void Purchase_VisitingBunny_QueuesPlayerRevealForNextFloor()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));
        Assert.Equal(0, state.VisitingBunnyPendingReveals);

        var newState = ShopSystem.Purchase(state, 7, new Random(99));

        Assert.Equal(1, newState.VisitingBunnyPendingReveals);
    }

    [Fact]
    public void Purchase_Enhance_EnhancesARandomNonEnhancedCard()
    {
        var deck = Enumerable.Range(0, 5)
            .Select(i => CardDefinitions.Spritz with { Id = $"s{i}" })
            .ToList<Card>();
        var state = BuildShopState(deck: deck);
        state = ShopSystem.EnterShop(state, new Random(42));

        var newState = ShopSystem.Purchase(state, 8, new Random(99));

        Assert.Equal(1, newState.PersistentDeck.Count(c => c.Enhanced));
    }

    [Fact]
    public void Purchase_Throws_WhenNotEnoughCopper()
    {
        var state = BuildShopState(copper: 0);
        state = ShopSystem.EnterShop(state, new Random(42));

        Assert.Throws<InvalidOperationException>(() =>
            ShopSystem.Purchase(state, 0, new Random(99)));
    }

    [Fact]
    public void Purchase_Throws_WhenSlotAlreadyPurchased()
    {
        var state = BuildShopState();
        state = ShopSystem.EnterShop(state, new Random(42));
        state = ShopSystem.Purchase(state, 0, new Random(99));

        Assert.Throws<InvalidOperationException>(() =>
            ShopSystem.Purchase(state, 0, new Random(99)));
    }

    [Fact]
    public void CanPurchase_FalseForUnaffordableSlot()
    {
        var state = BuildShopState(copper: 0);
        state = ShopSystem.EnterShop(state, new Random(42));

        Assert.False(ShopSystem.CanPurchase(state, 0));
    }

    [Fact]
    public void CanPurchase_FalseForEnhanceWhenAllEnhanced()
    {
        var deck = Enumerable.Range(0, 5)
            .Select(i => CardDefinitions.Spritz with { Id = $"s{i}", Enhanced = true })
            .ToList<Card>();
        var state = BuildShopState(deck: deck);
        state = ShopSystem.EnterShop(state, new Random(42));

        Assert.False(ShopSystem.CanPurchase(state, 8));
    }

    [Fact]
    public void VisitingBunny_RevealsAtNextFloorStart()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            GameStatus = GameStatus.Won,
            VisitingBunnyPendingReveals = 1
        };

        // Floor 1 → 2
        state = CampaignSystem.CompleteFloor(state, new Random(43));
        state = CampaignSystem.SkipCardReward(state, new Random(44));

        Assert.Equal("level2", state.CurrentLevelId);
        // Reveal counter consumed
        Assert.Equal(0, state.VisitingBunnyPendingReveals);
        // At least 1 player tile revealed (could be more from initial draws/Adopt; we expect ≥1)
        var revealedPlayer = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.True(revealedPlayer >= 1);
    }

    [Fact]
    public void LeaveShop_ClearsSlotsAndAdvances()
    {
        // Use a level that has shop configured: build a state at level1 with shop=true
        // Easier: drive directly through ShopSystem.ExitShop + AdvanceToNextFloor via LeaveShop
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = ShopSystem.EnterShop(state, new Random(43));
        Assert.Equal(GamePhase.Shop, state.GamePhase);

        var newState = CampaignSystem.LeaveShop(state, new Random(44));

        Assert.Null(newState.ShopSlots);
        Assert.Equal("level2", newState.CurrentLevelId);
        Assert.Equal(GamePhase.Playing, newState.GamePhase);
    }
}
