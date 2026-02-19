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

    private Label _spoonsLabel = null!;
    private Label _deckLabel = null!;
    private Label _turnLabel = null!;
    private Label _tileCountsLabel = null!;
    private Label _statusLabel = null!;
    private Label _copperLabel = null!;
    private Label _floorLabel = null!;
    private Label _statusEffectsLabel = null!;
    private Button _endTurnButton = null!;

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

        _tileCountsLabel = new Label { Text = "" };
        AddChild(_tileCountsLabel);

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
        var destroyedCount = 0;
        foreach (var tile in state.Board.Tiles)
        {
            if (!state.Board.IsUsablePosition(tile.Position)) continue;
            if (tile.IsDestroyed)
            {
                destroyedCount++;
                continue;
            }
            if (!tile.IsRevealed)
            {
                unrevealed[(int)tile.Owner]++;
            }
        }

        var tileText =
            $"Unrevealed Tiles:\n" +
            $"  Player: {unrevealed[(int)TileOwner.Player]}\n" +
            $"  Rival: {unrevealed[(int)TileOwner.Rival]}\n" +
            $"  Neutral: {unrevealed[(int)TileOwner.Neutral]}\n" +
            $"  Noble: {unrevealed[(int)TileOwner.Noble]}";
        if (destroyedCount > 0)
            tileText += $"\n  Destroyed: {destroyedCount}";
        _tileCountsLabel.Text = tileText;

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

    public static int GetFloorNumber(string levelId)
    {
        // Parse "level1" → 1, "level2" → 2, etc.
        if (levelId.StartsWith("level") && int.TryParse(levelId.Substring(5), out var num))
            return num;
        return 0;
    }
}
