using System.Collections.Generic;
using Maidsweeper.Core.Models;

namespace Maidsweeper.Scripts;

/// <summary>
/// Manages card targeting state: which card needs targets, how many,
/// and which positions have been selected so far.
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

    public string TargetingMessage => TargetsNeeded switch
    {
        1 => "Select a tile",
        2 when SelectedTargets.Count == 0 => "Select first tile",
        2 when SelectedTargets.Count == 1 => "Select second tile",
        _ => ""
    };

    public bool IsComplete => SelectedTargets.Count >= TargetsNeeded;

    /// <summary>
    /// Returns the number of targets required for a card effect, or 0 for immediate effects.
    /// </summary>
    public static int GetTargetCount(CardEffectType effectType) => effectType switch
    {
        CardEffectType.Spritz => 1,
        CardEffectType.Scurry => 2,
        _ => 0 // Recall, Tingle, Twirl are immediate
    };

    /// <summary>
    /// Returns true if this effect type requires targeting (not immediate).
    /// </summary>
    public static bool RequiresTargeting(CardEffectType effectType) => GetTargetCount(effectType) > 0;

    public void BeginTargeting(Card card)
    {
        TargetCard = card;
        TargetsNeeded = GetTargetCount(card.EffectType);
        SelectedTargets.Clear();
        IsTargeting = true;
    }

    public bool TrySelectTarget(Position pos, GameState state)
    {
        if (!IsTargeting) return false;

        // Must be unrevealed
        var tile = state.Board.GetTile(pos);
        if (tile.IsRevealed) return false;

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
        SelectedTargets.Clear();
    }

    public Position[] GetTargets()
    {
        return SelectedTargets.ToArray();
    }
}
