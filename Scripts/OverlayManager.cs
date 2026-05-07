using System.Collections.Generic;
using System.Linq;
using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Manages the shared overlay UI: game-over, card reward, upgrade reward,
/// remove card, nap card selection, and victory screens.
/// Emits signals for user selections; GameController handles state changes.
/// </summary>
public partial class OverlayManager : RefCounted
{
    [Signal]
    public delegate void RewardCardSelectedEventHandler(string cardId);

    [Signal]
    public delegate void UpgradeSelectedEventHandler(int optionIndex);

    [Signal]
    public delegate void RemoveCardSelectedEventHandler(string cardId);

    [Signal]
    public delegate void NapCardSelectedEventHandler(string cardId);

    [Signal]
    public delegate void EquipmentSelectedEventHandler(string equipmentId);

    [Signal]
    public delegate void ShopSlotPurchasedEventHandler(int slotIndex);

    [Signal]
    public delegate void SkipPressedEventHandler();

    [Signal]
    public delegate void PlayAgainPressedEventHandler();

    public OverlayMode CurrentMode { get; private set; }

    // Shared overlay nodes
    private ColorRect _dim = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _vbox = null!;
    private Label _title = null!;
    private Label _details = null!;
    private ScrollContainer _cardsScroll = null!;
    private HBoxContainer _cardsRow = null!;
    private VBoxContainer _upgradeButtons = null!;
    private Button _skipButton = null!;
    private Button _playAgainButton = null!;

    public bool IsVisible => _dim.Visible;

