using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Lightweight click-anywhere-to-dismiss popup shown when the player right-clicks a card.
/// Displays the card's name and help-variant text from CardTextLoader.
/// </summary>
public partial class CardHelpPopup : ColorRect
{
    private Label _title = null!;
    private Label _body = null!;

    public bool IsShown => Visible;

    public override void _Ready()
    {
        Color = new Color(0, 0, 0, 0.55f);
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.97f),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.85f, 0.7f, 0.15f)
        };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        panel.CustomMinimumSize = new Vector2(420, 0);
        panel.MouseFilter = MouseFilterEnum.Stop;
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(_title);

        _body = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0)
        };
        _body.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_body);

        var hint = new Label
        {
            Text = "Click anywhere or press Esc to close",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 10);
        hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(hint);
    }

    public void Show(Card card)
    {
        _title.Text = card.Name + (card.Enhanced ? " (Enhanced)" : "");
        _body.Text = CardTextLoader.GetHelp(card.Name, card.Description);
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true })
        {
            Hide();
            AcceptEvent();
        }
    }
}
