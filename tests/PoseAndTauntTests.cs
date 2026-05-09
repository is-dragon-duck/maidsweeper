using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M44: Pose (random courtier spawn on player tile) + Taunt (tag-N tiles, end rival
/// turn early on N-1 reveals).
/// </summary>
public class PoseAndTauntTests
{
    private static GameState BuildBoard(IReadOnlyList<TileOwner> owners, int width, int height)
    {
        var tiles = new List<Tile>();
        var idx = 0;
        for (var row = 0; row < height; row++)
        for (var col = 0; col < width; col++)
            tiles.Add(new Tile { Position = new Position(row, col), Owner = owners[idx++] });

        return new GameState
        {
            Board = new Board { Width = width, Height = height, Tiles = tiles },
            CurrentLevelId = "level1",
            Hand = new List<Card>(),
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };
    }

    // ---------- Pose ----------

    [Fact]
    public void Pose_SpawnsCourtierOnUnrevealedPlayerTile()
    {
        // Build a 2x2 with one Player and three Rivals — Pose must spawn on the player.
        var state = BuildBoard(
            new[] { TileOwner.Player, TileOwner.Rival, TileOwner.Rival, TileOwner.Rival },
            width: 2, height: 2);
        var pose = CardDefinitions.Pose with { Id = "p1" };

        var newState = CardEffectSystem.ExecutePose(state, pose, new Random(7));

        Assert.True(newState.Board.GetTile(new Position(0, 0)).IsCourtier);
        // Other tiles unchanged
        Assert.False(newState.Board.GetTile(new Position(0, 1)).IsCourtier);
    }

    [Fact]
    public void Pose_UnderlyingPlayerOwnerRetained()
    {
        var state = BuildBoard(
            new[] { TileOwner.Player, TileOwner.Rival },
            width: 2, height: 1);
        var newState = CardEffectSystem.ExecutePose(state, CardDefinitions.Pose, new Random(7));

        var tile = newState.Board.GetTile(new Position(0, 0));
        Assert.True(tile.IsCourtier);
        Assert.Equal(TileOwner.Player, tile.Owner);
        Assert.False(tile.IsRevealed);
    }

    [Fact]
    public void Pose_AssignsCourtierMoveTarget()
    {
        var state = BuildBoard(
            new[] { TileOwner.Player, TileOwner.Player },
            width: 2, height: 1);
        var newState = CardEffectSystem.ExecutePose(state, CardDefinitions.Pose, new Random(7));

        var courtier = newState.Board.Tiles.First(t => t.IsCourtier);
        Assert.NotNull(courtier.CourtierMoveTarget);
    }

    [Fact]
    public void Pose_CourtierMovesWhenInteractedWith()
    {
        var state = BuildBoard(
            new[] { TileOwner.Player, TileOwner.Player, TileOwner.Player },
            width: 3, height: 1);
        state = CardEffectSystem.ExecutePose(state, CardDefinitions.Pose, new Random(7));

        var courtier = state.Board.Tiles.First(t => t.IsCourtier);
        var origin = courtier.Position;
        var target = courtier.CourtierMoveTarget!.Value;

        // Player clicks the courtier — courtier moves to its target
        var result = GameRunner.ProcessReveal(state, origin, new Random(99));

        Assert.False(result.State.Board.GetTile(origin).IsCourtier);
        Assert.True(result.State.Board.GetTile(target).IsCourtier);
    }

    [Fact]
    public void Pose_SkipsTilesAlreadyHavingCourtier()
    {
        // Only player tile already has a courtier → Pose should be a no-op.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player,
                Specials = SpecialTileType.Courtier,
                CourtierMoveTarget = new Position(0, 1) },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1"
        };
        var courtierCountBefore = state.Board.Tiles.Count(t => t.IsCourtier);

        var newState = CardEffectSystem.ExecutePose(state, CardDefinitions.Pose, new Random(7));

        Assert.Equal(courtierCountBefore, newState.Board.Tiles.Count(t => t.IsCourtier));
    }

    // ---------- Taunt ----------

    [Fact]
    public void Taunt_CreatesActiveEffectWithCorrectThreshold()
    {
        var state = BuildBoard(
            Enumerable.Range(0, 9).Select(_ => TileOwner.Rival).ToList(),
            width: 3, height: 3);
        var targets = new[]
        {
            new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(1, 0)
        };

        var newState = CardEffectSystem.ExecuteTaunt(state, targets, CardDefinitions.Taunt);

        Assert.Single(newState.ActiveTaunts);
        var taunt = newState.ActiveTaunts[0];
        Assert.Equal(4, taunt.Positions.Count);
        Assert.Equal(3, taunt.RequiredReveals); // N-1
    }

    [Fact]
    public void Taunt_RivalRevealingThresholdEndsTurnEarly()
    {
        // Build board: 4 rivals + 5 players. Tag the 4 rivals; required = 3 reveals.
        // Seed rival intent so AI chooses rivals first; rival should chain through
        // them but stop after the 3rd taunt-reveal (early end).
        var owners = new List<TileOwner>
        {
            TileOwner.Rival, TileOwner.Rival, TileOwner.Rival, TileOwner.Rival, TileOwner.Player,
            TileOwner.Player, TileOwner.Player, TileOwner.Player, TileOwner.Player
        };
        var state = BuildBoard(owners, width: 3, height: 3);

        var rivalPositions = state.Board.Tiles
            .Where(t => t.Owner == TileOwner.Rival)
            .Select(t => t.Position)
            .ToList();
        var taunt = new TauntEffect
        {
            Positions = new HashSet<Position>(rivalPositions),
            RequiredReveals = 3
        };
        state = state with
        {
            ActiveTaunts = new List<TauntEffect> { taunt },
            // Seed strong intent on all 4 rivals so the AI definitely picks them
            RivalIntentPoints = rivalPositions.ToDictionary(p => p, _ => 5)
        };

        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(42));

        // Only 3 rivals revealed (the 4th was skipped by Taunt's early-end)
        var revealedRivals = newState.Board.Tiles
            .Count(t => t.IsRevealed && t.Owner == TileOwner.Rival && t.RevealedBy == PlayerType.Rival);
        Assert.Equal(3, revealedRivals);

        // Triggered Taunt was consumed
        Assert.Empty(newState.ActiveTaunts);
    }

    [Fact]
    public void Taunt_FewerRevealsThanThreshold_DoesNotEndTurnEarly()
    {
        // Threshold=3 but rival only has access to reveal 2 tagged tiles before
        // hitting a non-rival tile (which ends the chain naturally).
        var owners = new List<TileOwner>
        {
            TileOwner.Rival, TileOwner.Rival, TileOwner.Player, TileOwner.Rival
        };
        var state = BuildBoard(owners, width: 4, height: 1);

        var taunt = new TauntEffect
        {
            // Tag all 4 — but the rival only reveals 2 (chain breaks at the player)
            Positions = new HashSet<Position> { new(0, 0), new(0, 1), new(0, 2), new(0, 3) },
            RequiredReveals = 3
        };
        state = state with
        {
            ActiveTaunts = new List<TauntEffect> { taunt },
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 9,
                [new Position(0, 1)] = 8,
                [new Position(0, 2)] = 7,
                [new Position(0, 3)] = 6
            }
        };

        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(42));

        // Taunt NOT triggered — fewer than 3 tagged tiles revealed
        Assert.Single(newState.ActiveTaunts);
    }

    [Fact]
    public void Taunt_OnlyConsumesTriggeredTaunts_LeavesUntriggeredOnesActive()
    {
        // Two Taunts. One triggers (threshold=1 with first tagged reveal), other doesn't.
        var owners = new List<TileOwner>
        {
            TileOwner.Rival, TileOwner.Rival, TileOwner.Player
        };
        var state = BuildBoard(owners, width: 3, height: 1);

        var triggeredTaunt = new TauntEffect
        {
            Positions = new HashSet<Position> { new(0, 0) },
            RequiredReveals = 1 // Triggers immediately on first reveal of (0,0)
        };
        var unTriggeredTaunt = new TauntEffect
        {
            Positions = new HashSet<Position> { new(0, 2) }, // a player tile that won't be rival-revealed
            RequiredReveals = 1
        };
        state = state with
        {
            ActiveTaunts = new List<TauntEffect> { triggeredTaunt, unTriggeredTaunt },
            RivalIntentPoints = new Dictionary<Position, int>
            {
                [new Position(0, 0)] = 9
            }
        };

        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(42));

        // The first taunt triggered and was consumed; the second remains
        Assert.Single(newState.ActiveTaunts);
        Assert.Same(unTriggeredTaunt, newState.ActiveTaunts[0]);
    }

    [Fact]
    public void Taunt_PerFloorReset()
    {
        // Set a Taunt active, transition floors, expect ActiveTaunts cleared.
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        var taunt = new TauntEffect
        {
            Positions = new HashSet<Position> { new(0, 0) },
            RequiredReveals = 1
        };
        state = state with
        {
            ActiveTaunts = new List<TauntEffect> { taunt },
            GameStatus = GameStatus.Won
        };

        // Advance to next floor (Level 1 → 2 with card reward)
        state = CampaignSystem.CompleteFloor(state, new Random(43));
        state = CampaignSystem.SkipCardReward(state, new Random(44));

        Assert.Equal("level2", state.CurrentLevelId);
        Assert.Empty(state.ActiveTaunts);
    }

    // ---------- Reward pool ----------

    [Fact]
    public void RewardPool_IncludesPoseAndTaunt()
    {
        var pool = CardDefinitions.CreateRewardPool();
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Pose);
        Assert.Contains(pool, c => c.EffectType == CardEffectType.Taunt);
    }
}
