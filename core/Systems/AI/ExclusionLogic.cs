namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// Per-tile possible-owner flags. Mutated during constraint propagation.
/// </summary>
internal struct OwnerFlags
{
    public bool Player;
    public bool Rival;
    public bool Neutral;
    public bool Noble;

    public static OwnerFlags AllPossible() => new()
    {
        Player = true, Rival = true, Neutral = true, Noble = true
    };

    public static OwnerFlags FromKnownOwner(TileOwner owner) => new()
    {
        Player = owner == TileOwner.Player,
        Rival = owner == TileOwner.Rival,
        Neutral = owner == TileOwner.Neutral,
        Noble = owner == TileOwner.Noble
    };

    public bool IsExactly(TileOwner owner) =>
        owner switch
        {
            TileOwner.Player => Player && !Rival && !Neutral && !Noble,
            TileOwner.Rival => Rival && !Player && !Neutral && !Noble,
            TileOwner.Neutral => Neutral && !Player && !Rival && !Noble,
            TileOwner.Noble => Noble && !Player && !Rival && !Neutral,
            _ => false
        };
}

/// <summary>
/// Result of analyzing the board's revealed adjacency information.
/// </summary>
public sealed record ExclusionAnalysis
{
    public IReadOnlyList<Position> GuaranteedRivals { get; init; } = Array.Empty<Position>();
    public IReadOnlyCollection<Position> RuledOutRivals { get; init; } = Array.Empty<Position>();
}

/// <summary>
/// Constraint propagation over revealed-tile adjacency counts. Mirrors the alpha's
/// `exclusionLogic.ts`: each unrevealed tile starts with all four owners possible;
/// revealed tiles' counts iteratively rule out impossible owners until convergence.
/// </summary>
public static class ExclusionLogic
{
    public static ExclusionAnalysis Analyze(GameState state)
    {
        var flags = Initialize(state);
        PropagateUntilConvergence(state, flags);
        return ExtractResults(state, flags);
    }

    private static Dictionary<Position, OwnerFlags> Initialize(GameState state)
    {
        var result = new Dictionary<Position, OwnerFlags>();
        foreach (var tile in state.Board.Tiles)
        {
            if (!state.Board.IsUsablePosition(tile.Position)) continue;
            if (tile.IsDestroyed) continue;

            result[tile.Position] = tile.IsRevealed
                ? OwnerFlags.FromKnownOwner(tile.Owner)
                : OwnerFlags.AllPossible();
        }
        return result;
    }

    private static void PropagateUntilConvergence(
        GameState state,
        Dictionary<Position, OwnerFlags> flags)
    {
        const int maxIterations = 100;
        var iteration = 0;
        while (iteration < maxIterations && PropagateOnce(state, flags))
        {
            iteration++;
        }
    }

    /// <summary>
    /// One propagation pass over every revealed tile. Returns true if any flag changed.
    /// </summary>
    private static bool PropagateOnce(GameState state, Dictionary<Position, OwnerFlags> flags)
    {
        var anyChanged = false;

        foreach (var tile in state.Board.Tiles)
        {
            if (!tile.IsRevealed || tile.RevealedBy == null) continue;
            if (!state.Board.IsUsablePosition(tile.Position)) continue;
            if (tile.IsDestroyed) continue;

            var revealerOwner = tile.RevealedBy == PlayerType.Player
                ? TileOwner.Player
                : TileOwner.Rival;
            var requiredCount = tile.AdjacencyCount;

            var neighbors = BoardSystem.GetNeighbors(state.Board, tile.Position);

            // Count adjacency neighbors already revealed as the revealer's team
            var revealedAsTeam = 0;
            foreach (var n in neighbors)
            {
                var t = state.Board.GetTile(n);
                if (t.IsRevealed && t.Owner == revealerOwner) revealedAsTeam++;
            }

            // Unrevealed neighbors that could still be the revealer's team
            var unrevealedAdj = new List<Position>();
            var couldBeTeam = 0;
            foreach (var n in neighbors)
            {
                var t = state.Board.GetTile(n);
                if (t.IsRevealed) continue;
                unrevealedAdj.Add(n);
                if (flags.TryGetValue(n, out var nf) && IsTeam(nf, revealerOwner))
                    couldBeTeam++;
            }

            // Deduction 1: enough already revealed as team — none of the unrevealed can be team
            if (revealedAsTeam >= requiredCount)
            {
                foreach (var n in unrevealedAdj)
                {
                    if (!flags.TryGetValue(n, out var nf)) continue;
                    if (IsTeam(nf, revealerOwner))
                    {
                        SetTeam(ref nf, revealerOwner, false);
                        flags[n] = nf;
                        anyChanged = true;
                    }
                }
            }

            // Deduction 2: revealed + still-possible-team == required → all still-possible-team MUST be team
            if (revealedAsTeam + couldBeTeam == requiredCount && couldBeTeam > 0)
            {
                foreach (var n in unrevealedAdj)
                {
                    if (!flags.TryGetValue(n, out var nf)) continue;
                    if (!IsTeam(nf, revealerOwner)) continue;
                    // Rule out all non-team owners
                    foreach (TileOwner owner in Enum.GetValues<TileOwner>())
                    {
                        if (owner == revealerOwner) continue;
                        if (IsTeam(nf, owner))
                        {
                            SetTeam(ref nf, owner, false);
                            anyChanged = true;
                        }
                    }
                    flags[n] = nf;
                }
            }
        }

        return anyChanged;
    }

    private static ExclusionAnalysis ExtractResults(
        GameState state,
        Dictionary<Position, OwnerFlags> flags)
    {
        var guaranteed = new List<Position>();
        var ruledOut = new HashSet<Position>();

        foreach (var (pos, f) in flags)
        {
            var tile = state.Board.GetTile(pos);
            if (tile.IsRevealed) continue;

            if (f.IsExactly(TileOwner.Rival))
                guaranteed.Add(pos);
            if (!f.Rival)
                ruledOut.Add(pos);
        }

        return new ExclusionAnalysis
        {
            GuaranteedRivals = guaranteed,
            RuledOutRivals = ruledOut
        };
    }

    private static bool IsTeam(OwnerFlags f, TileOwner owner) => owner switch
    {
        TileOwner.Player => f.Player,
        TileOwner.Rival => f.Rival,
        TileOwner.Neutral => f.Neutral,
        TileOwner.Noble => f.Noble,
        _ => false
    };

    private static void SetTeam(ref OwnerFlags f, TileOwner owner, bool value)
    {
        switch (owner)
        {
            case TileOwner.Player: f.Player = value; break;
            case TileOwner.Rival: f.Rival = value; break;
            case TileOwner.Neutral: f.Neutral = value; break;
            case TileOwner.Noble: f.Noble = value; break;
        }
    }
}
