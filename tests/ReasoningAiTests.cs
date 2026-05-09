using System.Diagnostics;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// M42: ReasoningAi (constraint propagation + Monte Carlo + intent-weighted priority).
/// </summary>
public class ReasoningAiTests
{
    // ---------- Registry ----------

    [Fact]
    public void ReasoningAi_IsRegisteredCorrectly()
    {
        Assert.IsType<ReasoningAi>(AiRegistry.Get(AiType.Reasoning));
    }

    // ---------- Constraint propagation: prefer guaranteed rivals ----------

    [Fact]
    public void Reasoning_PrefersGuaranteedRivals()
    {
        // 3×2 board: (0,0) revealed rival adj=3 forces (0,1), (1,0), (1,1) to be guaranteed rivals.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                IsRevealed = true, RevealedBy = PlayerType.Rival, AdjacencyCount = 3 },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral },
            new() { Position = new Position(1, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 2, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 1)] = 1,
            [new Position(1, 0)] = 1,
            [new Position(1, 1)] = 1,
            [new Position(0, 2)] = 99,
            [new Position(1, 2)] = 99
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 2, PlayerCount = 1, RivalCount = 4, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ReasoningAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        // First pick should be one of the 3 guaranteed rivals
        var guaranteed = new HashSet<Position> { new(0, 1), new(1, 0), new(1, 1) };
        Assert.Contains(picks[0], guaranteed);
    }

    // ---------- Forbidden noble filtering ----------

