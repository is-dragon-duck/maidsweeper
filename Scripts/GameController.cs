using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Root controller: creates the game, handles input events, updates UI.
/// Bridges Godot signals to pure C# GameRunner and CampaignSystem.
/// Manages card targeting flow, game-over overlay, card reward screen, and victory screen.
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

    // Overlay (shared for game-over, card reward, and victory)
    private ColorRect _overlayDim = null!;
    private PanelContainer _overlayPanel = null!;
    private VBoxContainer _overlayVBox = null!;
    private Label _overlayTitle = null!;
    private Label _overlayDetails = null!;
    private Button _playAgainButton = null!;

    // Card reward UI (built inside overlay)
    private HBoxContainer _rewardCardsRow = null!;
    private Button _skipRewardButton = null!;

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

        CreateOverlay();
        StartNewCampaign();
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

    private void CreateOverlay()
    {
        // Semi-transparent dim layer
        _overlayDim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.5f),
            Visible = false
        };
        _overlayDim.SetAnchorsPreset(LayoutPreset.FullRect);
        _overlayDim.MouseFilter = MouseFilterEnum.Stop;
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

        _overlayVBox = new VBoxContainer();
        _overlayVBox.AddThemeConstantOverride("separation", 16);
        _overlayVBox.Alignment = BoxContainer.AlignmentMode.Center;
        _overlayPanel.AddChild(_overlayVBox);

        _overlayTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _overlayTitle.AddThemeFontSizeOverride("font_size", 32);
        _overlayVBox.AddChild(_overlayTitle);

        _overlayDetails = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _overlayDetails.AddThemeFontSizeOverride("font_size", 16);
        _overlayVBox.AddChild(_overlayDetails);

        // Card reward row (hidden by default)
        _rewardCardsRow = new HBoxContainer();
        _rewardCardsRow.AddThemeConstantOverride("separation", 12);
        _rewardCardsRow.Alignment = BoxContainer.AlignmentMode.Center;
        _rewardCardsRow.Visible = false;
        _overlayVBox.AddChild(_rewardCardsRow);

        _skipRewardButton = new Button
        {
            Text = "Skip",
            CustomMinimumSize = new Vector2(100, 35),
            Visible = false
        };
        _skipRewardButton.Pressed += OnSkipReward;
        _overlayVBox.AddChild(_skipRewardButton);

        _playAgainButton = new Button
        {
            Text = "Play Again",
            CustomMinimumSize = new Vector2(150, 40)
        };
        _playAgainButton.Pressed += OnPlayAgain;
        _overlayVBox.AddChild(_playAgainButton);
    }

    private void StartNewCampaign()
    {
        _rng = new Random();
        _state = CampaignSystem.StartCampaign(_rng);

        _globalClueOrder.Clear();
        _boardNode.BuildBoard(_state.Board);
        _overlayDim.Visible = false;
        RefreshUI();
    }

    private void StartNextFloor()
    {
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

    private void HandleGameOver()
    {
        if (_targeting.IsTargeting)
            CancelTargeting();

        if (_state.GameStatus == GameStatus.Won)
        {
            // Campaign progression
            _state = CampaignSystem.CompleteFloor(_state, _rng);

            if (_state.GamePhase == GamePhase.CampaignVictory)
            {
                ShowVictoryOverlay();
            }
            else if (_state.GamePhase == GamePhase.CardReward)
            {
                ShowCardRewardOverlay();
            }
        }
        else
        {
            ShowLossOverlay();
        }
    }

    private void ShowLossOverlay()
    {
        _overlayTitle.Text = "Game Over";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.25f));

        var revealedPlayer = _state.Board.Tiles.Count(t =>
            _state.Board.IsUsablePosition(t.Position) && t.IsRevealed && t.Owner == TileOwner.Player);
        var totalPlayer = _state.Board.Tiles.Count(t =>
            _state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Player);

        var floorNum = GetFloorNumber(_state.CurrentLevelId);
        _overlayDetails.Text = $"Floor {floorNum}: Found {revealedPlayer} of {totalPlayer} tiles ({_state.TurnNumber} turns)";

        _rewardCardsRow.Visible = false;
        _skipRewardButton.Visible = false;
        _playAgainButton.Visible = true;
        _playAgainButton.Text = "Play Again";
        _overlayDim.Visible = true;
    }

    private void ShowCardRewardOverlay()
    {
        var floorNum = GetFloorNumber(_state.CurrentLevelId);
        _overlayTitle.Text = $"Floor {floorNum} Cleared!";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.4f));
        _overlayDetails.Text = "Choose a card to add to your deck:";

        // Clear old reward cards
        foreach (var child in _rewardCardsRow.GetChildren())
        {
            child.QueueFree();
        }

        // Add reward card buttons
        if (_state.CardRewardOptions != null)
        {
            foreach (var card in _state.CardRewardOptions)
            {
                var cardUI = new CardUI();
                _rewardCardsRow.AddChild(cardUI);
                cardUI.Setup(card, true);
                cardUI.CardClicked += OnRewardCardClicked;
            }
        }

        _rewardCardsRow.Visible = true;
        _skipRewardButton.Visible = true;
        _playAgainButton.Visible = false;
        _overlayDim.Visible = true;
    }

    private void ShowVictoryOverlay()
    {
        _overlayTitle.Text = "Campaign Complete!";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.4f));
        _overlayDetails.Text = $"Deck size: {_state.PersistentDeck.Count} cards";

        _rewardCardsRow.Visible = false;
        _skipRewardButton.Visible = false;
        _playAgainButton.Visible = true;
        _playAgainButton.Text = "Play Again";
        _overlayDim.Visible = true;
    }

    private void OnRewardCardClicked(string cardId)
    {
        if (_state.CardRewardOptions == null) return;

        var selected = _state.CardRewardOptions.FirstOrDefault(c => c.Id == cardId);
        if (selected == null) return;

        _state = CampaignSystem.SelectCardReward(_state, selected, _rng);
        StartNextFloor();
    }

    private void OnSkipReward()
    {
        _state = CampaignSystem.SkipCardReward(_state, _rng);
        StartNextFloor();
    }

    private void OnTileClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

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
                HandleGameOver();
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
        if (_state.GamePhase != GamePhase.Playing) return;

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
                HandleGameOver();
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
                HandleGameOver();
            }
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot end turn: {e.Message}");
        }
    }

    private void OnPlayAgain()
    {
        StartNewCampaign();
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

    private static int GetFloorNumber(string levelId) => levelId switch
    {
        "level1" => 1,
        "level2" => 2,
        "level3" => 3,
        _ => 0
    };
}
