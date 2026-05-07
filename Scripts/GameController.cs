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
/// Delegates overlay display to OverlayManager.
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
    private readonly OverlayManager _overlay = new();

    // Debug card picker
    private ColorRect _debugOverlay = null!;
    private int _debugCardIdCounter;

    // Shop sub-flow: when the player clicks the shop's RemoveCard slot, the
    // RemoveCard overlay is shown with this slot index so the eventual selection
    // can route back through ShopSystem.Purchase instead of CampaignSystem.SelectUpgrade.
    private int? _pendingShopRemoveSlot;

    // Floor-end summary (copper gained / paid). Shown on the first overlay after
    // CompleteFloor and consumed when displayed.
    #nullable enable
    private string? _floorEndSummary;
    #nullable restore

    public override void _Ready()
    {
        // Set a dark navy/charcoal background distinct from tile colors
        var bg = new ColorRect
        {
            Color = new Color(0.12f, 0.12f, 0.15f)
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);
        MoveChild(bg, 0);

        _boardNode = GetNode<BoardNode>("Layout/TopArea/BoardMargin/Board");
        _handDisplay = GetNode<HandDisplay>("Layout/HandPanel/HandDisplay");
        _hud = GetNode<HUD>("Layout/TopArea/HUD");
        _targetingBanner = GetNode<PanelContainer>("Layout/TargetingBanner");
        _targetingLabel = GetNode<Label>("Layout/TargetingBanner/HBox/TargetingLabel");
        _cancelButton = GetNode<Button>("Layout/TargetingBanner/HBox/CancelButton");

        // Board signals
        _boardNode.TileClicked += OnTileClicked;
        _boardNode.TileRightClicked += OnTileRightClicked;
        _boardNode.TileHovered += OnTileHovered;
        _boardNode.TileUnhovered += OnTileUnhovered;

        // Hand/HUD signals
        _handDisplay.CardClicked += OnCardClicked;
        _hud.EndTurnPressed += OnEndTurnPressed;
        _hud.AnnotationTypeChanged += OnAnnotationTypeChanged;
        _hud.ViewPileRequested += OnViewPileRequested;
        _cancelButton.Pressed += OnCancelTargeting;

        // Overlay
        _overlay.Build(this);
        _overlay.RewardCardSelected += OnRewardCardSelected;
        _overlay.UpgradeSelected += OnUpgradeSelected;
        _overlay.RemoveCardSelected += OnRemoveCardSelected;
        _overlay.NapCardSelected += OnNapCardSelected;
        _overlay.EquipmentSelected += OnEquipmentSelected;
        _overlay.ShopSlotPurchased += OnShopSlotPurchased;
        _overlay.SkipPressed += OnSkipPressed;
        _overlay.PlayAgainPressed += OnPlayAgain;

        StartNewCampaign();
    }

    // ───────────────────────────────────────────────
    // Input
    // ───────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (_targeting.IsTargeting)
            {
                CancelTargeting();
                GetViewport().SetInputAsHandled();
            }
            else if (_overlay.IsVisible && _overlay.CurrentMode == OverlayMode.PileView)
            {
                _overlay.Hide();
                GetViewport().SetInputAsHandled();
            }
            else if (_overlay.IsVisible && _targeting.Mode == TargetingMode.ExhaustCardTarget)
            {
                HideNapOverlay();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && _targeting.IsTargeting)
        {
            CancelTargeting();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.F2 })
        {
            ToggleDebugCardPicker();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
        {
            DebugRevealAllPlayer();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Debug: reveal all remaining unrevealed Player tiles, triggering floor completion.
    /// </summary>
    private void DebugRevealAllPlayer()
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        var playerPositions = _state.Board.Tiles
            .Where(t => t.Owner == TileOwner.Player && !t.IsRevealed && !t.IsDestroyed
                        && _state.Board.IsUsablePosition(t.Position))
            .Select(t => t.Position)
            .ToList();

        foreach (var pos in playerPositions)
        {
            try
            {
                var result = GameRunner.ProcessReveal(_state, pos, _rng);
                _state = result.State;

                if (result.GameOver)
                {
                    RefreshUI();
                    HandleGameOver();
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                // Skip if reveal fails
            }
        }

        RefreshUI();
    }

    // ───────────────────────────────────────────────
    // Game lifecycle
    // ───────────────────────────────────────────────

    private void StartNewCampaign()
    {
        _rng = new Random();
        _state = CampaignSystem.StartCampaign(_rng);

        _globalClueOrder.Clear();
        _boardNode.BuildBoard(_state.Board);
        _overlay.Hide();
        RefreshUI();
    }

    private void StartNextFloor()
    {
        _globalClueOrder.Clear();
        _boardNode.BuildBoard(_state.Board);
        _overlay.Hide();
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

        TileOwner? perspective = _hud.SelectedAnnotationType;
        _boardNode.UpdateBoard(_state.Board, _globalClueOrder, perspective);
        _handDisplay.UpdateHand(_state);
        _hud.UpdateFromState(_state);
        UpdateTargetingUI();
    }

    // ───────────────────────────────────────────────
    // Targeting UI
    // ───────────────────────────────────────────────

    private void UpdateTargetingUI()
    {
        _targetingBanner.Visible = _targeting.IsTargeting && _targeting.Mode == TargetingMode.TileTarget;

        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.TileTarget)
        {
            var activeEffect = _targeting.GetActiveEffectType();
            var displayCard = _targeting.MaskSelectedCard ?? _targeting.TargetCard!;
            _targetingLabel.Text = $"{displayCard.Name}: {_targeting.TargetingMessage}";
            _handDisplay.SetSelectedCard(_targeting.TargetCard!.Id);

            var targetRevealed = TargetingController.TargetsRevealed(activeEffect);
            var areaCenterMode = TargetingController.TargetsAreaCenter(activeEffect);
            _boardNode.SetTargetingHighlights(_state.Board, targetRevealed, areaCenterMode);

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

    private void CancelTargeting()
    {
        _targeting.Cancel();
        UpdateTargetingUI();
    }

    // ───────────────────────────────────────────────
    // Game-over / campaign progression
    // ───────────────────────────────────────────────

    private void HandleGameOver()
    {
        if (_targeting.IsTargeting)
            CancelTargeting();

        if (_state.GameStatus == GameStatus.Won)
        {
            // Capture floor-end copper movement for display in the next overlay
            var unrevealedRivals = _state.Board.Tiles.Count(t =>
                _state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && !t.IsDestroyed
                && t.Owner == TileOwner.Rival);
            var rivalEarned = unrevealedRivals * EquipmentSystem.CopperMultiplier(_state);
            var complaintsPenalty = _state.ComplaintsStacks * 2;
            _floorEndSummary = BuildFloorEndSummary(rivalEarned, complaintsPenalty);

            _state = CampaignSystem.CompleteFloor(_state, _rng);
            RouteToCurrentPhase();
        }
        else
        {
            _overlay.ShowLoss(_state);
        }
    }

    /// <summary>
    /// Shows the appropriate overlay for the current GamePhase, or starts the
    /// next floor if the campaign has already advanced to Playing.
    /// </summary>
    private void RouteToCurrentPhase()
    {
        // Consume the floor-end summary so it only appears on the first overlay
        var summary = _floorEndSummary;
        _floorEndSummary = null;

        switch (_state.GamePhase)
        {
            case GamePhase.CardReward:
                _overlay.ShowCardReward(_state, summary);
                break;
            case GamePhase.UpgradeReward:
                _overlay.ShowUpgradeReward(_state, summary);
                break;
            case GamePhase.EquipmentReward:
                _overlay.ShowEquipmentReward(_state, summary);
                break;
            case GamePhase.Shop:
                _overlay.ShowShop(_state, summary);
                break;
            case GamePhase.CampaignVictory:
                _overlay.ShowVictory(_state);
                break;
            case GamePhase.Playing:
                StartNextFloor();
                break;
        }
    }

    /// <summary>
    /// Builds a short copper-summary string ("+3 copper" / "−4 to Complaints" / both)
    /// or returns null when there is no movement to report.
    /// </summary>
    #nullable enable
    private static string? BuildFloorEndSummary(int rivalEarned, int complaintsPenalty)
    {
        if (rivalEarned == 0 && complaintsPenalty == 0) return null;
        if (complaintsPenalty == 0) return $"+{rivalEarned} copper";
        if (rivalEarned == 0) return $"−{complaintsPenalty} copper to Complaints";
        return $"+{rivalEarned} copper (−{complaintsPenalty} to Complaints)";
    }
    #nullable restore

    // ───────────────────────────────────────────────
    // Overlay callbacks
    // ───────────────────────────────────────────────

    private void OnRewardCardSelected(string cardId)
    {
        if (_state.CardRewardOptions == null) return;

        var selected = _state.CardRewardOptions.FirstOrDefault(c => c.Id == cardId);
        if (selected == null) return;

        _state = CampaignSystem.SelectCardReward(_state, selected, _rng);
        RouteToCurrentPhase();
    }

    private void OnUpgradeSelected(int optionIndex)
    {
        if (_state.UpgradeOptions == null || optionIndex >= _state.UpgradeOptions.Count) return;
        var option = _state.UpgradeOptions[optionIndex];

        if (option.Type == UpgradeType.RemoveCard)
        {
            _overlay.ShowRemoveCard(_state);
            return;
        }

        _state = CampaignSystem.SelectUpgrade(_state, option, _rng);
        RouteToCurrentPhase();
    }

    private void OnRemoveCardSelected(string cardId)
    {
        var cardToRemove = _state.PersistentDeck.FirstOrDefault(c => c.Id == cardId);
        if (cardToRemove == null) return;

        // Shop's Remove-Card slot routes here too
        if (_pendingShopRemoveSlot is { } slotIndex)
        {
            _pendingShopRemoveSlot = null;
            try
            {
                _state = ShopSystem.Purchase(_state, slotIndex, _rng, cardToRemove);
            }
            catch (System.Exception e)
            {
                GD.Print($"Shop remove failed: {e.Message}");
            }
            _overlay.ShowShop(_state);
            return;
        }

        var removeOption = _state.UpgradeOptions?.FirstOrDefault(o => o.Type == UpgradeType.RemoveCard);
        if (removeOption == null) return;

        _state = CampaignSystem.SelectUpgrade(_state, removeOption, _rng, cardToRemove);
        RouteToCurrentPhase();
    }

    private void OnEquipmentSelected(string equipmentId)
    {
        if (_state.EquipmentOptions == null) return;

        var selected = _state.EquipmentOptions.FirstOrDefault(e => e.Id == equipmentId);
        if (selected == null) return;

        _state = CampaignSystem.SelectEquipment(_state, selected, _rng);
        RouteToCurrentPhase();
    }

    private void OnShopSlotPurchased(int slotIndex)
    {
        if (_state.ShopSlots == null) return;
        var slot = _state.ShopSlots[slotIndex];

        // Remove-Card slot needs the player to choose which card; sub-flow via RemoveCard overlay.
        if (slot.Kind == ShopSlotKind.RemoveCard)
        {
            if (!ShopSystem.CanPurchase(_state, slotIndex)) return;
            _pendingShopRemoveSlot = slotIndex;
            _overlay.ShowRemoveCard(_state);
            return;
        }

        try
        {
            _state = ShopSystem.Purchase(_state, slotIndex, _rng);
        }
        catch (System.Exception e)
        {
            GD.Print($"Shop purchase failed: {e.Message}");
        }

        _overlay.ShowShop(_state); // refresh slots/copper after purchase
    }

    private void OnNapCardSelected(string cardId)
    {
        var napCard = _targeting.TargetCard;
        if (napCard == null) return;

        var retrievedCard = _state.ExhaustPile.FirstOrDefault(c => c.Id == cardId);
        if (retrievedCard == null) return;

        try
        {
            _state = CardEffectSystem.PlayNap(_state, napCard, retrievedCard, _rng);
            _targeting.Cancel();
            _overlay.Hide();
            CheckPostCardPlay();
        }
        catch (Exception e)
        {
            GD.Print($"Nap failed: {e.Message}");
        }
    }

    private void OnSkipPressed()
    {
        switch (_overlay.CurrentMode)
        {
            case OverlayMode.RemoveCard:
                // "Back" from remove card → return to whichever flow opened it
                if (_pendingShopRemoveSlot != null)
                {
                    _pendingShopRemoveSlot = null;
                    _overlay.ShowShop(_state);
                }
                else
                {
                    _overlay.ShowUpgradeReward(_state);
                }
                return;

            case OverlayMode.UpgradeReward:
                _state = CampaignSystem.SkipUpgrade(_state, _rng);
                RouteToCurrentPhase();
                return;

            case OverlayMode.EquipmentReward:
                _state = CampaignSystem.SkipEquipment(_state, _rng);
                RouteToCurrentPhase();
                return;

            case OverlayMode.Shop:
                _state = CampaignSystem.LeaveShop(_state, _rng);
                RouteToCurrentPhase();
                return;

            case OverlayMode.NapSelection:
                HideNapOverlay();
                return;

            case OverlayMode.CardReward:
                _state = CampaignSystem.SkipCardReward(_state, _rng);
                RouteToCurrentPhase();
                return;

            case OverlayMode.PileView:
                _overlay.Hide();
                return;
        }
    }

    private void OnPlayAgain()
    {
        StartNewCampaign();
    }

    // ───────────────────────────────────────────────
    // Tile events
    // ───────────────────────────────────────────────

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

        if (_targeting.IsTargeting) return;

        try
        {
            var result = GameRunner.ProcessReveal(_state, pos, _rng);
            _state = result.State;
            RefreshUI();

            if (result.GameOver)
                HandleGameOver();
        }
        catch (InvalidOperationException)
        {
            // Already revealed or invalid
        }
    }

    private void OnTileHovered(int row, int col)
    {
        if (!_targeting.IsTargeting || _targeting.Mode != TargetingMode.TileTarget) return;

        var activeEffect = _targeting.GetActiveEffectType();
        var activeCard = _targeting.MaskSelectedCard ?? _targeting.TargetCard;
        var pos = new Position(row, col);

        var areaRadius = TargetingController.GetAreaRadius(activeEffect);
        if (areaRadius > 0)
        {
            if (activeEffect == CardEffectType.Peek && activeCard is { Enhanced: true })
                areaRadius = 1;
            _boardNode.SetAreaHighlight(pos, areaRadius, _state.Board);
        }
        else if (TargetingController.UsesCrossArea(activeEffect))
        {
            if (activeEffect == CardEffectType.Peek && activeCard is { Enhanced: true })
                _boardNode.SetAreaHighlight(pos, 1, _state.Board);
            else
                _boardNode.SetCrossHighlight(pos, _state.Board);
        }
    }

    private void OnTileUnhovered(int row, int col)
    {
        if (!_targeting.IsTargeting) return;
        _boardNode.ClearAreaHighlight();
    }

    private void OnAnnotationTypeChanged(int ownerIndex)
    {
        RefreshUI();
    }

    private void OnViewPileRequested(string pileName)
    {
        if (_overlay.IsVisible) return; // don't open pile view over another overlay

        var (title, cards) = pileName switch
        {
            "draw" => ("Draw Pile", _state.DrawPile.ToList()),
            "discard" => ("Discard Pile", _state.DiscardPile.ToList()),
            "exhaust" => ("Exhaust Pile", _state.ExhaustPile.ToList()),
            _ => ("Pile", new System.Collections.Generic.List<Card>())
        };
        _overlay.ShowPileView(title, cards);
    }

    private void OnTileRightClicked(int row, int col)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

        var pos = new Position(row, col);
        var tile = _state.Board.GetTile(pos);
        if (tile.IsRevealed || tile.IsDestroyed) return;
        if (!_state.Board.IsUsablePosition(pos)) return;

        var ownerType = _hud.SelectedAnnotationType;
        _state = AnnotationSystem.CyclePlayerAnnotation(_state, pos, ownerType);
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

    // ───────────────────────────────────────────────
    // Card play
    // ───────────────────────────────────────────────

    private void OnCardClicked(string cardId)
    {
        if (_state.GameStatus != GameStatus.Playing) return;
        if (_state.GamePhase != GamePhase.Playing) return;

        if (_targeting.IsTargeting && _targeting.Mode == TargetingMode.HandCardTarget)
        {
            HandleMaskCardSelection(cardId);
            return;
        }

        if (_targeting.IsTargeting)
            CancelTargeting();

        var card = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        if (!DeckSystem.CanPlayCard(_state, card))
            return;

        if (card.EffectType == CardEffectType.Mask)
        {
            _targeting.BeginHandCardTargeting(card);
            UpdateTargetingUI();
            return;
        }

        if (card.EffectType == CardEffectType.Nap)
        {
            if (_state.ExhaustPile.Count == 0)
            {
                PlayNapDirect(card, null);
                return;
            }
            _targeting.BeginExhaustCardTargeting(card);
            _overlay.ShowNapSelection(_state);
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
        if (cardId == _targeting.TargetCard!.Id) return;

        var selectedCard = _state.Hand.FirstOrDefault(c => c.Id == cardId);
        if (selectedCard == null) return;

        _targeting.TransitionToMaskedCardTargeting(selectedCard);

        if (TargetingController.RequiresTargeting(selectedCard.EffectType))
            UpdateTargetingUI();
        else
            ExecuteMaskedCard(null);
    }

    private void ExecuteTargetedCard()
    {
        var card = _targeting.TargetCard!;
        var targets = _targeting.GetTargets();

        if (_targeting.MaskSelectedCard != null)
        {
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

    private void CheckPostCardPlay()
    {
        var status = TurnSystem.CheckGameStatus(_state);
        _state = _state with { GameStatus = status };
        RefreshUI();

        if (status != GameStatus.Playing)
            HandleGameOver();
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
                HandleGameOver();
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
            CancelTargeting();

        try
        {
            var result = GameRunner.ProcessEndTurn(_state, _rng);
            _state = result.State;
            RefreshUI();

            if (result.GameOver)
                HandleGameOver();
        }
        catch (InvalidOperationException e)
        {
            GD.Print($"Cannot end turn: {e.Message}");
        }
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

    private void HideNapOverlay()
    {
        CancelTargeting();
        _overlay.Hide();
        RefreshUI();
    }

    // ───────────────────────────────────────────────
    // Debug card picker
    // ───────────────────────────────────────────────

    private void ToggleDebugCardPicker()
    {
        if (_state.GameStatus != GameStatus.Playing) return;

        if (_debugOverlay != null && _debugOverlay.Visible)
        {
            _debugOverlay.Visible = false;
            return;
        }

        ShowDebugCardPicker();
    }

    private void ShowDebugCardPicker()
    {
        if (_debugOverlay != null)
        {
            _debugOverlay.QueueFree();
            _debugOverlay = null!;
        }

        _debugOverlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.6f),
            Visible = true
        };
        _debugOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _debugOverlay.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_debugOverlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        _debugOverlay.AddChild(center);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.13f, 0.95f),
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        panel.CustomMinimumSize = new Vector2(360, 500);
        center.AddChild(panel);

        var outerVBox = new VBoxContainer();
        outerVBox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(outerVBox);

        var title = new Label
        {
            Text = "Debug: Add Card to Hand",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        outerVBox.AddChild(title);

        var checkRow = new HBoxContainer();
        checkRow.AddThemeConstantOverride("separation", 16);
        checkRow.Alignment = BoxContainer.AlignmentMode.Center;
        outerVBox.AddChild(checkRow);

        var enhancedCheck = new CheckBox { Text = "Enhanced" };
        checkRow.AddChild(enhancedCheck);

        var bonusSpoonCheck = new CheckBox { Text = "Bonus Spoon" };
        checkRow.AddChild(bonusSpoonCheck);

        var spoonButton = new Button { Text = "Refill Spoons (10)" };
        spoonButton.Pressed += () =>
        {
            _state = _state with { Spoons = 10, MaxSpoons = 10 };
            RefreshUI();
        };
        outerVBox.AddChild(spoonButton);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(320, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        outerVBox.AddChild(scroll);

        var cardList = new VBoxContainer();
        cardList.AddThemeConstantOverride("separation", 4);
        cardList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(cardList);

        var allCards = CardDefinitions.CreateRewardPool()
            .Concat(CardDefinitions.CreateStarterDeck())
            .GroupBy(c => c.Name)
            .Select(g => g.First())
            .OrderBy(c => c.Name)
            .ToList();

        foreach (var template in allCards)
        {
            var btn = new Button
            {
                Text = $"{template.Name} ({template.Cost})",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            var cardTemplate = template;
            btn.Pressed += () =>
            {
                var card = cardTemplate with
                {
                    Id = $"debug_{_debugCardIdCounter++}",
                    Enhanced = enhancedCheck.ButtonPressed,
                    BonusSpoon = bonusSpoonCheck.ButtonPressed
                };
                _state = _state with
                {
                    Hand = _state.Hand.Concat([card]).ToList()
                };
                RefreshUI();
            };
            cardList.AddChild(btn);
        }

        var closeBtn = new Button { Text = "Close" };
        closeBtn.Pressed += () => _debugOverlay.Visible = false;
        outerVBox.AddChild(closeBtn);
    }
}
