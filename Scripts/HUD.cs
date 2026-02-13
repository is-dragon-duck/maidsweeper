using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Displays game status information: energy, deck/discard counts,
/// turn indicator, tile counts, game status. Includes End Turn button.
/// </summary>
public partial class HUD : VBoxContainer
{
    [Signal]
    public delegate void EndTurnPressedEventHandler();

    private Label _energyLabel = null!;
    private Label _deckLabel = null!;
    private Label _turnLabel = null!;
    private Label _tileCountsLabel = null!;
    private Label _statusLabel = null!;
    private Label _copperLabel = null!;
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

        AddChild(new HSeparator());

        _energyLabel = new Label { Text = "Energy: 3 / 3" };
        AddChild(_energyLabel);

        _copperLabel = new Label { Text = "Copper: 0" };
        AddChild(_copperLabel);

        _deckLabel = new Label { Text = "Deck: 5 | Discard: 0" };
        AddChild(_deckLabel);

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
        _energyLabel.Text = $"Energy: {state.Energy} / {state.MaxEnergy}";
        _copperLabel.Text = $"Copper: {state.Copper}";
        _deckLabel.Text = $"Deck: {state.DrawPile.Count} | Discard: {state.DiscardPile.Count}";

        _turnLabel.Text = state.CurrentPlayer == PlayerType.Player ? "Your Turn" : "Rival Turn";

        // Tile counts (unrevealed only)
        var unrevealed = new int[4]; // Player, Rival, Neutral, Noble
        foreach (var tile in state.Board.Tiles)
        {
            if (!tile.IsRevealed)
            {
                unrevealed[(int)tile.Owner]++;
            }
        }
        _tileCountsLabel.Text =
            $"Unrevealed Tiles:\n" +
            $"  Player: {unrevealed[(int)TileOwner.Player]}\n" +
            $"  Rival: {unrevealed[(int)TileOwner.Rival]}\n" +
            $"  Neutral: {unrevealed[(int)TileOwner.Neutral]}\n" +
            $"  Noble: {unrevealed[(int)TileOwner.Noble]}";

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
}