    [Fact]
    public void Reasoning_NeverPicksNoble_WhenRivalNeverNoblesIsTrue()
    {
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
            [new Position(0, 0)] = 99,
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 1,
            RivalNeverNobles = true
        }};

        var ai = new ReasoningAi();
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    [Fact]
    public void Reasoning_FiltersLoungingNobles_WhenRivalNeverNoblesIsTrue()
    {
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player,
                Specials = SpecialTileType.LoungingNoble },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 99,
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 2, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 0,
            RivalNeverNobles = true
        }};

        var ai = new ReasoningAi();
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    // ---------- Reachability ----------

    [Fact]
    public void Reasoning_MayPickUnreachableInnerTile()
    {
        // The rival AI is allowed to "cheat" through portals — reachability only
        // gates the player. With high intent on an unreachable inner tile, Reasoning
        // should still pick it (the inner tile is the highest-priority candidate).
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral,
                Specials = SpecialTileType.Sanctum },
            new() { Position = new Position(0, 2), Owner = TileOwner.Rival,
                Specials = SpecialTileType.InnerTile }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 1,
            [new Position(0, 2)] = 99 // unreachable inner — still a valid AI target
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 1, PlayerCount = 0, RivalCount = 2, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ReasoningAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        // First pick is the high-intent inner tile (the AI doesn't respect player reachability)
        Assert.Equal(new Position(0, 2), picks[0]);
    }

    // ---------- MC sampler basic correctness ----------

    [Fact]
    public void MonteCarlo_AssignsForcedRivalConsistently()
    {
        // Board with one revealed rival forcing one neighbor to be a guaranteed rival.
        // After analysis pre-assigns the guaranteed rival, MC should never assign that
        // tile to anything else.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                IsRevealed = true, RevealedBy = PlayerType.Rival, AdjacencyCount = 1 },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var analysis = ExclusionLogic.Analyze(state);
        Assert.Contains(new Position(0, 1), analysis.GuaranteedRivals);

        var mc = MonteCarloSampler.Run(state, analysis, new Random(42), iterations: 20);
        var counts = mc.OwnerCounts[new Position(0, 1)];

        // (0,1) is guaranteed rival → MC always assigns it as rival
        Assert.Equal(20, counts.Rival);
        Assert.Equal(0, counts.Player);
        Assert.Equal(0, counts.Neutral);
        Assert.Equal(0, counts.Noble);
    }

    [Fact]
    public void MonteCarlo_RespectsCountConstraints()
    {
        // Board with 3 unrevealed tiles: 1 rival, 1 neutral, 1 player — random assignment
        // must always produce that distribution across iterations.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 1), Owner = TileOwner.Neutral },
            new() { Position = new Position(0, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var analysis = ExclusionLogic.Analyze(state);
        var mc = MonteCarloSampler.Run(state, analysis, new Random(42), iterations: 50);

        // For each iteration: total Rival across all tiles = 1, etc.
        var totalRival = mc.OwnerCounts.Values.Sum(c => c.Rival);
        var totalNeutral = mc.OwnerCounts.Values.Sum(c => c.Neutral);
        var totalPlayer = mc.OwnerCounts.Values.Sum(c => c.Player);

        Assert.Equal(50, totalRival);
        Assert.Equal(50, totalNeutral);
        Assert.Equal(50, totalPlayer);
    }

    [Fact]
    public void MonteCarlo_GuaranteedRivalsAreAlwaysRival()
    {
        // 3×2 board where (0,0) revealed rival adj=3 → (0,1), (1,0), (1,1) are guaranteed.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival,
                IsRevealed = true, RevealedBy = PlayerType.Rival, AdjacencyCount = 3 },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral },
            new() { Position = new Position(1, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 2), Owner = TileOwner.Player }
        };
        var board = new Board { Width = 3, Height = 2, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var analysis = ExclusionLogic.Analyze(state);
        var mc = MonteCarloSampler.Run(state, analysis, new Random(42), iterations: 30);

        // Each of the 3 guaranteed-rival positions is rival in all 30 iterations
        foreach (var pos in new[] { new Position(0, 1), new Position(1, 0), new Position(1, 1) })
        {
            Assert.Equal(30, mc.OwnerCounts[pos].Rival);
        }
    }

    // ---------- Determinism with seeded RNG ----------

    [Fact]
    public void Reasoning_DeterministicWithSameSeed()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var intent = state.RivalIntentPoints;
        var ctx = new AiContext { LevelConfig = LevelConfigs.Level1 };

        var ai = new ReasoningAi();
        var picks1 = ai.SelectTilesToReveal(state, intent, ctx, new Random(99));
        var picks2 = ai.SelectTilesToReveal(state, intent, ctx, new Random(99));

        Assert.Equal(picks1, picks2);
    }

    // ---------- Performance smoke test ----------

    [Fact]
    public void Reasoning_PerformanceUnder500ms_OnLargeBoard()
    {
        // Use the largest existing level config (Level8: 8×7) as a stand-in for the
        // 10×10 boards in the alpha. If MC is too slow on this, alpha-spec boards will too.
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        // Walk to level 8 (skip all rewards)
        var seed = 100;
        for (var i = 0; i < 7; i++)
        {
            state = state with { GameStatus = GameStatus.Won };
            state = CampaignSystem.CompleteFloor(state, new Random(seed++));
            while (state.GamePhase != GamePhase.Playing && state.GamePhase != GamePhase.CampaignVictory)
            {
                state = state.GamePhase switch
                {
                    GamePhase.CardReward => CampaignSystem.SkipCardReward(state, new Random(seed++)),
                    GamePhase.UpgradeReward => CampaignSystem.SkipUpgrade(state, new Random(seed++)),
                    GamePhase.EquipmentReward => CampaignSystem.SkipEquipment(state, new Random(seed++)),
                    GamePhase.Shop => CampaignSystem.LeaveShop(state, new Random(seed++)),
                    _ => state
                };
            }
        }
        Assert.Equal("level8", state.CurrentLevelId);

        var ai = new ReasoningAi();
        var ctx = new AiContext { LevelConfig = LevelConfigs.Level8 };

        var sw = Stopwatch.StartNew();
        var picks = ai.SelectTilesToReveal(state, state.RivalIntentPoints, ctx, new Random(7));
        sw.Stop();

        Assert.True(picks.Count >= 1);
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"ReasoningAi took {sw.ElapsedMilliseconds}ms on Level 8 — too slow");
    }

    // ---------- Empty board / nothing eligible ----------

    [Fact]
    public void Reasoning_ReturnsEmpty_WhenAllNoblesAndRivalNeverNoblesTrue()
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

        var ai = new ReasoningAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        Assert.Empty(picks);
    }

    // ---------- Chain on rival reveals ----------

    [Fact]
    public void Reasoning_ChainsOnRivalReveals()
    {
        // 1×3 board: 2 rivals + 1 neutral. Reasoning should pick rivals first and chain
        // (revealing each rival → continue → eventually hit neutral and stop).
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };
        var state = new GameState { Board = board, CurrentLevelId = "test" };

        var intent = new Dictionary<Position, int>
        {
            [new Position(0, 0)] = 5,
            [new Position(0, 1)] = 5,
            [new Position(0, 2)] = 5
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 3, Height = 1, PlayerCount = 0, RivalCount = 2, NeutralCount = 1, NobleCount = 0
        }};

        var ai = new ReasoningAi();
        var picks = ai.SelectTilesToReveal(state, intent, ctx, new Random(7));

        Assert.True(picks.Count >= 1);
        // All but the last pick must be rival (chain stops on first non-rival)
        for (var i = 0; i < picks.Count - 1; i++)
        {
            Assert.Equal(TileOwner.Rival, state.Board.GetTile(picks[i]).Owner);
        }
    }
}
