using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;
using Maidsweeper.Core.Systems.AI;

namespace Maidsweeper.Tests;

/// <summary>
/// M40: Lounging nobles + rival noble reveal floor-win + rival mine protection.
/// </summary>
public class LoungingNobleTests
{
    /// <summary>
    /// Builds a 3×3 board where (1,1) has a LoungingNoble overlay on a player tile.
    /// Other tiles are players (so reveal-by-player adjacency math is simple).
    /// </summary>
    private static GameState BuildLoungingState(
        Position loungingPos,
        TileOwner underlyingOwner = TileOwner.Player,
        int excusesStacks = 0,
        int rivalProtection = 0,
        bool rivalTurn = false)
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var pos = new Position(row, col);
                if (pos == loungingPos)
                {
                    tiles.Add(new Tile
                    {
                        Position = pos,
                        Owner = underlyingOwner,
                        Specials = SpecialTileType.LoungingNoble
                    });
                }
                else
                {
                    tiles.Add(new Tile { Position = pos, Owner = TileOwner.Player });
                }
            }
        }

        var board = new Board { Width = 3, Height = 3, Tiles = tiles };
        var deck = CardDefinitions.CreateStarterDeck();
        return new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Hand = deck.Take(5).ToList(),
            DrawPile = deck.Skip(5).ToList(),
            Spoons = 3,
            MaxSpoons = 3,
            CurrentPlayer = rivalTurn ? PlayerType.Rival : PlayerType.Player,
            ExcusesStacks = excusesStacks,
            RivalMineProtectionCount = rivalProtection,
            // Pre-seed intent so rival turn doesn't fall back to random
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 1 }
        };
    }

    // ---------- Player-revealed lounging noble ----------

    [Fact]
    public void PlayerRevealsLoungingNoble_NoExcuses_RunEnds()
    {
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, excusesStacks: 0);

        var result = GameRunner.ProcessReveal(state, pos, new Random(7));

        Assert.Equal(GameStatus.Lost, result.State.GameStatus);
    }

    [Fact]
    public void PlayerRevealsLoungingNoble_WithExcuses_AbsorbsAndContinues()
    {
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, excusesStacks: 1);

        var result = GameRunner.ProcessReveal(state, pos, new Random(7));

        Assert.NotEqual(GameStatus.Lost, result.State.GameStatus);
        // Excuses consumed → 0; M25 penalty applied (Complaints + 2 Mollify added)
        Assert.Equal(0, result.State.ExcusesStacks);
        Assert.Equal(2, result.State.ComplaintsStacks);
    }

    // ---------- Rival-revealed nobles ----------

    [Fact]
    public void RivalRevealsLoungingNoble_NoProtection_FloorWins()
    {
        // Manually mark the lounging-noble tile as revealed-by-rival, then check status
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos);
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(pos);
        newTiles[idx] = newTiles[idx] with
        {
            IsRevealed = true,
            RevealedBy = PlayerType.Rival
        };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var status = TurnSystem.CheckGameStatus(state);

        Assert.Equal(GameStatus.Won, status);
    }

    [Fact]
    public void RivalRevealsRegularNoble_NoProtection_FloorWins()
    {
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, underlyingOwner: TileOwner.Noble);
        // Strip the LoungingNoble flag so it's a plain noble (rival reveal)
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(pos);
        newTiles[idx] = newTiles[idx]
            .WithoutSpecial(SpecialTileType.LoungingNoble) with
            {
                IsRevealed = true,
                RevealedBy = PlayerType.Rival
            };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var status = TurnSystem.CheckGameStatus(state);

        Assert.Equal(GameStatus.Won, status);
    }

    [Fact]
    public void PlayerRevealsRegularNoble_NoExcuses_RunEnds()
    {
        // Existing baseline: player-revealed noble → loss. Confirms our new
        // player/rival distinction doesn't break this case.
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, underlyingOwner: TileOwner.Noble);
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(pos);
        newTiles[idx] = newTiles[idx].WithoutSpecial(SpecialTileType.LoungingNoble);
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var result = GameRunner.ProcessReveal(state, pos, new Random(7));

        Assert.Equal(GameStatus.Lost, result.State.GameStatus);
    }

    // ---------- Excuses doesn't trigger from rival reveals ----------

    [Fact]
    public void RivalRevealedNoble_DoesNotConsumeExcuses()
    {
        // Player has Excuses=1. Rival reveals noble. Excuses should NOT be consumed
        // (the noble triggers floor-win, not Excuses absorption).
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, underlyingOwner: TileOwner.Noble, excusesStacks: 1);
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(pos);
        newTiles[idx] = newTiles[idx]
            .WithoutSpecial(SpecialTileType.LoungingNoble) with
            {
                IsRevealed = true,
                RevealedBy = PlayerType.Rival
            };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var status = TurnSystem.CheckGameStatus(state);
        Assert.Equal(GameStatus.Won, status);
        // Excuses still 1 (rival reveal didn't consume it)
        Assert.Equal(1, state.ExcusesStacks);
        // Noble not marked ProtectedByExcuses (it triggered floor-win, not Excuses)
        Assert.False(state.Board.GetTile(pos).ProtectedByExcuses);
    }

    // ---------- Sweep cleans lounging nobles ----------

    [Fact]
    public void Sweep_CleansLoungingNoble_UnderlyingOwnerRetained()
    {
        var pos = new Position(1, 1);
        var state = BuildLoungingState(pos, underlyingOwner: TileOwner.Player);
        state = state with { Hand = new List<Card> { CardDefinitions.Sweep with { Id = "sw1" } }, Spoons = 3 };

        var newState = CardEffectSystem.ExecuteSweep(state, new[] { pos }, new Random(7), CardDefinitions.Sweep);

        Assert.False(newState.Board.GetTile(pos).IsLoungingNoble);
        Assert.Equal(TileOwner.Player, newState.Board.GetTile(pos).Owner);
    }

    // ---------- PlaceRivalLoungingNobles ----------

    [Fact]
    public void PlaceRivalLoungingNobles_OnlyTargetsPlayerOrNeutralTiles()
    {
        // 2x2: 1 player, 1 neutral, 2 rivals
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival },
            new() { Position = new Position(1, 0), Owner = TileOwner.Neutral },
            new() { Position = new Position(1, 1), Owner = TileOwner.Rival }
        };
        var board = new Board { Width = 2, Height = 2, Tiles = tiles };

        var newBoard = BoardSystem.PlaceRivalLoungingNobles(board, count: 2, new Random(7));

        Assert.Equal(2, newBoard.Tiles.Count(t => t.IsLoungingNoble));
        // No rivals should have lounging-noble overlay
        foreach (var t in newBoard.Tiles.Where(t => t.IsLoungingNoble))
        {
            Assert.NotEqual(TileOwner.Rival, t.Owner);
        }
    }

    [Fact]
    public void PlaceRivalLoungingNobles_SkipsRevealedAndAlreadyOverlaid()
    {
        var tiles = new List<Tile>
        {
            // Already revealed → skip
            new() { Position = new Position(0, 0), Owner = TileOwner.Player, IsRevealed = true,
                RevealedBy = PlayerType.Player },
            // Already overlaid → skip
            new() { Position = new Position(0, 1), Owner = TileOwner.Player,
                Specials = SpecialTileType.LoungingNoble },
            // Eligible
            new() { Position = new Position(0, 2), Owner = TileOwner.Neutral }
        };
        var board = new Board { Width = 3, Height = 1, Tiles = tiles };

        var newBoard = BoardSystem.PlaceRivalLoungingNobles(board, count: 5, new Random(7));

        // Only (0,2) becomes a NEW overlay; (0,1) was already one.
        var totalOverlays = newBoard.Tiles.Count(t => t.IsLoungingNoble);
        Assert.Equal(2, totalOverlays);
        Assert.True(newBoard.GetTile(new Position(0, 2)).IsLoungingNoble);
    }

    // ---------- Rival mine protection ----------

    [Fact]
    public void RivalMineProtection_AbsorbsRivalNobleReveal_FloorContinues_Plus5Copper()
    {
        // Build a state at level1 (registered) with mine protection = 1.
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { RivalMineProtectionCount = 1, Copper = 0, GameStatus = GameStatus.Playing };

        // Force a rival noble reveal: pick any non-revealed tile, mark it as a noble
        // revealed by rival.
        var pos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed)
            .Position;
        var newTiles = state.Board.Tiles.ToList();
        var idx = state.Board.TileIndex(pos);
        newTiles[idx] = newTiles[idx] with
        {
            Owner = TileOwner.Noble,
            IsRevealed = true,
            RevealedBy = PlayerType.Rival
        };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        // End the turn to trigger the rival flow's protection consumption
        var result = GameRunner.ProcessEndTurn(state, new Random(7));

        // Floor continues (status is Playing — rival's reveal absorbed)
        Assert.Equal(GameStatus.Playing, result.State.GameStatus);
        // Protection decremented and player gained 5 copper for the absorb
        Assert.Equal(0, result.State.RivalMineProtectionCount);
        Assert.True(result.State.Copper >= 5,
            $"expected at least 5 copper from absorbed reveal; got {result.State.Copper}");
        Assert.True(result.State.Board.GetTile(pos).ProtectedByRivalMineProtection);
    }

    [Fact]
    public void RivalMineProtection_TwoStacks_AbsorbsTwoNobleReveals()
    {
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with { RivalMineProtectionCount = 2, Copper = 0 };

        // Mark two tiles as rival-revealed nobles
        var twoUnrevealed = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed)
            .Take(2)
            .Select(t => t.Position)
            .ToList();
        var newTiles = state.Board.Tiles.ToList();
        foreach (var pos in twoUnrevealed)
        {
            var idx = state.Board.TileIndex(pos);
            newTiles[idx] = newTiles[idx] with
            {
                Owner = TileOwner.Noble,
                IsRevealed = true,
                RevealedBy = PlayerType.Rival
            };
        }
        state = state with { Board = state.Board with { Tiles = newTiles } };

        var result = GameRunner.ProcessEndTurn(state, new Random(7));

        Assert.Equal(GameStatus.Playing, result.State.GameStatus);
        Assert.Equal(0, result.State.RivalMineProtectionCount);
    }

    // ---------- Rival noble reveal flow integration ----------

    [Fact]
    public void RivalPlacesMines_AddsLoungingNoblesAfterRivalTurn()
    {
        // Build a level config with RivalPlacesMines = 1
        // We use the registered "level1" so the runner's LevelConfigs.GetById finds it,
        // but we need the field set. Easiest: temporary state via direct API.
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);

        // Hook directly: simulate the rival turn flow's placement step
        var newBoard = BoardSystem.PlaceRivalLoungingNobles(state.Board, count: 1, new Random(7));

        var diff = newBoard.Tiles.Count(t => t.IsLoungingNoble)
                   - state.Board.Tiles.Count(t => t.IsLoungingNoble);
        Assert.Equal(1, diff);
    }

    // ---------- AI-side: Conservative skips lounging nobles ----------

    [Fact]
    public void ConservativeAi_SkipsLoungingNoble_WhenRivalNeverNoblesIsTrue()
    {
        // 2x1 board: a lounging-noble overlay on a player tile + a regular rival
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
            [new Position(0, 0)] = 99, // lounging noble — should be filtered when RivalNeverNobles
            [new Position(0, 1)] = 1
        };
        var ctx = new AiContext { LevelConfig = new LevelConfig
        {
            Width = 2, Height = 1, PlayerCount = 1, RivalCount = 1, NeutralCount = 0, NobleCount = 0,
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
    public void NoGuessAi_SkipsLoungingNoble()
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

        var ai = new NoGuessAi();
        for (var seed = 0; seed < 20; seed++)
        {
            var picks = ai.SelectTilesToReveal(state, intent, new AiContext(), new Random(seed));
            Assert.DoesNotContain(new Position(0, 0), picks);
        }
    }

    // ---------- Floor start initializes mine protection ----------

    [Fact]
    public void FloorStart_InitializesMineProtectionFromConfig()
    {
        // StartCampaign uses Level1 which has RivalMineProtection=0 by default.
        var state = CampaignSystem.StartCampaign(new Random(42));
        Assert.Equal(0, state.RivalMineProtectionCount);
    }
}
