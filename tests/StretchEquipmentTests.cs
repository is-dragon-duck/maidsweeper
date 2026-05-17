using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M46: 8 stretch equipment items deferred from Stage 4.
/// </summary>
public class StretchEquipmentTests
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

    // ---------- Hyperfocus ----------

    [Fact]
    public void Hyperfocus_PullsNetCostZeroCardFromDrawPile()
    {
        // Deck contains a cost-1 card with BonusSpoon (net cost 0) plus higher-cost cards.
        var freebie = CardDefinitions.Spritz with { Id = "free", BonusSpoon = true }; // net cost 0
        var deck = new List<Card>
        {
            freebie,
            CardDefinitions.RecallImperious with { Id = "r1" }, // cost 2
            CardDefinitions.Brush with { Id = "br1" }            // cost 1
        };
        var state = BlankState(
            new List<Equipment> { EquipmentDefinitions.Hyperfocus with { Id = "h1" } },
            deck);
        // Drain the hand so Hyperfocus's pickup is the only thing in hand
        state = state with { Hand = new List<Card>() };

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        Assert.Contains(newState.Hand, c => c.Id == "free");
        Assert.DoesNotContain(newState.DrawPile, c => c.Id == "free");
    }

    [Fact]
    public void Hyperfocus_NoOpWhenNoNetZeroCardInDraw()
    {
        var deck = new List<Card>
        {
            CardDefinitions.RecallImperious with { Id = "r1" }, // cost 2
            CardDefinitions.Brush with { Id = "br1" }            // cost 1, no bonus → net 1
        };
        var state = BlankState(
            new List<Equipment> { EquipmentDefinitions.Hyperfocus with { Id = "h1" } },
            deck);
        state = state with { Hand = new List<Card>() };

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        Assert.Empty(newState.Hand);
    }

    // ---------- Choker ----------

    [Fact]
    public void Choker_SkipsRivalTurn_When5OrFewerUnrevealedRemain()
    {
        // Build a 2x3 board with 5 unrevealed and 1 revealed tile.
        var tiles = new List<Tile>();
        for (var i = 0; i < 6; i++)
        {
            var pos = new Position(i / 3, i % 3);
            tiles.Add(new Tile
            {
                Position = pos,
                Owner = TileOwner.Rival,
                IsRevealed = i == 0,
                RevealedBy = i == 0 ? PlayerType.Player : null
            });
        }
        var board = new Board { Width = 3, Height = 2, Tiles = tiles };
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { EquipmentDefinitions.Choker with { Id = "ch1" } },
            CurrentPlayer = PlayerType.Rival,
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 1)] = 5
            }
        };

        var revealedBefore = state.Board.Tiles.Count(t => t.IsRevealed);
        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(7));
        var revealedAfter = newState.Board.Tiles.Count(t => t.IsRevealed);

        Assert.Equal(revealedBefore, revealedAfter);
    }

    [Fact]
    public void Choker_DoesNotSuppress_WhenMoreThan5Unrevealed()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { EquipmentDefinitions.Choker with { Id = "ch1" } }
        };

        // Level 1 has 30 unrevealed tiles; Choker doesn't trigger
        var revealedBefore = state.Board.Tiles.Count(t => t.IsRevealed);
        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(99));
        var revealedAfter = newState.Board.Tiles.Count(t => t.IsRevealed);

        Assert.True(revealedAfter > revealedBefore);
    }

    // ---------- Mirror ----------

    [Fact]
    public void Mirror_RevealsRandomRivalTile_AtFloorStart()
    {
        var state = BlankState(
            new List<Equipment> { EquipmentDefinitions.Mirror with { Id = "m1" } });

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        var rivalsRevealed = newState.Board.Tiles
            .Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);
        Assert.True(rivalsRevealed >= 1);
    }

    [Fact]
    public void Mirror_RevealedRivalShowsPlayerAdjacency()
    {
        // Mirror reveals "as Player" so the badge on the rival tile displays
        // the player-neighbor count instead of the usual rival-neighbor count.
        var state = BlankState(
            new List<Equipment> { EquipmentDefinitions.Mirror with { Id = "m1" } });

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        var revealedRival = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Rival);
        Assert.Equal(PlayerType.Player, revealedRival.RevealedBy);

        var expectedPlayerAdj = BoardSystem.CalculateAdjacency(
            newState.Board, revealedRival.Position, PlayerType.Player);
        Assert.Equal(expectedPlayerAdj, revealedRival.AdjacencyCount);
    }

    [Fact]
    public void Mirror_AnnotatesThreeNeighborsWithPlayerAdjacency()
    {
        // Cluster picker chooses up to 3 unrevealed neighbors of the revealed rival.
        var state = BlankState(
            new List<Equipment> { EquipmentDefinitions.Mirror with { Id = "m1" } });

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        var revealedRival = newState.Board.Tiles
            .First(t => t.IsRevealed && t.Owner == TileOwner.Rival);
        var neighbors = BoardSystem.GetNeighbors(newState.Board, revealedRival.Position);
        var annotated = neighbors.Where(n =>
            newState.Board.GetTile(n).Annotations.AdjacencyInfo?.PlayerCount.HasValue == true).ToList();

        // Up to 3 neighbors; clamp to neighbor pool size.
        var expected = Math.Min(3, neighbors.Count(n =>
        {
            var t = newState.Board.GetTile(n);
            return !t.IsRevealed && !t.IsDestroyed;
        }));
        Assert.Equal(expected, annotated.Count);
    }

    // ---------- Busy Canary ----------

    [Fact]
    public void BusyCanary_AnnotatesTilesAtFloorStart()
    {
        // Use Level2 (1 noble) so Busy Canary has something to find
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, rng);
        var state = new GameState
        {
            Board = board,
            CurrentLevelId = "level2",
            PersistentDeck = CardDefinitions.CreateStarterDeck(),
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment> { EquipmentDefinitions.BusyCanary with { Id = "bc1" } }
        };

        var newState = EquipmentSystem.ApplyOnFloorStart(state, new Random(7));

        // Some tiles should now have owner-subset annotations
        var annotatedCount = newState.Board.Tiles
            .Count(t => t.Annotations.OwnerSubset != null);
        Assert.True(annotatedCount > 0);
    }

    // ---------- Double Broom ----------

    [Fact]
    public void DoubleBroom_BrushesTwoAdjacentTiles_OnPlayerReveal()
    {
        // Build a 3×3 board, all player tiles. Click center → Double Broom annotates 2 adjacent.
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
            tiles.Add(new Tile { Position = new Position(row, col), Owner = TileOwner.Player });

        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 3, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { EquipmentDefinitions.DoubleBroom with { Id = "db1" } },
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var result = GameRunner.ProcessReveal(state, new Position(1, 1), new Random(7));

        // Center revealed; check that exactly 2 unrevealed neighbors got owner-subset annotations
        var annotatedNeighbors = BoardSystem.GetNeighbors(result.State.Board, new Position(1, 1))
            .Count(n => result.State.Board.GetTile(n).Annotations.OwnerSubset != null);
        Assert.Equal(2, annotatedNeighbors);
    }

    [Fact]
    public void DoubleBroom_NoOp_WhenNoUnrevealedNeighbors()
    {
        // Single-tile board: no neighbors to brush
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player }
        };
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = new Board { Width = 1, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { EquipmentDefinitions.DoubleBroom with { Id = "db1" } },
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        // Should not throw
        var result = GameRunner.ProcessReveal(state, new Position(0, 0), new Random(7));
        Assert.True(result.State.Board.GetTile(new Position(0, 0)).IsRevealed);
    }

    // ---------- Broom Closet ----------

    [Fact]
    public void BroomCloset_RemovesAllSpritz_Adds3Sweep()
    {
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "s1" },
            CardDefinitions.Spritz with { Id = "s2" },
            CardDefinitions.Tingle with { Id = "t1" }
        };
        var state = BlankState(deck: deck);

        var newState = EquipmentSystem.ApplyOnAcquisition(
            state, EquipmentDefinitions.BroomCloset, new Random(7));

        Assert.DoesNotContain(newState.PersistentDeck, c => c.EffectType == CardEffectType.Spritz);
        Assert.Equal(3, newState.PersistentDeck.Count(c => c.EffectType == CardEffectType.Sweep));
        Assert.Single(newState.PersistentDeck.Where(c => c.EffectType == CardEffectType.Tingle));
    }

    // ---------- Cocktail ----------

    [Fact]
    public void Cocktail_RemovesAllScurry_Adds2BonusSpoonCards()
    {
        var deck = new List<Card>
        {
            CardDefinitions.Scurry with { Id = "sc1" },
            CardDefinitions.Scurry with { Id = "sc2" },
            CardDefinitions.Tingle with { Id = "t1" }
        };
        var state = BlankState(deck: deck);

        var newState = EquipmentSystem.ApplyOnAcquisition(
            state, EquipmentDefinitions.Cocktail, new Random(7));

        Assert.DoesNotContain(newState.PersistentDeck, c => c.EffectType == CardEffectType.Scurry);

        // 2 newly-added bonus-spoon cards (with cocktail_ prefix in their IDs)
        var added = newState.PersistentDeck.Where(c => c.Id.StartsWith("cocktail_")).ToList();
        Assert.Equal(2, added.Count);
        Assert.All(added, c => Assert.True(c.BonusSpoon));
    }

    // ---------- Novel ----------

    [Fact]
    public void Novel_ReplacesAllRecallVariants_WithDoublyUpgradedSarcastic()
    {
        var deck = new List<Card>
        {
            CardDefinitions.RecallImperious with { Id = "r1" },
            CardDefinitions.RecallVague with { Id = "r2" },
            CardDefinitions.RecallSarcastic with { Id = "r3" },
            CardDefinitions.Tingle with { Id = "t1" }
        };
        var state = BlankState(deck: deck);

        var newState = EquipmentSystem.ApplyOnAcquisition(
            state, EquipmentDefinitions.Novel, new Random(7));

        // 3 Recalls → 3 doubly-upgraded Sarcastics
        var sarcastics = newState.PersistentDeck
            .Where(c => c.EffectType == CardEffectType.RecallSarcastic)
            .ToList();
        Assert.Equal(3, sarcastics.Count);
        Assert.All(sarcastics, c =>
        {
            Assert.True(c.Enhanced);
            Assert.True(c.BonusSpoon);
        });
        // Other Recall variants gone
        Assert.DoesNotContain(newState.PersistentDeck, c => c.EffectType == CardEffectType.Recall);
        Assert.DoesNotContain(newState.PersistentDeck, c => c.EffectType == CardEffectType.RecallVague);
        // Tingle untouched
        Assert.Contains(newState.PersistentDeck, c => c.EffectType == CardEffectType.Tingle);
    }

    // ---------- Offering pool ----------

    [Fact]
    public void OfferingPool_IncludesAllStretchItems()
    {
        var pool = EquipmentDefinitions.CreateOfferingPool();
        var stretchTypes = new[]
        {
            EquipmentEffectType.Hyperfocus, EquipmentEffectType.Choker,
            EquipmentEffectType.Mirror, EquipmentEffectType.BusyCanary,
            EquipmentEffectType.DoubleBroom, EquipmentEffectType.BroomCloset,
            EquipmentEffectType.Cocktail, EquipmentEffectType.Novel
        };
        foreach (var t in stretchTypes)
        {
            Assert.Contains(pool, e => e.EffectType == t);
        }
    }
}
