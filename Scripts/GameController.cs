using System;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root controller: creates the game, handles input events, updates UI.
/// Bridges Godot signals to pure C# GameRunner.
/// </summary>
public partial class GameController : Node
{
    private BoardNode _boardNode = null!;
    private GameState _state = null!;
    private Random _rng = null!;

    public override void _Ready()
    {
        _boardNode = GetNode<BoardNode>("Board");
        _boardNode.TileClicked += OnTileClicked;

        StartNewGame();
    }

    private void StartNewGame()
    {
        _rng = new Random();
        _state = GameRunner.CreateGame(LevelConfigs.Level1, _rng);

        _boardNode.BuildBoard(_state.Board);

        GD.Print($"Game started: Turn {_state.TurnNumber}, Energy {_state.Energy}/{_state.MaxEnergy}, Hand: {_state.Hand.Count} cards");
    }

    private void OnTileClicked(int row, int col)
    {
        var pos = new Position(row, col);

        try
        {
            var result = GameRunner.ProcessReveal(_state, pos, _rng);
            _state = result.State;

            _boardNode.UpdateBoard(_state.Board);

            if (result.GameOver)
            {
                GD.Print($"Game over: {_state.GameStatus}");
            }
            else if (result.TurnEnded)
            {
                GD.Print($"Turn ended. Now turn {_state.TurnNumber}, Energy {_state.Energy}/{_state.MaxEnergy}, Hand: {_state.Hand.Count}");
            }
            else
            {
                var tile = _state.Board.GetTile(pos);
                GD.Print($"Revealed ({row},{col}): {tile.Owner}, adjacency={tile.AdjacencyCount}");
            }
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot reveal: {e.Message}");
        }
    }
}
