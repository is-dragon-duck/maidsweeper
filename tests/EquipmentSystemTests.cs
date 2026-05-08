using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// Tests for Stage 4 Set 1 equipment passive effects (M30):
/// Coffee, Frilly Dress, Dust Bunny, Handbag, Eyeshadow, Glasses.
/// </summary>
public class EquipmentSystemTests
{
    private static GameState BuildFloorStartState(IReadOnlyList<Equipment>? equipment = null, int seed = 42)
    {
        var rng = new Random(seed);
        var state = CampaignSystem.StartCampaign(rng);
        if (equipment != null && equipment.Count > 0)
        {
            state = state with { Equipment = equipment, GameStatus = GameStatus.Won };
            // Trigger floor transition so equipment hooks fire
            state = CampaignSystem.CompleteFloor(state, new Random(seed + 1));
            state = CampaignSystem.SkipCardReward(state, new Random(seed + 2));
        }
        return state;
    }

    // ========== Coffee ==========

    [Fact]
    public void Coffee_IncreasesMaxSpoonsBy1()
    {
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var state = BuildFloorStartState(new List<Equipment> { coffee });

        Assert.Equal(4, state.MaxSpoons);
        Assert.Equal(4, state.Spoons);
    }

    [Fact]
    public void Coffee_ReducesDrawCountOnTurn2Plus()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Coffee with { Id = "c1" } },
            ReadStacks = 0
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(4, drawCount); // 5 base - 1 Coffee = 4
    }

    [Fact]
    public void Coffee_DrawCountAlsoConsidersRead()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Coffee with { Id = "c1" } },
            ReadStacks = 1
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(5, drawCount); // 5 + 1 Read - 1 Coffee = 5
    }

    [Fact]
    public void Coffee_DoesNotReduceTurn1Draw()
    {
        // Floor start initial draw should be 5, not 4, even with Coffee
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var state = BuildFloorStartState(new List<Equipment> { coffee });

        // Hand should contain exactly 5 cards (the initial draw, unaffected by Coffee penalty)
        Assert.Equal(5, state.Hand.Count);
    }

    // ========== Frilly Dress ==========

    [Fact]
    public void FrillyDress_SuppressesTurnEndForFirstNeutralOnTurn1()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.False(result.TurnEnded);
        Assert.Equal(1, result.State.Turn1NeutralReveals);
        Assert.Equal(PlayerType.Player, result.State.CurrentPlayer);
    }

    [Fact]
    public void FrillyDress_FifthNeutralOnTurn1EndsTurn()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });
        state = state with { Turn1NeutralReveals = 4 };

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.True(result.TurnEnded);
        // Counter unchanged because suppression didn't fire
        Assert.Equal(4, result.State.Turn1NeutralReveals);
    }

    [Fact]
    public void FrillyDress_DoesNotSuppressOnTurn2()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });
        state = state with { TurnNumber = 2 };

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.True(result.TurnEnded);
    }

    [Fact]
    public void FrillyDress_DoesNotSuppressForRivalReveal()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });

        var rivalPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival)
            .Position;

        var result = GameRunner.ProcessReveal(state, rivalPos, new Random(99));

        Assert.True(result.TurnEnded);
        Assert.Equal(0, result.State.Turn1NeutralReveals);
    }

    // ========== Dust Bunny ==========

    [Fact]
    public void DustBunny_Reveals1PlayerTileAtFloorStart()
    {
        var dustBunny = EquipmentDefinitions.DustBunny with { Id = "db1" };
        var state = BuildFloorStartState(new List<Equipment> { dustBunny });

        var revealedPlayer = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && t.IsRevealed && t.Owner == TileOwner.Player);

        Assert.Equal(1, revealedPlayer);
    }

    // ========== Handbag ==========

    [Fact]
    public void Handbag_Draws2ExtraCardsOnFirstTurn()
    {
        var handbag = EquipmentDefinitions.Handbag with { Id = "h1" };
        var state = BuildFloorStartState(new List<Equipment> { handbag });

        Assert.Equal(7, state.Hand.Count); // 5 base + 2 from Handbag
    }

    [Fact]
    public void Handbag_NoEffectOnSubsequentTurns()
    {
        // GetTurnDrawCount is for non-initial draws and shouldn't include Handbag
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Handbag with { Id = "h1" } }
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(5, drawCount); // Handbag does not add to subsequent turns
    }

    // ========== Eyeshadow ==========

    [Fact]
    public void Eyeshadow_AddsDistractionIntentAtFloorStart()
    {
        // Without Eyeshadow: baseline intent has 4 baseline distractions.
        var baseline = BuildFloorStartState();
        var baselineSum = baseline.RivalIntentPoints.Values.Sum();

        // With Eyeshadow: +1 distraction → +1 to total intent point sum.
        var eyeshadow = EquipmentDefinitions.Eyeshadow with { Id = "e1" };
        var state = BuildFloorStartState(new List<Equipment> { eyeshadow });

        Assert.Equal(baselineSum + 1, state.RivalIntentPoints.Values.Sum());
    }

    [Fact]
    public void Eyeshadow_AddsDistractionAtEachTurnStart()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment> { EquipmentDefinitions.Eyeshadow with { Id = "e1" } },
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1
        };

        state = TurnSystem.StartPlayerTurn(state, new Random(42));
        var sumAfterTurn1 = state.RivalIntentPoints.Values.Sum();

        state = TurnSystem.StartPlayerTurn(state, new Random(42));
        var sumAfterTurn2 = state.RivalIntentPoints.Values.Sum();

        // Each StartPlayerTurn generates a fresh batch of intent (combined with carry-over)
        // and Eyeshadow adds +1. We just need sum to grow each turn by more than 1
        // (proving Eyeshadow fires *and* base generation runs each turn).
        Assert.True(sumAfterTurn1 > 0, "turn 1 should produce nonzero intent");
        Assert.True(sumAfterTurn2 > sumAfterTurn1, "turn 2 should add more intent on top of turn 1");
    }

    // ========== Glasses ==========

    [Fact]
    public void Glasses_AnnotatesARivalOrNobleAtTurn1()
    {
        var glasses = EquipmentDefinitions.Glasses with { Id = "g1" };
        var state = BuildFloorStartState(new List<Equipment> { glasses });

        // Glasses fires Tingle: a single rival tile gets exact-owner annotation.
        // (Level1 has no nobles.)
        var tingled = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed
                        && t.Annotations.OwnerSubset != null
                        && t.Annotations.OwnerSubset.Count == 1
                        && t.Annotations.OwnerSubset.Contains(TileOwner.Rival))
            .ToList();

        Assert.Single(tingled);
    }

    // ========== Multi-equipment combinations ==========

    [Fact]
    public void MultipleEquipment_AllEffectsStack()
    {
        var baseline = BuildFloorStartState();
        var baselineIntentSum = baseline.RivalIntentPoints.Values.Sum();

        var equipment = new List<Equipment>
        {
            EquipmentDefinitions.Coffee with { Id = "c1" },
            EquipmentDefinitions.Handbag with { Id = "h1" },
            EquipmentDefinitions.Eyeshadow with { Id = "e1" }
        };
        var state = BuildFloorStartState(equipment);

        Assert.Equal(4, state.MaxSpoons);     // Coffee
        Assert.Equal(7, state.Hand.Count);    // 5 + 2 Handbag
        Assert.Equal(baselineIntentSum + 1, state.RivalIntentPoints.Values.Sum()); // Eyeshadow
    }

    [Fact]
    public void Equipment_PersistsAcrossFloors_AppliesEachFloor()
    {
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { coffee },
            GameStatus = GameStatus.Won
        };

        // Floor 1 → 2
        state = CampaignSystem.CompleteFloor(state, new Random(43));
        state = CampaignSystem.SkipCardReward(state, new Random(44));
        Assert.Equal(4, state.MaxSpoons);

        // Floor 2 → 3
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(45));
        state = CampaignSystem.SkipCardReward(state, new Random(46));
        Assert.Equal(4, state.MaxSpoons);
        // Coffee still owned
        Assert.Single(state.Equipment);
    }

    // ===========================================================
    // M31: Set 2 — Deck Modifiers
    // ===========================================================

    private static GameState BuildAcquisitionState(IReadOnlyList<Card> deck)
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState
        {
            Board = board,
            PersistentDeck = deck,
            CurrentLevelId = "level1"
        };
    }

    [Fact]
    public void Bleach_EnhancesAllSpritzSweepBrushInDeck()
    {
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "s1" },
            CardDefinitions.Sweep with { Id = "sw1" },
            CardDefinitions.Brush with { Id = "br1" },
            CardDefinitions.Tingle with { Id = "t1" }, // not bleachable
            CardDefinitions.Twirl with { Id = "tw1" }  // not bleachable
        };
        var state = BuildAcquisitionState(deck);
        var bleach = EquipmentDefinitions.Bleach with { Id = "bl1" };

        state = EquipmentSystem.ApplyOnAcquisition(state, bleach, new Random(7));

        Assert.True(state.PersistentDeck.First(c => c.Id == "s1").Enhanced);
        Assert.True(state.PersistentDeck.First(c => c.Id == "sw1").Enhanced);
        Assert.True(state.PersistentDeck.First(c => c.Id == "br1").Enhanced);
        Assert.False(state.PersistentDeck.First(c => c.Id == "t1").Enhanced);
        Assert.False(state.PersistentDeck.First(c => c.Id == "tw1").Enhanced);
    }

    [Fact]
    public void Bleach_AutoEnhancesFutureSpritzAddedToDeck()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { EquipmentDefinitions.Bleach with { Id = "bl1" } }
        };

        var spritzReward = CardDefinitions.Spritz with { Id = "future_spritz" };
        state = CampaignSystem.SelectCardReward(
            state with { GamePhase = GamePhase.CardReward, GameStatus = GameStatus.Won },
            spritzReward,
            new Random(99));

        var added = state.PersistentDeck.First(c => c.Id == "future_spritz");
        Assert.True(added.Enhanced);
    }

    [Fact]
    public void Bleach_DoesNotAutoEnhanceNonBleachableRewards()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { EquipmentDefinitions.Bleach with { Id = "bl1" } }
        };

        var rendezvous = CardDefinitions.Rendezvous with { Id = "future_r" };
        state = CampaignSystem.SelectCardReward(
            state with { GamePhase = GamePhase.CardReward, GameStatus = GameStatus.Won },
            rendezvous,
            new Random(99));

        var added = state.PersistentDeck.First(c => c.Id == "future_r");
        Assert.False(added.Enhanced);
    }

    [Fact]
    public void Estrogen_Adds_BonusSpoon_To_3_NonEnhanced_Cards()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"s{i}" })
            .ToList<Card>();
        var state = BuildAcquisitionState(deck);
        var estrogen = EquipmentDefinitions.Estrogen with { Id = "e1" };

        state = EquipmentSystem.ApplyOnAcquisition(state, estrogen, new Random(7));

        Assert.Equal(3, state.PersistentDeck.Count(c => c.BonusSpoon));
    }

    [Fact]
    public void Estrogen_DoesNotPickEnhancedCards()
    {
        // Mix of 5 enhanced and 3 non-enhanced — Estrogen should pick from the 3
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "e1", Enhanced = true },
            CardDefinitions.Spritz with { Id = "e2", Enhanced = true },
            CardDefinitions.Spritz with { Id = "e3", Enhanced = true },
            CardDefinitions.Spritz with { Id = "e4", Enhanced = true },
            CardDefinitions.Spritz with { Id = "e5", Enhanced = true },
            CardDefinitions.Spritz with { Id = "n1" },
            CardDefinitions.Spritz with { Id = "n2" },
            CardDefinitions.Spritz with { Id = "n3" }
        };
        var state = BuildAcquisitionState(deck);
        state = EquipmentSystem.ApplyOnAcquisition(state, EquipmentDefinitions.Estrogen, new Random(7));

        // Enhanced cards untouched
        foreach (var id in new[] { "e1", "e2", "e3", "e4", "e5" })
            Assert.False(state.PersistentDeck.First(c => c.Id == id).BonusSpoon);

        // All 3 non-enhanced gained BonusSpoon
        foreach (var id in new[] { "n1", "n2", "n3" })
            Assert.True(state.PersistentDeck.First(c => c.Id == id).BonusSpoon);
    }

    [Fact]
    public void Progesterone_Enhances_3_NonBonusSpoon_Cards()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"s{i}" })
            .ToList<Card>();
        var state = BuildAcquisitionState(deck);
        var progesterone = EquipmentDefinitions.Progesterone with { Id = "p1" };

        state = EquipmentSystem.ApplyOnAcquisition(state, progesterone, new Random(7));

        Assert.Equal(3, state.PersistentDeck.Count(c => c.Enhanced));
    }

    [Fact]
    public void CrystalBall_Adds3_DoublyUpgraded_Tingles()
    {
        var deck = new List<Card> { CardDefinitions.Spritz with { Id = "s1" } };
        var state = BuildAcquisitionState(deck);

        state = EquipmentSystem.ApplyOnAcquisition(state, EquipmentDefinitions.CrystalBall, new Random(7));

        Assert.Equal(4, state.PersistentDeck.Count); // 1 original + 3 added

        var tingles = state.PersistentDeck
            .Where(c => c.EffectType == CardEffectType.Tingle)
            .ToList();
        Assert.Equal(3, tingles.Count);
        Assert.All(tingles, t =>
        {
            Assert.True(t.Enhanced);
            Assert.True(t.BonusSpoon);
        });
    }

    [Fact]
    public void Boots_Replaces1Card_WithDoublyUpgradedRandomReward()
    {
        var deck = Enumerable.Range(0, 5)
            .Select(i => CardDefinitions.Spritz with { Id = $"s{i}" })
            .ToList<Card>();
        var state = BuildAcquisitionState(deck);

        state = EquipmentSystem.ApplyOnAcquisition(state, EquipmentDefinitions.Boots, new Random(7));

        Assert.Equal(5, state.PersistentDeck.Count); // size unchanged

        // Exactly one card was added with Boots ID prefix and is doubly upgraded
        var added = state.PersistentDeck.Where(c => c.Id.StartsWith("boots_")).ToList();
        Assert.Single(added);
        Assert.True(added[0].Enhanced);
        Assert.True(added[0].BonusSpoon);

        // Exactly one of the original Spritz cards was removed
        Assert.Equal(4, state.PersistentDeck.Count(c => c.Id.StartsWith("s")));
    }

    // ========== Tiara ==========

    [Fact]
    public void Tiara_DoublesCopperFromUnrevealedRivalsAtFloorEnd()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { EquipmentDefinitions.Tiara with { Id = "t1" } },
            GameStatus = GameStatus.Won,
            Copper = 0
        };

        // Count remaining unrevealed rivals to compute expected
        var unrevealedRivals = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed
                        && t.Owner == TileOwner.Rival);

        var newState = CampaignSystem.CompleteFloor(state, new Random(99));

        Assert.Equal(unrevealedRivals * 2, newState.Copper);
    }

    [Fact]
    public void Tiara_DoublesCopperFrom5thPlayerReveal()
    {
        // Build a small board state where Tiara is owned and 4 player tiles already revealed
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(config, rng);
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = board,
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            Equipment = new List<Equipment> { EquipmentDefinitions.Tiara with { Id = "t1" } },
            PlayerTilesRevealedCount = 4,
            Copper = 0,
            CurrentLevelId = "level1"
        };

        var playerTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player);

        var result = GameRunner.ProcessReveal(state, playerTile.Position, new Random(99));

        // 5th reveal usually grants 1 copper; with Tiara → 2
        Assert.Equal(2, result.State.Copper);
    }

    [Fact]
    public void Tiara_DoublesTwirlCopper()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var twirl = CardDefinitions.Twirl with { Id = "tw1" };
        state = state with
        {
            Equipment = new List<Equipment> { EquipmentDefinitions.Tiara with { Id = "t1" } },
            Hand = new List<Card> { twirl },
            Spoons = 3,
            Copper = 0
        };

        var newState = CardEffectSystem.PlayCard(state, twirl, null, new Random(7));

        Assert.Equal(6, newState.Copper); // base 3 × 2 Tiara
    }

    [Fact]
    public void Tiara_DoesNotAffectCopperWhenNotOwned()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var twirl = CardDefinitions.Twirl with { Id = "tw1" };
        state = state with
        {
            Hand = new List<Card> { twirl },
            Spoons = 3,
            Copper = 0
        };

        var newState = CardEffectSystem.PlayCard(state, twirl, null, new Random(7));

        Assert.Equal(3, newState.Copper); // base 3, no Tiara
    }
}
