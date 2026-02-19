using System.Collections.Generic;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Manages card targeting state: which card needs targets, how many,
/// and which positions have been selected so far.
/// Supports tile targeting, hand card targeting (Mask), and exhaust card targeting (Nap).
/// Pure logic — no Godot dependencies.
/// </summary>
public class TargetingController
{
    public bool IsTargeting { get; private set; }
    #nullable enable
    public Card? TargetCard { get; private set; }
    #nullable restore
    public int TargetsNeeded { get; private set; }
    public List<Position> SelectedTargets { get; } = new();
    public TargetingMode Mode { get; private set; } = TargetingMode.TileTarget;

    // For Mask: the card selected from hand to play through Mask
    #nullable enable
    public Card? MaskSelectedCard { get; set; }
    #nullable restore

    public string TargetingMessage => Mode switch
    {
        TargetingMode.HandCardTarget => "Select a card from your hand",
        TargetingMode.ExhaustCardTarget => "Select a card from exhaust pile",
        TargetingMode.TileTarget => TargetsNeeded switch
        {
            1 => "Select a tile",
            2 when SelectedTargets.Count == 0 => "Select first tile",
            2 when SelectedTargets.Count == 1 => "Select second tile",
            _ => ""
        },
        _ => ""
    };

    public bool IsComplete => Mode == TargetingMode.TileTarget && SelectedTargets.Count >= TargetsNeeded;

    /// <summary>
    /// Returns the number of targets required for a card effect, or 0 for immediate effects.
    /// </summary>
    public static int GetTargetCount(CardEffectType effectType) => effectType switch
    {
        CardEffectType.Spritz => 1,
        CardEffectType.Scurry => 2,
        CardEffectType.Brush => 1,
        CardEffectType.Sweep => 1,
        CardEffectType.Argue => 1,
        CardEffectType.Eavesdrop => 1,
        CardEffectType.Peek => 1,
        CardEffectType.Explode => 1,
        CardEffectType.Deliver => 1,
        CardEffectType.AcceptHelp => 1,
        CardEffectType.Brat => 1,
        _ => 0 // Recall, Tingle, Twirl, Caffeinate, Breathe, LockIn, Rendezvous, Ramble, Glaze, Mollify, Mask, Nap are immediate/special
    };

    /// <summary>
    /// Returns the area radius for area-effect cards (0 for non-area cards).
    /// </summary>
    public static int GetAreaRadius(CardEffectType effectType) => effectType switch
    {
        CardEffectType.Brush => 1,  // 3x3
        CardEffectType.Sweep => 2,  // 5x5
        CardEffectType.Argue => 1,  // 3x3
        _ => 0
    };

    /// <summary>
    /// Returns true if this effect uses cross-shaped area (Peek, AcceptHelp).
    /// Enhanced Peek uses 3x3 area instead of cross, handled separately.
    /// </summary>
    public static bool UsesCrossArea(CardEffectType effectType) => effectType switch
    {
        CardEffectType.Peek => true,
        CardEffectType.AcceptHelp => true,
        _ => false
    };

    /// <summary>
    /// Returns true if this effect type requires tile targeting (not immediate or card-selection).
    /// </summary>
    public static bool RequiresTargeting(CardEffectType effectType) => GetTargetCount(effectType) > 0;

    /// <summary>
    /// Returns true if this card targets revealed tiles instead of unrevealed (Brat).
    /// </summary>
    public static bool TargetsRevealed(CardEffectType effectType) => effectType == CardEffectType.Brat;

    /// <summary>
    /// Returns true if this card uses card-selection mode (Mask, Nap).
    /// </summary>
    public static bool RequiresCardSelection(CardEffectType effectType) => effectType switch
    {
        CardEffectType.Mask => true,
        CardEffectType.Nap => true,
        _ => false
    };

    public void BeginTargeting(Card card)
    {
        TargetCard = card;
        TargetsNeeded = GetTargetCount(card.EffectType);
        SelectedTargets.Clear();
        MaskSelectedCard = null;
        IsTargeting = true;
        Mode = TargetingMode.TileTarget;
    }

    public void BeginHandCardTargeting(Card card)
    {
        TargetCard = card;
        TargetsNeeded = 0;
        SelectedTargets.Clear();
        MaskSelectedCard = null;
        IsTargeting = true;
        Mode = TargetingMode.HandCardTarget;
    }

    public void BeginExhaustCardTargeting(Card card)
    {
        TargetCard = card;
        TargetsNeeded = 0;
        SelectedTargets.Clear();
        MaskSelectedCard = null;
        IsTargeting = true;
        Mode = TargetingMode.ExhaustCardTarget;
    }

    /// <summary>
    /// After Mask selects a hand card, transition to tile targeting for that card's effect
    /// (if it requires targets), or mark as complete (if immediate).
    /// </summary>
    public void TransitionToMaskedCardTargeting(Card selectedCard)
    {
        MaskSelectedCard = selectedCard;
        if (RequiresTargeting(selectedCard.EffectType))
        {
            TargetsNeeded = GetTargetCount(selectedCard.EffectType);
            SelectedTargets.Clear();
            Mode = TargetingMode.TileTarget;
        }
        // If the selected card doesn't need targets, caller should execute immediately
    }

    public bool TrySelectTarget(Position pos, GameState state)
    {
        if (!IsTargeting || Mode != TargetingMode.TileTarget) return false;

        var tile = state.Board.GetTile(pos);

        // Brat targets revealed tiles; everything else targets unrevealed
        if (TargetsRevealed(GetActiveEffectType()))
        {
            if (!tile.IsRevealed) return false;
        }
        else
        {
            if (tile.IsRevealed) return false;
        }

        // Can't target destroyed tiles
        if (tile.IsDestroyed) return false;

        // Can't select same tile twice
        if (SelectedTargets.Contains(pos)) return false;

        SelectedTargets.Add(pos);
        return true;
    }

    public void Cancel()
    {
        IsTargeting = false;
        TargetCard = null;
        TargetsNeeded = 0;
        MaskSelectedCard = null;
        SelectedTargets.Clear();
        Mode = TargetingMode.TileTarget;
    }

    public Position[] GetTargets()
    {
        return SelectedTargets.ToArray();
    }

    /// <summary>
    /// Gets the effect type currently being targeted.
    /// For Mask with a selected card, returns the selected card's effect type.
    /// </summary>
    public CardEffectType GetActiveEffectType()
    {
        if (MaskSelectedCard != null)
            return MaskSelectedCard.EffectType;
        return TargetCard?.EffectType ?? CardEffectType.Spritz;
    }
}

public enum TargetingMode
{
    TileTarget,
    HandCardTarget,
    ExhaustCardTarget
}
