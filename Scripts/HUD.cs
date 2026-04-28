using System.Linq;
using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Displays game status information: spoons, deck/discard/exhaust counts,
/// turn indicator, tile counts, status effects, game status. Includes End Turn button.
/// </summary>
public partial class HUD : VBoxContainer
{
    [Signal]
    public delegate void EndTurnPressedEventHandler();

    [Signal]
    public delegate void AnnotationTypeChangedEventHandler(int ownerIndex);

    private Label _spoonsLabel = null!;
    private Label _deckLabel = null!;
    private Label _turnLabel = null!;
    private Label _statusLabel = null!;
    private Label _copperLabel = null!;
    private Label _floorLabel = null!;
    private Label _statusEffectsLabel = null!;
    private Button _endTurnButton = null!;

    // Annotation type selection buttons
    private HBoxContainer _annotationButtonsRow = null!;
    private Button[] _annotationButtons = new Button[4];
    private Label[] _annotationCountLabels = new Label[4];
    private TileOwner _selectedAnnotationType = TileOwner.Player;

    public TileOwner SelectedAnnotationType => _selectedAnnotationType;

    private static readonly Color[] AnnotationButtonColors =
    [
        new(1.0f, 0.55f, 0.65f),   // Player - pink
        new(0.45f, 0.65f, 1.0f),   // Rival - blue
        new(0.85f, 0.85f, 0.85f),  // Neutral - gray
        new(0.7f, 0.4f, 0.85f),    // Noble - purple
    ];

    public override void _Ready()
    {
        // Build UI programmatically
        var titleLabel = new Label { Text = "Maidsweeper" };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        AddChild(titleLabel);

        AddChild(new HSeparator());

        _turnLabel = new Label { Text = "Your Turn" };
        _turnLabel.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_turnLabel);

        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_statusLabel);

        _floorLabel = new Label { Text = "Floor 1/8" };
        _floorLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_floorLabel);

        AddChild(new HSeparator());

        _spoonsLabel = new Label { Text = "Spoons: 3 / 3" };
        AddChild(_spoonsLabel);

        _copperLabel = new Label { Text = "Copper: 0" };
        AddChild(_copperLabel);

        _deckLabel = new Label { Text = "Deck: 5 | Discard: 0 | Exhaust: 0" };
        AddChild(_deckLabel);

        _statusEffectsLabel = new Label { Text = "" };
        _statusEffectsLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        AddChild(_statusEffectsLabel);

        AddChild(new HSeparator());

        // Annotation type selection: colored square buttons with counts
        var annotationLabel = new Label { Text = "Annotate:" };
        annotationLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(annotationLabel);

        _annotationButtonsRow = new HBoxContainer();
        _annotationButtonsRow.AddThemeConstantOverride("separation", 4);
        AddChild(_annotationButtonsRow);

        var ownerNames = new[] { "P", "R", "N", "X" };
        for (var i = 0; i < 4; i++)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);

            var btn = new Button
            {
                CustomMinimumSize = new Vector2(40, 40),
                Text = ownerNames[i],
                ToggleMode = true,
                ButtonPressed = i == 0 // Player selected by default
            };

            var style = new StyleBoxFlat
            {
                BgColor = AnnotationButtonColors[i].Darkened(0.3f),
                ContentMarginLeft = 4, ContentMarginRight = 4,
                ContentMarginTop = 4, ContentMarginBottom = 4
            };
            btn.AddThemeStyleboxOverride("normal", style);

            var pressedStyle = new StyleBoxFlat
            {
                BgColor = AnnotationButtonColors[i],
                ContentMarginLeft = 4, ContentMarginRight = 4,
                ContentMarginTop = 4, ContentMarginBottom = 4,
                BorderWidthBottom = 3, BorderWidthTop = 3,
                BorderWidthLeft = 3, BorderWidthRight = 3,
                BorderColor = Colors.White
            };
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            var capturedIndex = i;
            btn.Pressed += () => OnAnnotationButtonPressed(capturedIndex);
            _annotationButtons[i] = btn;
            vbox.AddChild(btn);

            var countLabel = new Label
            {
                Text = "0",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            countLabel.AddThemeFontSizeOverride("font_size", 11);
            countLabel.AddThemeColorOverride("font_color", AnnotationButtonColors[i]);
            _annotationCountLabels[i] = countLabel;
            vbox.AddChild(countLabel);

            _annotationButtonsRow.AddChild(vbox);
        }

        AddChild(new HSeparator());

        _endTurnButton = new Button { Text = "End Turn" };
        _endTurnButton.Pressed += () => EmitSignal(SignalName.EndTurnPressed);
        AddChild(_endTurnButton);
    }

    public void UpdateFromState(GameState state)
    {
        _spoonsLabel.Text = $"Spoons: {state.Spoons} / {state.MaxSpoons}";
        _copperLabel.Text = $"Copper: {state.Copper}";
        _deckLabel.Text = $"Deck: {state.DrawPile.Count} | Discard: {state.DiscardPile.Count} | Exhaust: {state.ExhaustPile.Count}";

        var floorNum = GetFloorNumber(state.CurrentLevelId);
        _floorLabel.Text = $"Floor {floorNum}/8";

        _turnLabel.Text = state.CurrentPlayer == PlayerType.Player ? "Your Turn" : "Rival Turn";

        // Tile counts (unrevealed only, excluding unused and destroyed)
        var unrevealed = new int[4]; // Player, Rival, Neutral, Noble
        foreach (var tile in state.Board.Tiles)
        {
            if (!state.Board.IsUsablePosition(tile.Position)) continue;
            if (tile.IsDestroyed) continue;
            if (!tile.IsRevealed)
            {
                unrevealed[(int)tile.Owner]++;
            }
        }

        for (var i = 0; i < 4; i++)
        {
            _annotationCountLabels[i].Text = unrevealed[i].ToString();
        }

        // Status effects (only show non-zero)
        var effects = new System.Collections.Generic.List<string>();
        if (state.ExcusesStacks > 0)
            effects.Add($"Excuses: {state.ExcusesStacks}");
        if (state.ComplaintsStacks > 0)
            effects.Add($"Complaints: {state.ComplaintsStacks}");
        if (state.DistractionStacks > 0)
            effects.Add($"Distraction: {state.DistractionStacks}");
        if (state.AcceptHelpDiscount)
            effects.Add("Accept Help Discount");
        _statusEffectsLabel.Text = effects.Count > 0 ? string.Join("\n", effects) : "";
        _statusEffectsLabel.Visible = effects.Count > 0;

        // Game status
        _statusLabel.Text = state.GameStatus switch
        {
            GameStatus.Won => "YOU WIN!",
            GameStatus.Lost => "GAME OVER",
            _ => $"Turn {state.TurnNumber}"
        };

        // Disable End Turn when game over or not player's turn
        _endTurnButton.Disabled = state.GameStatus != GameStatus.Playing
            || state.CurrentPlayer != PlayerType.Player;
    }

    private void OnAnnotationButtonPressed(int index)
    {
        _selectedAnnotationType = (TileOwner)index;

        // Update toggle state: only the pressed one is active
        for (var i = 0; i < 4; i++)
        {
            _annotationButtons[i].SetPressedNoSignal(i == index);
        }

        EmitSignal(SignalName.AnnotationTypeChanged, index);
    }

    public static int GetFloorNumber(string levelId)
    {
        // Parse "level1" → 1, "level2" → 2, etc.
        if (levelId.StartsWith("level") && int.TryParse(levelId.Substring(5), out var num))
            return num;
        return 0;
    }
}
