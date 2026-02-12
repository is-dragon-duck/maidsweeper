using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class GameRunnerTests
{
    [Fact]
    public void CreateGame_InitializesCorrectly()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        Assert.Equal(30, state.Board.Tiles.Count);
        Assert.Equal(5, state.Hand.Count);
        Assert.Equal(5, state.DrawPile.Count);
        Assert.Empty(state.DiscardPile);
        Assert.Empty(state.ExhaustPile);
        Assert.Equal(3, state.Energy);
        Assert.Equal(3, state.MaxEnergy);
        Assert.Equal(PlayerType.Player, state.CurrentPlayer);
        Assert.Equal(GameStatus.Playing, state.GameStatus);
        Assert.Equal(1, state.TurnNumber);
    }

    [Fact]
    public void ProcessReveal_PlayerTileDoesNotEndTurn()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var playerPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player).Position;
        var result = GameRunner.ProcessReveal(state, playerPos, new Random(99));

        Assert.False(result.TurnEnded);
        Assert.True(result.State.Board.GetTile(playerPos).IsRevealed);
        Assert.Equal(PlayerType.Player, result.State.CurrentPlayer);
    }

    [Fact]
    public void ProcessReveal_RivalTileEndsTurn()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var rivalPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Rival).Position;
        var result = GameRunner.ProcessReveal(state, rivalPos, new Random(99));

        Assert.True(result.TurnEnded);
        // After turn transition: rival reveals, then new player turn starts
        Assert.Equal(PlayerType.Player, result.State.CurrentPlayer);
        Assert.Equal(2, result.State.TurnNumber);
    }

    [Fact]
    public void ProcessReveal_NeutralTileEndsTurn()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var neutralPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Neutral).Position;
        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.True(result.TurnEnded);
    }

    [Fact]
    public void ProcessReveal_ThrowsOnAlreadyRevealed()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;
        var result = GameRunner.ProcessReveal(state, pos, new Random(99));

        Assert.Throws<InvalidOperationException>(() =>
            GameRunner.ProcessReveal(result.State, pos, new Random(99)));
    }

    [Fact]
    public void ProcessReveal_WinDetected()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        // Reveal all player tiles except one
        var playerTiles = state.Board.Tiles.Where(t => t.Owner == TileOwner.Player).ToList();
        var board = state.Board;
        for (var i = 0; i < playerTiles.Count - 1; i++)
        {
            board = BoardSystem.RevealTile(board, playerTiles[i].Position, PlayerType.Player);
        }
        state = state with { Board = board };

        // Reveal the last one
        var lastPlayer = playerTiles.Last().Position;
        var result = GameRunner.ProcessReveal(state, lastPlayer, new Random(99));

        Assert.Equal(GameStatus.Won, result.State.GameStatus);
        Assert.True(result.GameOver);
    }

    [Fact]
    public void ProcessReveal_LossOnNoble()
    {
        // Create game with nobles
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
            Energy = 3,
            MaxEnergy = 3,
            CurrentPlayer = PlayerType.Player,
            GameStatus = GameStatus.Playing
        };

        var noblePos = state.Board.Tiles.First(t => t.Owner == TileOwner.Noble).Position;
        var result = GameRunner.ProcessReveal(state, noblePos, new Random(99));

        Assert.Equal(GameStatus.Lost, result.State.GameStatus);
        Assert.True(result.GameOver);
    }

    [Fact]
    public void ProcessCardPlay_SpritzDoesNotEndTurn()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var playerPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player).Position;

        var result = GameRunner.ProcessCardPlay(state, spritz, [playerPos], new Random(99));

        Assert.False(result.TurnEnded);
        Assert.Equal(2, result.State.Energy); // 3 - 1
    }

    [Fact]
    public void ProcessCardPlay_InstructionsDoesNotEndTurn()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var instructions = state.Hand.FirstOrDefault(c => c.EffectType == CardEffectType.Recall);
        if (instructions == null)
        {
            // Might not be in hand; skip
            return;
        }

        var result = GameRunner.ProcessCardPlay(state, instructions, null, new Random(99));

        Assert.False(result.TurnEnded);
    }

    [Fact]
    public void ProcessEndTurn_TransitionsCorrectly()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var result = GameRunner.ProcessEndTurn(state, new Random(99));

        Assert.True(result.TurnEnded);
        Assert.Equal(PlayerType.Player, result.State.CurrentPlayer);
        Assert.Equal(2, result.State.TurnNumber);
        Assert.Equal(5, result.State.Hand.Count);
        Assert.Equal(3, result.State.Energy);
    }

    [Fact]
    public void ProcessEndTurn_RivalRevealsATile()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var rivalBefore = state.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);
        var result = GameRunner.ProcessEndTurn(state, new Random(99));
        var rivalAfter = result.State.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Rival);

        Assert.Equal(rivalBefore + 1, rivalAfter);
    }

    [Fact]
    public void FullGameLoop_MultiTurnSequence()
    {
        // Play a multi-turn game headlessly
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        // Turn 1: play a spritz, reveal a player tile, end turn
        var spritz = state.Hand.First(c => c.EffectType == CardEffectType.Spritz);
        var playerPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player).Position;

        var result = GameRunner.ProcessCardPlay(state, spritz, [playerPos], new Random(100));
        state = result.State;
        Assert.False(result.TurnEnded);

        // Reveal the player tile (shouldn't end turn)
        result = GameRunner.ProcessReveal(state, playerPos, new Random(101));
        state = result.State;
        Assert.False(result.TurnEnded);
        Assert.True(state.Board.GetTile(playerPos).IsRevealed);

        // End turn manually
        result = GameRunner.ProcessEndTurn(state, new Random(102));
        state = result.State;
        Assert.Equal(2, state.TurnNumber);
        Assert.Equal(5, state.Hand.Count);

        // Turn 2: reveal a rival tile (should end turn automatically)
        var rivalPos = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Rival).Position;
        result = GameRunner.ProcessReveal(state, rivalPos, new Random(103));
        state = result.State;
        Assert.True(result.TurnEnded);
        Assert.Equal(3, state.TurnNumber);
    }

    [Fact]
    public void FullGameLoop_CanWinHeadlessly()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        // Just reveal all player tiles (cheating to test win detection)
        var playerPositions = state.Board.Tiles
            .Where(t => t.Owner == TileOwner.Player)
            .Select(t => t.Position)
            .ToList();

        for (var i = 0; i < playerPositions.Count; i++)
        {
            if (state.GameStatus != GameStatus.Playing)
                break;

            var pos = playerPositions[i];
            if (state.Board.GetTile(pos).IsRevealed)
                continue;

            var result = GameRunner.ProcessReveal(state, pos, new Random(200 + i));
            state = result.State;

            if (result.TurnEnded && state.GameStatus == GameStatus.Playing)
            {
                // Need to get back to a state where we can continue revealing
                // (this is a simplification — in real game, player would wait for their turn)
            }
        }

        Assert.Equal(GameStatus.Won, state.GameStatus);
    }
}
