using System;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root controller: creates the game, handles input events, updates UI.
/// Bridges Godot signals to pure C# GameRunner.
/// </summary>
public partial class GameController : MarginContainer
{
    private BoardNode _boardNode = null!;
    private HandDisplay _handDisplay = null!;
    private HUD _hud = null!;
    private GameState _state = null!;
    private Random _rng = null!;

    public override void _Ready()
    {
        _boardNode = GetNode<BoardNode>("Layout/TopArea/BoardMargin/Board");
        _handDisplay = GetNode<HandDisplay>("Layout/HandPanel/HandDisplay");
        _hud = GetNode<HUD>("Layout/TopArea/HUD");

        _boardNode.TileClicked += OnTileClicked;
        _handDisplay.CardClicked += OnCardClicked;
        _hud.EndTurnPressed += OnEndTurnPressed;

        StartNewGame();
    }

    private void StartNewGame()
    {
        _rng = new Random();
        _state = GameRunner.CreateGame(LevelConfigs.Level1, _rng);

        _boardNode.BuildBoard(_state.Board);
        RefreshUI();
    }

    private void RefreshUI()
    {
        _boardNode.UpdateBoard(_state.Board);
        _handDisplay.UpdateHand(_state);
        _hud.UpdateFromState(_state);
    }

    private void OnTileClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        var pos = new Position(row, col);

        try
        {
            var result = GameRunner.ProcessReveal(_state, pos, _rng);
            _state = result.State;
            RefreshUI();

            if (result.GameOver)
            {
                GD.Print($"Game over: {_state.GameStatus}");
            }
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot reveal: {e.Message}");
        }
    }

    private void OnCardClicked(string cardId)
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        var card = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        if (!DeckSystem.CanPlayCard(_state, card))
        {
            GD.Print($"Cannot afford {card.Name} (cost {card.Cost}, energy {_state.Energy})");
            return;
        }

        // For now, log that the card was clicked — targeting comes in Milestone 8
        GD.Print($"Card clicked: {card.Name} (cost {card.Cost}) — targeting not yet implemented");
    }

    private void OnEndTurnPressed()
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.CurrentPlayer != PlayerType.Player) return;

        try
        {
            var result = GameRunner.ProcessEndTurn(_state, _rng);
            _state = result.State;
            RefreshUI();
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot end turn: {e.Message}");
        }
    }
}
