using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// Tests for Stage 4 Set 1 equipment passive effects (M30):
/// Coffee, Frilly Dress, Dust Bunny, Handbag, Eyeshadow, Glasses.
/// </summary>
public class EquipmentSystemTests
{
    private static GameState BuildFloorStartState(IReadOnlyList<Equipment>? equipment = null, int seed = 42)
    {
        var rng = new Random(seed);
        var state = CampaignSystem.StartCampaign(rng);
        if (equipment != null && equipment.Count > 0)
        {
            state = state with { Equipment = equipment, GameStatus = GameStatus.Won };
            // Trigger floor transition so equipment hooks fire
            state = CampaignSystem.CompleteFloor(state, new Random(seed + 1));
            state = CampaignSystem.SkipCardReward(state, new Random(seed + 2));
        }
        return state;
    }

    // ========== Coffee ==========

    [Fact]
    public void Coffee_IncreasesMaxSpoonsBy1()
    {
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var state = BuildFloorStartState(new List<Equipment> { coffee });

        Assert.Equal(4, state.MaxSpoons);
        Assert.Equal(4, state.Spoons);
    }

    [Fact]
    public void Coffee_ReducesDrawCountOnTurn2Plus()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Coffee with { Id = "c1" } },
            ReadStacks = 0
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(4, drawCount); // 5 base - 1 Coffee = 4
    }

    [Fact]
    public void Coffee_DrawCountAlsoConsidersRead()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Coffee with { Id = "c1" } },
            ReadStacks = 1
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(5, drawCount); // 5 + 1 Read - 1 Coffee = 5
    }

    [Fact]
    public void Coffee_DoesNotReduceTurn1Draw()
    {
        // Floor start initial draw should be 5, not 4, even with Coffee
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var state = BuildFloorStartState(new List<Equipment> { coffee });

        // Hand should contain exactly 5 cards (the initial draw, unaffected by Coffee penalty)
        Assert.Equal(5, state.Hand.Count);
    }

    // ========== Frilly Dress ==========

    [Fact]
    public void FrillyDress_SuppressesTurnEndForFirstNeutralOnTurn1()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.False(result.TurnEnded);
        Assert.Equal(1, result.State.Turn1NeutralReveals);
        Assert.Equal(PlayerType.Player, result.State.CurrentPlayer);
    }

    [Fact]
    public void FrillyDress_FifthNeutralOnTurn1EndsTurn()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });
        state = state with { Turn1NeutralReveals = 4 };

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.True(result.TurnEnded);
        // Counter unchanged because suppression didn't fire
        Assert.Equal(4, result.State.Turn1NeutralReveals);
    }

    [Fact]
    public void FrillyDress_DoesNotSuppressOnTurn2()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });
        state = state with { TurnNumber = 2 };

        var neutralPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Neutral)
            .Position;

        var result = GameRunner.ProcessReveal(state, neutralPos, new Random(99));

        Assert.True(result.TurnEnded);
    }

    [Fact]
    public void FrillyDress_DoesNotSuppressForRivalReveal()
    {
        var dress = EquipmentDefinitions.FrillyDress with { Id = "d1" };
        var state = BuildFloorStartState(new List<Equipment> { dress });

        var rivalPos = state.Board.Tiles
            .First(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival)
            .Position;

        var result = GameRunner.ProcessReveal(state, rivalPos, new Random(99));

        Assert.True(result.TurnEnded);
        Assert.Equal(0, result.State.Turn1NeutralReveals);
    }

    // ========== Dust Bunny ==========

    [Fact]
    public void DustBunny_Reveals1PlayerTileAtFloorStart()
    {
        var dustBunny = EquipmentDefinitions.DustBunny with { Id = "db1" };
        var state = BuildFloorStartState(new List<Equipment> { dustBunny });

        var revealedPlayer = state.Board.Tiles
            .Count(t => state.Board.IsUsablePosition(t.Position)
                        && t.IsRevealed && t.Owner == TileOwner.Player);

        Assert.Equal(1, revealedPlayer);
    }

    // ========== Handbag ==========

    [Fact]
    public void Handbag_Draws2ExtraCardsOnFirstTurn()
    {
        var handbag = EquipmentDefinitions.Handbag with { Id = "h1" };
        var state = BuildFloorStartState(new List<Equipment> { handbag });

        Assert.Equal(7, state.Hand.Count); // 5 base + 2 from Handbag
    }

    [Fact]
    public void Handbag_NoEffectOnSubsequentTurns()
    {
        // GetTurnDrawCount is for non-initial draws and shouldn't include Handbag
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            Equipment = new List<Equipment> { EquipmentDefinitions.Handbag with { Id = "h1" } }
        };

        var drawCount = EquipmentSystem.GetTurnDrawCount(state);

        Assert.Equal(5, drawCount); // Handbag does not add to subsequent turns
    }

    // ========== Eyeshadow ==========

    [Fact]
    public void Eyeshadow_Adds1DistractionAtTurn1()
    {
        var eyeshadow = EquipmentDefinitions.Eyeshadow with { Id = "e1" };
        var state = BuildFloorStartState(new List<Equipment> { eyeshadow });

        Assert.Equal(1, state.DistractionStacks);
    }

    [Fact]
    public void Eyeshadow_AddsDistractionAtEachTurnStart()
    {
        var state = new GameState
        {
            Board = BoardSystem.CreateBoard(LevelConfigs.Level1, new Random(1)),
            DrawPile = CardDefinitions.CreateStarterDeck(),
            Equipment = new List<Equipment> { EquipmentDefinitions.Eyeshadow with { Id = "e1" } },
            DistractionStacks = 0,
            CurrentPlayer = PlayerType.Player,
            TurnNumber = 1
        };

        state = TurnSystem.StartPlayerTurn(state, new Random(42));

        // After StartPlayerTurn (turn 1 → turn 2), Eyeshadow added 1 stack
        Assert.Equal(1, state.DistractionStacks);

        state = TurnSystem.StartPlayerTurn(state, new Random(42));

        // Turn 3 — another Eyeshadow stack added (DistractionStacks not auto-reset within a player turn loop)
        Assert.Equal(2, state.DistractionStacks);
    }

    // ========== Glasses ==========

    [Fact]
    public void Glasses_AnnotatesARivalOrNobleAtTurn1()
    {
        var glasses = EquipmentDefinitions.Glasses with { Id = "g1" };
        var state = BuildFloorStartState(new List<Equipment> { glasses });

        // Glasses fires Tingle: a single rival tile gets exact-owner annotation.
        // (Level1 has no nobles.)
        var tingled = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed
                        && t.Annotations.OwnerSubset != null
                        && t.Annotations.OwnerSubset.Count == 1
                        && t.Annotations.OwnerSubset.Contains(TileOwner.Rival))
            .ToList();

        Assert.Single(tingled);
    }

    // ========== Multi-equipment combinations ==========

    [Fact]
    public void MultipleEquipment_AllEffectsStack()
    {
        var equipment = new List<Equipment>
        {
            EquipmentDefinitions.Coffee with { Id = "c1" },
            EquipmentDefinitions.Handbag with { Id = "h1" },
            EquipmentDefinitions.Eyeshadow with { Id = "e1" }
        };
        var state = BuildFloorStartState(equipment);

        Assert.Equal(4, state.MaxSpoons);     // Coffee
        Assert.Equal(7, state.Hand.Count);    // 5 + 2 Handbag
        Assert.Equal(1, state.DistractionStacks); // Eyeshadow
    }

    [Fact]
    public void Equipment_PersistsAcrossFloors_AppliesEachFloor()
    {
        var coffee = EquipmentDefinitions.Coffee with { Id = "c1" };
        var rng = new Random(42);
        var state = CampaignSystem.StartCampaign(rng);
        state = state with
        {
            Equipment = new List<Equipment> { coffee },
            GameStatus = GameStatus.Won
        };

        // Floor 1 → 2
        state = CampaignSystem.CompleteFloor(state, new Random(43));
        state = CampaignSystem.SkipCardReward(state, new Random(44));
        Assert.Equal(4, state.MaxSpoons);

        // Floor 2 → 3
        state = state with { GameStatus = GameStatus.Won };
        state = CampaignSystem.CompleteFloor(state, new Random(45));
        state = CampaignSystem.SkipCardReward(state, new Random(46));
        Assert.Equal(4, state.MaxSpoons);
        // Coffee still owned
        Assert.Single(state.Equipment);
    }
}
