using Godot;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Visual representation of a single card in the hand.
/// Draws card background, name, cost badge, description,
/// and upgrade indicators (Enhanced, BonusSpoon).
/// </summary>
public partial class CardUI : Control
{
    [Signal]
    public delegate void CardClickedEventHandler(string cardId);

    private static readonly Vector2 CardSize = new(100, 140);

    private static readonly Color CardBg = new(0.95f, 0.92f, 0.85f);
    private static readonly Color CardBgDimmed = new(0.7f, 0.68f, 0.65f);
    private static readonly Color CardBgMollify = new(0.95f, 0.82f, 0.82f);
    private static readonly Color CardBgMollifyDimmed = new(0.75f, 0.62f, 0.62f);
    private static readonly Color CardBorder = new(0.4f, 0.35f, 0.3f);
    private static readonly Color CardBorderSelected = new(0.9f, 0.7f, 0.1f);
    private static readonly Color CardBorderEnhanced = new(0.85f, 0.7f, 0.15f);
    private static readonly Color CostBadgeBg = new(0.2f, 0.4f, 0.7f);
    private static readonly Color TextColor = new(0.15f, 0.15f, 0.15f);
    private static readonly Color TextDimmed = new(0.5f, 0.5f, 0.5f);
    private static readonly Color ExhaustColor = new(0.6f, 0.2f, 0.2f);
    private static readonly Color EnhancedColor = new(0.85f, 0.7f, 0.15f);
    private static readonly Color BonusSpoonColor = new(0.3f, 0.7f, 0.4f);

    private Card _card = null!;
    private bool _affordable = true;
    private bool _selected;
    private bool _hovered;

    public Card Card => _card;

    public override void _Ready()
    {
        CustomMinimumSize = CardSize;
        Size = CardSize;
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, CardSize);

        // Background (red tint for Mollify)
        Color bg;
        if (_card.EffectType == CardEffectType.Mollify)
            bg = _affordable ? CardBgMollify : CardBgMollifyDimmed;
        else
            bg = _affordable ? CardBg : CardBgDimmed;
        DrawRect(rect, bg);

        // Border: gold+thick for enhanced, yellow for selected, default otherwise
        Color borderColor;
        float borderWidth;
        if (_card.Enhanced)
        {
            borderColor = _selected ? CardBorderSelected : CardBorderEnhanced;
            borderWidth = 3.0f;
        }
        else if (_selected || _hovered)
        {
            borderColor = _selected ? CardBorderSelected : CardBorder;
            borderWidth = 3.0f;
        }
        else
        {
            borderColor = CardBorder;
            borderWidth = 1.0f;
        }
        DrawRect(rect, borderColor, false, borderWidth);

        var font = ThemeDB.FallbackFont;
        var textCol = _affordable ? TextColor : TextDimmed;

        // Cost badge (top-left circle)
        var badgeCenter = new Vector2(16, 16);
        DrawCircle(badgeCenter, 12, CostBadgeBg);
        var costText = _card.Cost.ToString();
        var costSize = font.GetStringSize(costText, fontSize: 16);
        DrawString(font, badgeCenter - new Vector2(costSize.X / 2, -5), costText,
            fontSize: 16, modulate: Colors.White);

        // Enhanced indicator: "E+" top-right corner
        if (_card.Enhanced)
        {
            DrawString(font, new Vector2(CardSize.X - 24, 14), "E+",
                fontSize: 11, modulate: EnhancedColor);
        }

        // Card name (centered, below badge)
        var nameSize = font.GetStringSize(_card.Name, fontSize: 12);
        var nameX = (CardSize.X - nameSize.X) / 2;
        DrawString(font, new Vector2(nameX, 44), _card.Name, fontSize: 12, modulate: textCol);

        // Description (wrapped manually — just truncate for now)
        var desc = _card.Description;
        var descFontSize = 10;
        var lineY = 64f;
        foreach (var line in WrapText(font, desc, descFontSize, CardSize.X - 12))
        {
            DrawString(font, new Vector2(6, lineY), line, fontSize: descFontSize, modulate: textCol);
            lineY += 14;
            if (lineY > CardSize.Y - 20) break;
        }

        // Bottom row: Exhaust indicator (left) and BonusSpoon indicator (right)
        if (_card.Exhaust)
        {
            DrawString(font, new Vector2(6, CardSize.Y - 8), "Exhaust",
                fontSize: 10, modulate: ExhaustColor);
        }

        // BonusSpoon indicator: small green circle bottom-right
        if (_card.BonusSpoon)
        {
            var spoonCenter = new Vector2(CardSize.X - 14, CardSize.Y - 14);
            DrawCircle(spoonCenter, 6, BonusSpoonColor);
            DrawString(font, spoonCenter - new Vector2(3, -3), "S",
                fontSize: 8, modulate: Colors.White);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            EmitSignal(SignalName.CardClicked, _card.Id);
            AcceptEvent();
        }
    }

    public void Setup(Card card, bool affordable)
    {
        _card = card;
        _affordable = affordable;
        _selected = false;
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public void SetAffordable(bool affordable)
    {
        _affordable = affordable;
        QueueRedraw();
    }

    private static string[] WrapText(Font font, string text, int fontSize, float maxWidth)
    {
        var words = text.Split(' ');
        var lines = new System.Collections.Generic.List<string>();
        var current = "";

        foreach (var word in words)
        {
            var test = current.Length == 0 ? word : current + " " + word;
            if (font.GetStringSize(test, fontSize: fontSize).X > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines.ToArray();
    }
}
