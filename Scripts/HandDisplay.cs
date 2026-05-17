using Godot;
using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Scripts;

/// <summary>
/// Displays the player's hand as a row of CardUI nodes.
/// Bottom-anchored HBoxContainer.
/// </summary>
public partial class HandDisplay : HBoxContainer
{
    [Signal]
    public delegate void CardClickedEventHandler(string cardId);

    [Signal]
    public delegate void CardRightClickedEventHandler(string cardId);

    public void UpdateHand(GameState state)
    {
        // Clear existing cards
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        // Create a CardUI for each card in hand
        foreach (var card in state.Hand)
        {
            var cardUI = new CardUI();
            AddChild(cardUI);
            var effectiveCost = DeckSystem.GetEffectiveCost(state, card);
            var affordable = state.Spoons >= effectiveCost;
            cardUI.Setup(card, affordable, effectiveCost);
            cardUI.CardClicked += OnCardClicked;
            cardUI.CardRightClicked += OnCardRightClicked;
        }
    }

    #nullable enable
    public void SetSelectedCard(string? cardId)
    #nullable restore
    {
        foreach (var child in GetChildren())
        {
            if (child is CardUI cardUI)
            {
                cardUI.SetSelected(cardUI.Card.Id == cardId);
            }
        }
    }

    public void ClearSelection()
    {
        SetSelectedCard(null);
    }

    private void OnCardClicked(string cardId)
    {
        EmitSignal(SignalName.CardClicked, cardId);
    }

    private void OnCardRightClicked(string cardId)
    {
        EmitSignal(SignalName.CardRightClicked, cardId);
    }
}
