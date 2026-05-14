using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M45: Mop (draws on courtier-clean) + Espresso (turn-start auto-play) equipment.
/// </summary>
public class MopAndEspressoTests
{
    /// <summary>
    /// Builds a 3×3 board where (1,1) is a Player tile with a courtier whose move
    /// target is (1,2). Other tiles are Players.
    /// </summary>
    private static GameState BuildCourtierBoard(IReadOnlyList<Equipment> equipment, IReadOnlyList<Card>? hand = null, IReadOnlyList<Card>? draw = null)
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
        {
            var pos = new Position(row, col);
            if (pos == new Position(1, 1))
            {
                tiles.Add(new Tile
                {
                    Position = pos, Owner = TileOwner.Player,
                    Specials = SpecialTileType.Courtier,
                    CourtierMoveTarget = new Position(1, 2)
                });
            }
            else
            {
                tiles.Add(new Tile { Position = pos, Owner = TileOwner.Player });
            }
        }

        var deck = CardDefinitions.CreateStarterDeck();
        return new GameState
        {
            Board = new Board { Width = 3, Height = 3, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = equipment,
            Hand = hand ?? deck.Take(3).ToList(),
            DrawPile = draw ?? deck.Skip(3).ToList(),
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player,
            // Pre-seed intent so rival turn doesn't fall into the random branch
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 1 }
        };
    }

    // ---------- Mop ----------

    [Fact]
    public void Mop_CleaningCourtier_Draws1Card()
    {
        var mop = EquipmentDefinitions.Mop with { Id = "m1" };
        var state = BuildCourtierBoard(new List<Equipment> { mop });
        var handBefore = state.Hand.Count;

        // Click the courtier
        var result = GameRunner.ProcessReveal(state, new Position(1, 1), new Random(7));

        // Hand grew by 1 (Mop drew a card from courtier-clean) — note the click ends
        // the player's turn, but the immediate draw happens before turn transition;
        // the rival turn then starts a new player turn with a fresh hand.
        // To assert just the Mop draw, check the state RIGHT after the courtier clean.
        // Easier: build an explicit state without going through the turn transition.
        var directState = state with
        {
            Board = BoardSystem.CleanCourtier(state.Board, new Position(1, 1), new Random(7))
        };
        directState = EquipmentSystem.OnCourtierCleaned(directState, new Random(7));

        Assert.Equal(handBefore + 1, directState.Hand.Count);
    }

    [Fact]
    public void Mop_NoEquipment_NoDraw()
    {
        var state = BuildCourtierBoard(new List<Equipment>());
        var handBefore = state.Hand.Count;

        var board = BoardSystem.CleanCourtier(state.Board, new Position(1, 1), new Random(7));
        var newState = state with { Board = board };
        newState = EquipmentSystem.OnCourtierCleaned(newState, new Random(7));

        Assert.Equal(handBefore, newState.Hand.Count);
    }

    [Fact]
    public void Mop_RevealingNonCourtierTile_DoesNotDraw()
    {
        // Mop equipped but the player reveals a non-courtier tile via Spritz.
        var mop = EquipmentDefinitions.Mop with { Id = "m1" };
        var spritz = CardDefinitions.Spritz with { Id = "s1" };
        var state = BuildCourtierBoard(
            new List<Equipment> { mop },
            hand: new List<Card> { spritz });
        var handBefore = state.Hand.Count;

        // Spritz a non-courtier tile (1,2) — no courtier there, no Mop trigger
        var newState = CardEffectSystem.ExecuteSpritz(state, new[] { new Position(1, 2) }, spritz, new Random(7));

        Assert.Equal(handBefore, newState.Hand.Count);
    }

    [Fact]
    public void Mop_AreaCardCleansMultipleCourtiers_DrawsPerCourtier()
    {
        // Build a 3x3 board with TWO courtiers; Sweep area covers both.
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
        {
            var pos = new Position(row, col);
            if (pos == new Position(0, 0) || pos == new Position(2, 2))
            {
                tiles.Add(new Tile
                {
                    Position = pos, Owner = TileOwner.Player,
                    Specials = SpecialTileType.Courtier,
                    CourtierMoveTarget = new Position(1, 1)
                });
            }
            else
            {
                tiles.Add(new Tile { Position = pos, Owner = TileOwner.Player });
            }
        }

        var sweep = CardDefinitions.Sweep with { Id = "sw1" };
        var deck = CardDefinitions.CreateStarterDeck();
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 3, Tiles = tiles },
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { EquipmentDefinitions.Mop with { Id = "m1" } },
            Hand = new List<Card> { sweep },
            DrawPile = deck,
            Spoons = 3,
            CurrentPlayer = PlayerType.Player
        };
        var handBefore = state.Hand.Count; // 1

        var newState = CardEffectSystem.ExecuteSweep(state, new[] { new Position(1, 1) }, new Random(7), sweep);

        // 2 courtiers cleaned → 2 cards drawn → hand size before card-removal is 1 + 2 = 3
        // (Sweep card itself is still in hand because Execute* doesn't remove it)
        Assert.Equal(handBefore + 2, newState.Hand.Count);
    }

    // ---------- Espresso ----------

    [Fact]
    public void Espresso_AtTurnStart_DrawsExtra_AndAutoPlaysCheapest()
    {
        // Hand: [LockIn cost-0 auto-playable, Spritz target-card]. Draw pile: only
        // Spritz cards (none auto-playable) so the drawn card doesn't get auto-played.
        var espresso = EquipmentDefinitions.Espresso with { Id = "e1" };
        var lockIn = CardDefinitions.LockIn with { Id = "lock1" };
        var spritz = CardDefinitions.Spritz with { Id = "spritz1" };
        var drawPile = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"draw{i}" })
            .ToList<Card>();

        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { espresso },
            Hand = new List<Card> { lockIn, spritz },
            DrawPile = drawPile,
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };
        var handBefore = state.Hand.Count; // 2

        var newState = EquipmentSystem.ApplyOnTurnStart(state, new Random(7));

        // Espresso draws +1 (hand=3) → auto-plays LockIn (cost 0 — cheapest non-targeting,
        // hand becomes 2 after removal) → LockIn draws +2 (hand becomes 4).
        // Net: handBefore + 2.
        Assert.Equal(handBefore + 2, newState.Hand.Count);
        Assert.DoesNotContain(newState.Hand, c => c.Id == "lock1");
    }

    [Fact]
    public void Espresso_NoAutoPlayableCards_OnlyDrawsExtra()
    {
        // Hand contains only Spritz (requires targeting). Draw pile is also all
        // Spritzes so the drawn card is also non-auto-playable.
        var espresso = EquipmentDefinitions.Espresso with { Id = "e1" };
        var spritz = CardDefinitions.Spritz with { Id = "s1" };
        var drawPile = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"draw{i}" })
            .ToList<Card>();

        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { espresso },
            Hand = new List<Card> { spritz },
            DrawPile = drawPile,
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };
        var handBefore = state.Hand.Count;

        var newState = EquipmentSystem.ApplyOnTurnStart(state, new Random(7));

        // Just the +1 from Espresso draw; no auto-play (no card eligible)
        Assert.Equal(handBefore + 1, newState.Hand.Count);
        Assert.Contains(newState.Hand, c => c.Id == "s1");
    }

    [Fact]
    public void Espresso_NoSpoons_DoesNotAutoPlay()
    {
        // Espresso with 0 spoons can't auto-play any card (LockIn cost 0 still works though).
        var espresso = EquipmentDefinitions.Espresso with { Id = "e1" };
        var caffeinate = CardDefinitions.Caffeinate with { Id = "c1" }; // cost 1
        var deck = CardDefinitions.CreateStarterDeck();

        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            CurrentLevelId = "level1",
            Equipment = new List<Equipment> { espresso },
            Hand = new List<Card> { caffeinate },
            DrawPile = deck,
            Spoons = 0,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };

        var newState = EquipmentSystem.ApplyOnTurnStart(state, new Random(7));

        // Caffeinate (cost 1) wasn't played — still in hand
        Assert.Contains(newState.Hand, c => c.Id == "c1");
    }

    [Fact]
    public void Espresso_PrereqIsCoffee()
    {
        var espresso = EquipmentDefinitions.Espresso;
        Assert.Single(espresso.Prereqs);
        Assert.Equal(EquipmentEffectType.Coffee, espresso.Prereqs[0]);
    }

    [Fact]
    public void OfferingPool_IncludesMopAndEspresso()
    {
        var pool = EquipmentDefinitions.CreateOfferingPool();
        Assert.Contains(pool, e => e.EffectType == EquipmentEffectType.Mop);
        Assert.Contains(pool, e => e.EffectType == EquipmentEffectType.Espresso);
    }
}
