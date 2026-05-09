using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M47: Equipment prerequisite chains (12 chained items + offering filter).
/// </summary>
public class PrereqChainEquipmentTests
{
    private static GameState BlankState(IReadOnlyList<Equipment>? equipment = null,
        IReadOnlyList<Card>? deck = null)
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            PersistentDeck = deck ?? CardDefinitions.CreateStarterDeck(),
            DrawPile = deck ?? CardDefinitions.CreateStarterDeck(),
            Equipment = equipment ?? Array.Empty<Equipment>(),
            Spoons = 3, MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };
    }

    // ---------- Prereq filter ----------

    [Fact]
    public void OfferingFilter_HidesItemWithoutPrereq()
    {
        // Tea requires Frilly Dress. Without Frilly Dress, Tea must not be in the offering pool.
        var owned = new List<Equipment>(); // no Frilly Dress
        var offered = CampaignSystem.GenerateEquipmentOptions(owned, new Random(7));
        Assert.DoesNotContain(offered, e => e.EffectType == EquipmentEffectType.Tea);
    }

    [Fact]
    public void OfferingFilter_ShowsItemAfterPrereqAcquired()
    {
        // With Frilly Dress owned, Tea is eligible (may or may not be in 3-pick rotation,
        // but should appear at least sometimes across many seeds).
        var owned = new List<Equipment> { EquipmentDefinitions.FrillyDress with { Id = "fd1" } };
        var teaSeen = false;
        for (var seed = 0; seed < 100 && !teaSeen; seed++)
        {
            var offered = CampaignSystem.GenerateEquipmentOptions(owned, new Random(seed));
            if (offered.Any(e => e.EffectType == EquipmentEffectType.Tea)) teaSeen = true;
        }
        Assert.True(teaSeen, "Tea should appear in offerings when Frilly Dress is owned");
    }

    [Fact]
    public void OfferingFilter_MultiPrereq_RequiresAll()
    {
        // Favor needs both Tea AND Cocktail. With only one, should not appear.
        var ownedOne = new List<Equipment> { EquipmentDefinitions.Tea with { Id = "t1" } };
        for (var seed = 0; seed < 30; seed++)
        {
            var offered = CampaignSystem.GenerateEquipmentOptions(ownedOne, new Random(seed));
            Assert.DoesNotContain(offered, e => e.EffectType == EquipmentEffectType.Favor);
        }

        // With both, eligible
        var ownedBoth = new List<Equipment>
        {
            EquipmentDefinitions.Tea with { Id = "t1" },
            EquipmentDefinitions.Cocktail with { Id = "c1" }
        };
        var favorSeen = false;
        for (var seed = 0; seed < 100 && !favorSeen; seed++)
        {
            var offered = CampaignSystem.GenerateEquipmentOptions(ownedBoth, new Random(seed));
            if (offered.Any(e => e.EffectType == EquipmentEffectType.Favor)) favorSeen = true;
        }
        Assert.True(favorSeen, "Favor should appear when Tea + Cocktail both owned");
    }

    [Fact]
    public void ShopOffering_AlsoFiltersOnPrereqs()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        // No equipment owned
        state = ShopSystem.EnterShop(state, new Random(7));
        var equipmentSlots = state.ShopSlots!
            .Where(s => s.Kind == ShopSlotKind.Equipment)
            .Select(s => s.Equipment)
            .Where(e => e != null)
            .ToList();
        // None of the chained equipment with prereqs should appear
        var prereqTypes = new[]
        {
            EquipmentEffectType.Tea, EquipmentEffectType.Mascara, EquipmentEffectType.Pockets,
            EquipmentEffectType.MatedPair, EquipmentEffectType.BabyBunny,
            EquipmentEffectType.TripleBroom, EquipmentEffectType.QuadrupleBroom,
            EquipmentEffectType.DiyGel, EquipmentEffectType.Geode,
            EquipmentEffectType.DiscoBall, EquipmentEffectType.Fanfic,
            EquipmentEffectType.Favor, EquipmentEffectType.Espresso
        };
        foreach (var slot in equipmentSlots)
        {
            Assert.DoesNotContain(slot!.EffectType, prereqTypes);
        }
    }

    // ---------- Tea ----------

    [Fact]
    public void Tea_RemovesFrillyDressCap()
    {
        // Setup: Frilly Dress + Tea, turn 1, already revealed 4 neutrals.
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.FrillyDress with { Id = "fd1" },
                EquipmentDefinitions.Tea with { Id = "t1" }
            },
            TurnNumber = 1,
            Turn1NeutralReveals = 4 // already at original cap
        };

        var neutralTile = state.Board.Tiles
            .First(t => t.Owner == TileOwner.Neutral);
        var (newState, suppressed) = EquipmentSystem.ApplyFrillyDress(state, neutralTile);

        Assert.True(suppressed); // Tea removes the cap → still suppresses
        Assert.Equal(5, newState.Turn1NeutralReveals);
    }

    // ---------- Mascara ----------

    [Fact]
    public void Mascara_AddsTwoDistractionsAtTurnStart()
    {
        // With Eyeshadow + Mascara: turn-start adds 1 (Eyeshadow) + 2 (Mascara) = 3 distractions
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Eyeshadow with { Id = "ey1" },
                EquipmentDefinitions.Mascara with { Id = "m1" }
            },
            // Pre-seed intent so AddDistractionPoint has candidates
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 5
            }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();

        var newState = EquipmentSystem.ApplyOnTurnStart(state, new Random(7));

        // +3 total distractions
        Assert.Equal(sumBefore + 3, newState.RivalIntentPoints.Values.Sum());
    }

    // ---------- Pockets ----------

    [Fact]
    public void Pockets_UpgradesHandbagDrawTo3()
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Handbag with { Id = "h1" },
                EquipmentDefinitions.Pockets with { Id = "p1" }
            }
        };
        var handBefore = state.Hand.Count;

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        // Handbag+Pockets = 3 cards drawn at floor start (Pockets replaces +2 with +3)
        Assert.Equal(handBefore + 3, newState.Hand.Count);
    }

    // ---------- Mated Pair / Baby Bunny ----------

    [Fact]
    public void MatedPair_Reveals2PlayerTiles()
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.DustBunny with { Id = "db1" },
                EquipmentDefinitions.MatedPair with { Id = "mp1" }
            }
        };

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        var revealedPlayerCount = newState.Board.Tiles
            .Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(2, revealedPlayerCount);
    }

    [Fact]
    public void BabyBunny_Reveals3PlayerTiles()
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.DustBunny with { Id = "db1" },
                EquipmentDefinitions.MatedPair with { Id = "mp1" },
                EquipmentDefinitions.BabyBunny with { Id = "bb1" }
            }
        };

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        var revealedPlayerCount = newState.Board.Tiles
            .Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        Assert.Equal(3, revealedPlayerCount);
    }

    // ---------- Triple / Quadruple Broom ----------

    [Fact]
    public void TripleBroom_Brushes3OnReveal()
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
            tiles.Add(new Tile { Position = new Position(row, col), Owner = TileOwner.Player });

        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 3, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.DoubleBroom with { Id = "db1" },
                EquipmentDefinitions.TripleBroom with { Id = "tb1" }
            },
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var result = GameRunner.ProcessReveal(state, new Position(1, 1), new Random(7));
        var annotatedNeighbors = BoardSystem.GetNeighbors(result.State.Board, new Position(1, 1))
            .Count(n => result.State.Board.GetTile(n).Annotations.OwnerSubset != null);
        Assert.Equal(3, annotatedNeighbors);
    }

    [Fact]
    public void QuadrupleBroom_Brushes4OnReveal()
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
            tiles.Add(new Tile { Position = new Position(row, col), Owner = TileOwner.Player });

        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 3, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.DoubleBroom with { Id = "db1" },
                EquipmentDefinitions.TripleBroom with { Id = "tb1" },
                EquipmentDefinitions.QuadrupleBroom with { Id = "qb1" }
            },
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var result = GameRunner.ProcessReveal(state, new Position(1, 1), new Random(7));
        var annotatedNeighbors = BoardSystem.GetNeighbors(result.State.Board, new Position(1, 1))
            .Count(n => result.State.Board.GetTile(n).Annotations.OwnerSubset != null);
        Assert.Equal(4, annotatedNeighbors);
    }

    // ---------- DIY Gel ----------

    [Fact]
    public void DiyGel_AutoEnhancesAddedCards()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Progesterone with { Id = "p1" },
                EquipmentDefinitions.DiyGel with { Id = "dg1" }
            }
        };

        // Add a non-bleachable card via SelectCardReward → should be enhanced by DIY Gel
        var rendezvous = CardDefinitions.Rendezvous with { Id = "future_r" };
        state = CampaignSystem.SelectCardReward(
            state with { GamePhase = GamePhase.CardReward, GameStatus = GameStatus.Won },
            rendezvous,
            new Random(99));

        var added = state.PersistentDeck.First(c => c.Id == "future_r");
        Assert.True(added.Enhanced);
    }

    // ---------- Geode ----------

    [Fact]
    public void Geode_DrawsACardOnTinglePlay()
    {
        // Build state with Crystal Ball (prereq) + Geode, plus Tingle in hand.
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, rng); // has noble for Tingle target
        var tingle = CardDefinitions.Tingle with { Id = "t1" };
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level2",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.CrystalBall with { Id = "cb1" },
                EquipmentDefinitions.Geode with { Id = "g1" }
            },
            Hand = new List<Card> { tingle },
            DrawPile = deck,
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };
        var handBefore = state.Hand.Count; // 1

        var newState = CardEffectSystem.ExecuteTingle(state, new Random(7), tingle);

        // Geode draws +1; Tingle itself doesn't auto-remove from hand here (Execute only does effect).
        Assert.Equal(handBefore + 1, newState.Hand.Count);
    }

    // ---------- Disco Ball ----------

    [Fact]
    public void DiscoBall_OnAcquisition_Adds2DoublyUpgradedTingles()
    {
        var state = BlankState();
        var sizeBefore = state.PersistentDeck.Count;

        var newState = EquipmentSystem.ApplyOnAcquisition(
            state, EquipmentDefinitions.DiscoBall, new Random(7));

        var added = newState.PersistentDeck.Where(c => c.Id.StartsWith("discoball_")).ToList();
        Assert.Equal(2, added.Count);
        Assert.All(added, c =>
        {
            Assert.Equal(CardEffectType.Tingle, c.EffectType);
            Assert.True(c.Enhanced);
            Assert.True(c.BonusSpoon);
        });
        Assert.Equal(sizeBefore + 2, newState.PersistentDeck.Count);
    }

    // ---------- Fanfic ----------

    [Fact]
    public void Fanfic_SarcasticDrawsACard_AndCosts1Copper()
    {
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var sarcastic = CardDefinitions.RecallSarcastic with { Id = "rs1" };
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Novel with { Id = "n1" },
                EquipmentDefinitions.Fanfic with { Id = "f1" }
            },
            Hand = new List<Card> { sarcastic },
            DrawPile = deck,
            Copper = 5,
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };
        var handBefore = state.Hand.Count;

        var newState = CardEffectSystem.ExecuteRecallSarcastic(state, new Random(7), sarcastic);

        // Hand grew by 1 (Fanfic draw); copper down by 1
        Assert.Equal(handBefore + 1, newState.Hand.Count);
        Assert.Equal(4, newState.Copper);
    }

    [Fact]
    public void Fanfic_CopperFlooredAtZero()
    {
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        var sarcastic = CardDefinitions.RecallSarcastic with { Id = "rs1" };
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Novel with { Id = "n1" },
                EquipmentDefinitions.Fanfic with { Id = "f1" }
            },
            Hand = new List<Card> { sarcastic },
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Copper = 0,
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var newState = CardEffectSystem.ExecuteRecallSarcastic(state, new Random(7), sarcastic);

        Assert.Equal(0, newState.Copper); // Doesn't go negative
    }

    // ---------- Favor ----------

    [Fact]
    public void Favor_WinsFloorWith1PlayerTileRemaining()
    {
        // 3-tile board: 2 players (one revealed) + 1 rival. Without Favor: not won
        // (1 player still unrevealed). With Favor: won.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player,
                IsRevealed = true, RevealedBy = PlayerType.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player }, // unrevealed
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };

        // No Favor → still Playing
        var stateNoFavor = new GameState
        {
            Board = board,
            CurrentLevelId = "level1"
        };
        Assert.Equal(GameStatus.Playing, TurnSystem.CheckGameStatus(stateNoFavor));

        // With Favor → Won
        var stateFavor = stateNoFavor with
        {
            Equipment = new List<Equipment>
            {
                EquipmentDefinitions.Tea with { Id = "t1" },
                EquipmentDefinitions.Cocktail with { Id = "c1" },
                EquipmentDefinitions.Favor with { Id = "fv1" }
            }
        };
        Assert.Equal(GameStatus.Won, TurnSystem.CheckGameStatus(stateFavor));
    }

    // ---------- Offering pool ----------

    [Fact]
    public void OfferingPool_IncludesAllChainedItems()
    {
        var pool = EquipmentDefinitions.CreateOfferingPool();
        var chainedTypes = new[]
        {
            EquipmentEffectType.Tea, EquipmentEffectType.Mascara, EquipmentEffectType.Pockets,
            EquipmentEffectType.MatedPair, EquipmentEffectType.BabyBunny,
            EquipmentEffectType.TripleBroom, EquipmentEffectType.QuadrupleBroom,
            EquipmentEffectType.DiyGel, EquipmentEffectType.Geode,
            EquipmentEffectType.DiscoBall, EquipmentEffectType.Fanfic, EquipmentEffectType.Favor
        };
        foreach (var t in chainedTypes)
        {
            Assert.Contains(pool, e => e.EffectType == t);
        }
    }
}
