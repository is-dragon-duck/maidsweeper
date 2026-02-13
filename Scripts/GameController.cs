using System;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root controller: creates the game, handles input events, updates UI.
/// Bridges Godot signals to pure C# GameRunner.
/// Manages card targeting flow.
/// </summary>
public partial class GameController : MarginContainer
{
    private BoardNode _boardNode = null!;
    private HandDisplay _handDisplay = null!;
    private HUD _hud = null!;
    private PanelContainer _targetingBanner = null!;
    private Label _targetingLabel = null!;
    private Button _cancelButton = null!;
    private GameState _state = null!;
    private Random _rng = null!;
    private readonly TargetingController _targeting = new();

    public override void _Ready()
    {
        _boardNode = GetNode<BoardNode>("Layout/TopArea/BoardMargin/Board");
        _handDisplay = GetNode<HandDisplay>("Layout/HandPanel/HandDisplay");
        _hud = GetNode<HUD>("Layout/TopArea/HUD");
        _targetingBanner = GetNode<PanelContainer>("Layout/TargetingBanner");
        _targetingLabel = GetNode<Label>("Layout/TargetingBanner/HBox/TargetingLabel");
        _cancelButton = GetNode<Button>("Layout/TargetingBanner/HBox/CancelButton");

        _boardNode.TileClicked += OnTileClicked;
        _handDisplay.CardClicked += OnCardClicked;
        _hud.EndTurnPressed += OnEndTurnPressed;
        _cancelButton.Pressed += OnCancelTargeting;

        StartNewGame();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && _targeting.IsTargeting)
        {
            CancelTargeting();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && _targeting.IsTargeting)
        {
            CancelTargeting();
            GetViewport().SetInputAsHandled();
        }
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
        UpdateTargetingUI();
    }

    private void UpdateTargetingUI()
    {
        _targetingBanner.Visible = _targeting.IsTargeting;

        if (_targeting.IsTargeting)
        {
            _targetingLabel.Text = $"{_targeting.TargetCard!.Name}: {_targeting.TargetingMessage}";
            _handDisplay.SetSelectedCard(_targeting.TargetCard.Id);
            _boardNode.SetTargetingHighlights(_state.Board);

            // Re-apply selected highlights
            foreach (var pos in _targeting.SelectedTargets)
            {
                _boardNode.SetTargetSelected(pos, true);
            }
        }
        else
        {
            _handDisplay.ClearSelection();
            _boardNode.ClearTargetingHighlights();
        }
    }

    private void OnTileClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        var pos = new Position(row, col);

        if (_targeting.IsTargeting)
        {
            HandleTargetingClick(pos);
            return;
        }

        // Normal reveal
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

    private void HandleTargetingClick(Position pos)
    {
        if (!_targeting.TrySelectTarget(pos, _state))
            return;

        _boardNode.SetTargetSelected(pos, true);

        if (_targeting.IsComplete)
        {
            ExecuteTargetedCard();
        }
        else
        {
            // Update banner message for multi-target
            _targetingLabel.Text = $"{_targeting.TargetCard!.Name}: {_targeting.TargetingMessage}";
        }
    }

    private void OnCardClicked(string cardId)
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        // If already targeting, cancel first
        if (_targeting.IsTargeting)
        {
            CancelTargeting();
        }

        var card = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        if (!DeckSystem.CanPlayCard(_state, card))
        {
            GD.Print($"Cannot afford {card.Name} (cost {card.Cost}, energy {_state.Energy})");
            return;
        }

        if (TargetingController.RequiresTargeting(card.EffectType))
        {
            // Enter targeting mode
            _targeting.BeginTargeting(card);
            UpdateTargetingUI();
        }
        else
        {
            // Immediate card — play directly
            PlayCard(card, null);
        }
    }

    private void ExecuteTargetedCard()
    {
        var card = _targeting.TargetCard!;
        var targets = _targeting.GetTargets();
        CancelTargeting();
        PlayCard(card, targets);
    }

    #nullable enable
    private void PlayCard(Card card, Position[]? targets)
    #nullable restore
    {
        try
        {
            var result = GameRunner.ProcessCardPlay(_state, card, targets, _rng);
            _state = result.State;
            RefreshUI();

            if (result.GameOver)
            {
                GD.Print($"Game over: {_state.GameStatus}");
            }
            else if (result.TurnEnded)
            {
                GD.Print($"Card caused turn end. Now turn {_state.TurnNumber}");
            }
        }
        catch (Exception e)
        {
            GD.Print($"Card play failed: {e.Message}");
        }
    }

    private void OnEndTurnPressed()
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.CurrentPlayer != PlayerType.Player) return;

        if (_targeting.IsTargeting)
        {
            CancelTargeting();
        }

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

    private void OnCancelTargeting()
    {
        CancelTargeting();
    }

    private void CancelTargeting()
    {
        _targeting.Cancel();
        UpdateTargetingUI();
    }
}
