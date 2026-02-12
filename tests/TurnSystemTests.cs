using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class TurnSystemTests
{
    [Fact]
    public void StartPlayerTurn_DiscardsHandAndDraws5()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        // Play out a card to change hand size
        var initialHandCount = state.Hand.Count;
        Assert.Equal(5, initialHandCount);

        var newState = TurnSystem.StartPlayerTurn(state, new Random(99));

        Assert.Equal(5, newState.Hand.Count);
        Assert.Equal(3, newState.Energy);
        Assert.Equal(PlayerType.Player, newState.CurrentPlayer);
    }

    [Fact]
    public void StartPlayerTurn_IncrementseTurnNumber()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        Assert.Equal(1, state.TurnNumber);

        var newState = TurnSystem.StartPlayerTurn(state, new Random(99));

        Assert.Equal(2, newState.TurnNumber);
    }

    [Fact]
    public void StartPlayerTurn_ResetsEnergy()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng) with { Energy = 0 };

        var newState = TurnSystem.StartPlayerTurn(state, new Random(99));

        Assert.Equal(3, newState.Energy);
    }

    [Fact]
    public void ExecuteRivalTurn_RevealsOneRivalTile()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        var rivalUnrevealedBefore = state.Board.Tiles.Count(t => !t.IsRevealed && t.Owner == TileOwner.Rival);

        var newState = TurnSystem.ExecuteRivalTurn(state, new Random(99));

        var rivalUnrevealedAfter = newState.Board.Tiles.Count(t => !t.IsRevealed && t.Owner == TileOwner.Rival);
        Assert.Equal(rivalUnrevealedBefore - 1, rivalUnrevealedAfter);

        // The revealed tile should be marked as revealed by rival
        var newlyRevealed = newState.Board.Tiles.First(t =>
            t.IsRevealed && t.Owner == TileOwner.Rival && !state.Board.GetTile(t.Position).IsRevealed);
        Assert.Equal(PlayerType.Rival, newlyRevealed.RevealedBy);
    }

    [Fact]
    public void CheckGameStatus_WonWhenAllPlayerTilesRevealed()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        // Reveal all player tiles
        var board = state.Board;
        foreach (var tile in board.Tiles.Where(t => t.Owner == TileOwner.Player))
        {
            board = BoardSystem.RevealTile(board, tile.Position, PlayerType.Player);
        }
        state = state with { Board = board };

        Assert.Equal(GameStatus.Won, TurnSystem.CheckGameStatus(state));
    }

    [Fact]
    public void CheckGameStatus_LostWhenNobleRevealed()
    {
        // Create board with mines
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var rng = new Random(42);
        var board = BoardSystem.CreateBoard(config, rng);

        // Find and reveal a noble
        var noblePos = board.Tiles.First(t => t.Owner == TileOwner.Noble).Position;
        board = BoardSystem.RevealTile(board, noblePos, PlayerType.Player);

        var state = new GameState { Board = board };
        Assert.Equal(GameStatus.Lost, TurnSystem.CheckGameStatus(state));
    }

    [Fact]
    public void CheckGameStatus_PlayingWhenInProgress()
    {
        var rng = new Random(42);
        var state = GameRunner.CreateGame(LevelConfigs.Level1, rng);

        Assert.Equal(GameStatus.Playing, TurnSystem.CheckGameStatus(state));
    }

    [Fact]
    public void ShouldEndTurn_TrueForNonPlayerTiles()
    {
        Assert.True(TurnSystem.ShouldEndTurn(new Tile { Position = new(0, 0), Owner = TileOwner.Rival }));
        Assert.True(TurnSystem.ShouldEndTurn(new Tile { Position = new(0, 0), Owner = TileOwner.Neutral }));
        Assert.True(TurnSystem.ShouldEndTurn(new Tile { Position = new(0, 0), Owner = TileOwner.Noble }));
    }

    [Fact]
    public void ShouldEndTurn_FalseForPlayerTile()
    {
        Assert.False(TurnSystem.ShouldEndTurn(new Tile { Position = new(0, 0), Owner = TileOwner.Player }));
    }
}
