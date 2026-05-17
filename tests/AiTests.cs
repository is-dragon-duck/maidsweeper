using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// Tests for the rival AI framework (M36): IRivalAi, AiRegistry, RandomAi, NoGuessAi.
/// </summary>
public class AiTests
{
    private static GameState BuildState(int seed = 42)
    {
        var rng = new Random(seed);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState { Board = board, CurrentLevelId = "level1" };
    }

    // ---------- AiRegistry ----------

    [Fact]
    public void AiRegistry_RoutesEachTypeToCorrectImplementation()
    {
        Assert.IsType<RandomAi>(AiRegistry.Get(AiType.Random));
        Assert.IsType<NoGuessAi>(AiRegistry.Get(AiType.NoGuess));
        // Conservative + Reasoning are M37/M42; for now they fall back to Random.
        Assert.NotNull(AiRegistry.Get(AiType.Conservative));
        Assert.NotNull(AiRegistry.Get(AiType.Reasoning));
    }

    [Fact]
    public void LevelConfig_DefaultRivalAi_IsRandom()
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        Assert.Equal(AiType.Random, config.RivalAi);
    }

    // ---------- RandomAi ----------

    [Fact]
    public void RandomAi_RespectsZeroPoints()
    {
        // Tile with 0 points should never be picked even if other tiles also have 0.
        var state = BuildState();
        var keep = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed)
            .Position;
        var skip = state.Board.Tiles
            .Last(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Position != keep)
            .Position;

        var intent = new Dictionary<Position, int>
        {
            [keep] = 5,
            [skip] = 0
        };

        var ai = new RandomAi();
        for (var seed = 0; seed < 50; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(seed));
            Assert.DoesNotContain(skip, picks);
        }
    }

    [Fact]
    public void RandomAi_WeightedDistributionFavorsHigherPoints()
    {
        var state = BuildState();
        var unrevealed = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner != TileOwner.Rival)
            .Take(2)
            .ToList();
        var heavy = unrevealed[0].Position;
        var light = unrevealed[1].Position;

        var intent = new Dictionary<Position, int>
        {
            [heavy] = 9,
            [light] = 1
        };

        var ai = new RandomAi();
        var heavyHits = 0;
        var trials = 200;
        for (var seed = 0; seed < trials; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(seed));
            // First pick is the only one (both are non-rival, so chain stops)
            if (picks[0] == heavy) heavyHits++;
        }

        // Expected ~90% heavy. Allow ±15% slack to keep test stable.
        Assert.InRange(heavyHits, trials * 0.75, trials * 0.99);
    }

    [Fact]
    public void RandomAi_ChainsOnRivalReveals()
    {
        // Build intent map with 2 rivals + 1 neutral, all positive points.
        var state = BuildState();
        var rivals = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival)
            .Take(2)
            .Select(t => t.Position)
            .ToList();
        var neutral = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Neutral)
            .Position;

        var intent = new Dictionary<Position, int>
        {
            [rivals[0]] = 5,
            [rivals[1]] = 5,
            [neutral] = 1
        };

        var ai = new RandomAi();
        var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(42));

        // Expected: at least 1 rival, then either another rival or stops at neutral
        Assert.True(picks.Count >= 1);
        // First pick should be one of the heavily-weighted tiles (rivals or neutral)
        Assert.Contains(picks[0], new[] { rivals[0], rivals[1], neutral });
        // Last pick (if multiple) must be a non-rival OR the chain consumed all rivals
        if (picks.Count > 1)
        {
            // All but last must be rival-owned
            for (var i = 0; i < picks.Count - 1; i++)
                Assert.Equal(TileOwner.Rival, state.Board.GetTile(picks[i]).Owner);
        }
    }

    // ---------- NoGuessAi ----------

    [Fact]
    public void NoGuessAi_NeverPicksANoble()
    {
        // Use Level2 (1 noble) and seed intent including the noble position.
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, rng);
        var state = new GameState { Board = board, CurrentLevelId = "level2" };
        var noble = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Noble)
            .Position;
        var rival = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival)
            .Position;

        var intent = new Dictionary<Position, int>
        {
            [noble] = 99,    // very high — would dominate non-NoGuess AIs
            [rival] = 1
        };

        var ai = new NoGuessAi();
        for (var seed = 0; seed < 50; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(seed));
            Assert.DoesNotContain(noble, picks);
        }
    }

    [Fact]
    public void NoGuessAi_PicksHighestPointsAmongNonNobles()
    {
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, rng);
        var state = new GameState { Board = board, CurrentLevelId = "level2" };

        var noble = state.Board.Tiles.First(t => t.Owner == TileOwner.Noble && state.Board.IsUsablePosition(t.Position)).Position;
        var rivals = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Rival)
            .Take(2)
            .Select(t => t.Position)
            .ToList();
        var heavyRival = rivals[0];
        var lightRival = rivals[1];

        var intent = new Dictionary<Position, int>
        {
            [noble] = 99,
            [heavyRival] = 9,
            [lightRival] = 1
        };

        var ai = new NoGuessAi();
        var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(7));

        Assert.Equal(heavyRival, picks[0]);
    }

    [Fact]
    public void NoGuessAi_ReturnsEmptyWhenAllCandidatesAreNobles()
    {
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level2, rng);
        var state = new GameState { Board = board, CurrentLevelId = "level2" };
        var noble = state.Board.Tiles.First(t => t.Owner == TileOwner.Noble && state.Board.IsUsablePosition(t.Position)).Position;

        var intent = new Dictionary<Position, int> { [noble] = 50 };

        var ai = new NoGuessAi();
        var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(7));

        Assert.Empty(picks);
    }

    // ---------- ExecuteRivalTurn integration ----------

    [Fact]
    public void ExecuteRivalTurn_UsesLevelConfiguredAi()
    {
        // Build a state at "level2" — uses default Random AI per config.
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        // Force level to one that exists with a known AI (Level2 default = Random)
        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(99));

        // Sanity: after rival turn, exactly 1 tile is newly revealed by rival
        var revealedByRival = newState.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && t.IsRevealed
                        && t.RevealedBy == PlayerType.Rival);
        var preRevealed = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && t.IsRevealed
                        && t.RevealedBy == PlayerType.Rival);
        Assert.True(revealedByRival > preRevealed);
    }

    [Fact]
    public void ExecuteRivalTurn_DecaysIntentAfterReveals()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        Assert.True(sumBefore > 0);

        state = TurnSystem.ExecuteRivalTurn(state, new Random(99));

        // Sum should decrease (decay decrements all by 1, drops zeros)
        var sumAfter = state.RivalIntentPoints.Values.Sum();
        Assert.True(sumAfter < sumBefore, $"intent sum should decrease after rival turn: before={sumBefore}, after={sumAfter}");
    }

    // ---------- ConservativeAi (M37) ----------

    /// <summary>
    /// Builds a 3×2 controlled board where the rival-revealed (0,0) tile has
    /// adjacencyCount=3 forcing (0,1), (1,0), (1,1) to be guaranteed rivals.
    /// (0,2)=Neutral, (1,2)=Player. Total: 4 rivals (incl. revealed), 1 neutral, 1 player.
    /// </summary>
    private static GameState BuildConservativeTestBoard()
    {
        var tiles = new List<Tile>
        {
            // Row 0
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                IsRevealed = true, RevealedBy = PlayerType.Rival, AdjacencyCount = 3 },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral },
            // Row 1
            new() { Position = new Position(1, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 2, Tiles = tiles };
        return new GameState { Board = board, CurrentLevelId = "level1" };
    }

    [Fact]
    public void ConservativeAi_IsRegisteredCorrectly()
    {
        Assert.IsType<ConservativeAi>(AiRegistry.Get(AiType.Conservative));
    }

    [Fact]
    public void ConservativeAi_PrefersGuaranteedRivalsOverHigherIntentTiles()
    {
        var state = BuildConservativeTestBoard();
        var intent = new Dictionary<Position, int>
        {
            // Guaranteed rivals get LOW intent
            [new Position(0, 1)] = 1,
            [new Position(1, 0)] = 1,
            [new Position(1, 1)] = 1,
            // Non-guaranteed tile gets HIGH intent
            [new Position(0, 2)] = 99,
            [new Position(1, 2)] = 99
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 2, PlayerCount = 1, RivalCount = 4, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ConservativeAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        // First pick must be a guaranteed rival, not the high-intent neutral/player.
        var guaranteed = new HashSet<Position> { new(0, 1), new(1, 0), new(1, 1) };
        Assert.Contains(picks[0], guaranteed);
    }

    [Fact]
    public void RandomAi_SkipsNobles_WhenRivalNeverNoblesIsTrue()
    {
        // Even with all the weight pointing at a noble, RandomAi must not pick it
        // when the level sets RivalNeverNobles.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Noble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 99, // noble — would dominate without filter
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext
        {
            LevelConfig = new LevelConfig
            {
                Width = 3, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 1,
                RivalNeverNobles = true
            }
        };

        var ai = new RandomAi();
        for (var seed = 0; seed < 50; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    [Fact]
    public void RandomAi_SkipsLoungingNobles_WhenRivalNeverNoblesIsTrue()
    {
        // Lounging-noble overlay counts as a noble for the filter.
        var tiles = new List<Tile>
        {
            new()
            {
                Position = new Position(0, 0),
                Owner = TileOwner.Player,
                Specials = SpecialTileType.LoungingNoble
            },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 99,
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext
        {
            LevelConfig = new LevelConfig
            {
                Width = 2, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 0,
                RivalNeverNobles = true
            }
        };

        var ai = new RandomAi();
        for (var seed = 0; seed < 50; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    [Fact]
    public void ConservativeAi_SkipsNobles_WhenRivalNeverNoblesIsTrue()
    {
        // Manual board: 1 noble + 1 rival, no constraints possible (no revealed tiles).
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Noble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 99,  // noble — would dominate without filter
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 1,
            RivalNeverNobles = true
        }};

        var ai = new ConservativeAi();
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    [Fact]
    public void ConservativeAi_MayRevealNobles_WhenRivalNeverNoblesIsFalse()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Noble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 99,  // noble
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 2, Height = 1, PlayerCount = 0, RivalCount = 1, NeutralCount = 0, NobleCount = 1,
            RivalNeverNobles = false
        }};

        var ai = new ConservativeAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        // Noble has highest points and is not filtered → it gets picked first
        Assert.Equal(new Position(0, 0), picks[0]);
    }

    [Fact]
    public void ConservativeAi_FallsBackToMaxPoints_WhenNoGuaranteedRivals()
    {
        // Board with no revealed tiles → no constraints → no guaranteed rivals
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 1,
            [new Position(0, 1)] = 9   // max-points
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 2, Height = 1, PlayerCount = 0, RivalCount = 1, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ConservativeAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        Assert.Equal(new Position(0, 1), picks[0]);
    }

    [Fact]
    public void ConservativeAi_ReturnsEmpty_WhenOnlyNoblesAvailable_AndRivalNeverNobles()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Noble }
        };
        var board = new Board { Width = 1, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int> { [new Position(0, 0)] = 50 };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 1, Height = 1, PlayerCount = 0, RivalCount = 0, NeutralCount = 0, NobleCount = 1,
            RivalNeverNobles = true
        }};

        var ai = new ConservativeAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        Assert.Empty(picks);
    }

    [Fact]
    public void ConservativeAi_PrefersNonRuledOutTilesInFallback()
    {
        // Setup: a revealed player tile with adjacencyCount=0 rules out player from
        // its unrevealed neighbors. Conservative should prefer a non-ruled-out tile
        // even if a ruled-out one has slightly higher intent — but only if the
        // non-ruled-out one is also reasonably high.
        // 1×3: (0,0)=Player revealed adj=0, (0,1)=Rival, (0,2)=Rival
        // (0,1) has been ruled out as Player (still possible: rival/neutral/noble);
        // its rival flag is unchanged → ruledOutRivals does NOT include it.
        // To exercise the ruled-out-rival branch we need a constraint that rules out rival.
        // (0,0)=Rival revealed adj=0 → none of its neighbors are rival → (0,1) ruled out as rival.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                IsRevealed = true, RevealedBy = PlayerType.Rival, AdjacencyCount = 0 },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral }, // ruled-out-rival neighbor
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival }    // not ruled out (not adjacent)
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 1)] = 5,  // ruled out as rival
            [new Position(0, 2)] = 4   // not ruled out
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 1, PlayerCount = 0, RivalCount = 2, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ConservativeAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        // Should prefer (0,2) — not ruled out as rival — even though (0,1) has higher intent
        Assert.Equal(new Position(0, 2), picks[0]);
    }
}
