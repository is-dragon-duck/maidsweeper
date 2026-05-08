using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// Tests for the rival intent point system (M35):
/// per-tile weights drive rival reveals, regenerate at turn start, decay after reveals.
/// </summary>
public class IntentSystemTests
{
    private static GameState BuildState(int seed = 42)
    {
        var rng = new Random(seed);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState { Board = board };
    }

    // ---------- Generation ----------

    [Fact]
    public void Generate_ProducesPointsOnUpTo8Positions()
    {
        var state = BuildState();
        var rng = new Random(7);

        var points = IntentSystem.GenerateTurnIntent(state, rng);

        Assert.True(points.Count > 0);
        Assert.True(points.Count <= 8); // BasePoints array length
    }

    [Fact]
    public void Generate_AssignsBasePointsBeforeDistractions()
    {
        // With 8 base picks at [5,3,3,3,3,1,1,1] = 20, plus 4 distractions = 24 total
        var state = BuildState();
        var rng = new Random(7);

        var points = IntentSystem.GenerateTurnIntent(state, rng);
        var sum = points.Values.Sum();

        // Sum of 8 base assignments = 20; distractions add 4 more = 24
        Assert.Equal(24, sum);
    }

    [Fact]
    public void Generate_RivalTilesPickedWhenAvailable()
    {
        // Run many times — at least one rival tile should appear in the pool each time.
        // (The base generator picks 2 rival tiles before others; if any unrevealed rivals
        // exist, at least one should appear in the points map.)
        for (var seed = 0; seed < 20; seed++)
        {
            var state = BuildState(seed);
            var points = IntentSystem.GenerateTurnIntent(state, new Random(seed + 100));

            var rivalPositions = state.Board.Tiles
                .Where(t => t.Owner == TileOwner.Rival)
                .Select(t => t.Position)
                .ToHashSet();

            Assert.Contains(points.Keys, p => rivalPositions.Contains(p));
        }
    }

    [Fact]
    public void Generate_AllPositionsHavePositivePoints()
    {
        var state = BuildState();
        var points = IntentSystem.GenerateTurnIntent(state, new Random(7));

        Assert.All(points.Values, v => Assert.True(v > 0));
    }

    [Fact]
    public void Generate_EmptyBoard_ReturnsEmptyMap()
    {
        // Use a fully-revealed board (so no eligible tiles)
        var state = BuildState();
        var newTiles = state.Board.Tiles.Select(t => t with { IsRevealed = true }).ToList();
        var board = state.Board with { Tiles = newTiles };
        var allRevealed = state with { Board = board };

        var points = IntentSystem.GenerateTurnIntent(allRevealed, new Random(7));

        Assert.Empty(points);
    }

    // ---------- AddDistractionPoint ----------

    [Fact]
    public void AddDistractionPoint_IncrementsRandomNonzeroTile()
    {
        var points = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 5,
            [new Position(1, 1)] = 3
        };
        var sumBefore = points.Values.Sum();

        IntentSystem.AddDistractionPoint(points, new HashSet<Position>(), new Random(7));

