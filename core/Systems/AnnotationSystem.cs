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
}
