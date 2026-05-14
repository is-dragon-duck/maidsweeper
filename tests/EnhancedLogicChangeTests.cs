using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M53: Enhanced effects with logic changes. Pins each □ card's enhanced
/// behavior to the alpha spec.
/// </summary>
public class EnhancedLogicChangeTests
{
    private static GameState BlankLevel1State(IReadOnlyList<Card>? deck = null)
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Spoons = 3, MaxSpoons = 3,
            DrawPile = deck ?? Array.Empty<Card>(),
            CurrentPlayer = PlayerType.Player
        };
    }

    // ---------- Spritz ----------

    [Fact]
    public void Spritz_Base_DoesNotDefuseLoungingNoble()
    {
        var pos = new Position(0, 0);
        var tiles = new List<Tile>
        {
            new() { Position = pos, Owner = TileOwner.Player,
                Specials = SpecialTileType.LoungingNoble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };

        var newState = CardEffectSystem.ExecuteSpritz(state,
            new[] { pos }, CardDefinitions.Spritz, new Random(7));

        Assert.True(newState.Board.GetTile(pos).IsLoungingNoble);
    }

    [Fact]
    public void Spritz_Enhanced_DefusesLoungingNobleAndAwards3Copper()
    {
        var pos = new Position(0, 0);
        var tiles = new List<Tile>
        {
            new() { Position = pos, Owner = TileOwner.Player,
                Specials = SpecialTileType.LoungingNoble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var enhanced = CardDefinitions.Spritz with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteSpritz(state,
            new[] { pos }, enhanced, new Random(7));

        Assert.False(newState.Board.GetTile(pos).IsLoungingNoble);
        Assert.Equal(state.Copper + 3, newState.Copper);
    }

    [Fact]
    public void Spritz_Enhanced_AlsoAnnotatesAnAdjacentTile()
    {
        // 3 tiles in a row. Spritz center → adjacent (left or right) should also get annotated.
        var pos = new Position(0, 1);
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = pos, Owner = TileOwner.Player },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral }
        };
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var enhanced = CardDefinitions.Spritz with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteSpritz(state,
            new[] { pos }, enhanced, new Random(7));

        // Target tile annotated
        Assert.NotNull(newState.Board.GetTile(pos).Annotations.OwnerSubset);

        // Some adjacent tile was also annotated (the alpha picks one at random).
        var leftAnnotated = newState.Board.GetTile(new Position(0, 0)).Annotations.OwnerSubset != null;
        var rightAnnotated = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset != null;
        Assert.True(leftAnnotated || rightAnnotated);
    }

    [Fact]
    public void Spritz_Base_OnlyAnnotatesTarget_NotAdjacent()
    {
        var pos = new Position(0, 1);
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = pos, Owner = TileOwner.Player },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral }
        };
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };

        var newState = CardEffectSystem.ExecuteSpritz(state,
            new[] { pos }, CardDefinitions.Spritz, new Random(7));

        Assert.NotNull(newState.Board.GetTile(pos).Annotations.OwnerSubset);
        Assert.Null(newState.Board.GetTile(new Position(0, 0)).Annotations.OwnerSubset);
        Assert.Null(newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset);
    }

    // ---------- Tingle ----------

    [Fact]
    public void Tingle_Base_DoesNotAddAdjacencyInfo()
    {
        var state = BlankLevel1State();
        var newState = CardEffectSystem.ExecuteTingle(state, new Random(7), CardDefinitions.Tingle);

        var annotated = newState.Board.Tiles
            .Where(t => t.Annotations.OwnerSubset != null && t.Annotations.OwnerSubset.Count == 1)
            .ToList();
        Assert.Single(annotated);
        Assert.Null(annotated[0].Annotations.AdjacencyInfo);
    }

    [Fact]
    public void Tingle_Enhanced_AddsPlayerAdjacencyInfoOnTarget()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Tingle with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteTingle(state, new Random(7), enhanced);

        var annotated = newState.Board.Tiles
            .Where(t => t.Annotations.OwnerSubset != null && t.Annotations.OwnerSubset.Count == 1)
            .ToList();
        Assert.Single(annotated);

        var info = annotated[0].Annotations.AdjacencyInfo;
        Assert.NotNull(info);
        Assert.NotNull(info!.PlayerCount);
        // No other owner counts populated
        Assert.Null(info.RivalCount);
        Assert.Null(info.NeutralCount);
        Assert.Null(info.NobleCount);

        var expected = BoardSystem.CalculateAdjacency(newState.Board,
            annotated[0].Position, PlayerType.Player);
        Assert.Equal(expected, info.PlayerCount!.Value);
    }

    // ---------- Brush ----------

    [Fact]
    public void Brush_Enhanced_AppliesTwice_NarrowsToTwoOwners()
    {
        // Single-tile board so we always hit the same tile; with 2 iterations,
        // up to 2 different non-owners get excluded, narrowing the subset.
        var tile = new Tile { Position = new Position(0, 0), Owner = TileOwner.Player };
        var state = new GameState
        {
            Board = new Board { Width = 1, Height = 1, Tiles = new List<Tile> { tile } },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var enhanced = CardDefinitions.Brush with { Enhanced = true };

        // Across seeds, at least one should produce a 2-owner subset (vs always 3 with base)
        var sawTwoOwners = false;
        for (var seed = 0; seed < 30; seed++)
        {
            var newState = CardEffectSystem.ExecuteBrush(state,
                new[] { new Position(0, 0) }, new Random(seed), enhanced);
            var subset = newState.Board.GetTile(new Position(0, 0)).Annotations.OwnerSubset;
            Assert.NotNull(subset);
            // Must always include the actual owner (Player)
            Assert.Contains(TileOwner.Player, subset);
            if (subset!.Count == 2) sawTwoOwners = true;
        }
        Assert.True(sawTwoOwners, "Enhanced Brush should sometimes narrow to 2 owners");
    }

    [Fact]
    public void Brush_Base_AlwaysLeavesThreeOwners()
    {
        var tile = new Tile { Position = new Position(0, 0), Owner = TileOwner.Player };
        var state = new GameState
        {
            Board = new Board { Width = 1, Height = 1, Tiles = new List<Tile> { tile } },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        for (var seed = 0; seed < 30; seed++)
        {
            var newState = CardEffectSystem.ExecuteBrush(state,
                new[] { new Position(0, 0) }, new Random(seed), CardDefinitions.Brush);
            var subset = newState.Board.GetTile(new Position(0, 0)).Annotations.OwnerSubset!;
            Assert.Equal(3, subset.Count);
        }
    }

    // ---------- Caffeinate ----------

    [Fact]
    public void Caffeinate_Base_Exhausts()
    {
        var state = BlankLevel1State();
        var card = CardDefinitions.Caffeinate with { Id = "c1" };
        state = state with { Hand = new List<Card> { card } };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(7));

        Assert.Contains(card, newState.ExhaustPile);
        Assert.DoesNotContain(card, newState.DiscardPile);
    }

    [Fact]
    public void Caffeinate_Enhanced_DoesNotExhaust()
    {
        var state = BlankLevel1State();
        var card = CardDefinitions.Caffeinate with { Id = "c1", Enhanced = true };
        state = state with { Hand = new List<Card> { card } };

        var newState = CardEffectSystem.PlayCard(state, card, null, new Random(7));

        Assert.DoesNotContain(card, newState.ExhaustPile);
        Assert.Contains(card, newState.DiscardPile);
    }

    // ---------- Taunt ----------

    [Fact]
    public void Taunt_Base_Requires4TargetsAnd3Reveals()
    {
        var state = BlankLevel1State();
        var targets = new[]
        {
            new Position(0, 0), new Position(0, 1),
            new Position(0, 2), new Position(0, 3)
        };
        var newState = CardEffectSystem.ExecuteTaunt(state, targets, CardDefinitions.Taunt);
        var taunt = newState.ActiveTaunts.Single();
        Assert.Equal(4, taunt.Positions.Count);
        Assert.Equal(3, taunt.RequiredReveals);
    }

    [Fact]
    public void Taunt_Enhanced_Requires3TargetsAnd2Reveals()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Taunt with { Enhanced = true };
        var targets = new[]
        {
            new Position(0, 0), new Position(0, 1), new Position(0, 2)
        };
        var newState = CardEffectSystem.ExecuteTaunt(state, targets, enhanced);
        var taunt = newState.ActiveTaunts.Single();
        Assert.Equal(3, taunt.Positions.Count);
        Assert.Equal(2, taunt.RequiredReveals);
    }

    [Fact]
    public void Taunt_Base_With3Targets_Throws()
    {
        var state = BlankLevel1State();
        var targets = new[] { new Position(0, 0), new Position(0, 1), new Position(0, 2) };
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteTaunt(state, targets, CardDefinitions.Taunt));
    }

    [Fact]
    public void Taunt_Enhanced_With4Targets_Throws()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Taunt with { Enhanced = true };
        var targets = new[]
        {
            new Position(0, 0), new Position(0, 1),
            new Position(0, 2), new Position(0, 3)
        };
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteTaunt(state, targets, enhanced));
    }

    // ---------- Gaze ----------

    [Fact]
    public void Gaze_Base_StopsAtFirstRival_DoesNotAnnotateNoble()
    {
        // Strip: Player, Rival, Noble. Gaze right from (0,0). Base stops at rival
        // → rival annotated; noble not yet found, NOT annotated as noble specifically.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Noble }
        };
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        // Rival annotated as exactly Rival
        var rivalAnn = newState.Board.GetTile(new Position(0, 1)).Annotations.OwnerSubset!;
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Rival }, rivalAnn);

        // The noble at (0,2) was not part of base scan and has NO annotation
        var nobleAnn = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset;
        Assert.Null(nobleAnn);
    }

    [Fact]
    public void Gaze_Enhanced_FindsBothRivalAndNoble()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Noble }
        };
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var card = new Card
        {
            Id = "g1", Name = "Gaze →", Cost = 1,
            EffectType = CardEffectType.Gaze, Direction = LineDirection.Right,
            Enhanced = true
        };

        var newState = CardEffectSystem.ExecuteGaze(state, new[] { new Position(0, 0) }, card);

        var rivalAnn = newState.Board.GetTile(new Position(0, 1)).Annotations.OwnerSubset!;
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Rival }, rivalAnn);

        var nobleAnn = newState.Board.GetTile(new Position(0, 2)).Annotations.OwnerSubset!;
        Assert.Equal(new HashSet<TileOwner> { TileOwner.Noble }, nobleAnn);
    }

    // ---------- Fetch ----------

    [Fact]
    public void Fetch_Enhanced_DrawsOneCard()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player }
        };
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "d1" },
            CardDefinitions.Spritz with { Id = "d2" }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            DrawPile = deck,
            Spoons = 3
        };
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right,
            Enhanced = true
        };
        var handBefore = state.Hand.Count;

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card, new Random(7));

        Assert.Equal(handBefore + 1, newState.Hand.Count);
    }

    [Fact]
    public void Fetch_Base_DoesNotDraw()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Player }
        };
        var deck = new List<Card>
        {
            CardDefinitions.Spritz with { Id = "d1" }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            DrawPile = deck,
            Spoons = 3
        };
        var card = new Card
        {
            Id = "f1", Name = "Fetch →", Cost = 1,
            EffectType = CardEffectType.Fetch, Direction = LineDirection.Right
        };
        var handBefore = state.Hand.Count;

        var newState = CardEffectSystem.ExecuteFetch(state, new[] { new Position(0, 0) }, card, new Random(7));

        Assert.Equal(handBefore, newState.Hand.Count);
    }

    // ---------- Rendezvous ----------

    [Fact]
    public void Rendezvous_Enhanced_PicksClosestTiles()
    {
        // 5×1 strip: Player at col 0, Rival at col 4; target at col 2.
        // Player-closest to target = col 0 (distance 2). Rival-closest = col 4.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 3), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 4), Owner = TileOwner.Rival }
        };
        var state = new GameState
        {
            Board = new Board { Width = 5, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var enhanced = CardDefinitions.Rendezvous with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteRendezvous(state, new Random(7), enhanced,
            new[] { new Position(0, 2) });

        Assert.True(newState.Board.GetTile(new Position(0, 0)).IsRevealed);
        Assert.True(newState.Board.GetTile(new Position(0, 4)).IsRevealed);
    }

    [Fact]
    public void Rendezvous_Base_NoTargetRequired()
    {
        var state = BlankLevel1State();
        // Should not throw without a target
        _ = CardEffectSystem.ExecuteRendezvous(state, new Random(7), CardDefinitions.Rendezvous);
    }

    [Fact]
    public void Rendezvous_Enhanced_RequiresTarget_ThrowsWithout()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Rendezvous with { Enhanced = true };
        Assert.Throws<ArgumentException>(() =>
            CardEffectSystem.ExecuteRendezvous(state, new Random(7), enhanced, null));
    }

    [Fact]
    public void Rendezvous_Enhanced_AnnotatesCloserUnrevealedTiles()
    {
        // 5×1 strip: Rival at col 0, Player at col 4, target at col 4.
        // Closest rival is col 0 (dist 4). Closest player is col 4 itself (dist 0).
        // After revealing rival at col 0, tiles strictly closer to col 4 (cols 1,2,3 — neutrals)
        // get annotated as "not rival".
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 3), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 4), Owner = TileOwner.Player }
        };
        var state = new GameState
        {
            Board = new Board { Width = 5, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1",
            Spoons = 3
        };
        var enhanced = CardDefinitions.Rendezvous with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteRendezvous(state, new Random(7), enhanced,
            new[] { new Position(0, 4) });

        // Col 4 (player) was revealed → its closer-tiles are col 4 itself only; no annotations needed there.
        // Col 0 (rival) revealed at dist 4 → cols 1,2,3 (dist 3,2,1) annotated as "not rival".
        foreach (var col in new[] { 1, 2, 3 })
        {
            var subset = newState.Board.GetTile(new Position(0, col)).Annotations.OwnerSubset;
            Assert.NotNull(subset);
            Assert.DoesNotContain(TileOwner.Rival, subset);
        }
    }

    // ---------- Description text ----------

    [Theory]
    [InlineData("Spritz", "defuse")]
    [InlineData("Tingle", "player-neighbor")]
    [InlineData("Brush", "twice")]
    [InlineData("Caffeinate", "does not exhaust")]
    [InlineData("Taunt", "3 tiles")]
    [InlineData("Rendezvous", "closest")]
    public void EnhancedCard_DescriptionMentionsLogicChange(string cardName, string substring)
    {
        var card = cardName switch
        {
            "Spritz" => CardDefinitions.Spritz,
            "Tingle" => CardDefinitions.Tingle,
            "Brush" => CardDefinitions.Brush,
            "Caffeinate" => CardDefinitions.Caffeinate,
            "Taunt" => CardDefinitions.Taunt,
            "Rendezvous" => CardDefinitions.Rendezvous,
            _ => throw new ArgumentException(cardName)
        };
        Assert.Contains(substring, card.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GazeAndFetch_Descriptions_MentionEnhanced()
    {
        Assert.Contains("noble", CardDefinitions.GazeUp.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("draw", CardDefinitions.FetchUp.Description, StringComparison.OrdinalIgnoreCase);
    }
}