        Assert.Equal(sumBefore + 1, points.Values.Sum());
    }

    [Fact]
    public void AddDistractionPoint_NoOpOnEmpty()
    {
        var points = new Dictionary<Position, int>();
        IntentSystem.AddDistractionPoint(points, new HashSet<Position>(), new Random(7));
        Assert.Empty(points);
    }

    [Fact]
    public void AddDistractionPoint_SkipsExcludedPositions()
    {
        var keep = new Position(0, 0);
        var skip = new Position(1, 1);
        var points = new Dictionary<Position, int>
        {
            [keep] = 5,
            [skip] = 3
        };
        var excluded = new HashSet<Position> { skip };

        // Run many times — distraction should never land on `skip`.
        for (var i = 0; i < 50; i++)
        {
            var copy = new Dictionary<Position, int>(points);
            IntentSystem.AddDistractionPoint(copy, excluded, new Random(i));
            Assert.Equal(points[skip], copy[skip]); // unchanged
            Assert.Equal(points[keep] + 1, copy[keep]); // incremented
        }
    }

    // ---------- Decay ----------

    [Fact]
    public void Decay_RemovesRevealedPositions()
    {
        var state = BuildState();
        var revealed = new Position(0, 0);
        var keep = new Position(1, 1);
        state = state with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [revealed] = 5,
                [keep] = 3
            }
        };

        var newPoints = IntentSystem.DecayIntent(state, new[] { revealed });

        Assert.False(newPoints.ContainsKey(revealed));
        Assert.True(newPoints.ContainsKey(keep));
    }

    [Fact]
    public void Decay_DecrementsRemainingPoints()
    {
        var state = BuildState() with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(1, 1)] = 5,
                [new Position(2, 2)] = 1
            }
        };

        var newPoints = IntentSystem.DecayIntent(state, Array.Empty<Position>());

        Assert.Equal(4, newPoints[new Position(1, 1)]);
        // Position with 1 point decays to 0 and is dropped
        Assert.False(newPoints.ContainsKey(new Position(2, 2)));
    }

    [Fact]
    public void Decay_DropsZeroPointTiles()
    {
        var state = BuildState() with
        {
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 1,
                [new Position(1, 1)] = 1
            }
        };

        var newPoints = IntentSystem.DecayIntent(state, Array.Empty<Position>());

        Assert.Empty(newPoints);
    }

    [Fact]
    public void Decay_RemovesNeighborsOf0AdjRivalReveal()
    {
        // Build a controlled board where we know the 0-adj rival's neighbors
        var rng = new Random(42);
        var state = BuildState();
        // Pick a rival tile and force its adjacency to 0 (no rival neighbors)
        // Easiest: reveal it and set AdjacencyCount=0 directly
        var rivalTile = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival);
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(rivalTile.Position);
        newTiles[idx] = rivalTile with
        {
            IsRevealed = true,
            RevealedBy = PlayerType.Rival,
            AdjacencyCount = 0
        };
        var board = state.Board with { Tiles = newTiles };

        var neighborPositions = BoardSystem.GetNeighbors(board, rivalTile.Position);
        var pointsMap = new Dictionary<Position, int>();
        // Seed all neighbors with high points so decrement won't drop them
        foreach (var n in neighborPositions) pointsMap[n] = 5;
        // Plus a far-away tile that should NOT be removed
        var farPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position)
                        && !neighborPositions.Contains(t.Position)
                        && t.Position != rivalTile.Position).Position;
        pointsMap[farPos] = 5;

        state = state with { Board = board, RivalIntentPoints = pointsMap };

        var newPoints = IntentSystem.DecayIntent(state, new[] { rivalTile.Position });

        // All neighbors removed (because revealed tile had 0-adj rival)
        foreach (var n in neighborPositions)
            Assert.False(newPoints.ContainsKey(n), $"neighbor {n} should be removed");

        // Far tile decremented but still present (started at 5)
        Assert.Equal(4, newPoints[farPos]);
    }

    // ---------- Combine ----------

    [Fact]
    public void Combine_SumsOverlappingPositions()
    {
        var a = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 5,
            [new Position(1, 1)] = 3
        };
        var b = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 2,
            [new Position(2, 2)] = 4
        };

        var combined = IntentSystem.Combine(a, b);

        Assert.Equal(7, combined[new Position(0, 0)]); // 5 + 2
        Assert.Equal(3, combined[new Position(1, 1)]);
        Assert.Equal(4, combined[new Position(2, 2)]);
    }

    // ---------- Carry-over via StartPlayerTurn ----------

    [Fact]
    public void StartPlayerTurn_CarriesOverPreviousIntent()
    {
        var state = BuildState() with
        {
            DrawPile = CardDefinitions.CreateStarterDeck(),
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1,
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 5
            }
        };

        var newState = TurnSystem.StartPlayerTurn(state, new Random(7));

        // Carried-over (0,0) point still in the dict
        Assert.True(newState.RivalIntentPoints.ContainsKey(new Position(0, 0)));
        Assert.True(newState.RivalIntentPoints[new Position(0, 0)] >= 5);
    }

    // ---------- PickHighestPoints ----------

    [Fact]
    public void PickHighestPoints_ReturnsMaxValue()
    {
        var points = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 5,
            [new Position(1, 1)] = 3,
            [new Position(2, 2)] = 8
        };

        var picked = IntentSystem.PickHighestPoints(points, new Random(7));

        Assert.Equal(new Position(2, 2), picked);
    }

    [Fact]
    public void PickHighestPoints_TiesPickedRandomly()
    {
        var points = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 5,
            [new Position(1, 1)] = 5,
            [new Position(2, 2)] = 5
        };

        var picks = new HashSet<Position>();
        for (var i = 0; i < 50; i++)
        {
            var picked = IntentSystem.PickHighestPoints(points, new Random(i));
            if (picked.HasValue) picks.Add(picked.Value);
        }

        // Across 50 seeds, all 3 tied positions should appear at least once
        Assert.Equal(3, picks.Count);
    }

    [Fact]
    public void PickHighestPoints_NullOnEmpty()
    {
        var picked = IntentSystem.PickHighestPoints(new Dictionary<Position, int>(), new Random(7));
        Assert.Null(picked);
    }
}
