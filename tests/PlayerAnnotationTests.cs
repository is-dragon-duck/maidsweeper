using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

public class PlayerAnnotationTests
{
    private static GameState CreateTestGame(int seed = 42)
    {
        var config = new LevelConfig
        {
            Width = 3, Height = 3,
            PlayerCount = 3, RivalCount = 3, NeutralCount = 2, NobleCount = 1
        };
        var rng = new Random(seed);
        var board = BoardSystem.CreateBoard(config, rng);
        return new GameState
        {
            Board = board,
            Spoons = 3,
            MaxSpoons = 3
        };
    }

    [Fact]
    public void ToggleFlag_FlagsUnrevealedTile()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        var newState = AnnotationSystem.ToggleFlag(state, pos);

        Assert.True(newState.Board.GetTile(pos).Annotations.Flagged);
    }

    [Fact]
    public void ToggleFlag_UnflagsFlaggedTile()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        state = AnnotationSystem.ToggleFlag(state, pos);
        var newState = AnnotationSystem.ToggleFlag(state, pos);

        Assert.False(newState.Board.GetTile(pos).Annotations.Flagged);
    }

    [Fact]
    public void ToggleFlag_NoOpOnRevealedTile()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        // Reveal the tile first
        var newBoard = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player);
        state = state with { Board = newBoard };

        var newState = AnnotationSystem.ToggleFlag(state, pos);

        Assert.False(newState.Board.GetTile(pos).Annotations.Flagged);
    }

    [Fact]
    public void TogglePlayerExclusion_ExcludesOwnerType()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        var newState = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Noble);

        var annotations = newState.Board.GetTile(pos).Annotations;
        Assert.NotNull(annotations.PlayerExcluded);
        Assert.Contains(TileOwner.Noble, annotations.PlayerExcluded!);
    }

    [Fact]
    public void TogglePlayerExclusion_TogglesOffWhenAlreadyExcluded()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        state = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Noble);
        var newState = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Noble);

        var annotations = newState.Board.GetTile(pos).Annotations;
        Assert.Null(annotations.PlayerExcluded); // Empty set becomes null
    }

    [Fact]
    public void PlayerExclusion_IntersectsWithCardAnnotation()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        // Card says {Player, Neutral} (safe from Spritz)
        state = AnnotationSystem.AddOwnerSubset(state, pos,
            new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral });

        // Player excludes Neutral
        state = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Neutral);

        var effective = state.Board.GetTile(pos).Annotations.EffectiveOwnerSubset;
        Assert.NotNull(effective);
        Assert.Single(effective!);
        Assert.Contains(TileOwner.Player, effective);
    }

    [Fact]
    public void AutoFlag_WhenPlayerExcludedFromEffectiveSubset()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        // Player excludes Player owner type
        var newState = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Player);

        // Should auto-flag since Player is no longer possible
        Assert.True(newState.Board.GetTile(pos).Annotations.Flagged);
    }

    [Fact]
    public void AutoFlag_WhenCardAnnotationExcludesPlayer()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        // Card says {Rival, Noble} (dangerous from Spritz)
        var newState = AnnotationSystem.AddOwnerSubset(state, pos,
            new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Noble });

        // Should auto-flag since Player is not in the subset
        Assert.True(newState.Board.GetTile(pos).Annotations.Flagged);
    }

    [Fact]
    public void PlayerAnnotations_DontAffectGameLogic()
    {
        var state = CreateTestGame();
        // Find a player tile
        var playerTile = state.Board.Tiles.First(t => !t.IsRevealed && t.Owner == TileOwner.Player);

        // Flag it and exclude Player (incorrect deduction by player)
        state = AnnotationSystem.ToggleFlag(state, playerTile.Position);
        state = AnnotationSystem.TogglePlayerExclusion(state, playerTile.Position, TileOwner.Player);

        // Revealing it should still work — it's still a Player tile
        var newBoard = BoardSystem.RevealTile(state.Board, playerTile.Position, PlayerType.Player);
        var revealed = newBoard.GetTile(playerTile.Position);

        Assert.True(revealed.IsRevealed);
        Assert.Equal(TileOwner.Player, revealed.Owner);
    }

    [Fact]
    public void MultipleExclusions_NarrowSubsetCorrectly()
    {
        var state = CreateTestGame();
        var pos = state.Board.Tiles.First(t => !t.IsRevealed).Position;

        state = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Noble);
        state = AnnotationSystem.TogglePlayerExclusion(state, pos, TileOwner.Rival);

        var effective = state.Board.GetTile(pos).Annotations.EffectiveOwnerSubset;
        Assert.NotNull(effective);
        Assert.Equal(2, effective!.Count);
        Assert.Contains(TileOwner.Player, effective);
        Assert.Contains(TileOwner.Neutral, effective);
    }

    [Fact]
    public void EffectiveOwnerSubset_NullWhenNoAnnotations()
    {
        var annotations = new TileAnnotations();
        Assert.Null(annotations.EffectiveOwnerSubset);
    }

    [Fact]
    public void EffectiveOwnerSubset_EqualsCardSubsetWhenNoPlayerExclusions()
    {
        var annotations = new TileAnnotations
        {
            OwnerSubset = new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral }
        };

        var effective = annotations.EffectiveOwnerSubset;
        Assert.NotNull(effective);
        Assert.Equal(2, effective!.Count);
        Assert.Contains(TileOwner.Player, effective);
        Assert.Contains(TileOwner.Neutral, effective);
    }
}
