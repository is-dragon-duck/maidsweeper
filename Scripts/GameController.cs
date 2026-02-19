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
/// Manages card targeting flow, game-over overlay, card reward screen,
/// upgrade reward screen, Mask/Nap card-selection, and victory screen.
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

    // Overlay (shared for game-over, card reward, upgrade reward, nap, and victory)
    private ColorRect _overlayDim = null!;
    private PanelContainer _overlayPanel = null!;
    private VBoxContainer _overlayVBox = null!;
    private Label _overlayTitle = null!;
    private Label _overlayDetails = null!;
    private Button _playAgainButton = null!;

    // Card reward / selection UI (built inside overlay)
    private HBoxContainer _rewardCardsRow = null!;
    private Button _skipRewardButton = null!;

    // Upgrade reward UI
    private VBoxContainer _upgradeButtonsContainer = null!;

    public override void _Ready()
    {
        _boardNode = GetNode<BoardNode>("Layout/TopArea/BoardMargin/Board");
        _handDisplay = GetNode<HandDisplay>("Layout/HandPanel/HandDisplay");
        _hud = GetNode<HUD>("Layout/TopArea/HUD");
        _targetingBanner = GetNode<PanelContainer>("Layout/TargetingBanner");
        _targetingLabel = GetNode<Label>("Layout/TargetingBanner/HBox/TargetingLabel");
        _cancelButton = GetNode<Button>("Layout/TargetingBanner/HBox/CancelButton");

        _boardNode.TileClicked += OnTileClicked;
        _boardNode.TileRightClicked += OnTileRightClicked;
        _handDisplay.CardClicked += OnCardClicked;
        _hud.EndTurnPressed += OnEndTurnPressed;
        _cancelButton.Pressed += OnCancelTargeting;

        CreateOverlay();
        StartNewCampaign();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (_targeting.IsTargeting)
            {
                CancelTargeting();
                GetViewport().SetInputAsHandled();
            }
            else if (_overlayDim.Visible && _targeting.Mode == TargetingMode.ExhaustCardTarget)
            {
                // Cancel Nap overlay via Escape
                HideNapOverlay();
                GetViewport().SetInputAsHandled();
            }
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

        // Card reward / Nap selection row (hidden by default)
        _rewardCardsRow = new HBoxContainer();
        _rewardCardsRow.AddThemeConstantOverride("separation", 12);
        _rewardCardsRow.Alignment = BoxContainer.AlignmentMode.Center;
        _rewardCardsRow.Visible = false;
        _overlayVBox.AddChild(_rewardCardsRow);

        // Upgrade buttons container (hidden by default)
        _upgradeButtonsContainer = new VBoxContainer();
        _upgradeButtonsContainer.AddThemeConstantOverride("separation", 8);
        _upgradeButtonsContainer.Visible = false;
        _overlayVBox.AddChild(_upgradeButtonsContainer);

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
        _targetingBanner.Visible = _targeting.IsTargeting && _targeting.Mode == TargetingMode.TileTarget;

        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.TileTarget)
        {
            var activeEffect = _targeting.GetActiveEffectType();
            var displayCard = _targeting.MaskSelectedCard ?? _targeting.TargetCard!;
            _targetingLabel.Text = $"{displayCard.Name}: {_targeting.TargetingMessage}";
            _handDisplay.SetSelectedCard(_targeting.TargetCard!.Id);

            // Set targeting highlights based on card type
            var targetRevealed = TargetingController.TargetsRevealed(activeEffect);
            _boardNode.SetTargetingHighlights(_state.Board, targetRevealed);

            foreach (var pos in _targeting.SelectedTargets)
            {
                _boardNode.SetTargetSelected(pos, true);
            }
        }
        else if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.HandCardTarget)
        {
            _targetingBanner.Visible = true;
            _targetingLabel.Text = $"{_targeting.TargetCard!.Name}: {_targeting.TargetingMessage}";
            _handDisplay.SetSelectedCard(_targeting.TargetCard!.Id);
            _boardNode.ClearTargetingHighlights();
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
            else if (_state.GamePhase == GamePhase.UpgradeReward)
            {
                ShowUpgradeRewardOverlay();
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
        _upgradeButtonsContainer.Visible = false;
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

        ClearRewardCards();

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
        _upgradeButtonsContainer.Visible = false;
        _skipRewardButton.Visible = true;
        _playAgainButton.Visible = false;
        _overlayDim.Visible = true;
    }

    private void ShowUpgradeRewardOverlay()
    {
        _overlayTitle.Text = "Upgrade Your Deck";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.85f, 0.7f, 0.15f));
        _overlayDetails.Text = "Choose an upgrade:";

        _rewardCardsRow.Visible = false;
        ClearUpgradeButtons();

        if (_state.UpgradeOptions != null)
        {
            foreach (var option in _state.UpgradeOptions)
            {
                var label = option.Type switch
                {
                    UpgradeType.Enhance when option.TargetCard != null =>
                        $"Enhance: {option.TargetCard.Name}",
                    UpgradeType.BonusSpoon when option.TargetCard != null =>
                        $"Bonus Spoon: {option.TargetCard.Name}",
                    UpgradeType.RemoveCard =>
                        "Remove a Card",
                    _ => option.Type.ToString()
                };

                var btn = new Button
                {
                    Text = label,
                    CustomMinimumSize = new Vector2(250, 35)
                };

                var capturedOption = option;
                btn.Pressed += () => OnUpgradeSelected(capturedOption);
                _upgradeButtonsContainer.AddChild(btn);
            }
        }

        _upgradeButtonsContainer.Visible = true;
        _skipRewardButton.Visible = true;
        _skipRewardButton.Text = "Skip";
        _playAgainButton.Visible = false;
        _overlayDim.Visible = true;
    }

    private void ShowRemoveCardOverlay()
    {
        _overlayTitle.Text = "Remove a Card";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 0.3f));
        _overlayDetails.Text = "Click a card to remove it from your deck:";

        ClearRewardCards();
        ClearUpgradeButtons();

        foreach (var card in _state.PersistentDeck)
        {
            var cardUI = new CardUI();
            _rewardCardsRow.AddChild(cardUI);
            cardUI.Setup(card, true);
            cardUI.CardClicked += OnRemoveCardClicked;
        }

        _rewardCardsRow.Visible = true;
        _upgradeButtonsContainer.Visible = false;
        _skipRewardButton.Visible = true;
        _skipRewardButton.Text = "Back";
        _playAgainButton.Visible = false;
    }

    private void ShowNapOverlay()
    {
        _overlayTitle.Text = "Nap: Retrieve a Card";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 0.9f));
        _overlayDetails.Text = "Choose a card from the exhaust pile:";

        ClearRewardCards();
        ClearUpgradeButtons();

        foreach (var card in _state.ExhaustPile)
        {
            var cardUI = new CardUI();
            _rewardCardsRow.AddChild(cardUI);
            cardUI.Setup(card, true);
            cardUI.CardClicked += OnNapCardClicked;
        }

        _rewardCardsRow.Visible = true;
        _upgradeButtonsContainer.Visible = false;
        _skipRewardButton.Visible = true;
        _skipRewardButton.Text = "Cancel";
        _playAgainButton.Visible = false;
        _overlayDim.Visible = true;
    }

    private void HideNapOverlay()
    {
        CancelTargeting();
        _overlayDim.Visible = false;
        RefreshUI();
    }

    private void ShowVictoryOverlay()
    {
        _overlayTitle.Text = "Campaign Complete!";
        _overlayTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.4f));
        _overlayDetails.Text = $"Deck size: {_state.PersistentDeck.Count} cards";

        _rewardCardsRow.Visible = false;
        _upgradeButtonsContainer.Visible = false;
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

        if (_state.GamePhase == GamePhase.UpgradeReward)
        {
            ShowUpgradeRewardOverlay();
        }
        else
        {
            StartNextFloor();
        }
    }

    private void OnUpgradeSelected(UpgradeOption option)
    {
        if (option.Type == UpgradeType.RemoveCard)
        {
            ShowRemoveCardOverlay();
            return;
        }

        _state = CampaignSystem.SelectUpgrade(_state, option, _rng);
        StartNextFloor();
    }

    private void OnRemoveCardClicked(string cardId)
    {
        var cardToRemove = _state.PersistentDeck.FirstOrDefault(c => c.Id == cardId);
        if (cardToRemove == null) return;

        var removeOption = _state.UpgradeOptions?.FirstOrDefault(o => o.Type == UpgradeType.RemoveCard);
        if (removeOption == null) return;

        _state = CampaignSystem.SelectUpgrade(_state, removeOption, _rng, cardToRemove);
        StartNextFloor();
    }

    private void OnNapCardClicked(string cardId)
    {
        var napCard = _targeting.TargetCard;
        if (napCard == null) return;

        var retrievedCard = _state.ExhaustPile.FirstOrDefault(c => c.Id == cardId);
        if (retrievedCard == null) return;

        try
        {
            _state = CardEffectSystem.PlayNap(_state, napCard, retrievedCard, _rng);
            _targeting.Cancel();
            _overlayDim.Visible = false;
            CheckPostCardPlay();
        }
        catch (Exception e)
        {
            GD.Print($"Nap failed: {e.Message}");
        }
    }

    private void OnSkipReward()
    {
        // Context-dependent skip button
        if (_state.GamePhase == GamePhase.UpgradeReward && _skipRewardButton.Text == "Back")
        {
            // Back from remove card sub-overlay → show upgrade options again
            ShowUpgradeRewardOverlay();
            return;
        }

        if (_state.GamePhase == GamePhase.UpgradeReward)
        {
            _state = CampaignSystem.SkipUpgrade(_state, _rng);
            StartNextFloor();
            return;
        }

        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.ExhaustCardTarget)
        {
            // Cancel Nap
            HideNapOverlay();
            return;
        }

        if (_state.GamePhase == GamePhase.CardReward)
        {
            _state = CampaignSystem.SkipCardReward(_state, _rng);

            if (_state.GamePhase == GamePhase.UpgradeReward)
            {
                ShowUpgradeRewardOverlay();
            }
            else
            {
                StartNextFloor();
            }
            return;
        }
    }

    private void OnTileClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

        var pos = new Position(row, col);

        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.TileTarget)
        {
            HandleTargetingClick(pos);
            return;
        }

        if (_targeting.IsTargeting) return; // In card-selection mode, ignore tile clicks

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

    private void OnTileRightClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

        var pos = new Position(row, col);
        var tile = _state.Board.GetTile(pos);
        if (tile.IsRevealed || tile.IsDestroyed) return;
        if (!_state.Board.IsUsablePosition(pos)) return;

        _state = AnnotationSystem.ToggleFlag(_state, pos);
        RefreshUI();
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
            var displayCard = _targeting.MaskSelectedCard ?? _targeting.TargetCard!;
            _targetingLabel.Text = $"{displayCard.Name}: {_targeting.TargetingMessage}";
        }
    }

    private void OnCardClicked(string cardId)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

        // Mask: hand card selection mode — pick a card to play through Mask
        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.HandCardTarget)
        {
            HandleMaskCardSelection(cardId);
            return;
        }

        if (_targeting.IsTargeting)
        {
            CancelTargeting();
        }

        var card = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        if (!DeckSystem.CanPlayCard(_state, card))
            return;

        // Mask: enter hand card selection mode
        if (card.EffectType == CardEffectType.Mask)
        {
            _targeting.BeginHandCardTargeting(card);
            UpdateTargetingUI();
            return;
        }

        // Nap: show exhaust pile overlay
        if (card.EffectType == CardEffectType.Nap)
        {
            if (_state.ExhaustPile.Count == 0)
            {
                // Play Nap with no card to retrieve (still gets bonus spoon etc.)
                PlayNapDirect(card, null);
                return;
            }
            _targeting.BeginExhaustCardTargeting(card);
            ShowNapOverlay();
            return;
        }

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

    private void HandleMaskCardSelection(string cardId)
    {
        // Don't allow selecting Mask itself
        if (cardId == _targeting.TargetCard!.Id) return;

        var selectedCard = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (selectedCard == null) return;

        _targeting.TransitionToMaskedCardTargeting(selectedCard);

        if (TargetingController.RequiresTargeting(selectedCard.EffectType))
        {
            // Now in tile targeting mode for the masked card
            UpdateTargetingUI();
        }
        else
        {
            // Immediate effect — play Mask + selected card now
            ExecuteMaskedCard(null);
        }
    }

    private void ExecuteTargetedCard()
    {
        var card = _targeting.TargetCard!;
        var targets = _targeting.GetTargets();

        if (_targeting.MaskSelectedCard != null)
        {
            // This was a Mask → selected card flow
            ExecuteMaskedCard(targets);
        }
        else
        {
            CancelTargeting();
            PlayCard(card, targets);
        }
    }

    #nullable enable
    private void ExecuteMaskedCard(Position[]? targets)
    #nullable restore
    {
        var maskCard = _targeting.TargetCard!;
        var selectedCard = _targeting.MaskSelectedCard!;
        CancelTargeting();

        try
        {
            _state = CardEffectSystem.PlayMaskedCard(_state, maskCard, selectedCard, targets, _rng);
            CheckPostCardPlay();
        }
        catch (Exception e)
        {
            GD.Print($"Masked card play failed: {e.Message}");
            RefreshUI();
        }
    }

    #nullable enable
    private void PlayNapDirect(Card napCard, Card? retrievedCard)
    #nullable restore
    {
        try
        {
            _state = CardEffectSystem.PlayNap(_state, napCard, retrievedCard, _rng);
            CheckPostCardPlay();
        }
        catch (Exception e)
        {
            GD.Print($"Nap failed: {e.Message}");
            RefreshUI();
        }
    }

    /// <summary>
    /// After a card play that bypasses GameRunner (Mask, Nap), check game status.
    /// </summary>
    private void CheckPostCardPlay()
    {
        var status = TurnSystem.CheckGameStatus(_state);
        _state = _state with { GameStatus = status };
        RefreshUI();

        if (status != GameStatus.Playing)
        {
            HandleGameOver();
        }
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
        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.ExhaustCardTarget)
        {
            HideNapOverlay();
            return;
        }
        CancelTargeting();
    }

    private void CancelTargeting()
    {
        _targeting.Cancel();
        UpdateTargetingUI();
    }

    private void ClearRewardCards()
    {
        foreach (var child in _rewardCardsRow.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void ClearUpgradeButtons()
    {
        foreach (var child in _upgradeButtonsContainer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static int GetFloorNumber(string levelId) => HUD.GetFloorNumber(levelId);
}