    /// <summary>
    /// Builds the overlay DOM and adds it as a child of the given parent.
    /// </summary>
    public void Build(Control parent)
    {
        _dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.5f),
            Visible = false
        };
        _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _dim.MouseFilter = Control.MouseFilterEnum.Stop;
        parent.AddChild(_dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        _dim.AddChild(center);

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
        _panel = new PanelContainer();
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _panel.CustomMinimumSize = new Vector2(400, 0);
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft); // don't stretch
        center.AddChild(_panel);

        _vbox = new VBoxContainer();
        _vbox.AddThemeConstantOverride("separation", 16);
        _vbox.Alignment = BoxContainer.AlignmentMode.Center;
        _panel.AddChild(_vbox);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 32);
        _vbox.AddChild(_title);

        _details = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _details.AddThemeFontSizeOverride("font_size", 16);
        _vbox.AddChild(_details);

        _cardsScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(600, 160) // fixed width so cards scroll; height for cards + scrollbar
        };
        _vbox.AddChild(_cardsScroll);

        _cardsRow = new HBoxContainer();
        _cardsRow.AddThemeConstantOverride("separation", 12);
        _cardsRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _cardsScroll.AddChild(_cardsRow);

        _upgradeButtons = new VBoxContainer();
        _upgradeButtons.AddThemeConstantOverride("separation", 8);
        _upgradeButtons.Visible = false;
        _vbox.AddChild(_upgradeButtons);

        _skipButton = new Button
        {
            Text = "Skip",
            CustomMinimumSize = new Vector2(100, 35),
            Visible = false
        };
        _skipButton.Pressed += () => EmitSignal(SignalName.SkipPressed);
        _vbox.AddChild(_skipButton);

        _playAgainButton = new Button
        {
            Text = "Play Again",
            CustomMinimumSize = new Vector2(150, 40)
        };
        _playAgainButton.Pressed += () => EmitSignal(SignalName.PlayAgainPressed);
        _vbox.AddChild(_playAgainButton);
    }

    public void Hide()
    {
        _dim.Visible = false;
        CurrentMode = OverlayMode.None;
    }

    // ───────────────────────────────────────────────
    // Overlay types
    // ───────────────────────────────────────────────

    public void ShowLoss(GameState state)
    {
        CurrentMode = OverlayMode.Loss;
        SetTitle("Game Over", new Color(0.9f, 0.3f, 0.25f));

        var revealedPlayer = state.Board.Tiles.Count(t =>
            state.Board.IsUsablePosition(t.Position) && t.IsRevealed && t.Owner == TileOwner.Player);
        var totalPlayer = state.Board.Tiles.Count(t =>
            state.Board.IsUsablePosition(t.Position) && t.Owner == TileOwner.Player);

        var floorNum = GetFloorNumber(state.CurrentLevelId);
        _details.Text = $"Floor {floorNum}: Found {revealedPlayer} of {totalPlayer} tiles ({state.TurnNumber} turns)";

        ShowEndButtons("Play Again");
    }

    public void ShowVictory(GameState state)
    {
        CurrentMode = OverlayMode.Victory;
        SetTitle("Campaign Complete!", new Color(0.3f, 0.9f, 0.4f));
        _details.Text = $"Deck size: {state.PersistentDeck.Count} cards";
        ShowEndButtons("Play Again");
    }

    #nullable enable
    public void ShowCardReward(GameState state, string? floorSummary = null)
    #nullable restore
    {
        CurrentMode = OverlayMode.CardReward;
        var floorNum = GetFloorNumber(state.CurrentLevelId);
        SetTitle($"Floor {floorNum} Cleared!", new Color(0.3f, 0.9f, 0.4f));
        _details.Text = ComposeDetails("Choose a card to add to your deck:", floorSummary);

        ClearCards();
        if (state.CardRewardOptions != null)
        {
            foreach (var card in state.CardRewardOptions)
            {
                var cardUI = new CardUI();
                _cardsRow.AddChild(cardUI);
                cardUI.Setup(card, true);
                cardUI.CardClicked += id => EmitSignal(SignalName.RewardCardSelected, id);
            }
        }

        _cardsScroll.Visible = true;
        _upgradeButtons.Visible = false;
        _skipButton.Visible = true;
        _skipButton.Text = "Skip";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    #nullable enable
    public void ShowUpgradeReward(GameState state, string? floorSummary = null)
    #nullable restore
    {
        CurrentMode = OverlayMode.UpgradeReward;
        SetTitle("Upgrade Your Deck", new Color(0.85f, 0.7f, 0.15f));
        _details.Text = ComposeDetails("Choose an upgrade:", floorSummary);

        _cardsScroll.Visible = false;
        ClearUpgradeButtons();

        if (state.UpgradeOptions != null)
        {
            for (var i = 0; i < state.UpgradeOptions.Count; i++)
            {
                var option = state.UpgradeOptions[i];
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

                var index = i;
                btn.Pressed += () => EmitSignal(SignalName.UpgradeSelected, index);
                _upgradeButtons.AddChild(btn);
            }
        }

        _upgradeButtons.Visible = true;
        _skipButton.Visible = true;
        _skipButton.Text = "Skip";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    public void ShowRemoveCard(GameState state)
    {
        CurrentMode = OverlayMode.RemoveCard;
        SetTitle("Remove a Card", new Color(0.9f, 0.5f, 0.3f));
        _details.Text = "Click a card to remove it from your deck:";

        ClearCards();
        ClearUpgradeButtons();

        foreach (var card in state.PersistentDeck)
        {
            var cardUI = new CardUI();
            _cardsRow.AddChild(cardUI);
            cardUI.Setup(card, true);
            cardUI.CardClicked += id => EmitSignal(SignalName.RemoveCardSelected, id);
        }

        _cardsScroll.Visible = true;
        _upgradeButtons.Visible = false;
        _skipButton.Visible = true;
        _skipButton.Text = "Back";
        _playAgainButton.Visible = false;
    }

    public void ShowNapSelection(GameState state)
    {
        CurrentMode = OverlayMode.NapSelection;
        SetTitle("Nap: Retrieve a Card", new Color(0.3f, 0.7f, 0.9f));
        _details.Text = "Choose a card from the exhaust pile:";

        ClearCards();
        ClearUpgradeButtons();

        foreach (var card in state.ExhaustPile)
        {
            var cardUI = new CardUI();
            _cardsRow.AddChild(cardUI);
            cardUI.Setup(card, true);
            cardUI.CardClicked += id => EmitSignal(SignalName.NapCardSelected, id);
        }

        _cardsScroll.Visible = true;
        _upgradeButtons.Visible = false;
        _skipButton.Visible = true;
        _skipButton.Text = "Cancel";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    #nullable enable
    public void ShowEquipmentReward(GameState state, string? floorSummary = null)
    #nullable restore
    {
        CurrentMode = OverlayMode.EquipmentReward;
        var floorNum = GetFloorNumber(state.CurrentLevelId);
        SetTitle($"Floor {floorNum} Cleared!", new Color(0.4f, 0.7f, 0.95f));
        _details.Text = ComposeDetails("Choose equipment for ongoing passive effects:", floorSummary);

        ClearCards();
        ClearUpgradeButtons();

        if (state.EquipmentOptions != null)
        {
            foreach (var equipment in state.EquipmentOptions)
            {
                var btn = new Button
                {
                    Text = $"{equipment.Name}\n{equipment.Description}",
                    CustomMinimumSize = new Vector2(360, 60),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                var captured = equipment;
                btn.Pressed += () => EmitSignal(SignalName.EquipmentSelected, captured.Id);
                _upgradeButtons.AddChild(btn);
            }
        }

        _cardsScroll.Visible = false;
        _upgradeButtons.Visible = true;
        _skipButton.Visible = true;
        _skipButton.Text = "Skip";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    #nullable enable
    public void ShowShop(GameState state, string? floorSummary = null)
    #nullable restore
    {
        CurrentMode = OverlayMode.Shop;
        var floorNum = GetFloorNumber(state.CurrentLevelId);
        SetTitle($"Floor {floorNum} — Shop", new Color(0.95f, 0.85f, 0.3f));
        var baseDetails = $"Copper: {state.Copper}";
        _details.Text = ComposeDetails(baseDetails, floorSummary);

        ClearCards();
        ClearUpgradeButtons();

        if (state.ShopSlots != null)
        {
            foreach (var slot in state.ShopSlots)
            {
                var label = ShopSlotLabel(slot);
                var btn = new Button
                {
                    Text = $"{label}  —  {slot.Price}c",
                    CustomMinimumSize = new Vector2(360, 35),
                    Disabled = !ShopSystem.CanPurchase(state, slot.Index) || slot.IsPurchased
                };
                if (slot.IsPurchased)
                    btn.Text = $"{label}  —  Sold";

                var capturedIdx = slot.Index;
                btn.Pressed += () => EmitSignal(SignalName.ShopSlotPurchased, capturedIdx);
                _upgradeButtons.AddChild(btn);
            }
        }

        _cardsScroll.Visible = false;
        _upgradeButtons.Visible = true;
        _skipButton.Visible = true;
        _skipButton.Text = "Done";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    private static string ShopSlotLabel(ShopSlot slot) => slot.Kind switch
    {
        ShopSlotKind.RegularCard when slot.Card != null => $"{slot.Card.Name} ({slot.Card.Cost})",
        ShopSlotKind.BonusSpoonCard when slot.Card != null => $"{slot.Card.Name} +Spoon ({slot.Card.Cost})",
        ShopSlotKind.EnhancedCard when slot.Card != null => $"{slot.Card.Name} Enhanced ({slot.Card.Cost})",
        ShopSlotKind.Equipment when slot.Equipment != null => $"{slot.Equipment.Name}",
        ShopSlotKind.RemoveCard => "Remove a Card",
        ShopSlotKind.VisitingBunny => "Visiting Bunny (reveal 1 next floor)",
        ShopSlotKind.Enhance => "Enhance a random card",
        _ => "(empty)"
    };

    public void ShowPileView(string title, List<Card> cards)
    {
        CurrentMode = OverlayMode.PileView;
        var titleColor = new Color(0.7f, 0.8f, 0.9f);
        SetTitle(title, titleColor);
        _details.Text = cards.Count == 0 ? "(empty)" : $"{cards.Count} card{(cards.Count == 1 ? "" : "s")}";

        ClearCards();
        ClearUpgradeButtons();

        foreach (var card in cards)
        {
            var cardUI = new CardUI();
            _cardsRow.AddChild(cardUI);
            cardUI.Setup(card, true);
            // No click action — view only
        }

        _cardsScroll.Visible = cards.Count > 0;
        _upgradeButtons.Visible = false;
        _skipButton.Visible = true;
        _skipButton.Text = "Close";
        _playAgainButton.Visible = false;
        _dim.Visible = true;
    }

    // ───────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────

    private void SetTitle(string text, Color color)
    {
        _title.Text = text;
        _title.AddThemeColorOverride("font_color", color);
    }

    #nullable enable
    private static string ComposeDetails(string baseLine, string? floorSummary)
    {
        return string.IsNullOrEmpty(floorSummary)
            ? baseLine
            : $"{floorSummary}\n{baseLine}";
    }
    #nullable restore

    private void ShowEndButtons(string buttonText)
    {
        _cardsScroll.Visible = false;
        _upgradeButtons.Visible = false;
        _skipButton.Visible = false;
        _playAgainButton.Visible = true;
        _playAgainButton.Text = buttonText;
        _dim.Visible = true;
    }

    private void ClearCards()
    {
        foreach (var child in _cardsRow.GetChildren())
            child.QueueFree();
    }

    private void ClearUpgradeButtons()
    {
        foreach (var child in _upgradeButtons.GetChildren())
            child.QueueFree();
    }

    private static int GetFloorNumber(string levelId) => HUD.GetFloorNumber(levelId);
}

public enum OverlayMode
{
    None,
    Loss,
    Victory,
    CardReward,
    UpgradeReward,
    EquipmentReward,
    Shop,
    RemoveCard,
    NapSelection,
    PileView
}
