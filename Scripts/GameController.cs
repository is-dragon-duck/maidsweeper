using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root controller: creates the game, handles input events, updates UI.
/// Bridges Godot signals to pure C# GameRunner.
/// Manages card targeting flow and game-over overlay.
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
    private readonly List<string> _globalClueOrder = new();

    // Game-over overlay (created programmatically)
    private ColorRect _overlayDim = null!;
    private PanelContainer _overlayPanel = null!;
    private Label _overlayTitle = null!;
    private Label _overlayDetails = null!;
    private Button _playAgainButton = null!;

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

        CreateGameOverOverlay();
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

    private void CreateGameOverOverlay()
    {
        // Semi-transparent dim layer
        _overlayDim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.5f),
            Visible = false
        };
        _overlayDim.SetAnchorsPreset(LayoutPreset.FullRect);
        _overlayDim.MouseFilter = MouseFilterEnum.Stop; // Block clicks through to game
        AddChild(_overlayDim);

        // Center container
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        _overlayDim.AddChild(center);

        // Panel
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f),
            ContentMarginLeft = 40,
            ContentMarginRight = 40,
            ContentMarginTop = 30,
            ContentMarginBottom = 30,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        _overlayPanel = new PanelContainer();
        _overlayPanel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(_overlayPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        _overlayPanel.AddChild(vbox);

        _overlayTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _overlayTitle.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(_overlayTitle);

        _overlayDetails = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _overlayDetails.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(_overlayDetails);

        _playAgainButton = new Button
        {
            Text = "Play Again",
            CustomMinimumSize = new Vector2(150, 40)
        };
        _playAgainButton.Pressed += OnPlayAgain;
        vbox.AddChild(_playAgainButton);
    }

    private void StartNewGame()
    {
        _rng = new Random();
        _state = GameRunner.CreateGame(LevelConfigs.Level1, _rng);

        _globalClueOrder.Clear();
        _boardNode.BuildBoard(_state.Board);
        _overlayDim.Visible = false;
        RefreshUI();
    }

    private void RefreshUI()
    {
        // Append any new clue IDs (preserves existing order, only adds new ones)
        foreach (var tile in _state.Board.Tiles)
        {
            foreach (var clue in tile.Annotations.ClueResults)
            {
                if (!_globalClueOrder.Contains(clue.ClueId))
                    _globalClueOrder.Add(clue.ClueId);
            }
        }

        _boardNode.UpdateBoard(_state.Board, _globalClueOrder);
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

    private void ShowGameOverOverlay()
    {
        if (_targeting.IsTargeting)
            CancelTargeting();

        var won = _state.GameStatus == GameStatus.Won;

        _overlayTitle.Text = won ? "Floor Cleared!" : "Game Over";
        _overlayTitle.AddThemeColorOverride("font_color",
            won ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.25f));

        var revealedPlayer = _state.Board.Tiles.Count(t => t.IsRevealed && t.Owner == TileOwner.Player);
        var totalPlayer = _state.Board.Tiles.Count(t => t.Owner == TileOwner.Player);

        _overlayDetails.Text = won
            ? $"All {totalPlayer} tiles found in {_state.TurnNumber} turns"
            : $"Found {revealedPlayer} of {totalPlayer} tiles ({_state.TurnNumber} turns)";

        _overlayDim.Visible = true;
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
                ShowGameOverOverlay();
            }
        }
        catch (InvalidOperationException)
        {
            // Already revealed or invalid — silently ignore
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
            _targetingLabel.Text = $"{_targeting.TargetCard!.Name}: {_targeting.TargetingMessage}";
        }
    }

    private void OnCardClicked(string cardId)
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        if (_targeting.IsTargeting)
        {
            CancelTargeting();
        }

        var card = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        if (!DeckSystem.CanPlayCard(_state, card))
            return;

        if (TargetingController.RequiresTargeting(card.EffectType))
        {
            _targeting.BeginTargeting(card);
            UpdateTargetingUI();
        }
        else
        {
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
                ShowGameOverOverlay();
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

            if (result.GameOver)
            {
                ShowGameOverOverlay();
            }
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot end turn: {e.Message}");
        }
    }

    private void OnPlayAgain()
    {
        StartNewGame();
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
