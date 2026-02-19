namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class AnnotationSystem
{
    /// <summary>
    /// Adds or intersects an owner subset annotation on a tile.
    /// If the tile has no existing subset, sets it.
    /// If the tile already has a subset, intersects with the new one.
    /// Returns a new GameState with the updated annotation.
    /// </summary>
    public static GameState AddOwnerSubset(GameState state, Position pos, HashSet<TileOwner> newSubset)
    {
        var board = state.Board;
        var tile = board.GetTile(pos);

        if (tile.IsRevealed)
            return state;

        var existing = tile.Annotations.OwnerSubset;
        HashSet<TileOwner> finalSubset;

        if (existing != null)
        {
            // Intersection: only owners that appear in both sets
            finalSubset = new HashSet<TileOwner>(existing);
            finalSubset.IntersectWith(newSubset);
        }
        else
        {
            finalSubset = new HashSet<TileOwner>(newSubset);
        }

        var newAnnotations = tile.Annotations with { OwnerSubset = finalSubset };

        // Auto-flag: if Player is no longer in the effective subset, flag the tile
        var effective = newAnnotations.EffectiveOwnerSubset;
        if (effective != null && !effective.Contains(TileOwner.Player))
        {
            newAnnotations = newAnnotations with { Flagged = true };
        }

        var newTile = tile with { Annotations = newAnnotations };

        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = newTile;

        return state with { Board = board with { Tiles = newTiles } };
    }

    /// <summary>
    /// Adds a clue result to a tile's annotations.
    /// </summary>
    public static GameState AddClueResult(GameState state, Position pos, ClueResult clueResult)
    {
        var board = state.Board;
        var tile = board.GetTile(pos);

        if (tile.IsRevealed)
            return state;

        var newClueResults = tile.Annotations.ClueResults.ToList();
        newClueResults.Add(clueResult);

        var newAnnotations = tile.Annotations with { ClueResults = newClueResults };
        var newTile = tile with { Annotations = newAnnotations };

        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = newTile;

        return state with { Board = board with { Tiles = newTiles } };
    }

    /// <summary>
    /// Adds adjacency info (per-owner neighbor counts) to a tile's annotations.
    /// </summary>
    public static GameState AddAdjacencyInfo(GameState state, Position pos, AdjacencyInfo info)
    {
        var board = state.Board;
        var tile = board.GetTile(pos);

        if (tile.IsRevealed)
            return state;

        var newAnnotations = tile.Annotations with { AdjacencyInfo = info };
        var newTile = tile with { Annotations = newAnnotations };

        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = newTile;

        return state with { Board = board with { Tiles = newTiles } };
    }

    /// <summary>
    /// Toggles the player flag on a tile (black slash / "not mine").
    /// Only works on unrevealed tiles.
    /// </summary>
    public static GameState ToggleFlag(GameState state, Position pos)
    {
        var board = state.Board;
        var tile = board.GetTile(pos);

        if (tile.IsRevealed)
            return state;

        var newAnnotations = tile.Annotations with { Flagged = !tile.Annotations.Flagged };
        var newTile = tile with { Annotations = newAnnotations };

        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = newTile;

        return state with { Board = board with { Tiles = newTiles } };
    }

    /// <summary>
    /// Toggles a player exclusion on a tile. If the owner is already excluded,
    /// removes the exclusion. If not excluded, adds it.
    /// Auto-flags the tile if Player is excluded from the effective owner subset.
    /// </summary>
    public static GameState TogglePlayerExclusion(GameState state, Position pos, TileOwner ownerToToggle)
    {
        var board = state.Board;
        var tile = board.GetTile(pos);

        if (tile.IsRevealed)
            return state;

        var excluded = tile.Annotations.PlayerExcluded != null
            ? new HashSet<TileOwner>(tile.Annotations.PlayerExcluded)
            : new HashSet<TileOwner>();

        if (excluded.Contains(ownerToToggle))
            excluded.Remove(ownerToToggle);
        else
            excluded.Add(ownerToToggle);

        var newAnnotations = tile.Annotations with
        {
            PlayerExcluded = excluded.Count > 0 ? excluded : null
        };

        // Auto-flag: if Player is no longer in the effective subset, flag the tile
        var effective = newAnnotations.EffectiveOwnerSubset;
        if (effective != null && !effective.Contains(TileOwner.Player))
        {
            newAnnotations = newAnnotations with { Flagged = true };
        }

        var newTile = tile with { Annotations = newAnnotations };
        var newTiles = board.Tiles.ToList();
        newTiles[board.TileIndex(pos)] = newTile;

        return state with { Board = board with { Tiles = newTiles } };
    }
}
