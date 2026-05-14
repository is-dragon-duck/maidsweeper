using Maidsweeper.Core.Models;
using Maidsweeper.Core.Systems;

namespace Maidsweeper.Tests;

/// <summary>
/// M52: Enhanced versions of cards whose enhancement is a numerical or area bump.
/// Pins each Enhanced flag → bumped behavior against the alpha row in STAGE_5_PLAN.md.
/// </summary>
public class EnhancedNumericalBumpTests
{
    private static GameState BlankLevel1State()
    {
        var rng = new Random(1);
        var board = BoardSystem.CreateBoard(LevelConfigs.Level1, rng);
        return new GameState
        {
            Board = board,
            CurrentLevelId = "level1",
            Spoons = 3, MaxSpoons = 3,
            CurrentPlayer = PlayerType.Player
        };
    }

    // ---------- Breathe: 3 → 5 ----------

    [Fact]
    public void Breathe_Base_Draws3()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"d{i}" })
            .ToList();
        var state = BlankLevel1State() with { DrawPile = deck };

        var newState = CardEffectSystem.ExecuteBreathe(
            state, new Random(7), CardDefinitions.Breathe);

        Assert.Equal(3, newState.Hand.Count);
    }

    [Fact]
    public void Breathe_Enhanced_Draws5()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"d{i}" })
            .ToList();
        var state = BlankLevel1State() with { DrawPile = deck };
        var enhanced = CardDefinitions.Breathe with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteBreathe(state, new Random(7), enhanced);

        Assert.Equal(5, newState.Hand.Count);
    }

    // ---------- Lock In: 2 → 4 ----------

    [Fact]
    public void LockIn_Base_Draws2()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"d{i}" })
            .ToList();
        var state = BlankLevel1State() with { DrawPile = deck };

        var newState = CardEffectSystem.ExecuteLockIn(state, new Random(7), CardDefinitions.LockIn);

        Assert.Equal(2, newState.Hand.Count);
    }

    [Fact]
    public void LockIn_Enhanced_Draws4()
    {
        var deck = Enumerable.Range(0, 10)
            .Select(i => CardDefinitions.Spritz with { Id = $"d{i}" })
            .ToList();
        var state = BlankLevel1State() with { DrawPile = deck };
        var enhanced = CardDefinitions.LockIn with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteLockIn(state, new Random(7), enhanced);

        Assert.Equal(4, newState.Hand.Count);
    }

    // ---------- Sweep: 5×5 → 7×7 ----------

    /// <summary>
    /// Build a 9×9 player-only board with dirt on every tile so we can count
    /// how many get cleaned for a given Sweep radius.
    /// </summary>
    private static GameState BuildDirtyBoard()
    {
        var tiles = new List<Tile>();
        for (var row = 0; row < 9; row++)
        for (var col = 0; col < 9; col++)
        {
            tiles.Add(new Tile
            {
                Position = new Position(row, col),
                Owner = TileOwner.Player,
                Specials = SpecialTileType.ExtraDirty
            });
        }
        return new GameState
        {
            Board = new Board { Width = 9, Height = 9, Tiles = tiles },
            CurrentLevelId = "level1"
        };
    }

    [Fact]
    public void Sweep_Base_Cleans25Tiles()
    {
        var state = BuildDirtyBoard();
        var newState = CardEffectSystem.ExecuteSweep(
            state, new[] { new Position(4, 4) }, new Random(7), CardDefinitions.Sweep);

        var cleaned = newState.Board.Tiles.Count(t => !t.IsDirty);
        Assert.Equal(25, cleaned); // 5×5 area centered at (4,4)
    }

    [Fact]
    public void Sweep_Enhanced_Cleans49Tiles()
    {
        var state = BuildDirtyBoard();
        var enhanced = CardDefinitions.Sweep with { Enhanced = true };
        var newState = CardEffectSystem.ExecuteSweep(
            state, new[] { new Position(4, 4) }, new Random(7), enhanced);

        var cleaned = newState.Board.Tiles.Count(t => !t.IsDirty);
        Assert.Equal(49, cleaned); // 7×7 area centered at (4,4)
    }

    // ---------- Pose: 1 → 2 courtiers ----------

    [Fact]
    public void Pose_Base_SpawnsOneCourtier()
    {
        var state = BlankLevel1State();
        var courtierBefore = state.Board.Tiles.Count(t => t.IsCourtier);
        var newState = CardEffectSystem.ExecutePose(state, CardDefinitions.Pose, new Random(7));
        var courtierAfter = newState.Board.Tiles.Count(t => t.IsCourtier);

        Assert.Equal(courtierBefore + 1, courtierAfter);
    }

    [Fact]
    public void Pose_Enhanced_SpawnsTwoCourtiers()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Pose with { Enhanced = true };
        var courtierBefore = state.Board.Tiles.Count(t => t.IsCourtier);
        var newState = CardEffectSystem.ExecutePose(state, enhanced, new Random(7));
        var courtierAfter = newState.Board.Tiles.Count(t => t.IsCourtier);

        Assert.Equal(courtierBefore + 2, courtierAfter);
    }

    [Fact]
    public void Pose_Enhanced_TwoSpawns_OnDistinctTiles()
    {
        // Tiny 2×1 board with one player tile available — second spawn should
        // not find a candidate, so we only get 1.
        var tiles = new List<Tile>
        {
            new() { Position = new Position(0, 0), Owner = TileOwner.Player },
            new() { Position = new Position(0, 1), Owner = TileOwner.Rival }
        };
        var state = new GameState
        {
            Board = new Board { Width = 2, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1"
        };
        var enhanced = CardDefinitions.Pose with { Enhanced = true };
        var newState = CardEffectSystem.ExecutePose(state, enhanced, new Random(7));

        // Only 1 eligible tile; second spawn should be a no-op.
        Assert.Equal(1, newState.Board.Tiles.Count(t => t.IsCourtier));
    }

    // ---------- Description text updates ----------

    [Theory]
    [InlineData("Breathe", "5")]
    [InlineData("Lock In", "4")]
    [InlineData("Sweep", "7x7")]
    [InlineData("Pose", "2")]
    public void EnhancedCards_DescriptionMentionsBumpedNumber(string cardName, string substring)
    {
        var card = cardName switch
        {
            "Breathe" => CardDefinitions.Breathe,
            "Lock In" => CardDefinitions.LockIn,
            "Sweep" => CardDefinitions.Sweep,
            "Pose" => CardDefinitions.Pose,
            _ => throw new ArgumentException(cardName)
        };
        Assert.Contains(substring, card.Description);
    }

    // ---------- Verification of already-implemented enhanced cards ----------

    [Fact]
    public void Twirl_Enhanced_Gains5Copper()
    {
        var state = BlankLevel1State();
        var enhanced = CardDefinitions.Twirl with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteTwirl(state, enhanced);

        Assert.Equal(state.Copper + 5, newState.Copper);
    }

    [Fact]
    public void Twirl_Base_Gains3Copper()
    {
        var state = BlankLevel1State();
        var newState = CardEffectSystem.ExecuteTwirl(state, CardDefinitions.Twirl);
        Assert.Equal(state.Copper + 3, newState.Copper);
    }

    [Fact]
    public void Brat_Enhanced_GrantsTwoCopper()
    {
        // Set up a revealed tile so Brat has a target.
        var tiles = new List<Tile>();
        for (var col = 0; col < 3; col++)
        {
            tiles.Add(new Tile
            {
                Position = new Position(0, col),
                Owner = TileOwner.Player,
                IsRevealed = col == 0,
                RevealedBy = col == 0 ? PlayerType.Player : null
            });
        }
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1"
        };
        var enhanced = CardDefinitions.Brat with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteBrat(state, new[] { new Position(0, 0) }, enhanced);

        Assert.Equal(state.Copper + 2, newState.Copper);
    }

    [Fact]
    public void Brat_Base_NoCopper()
    {
        var tiles = new List<Tile>();
        for (var col = 0; col < 3; col++)
        {
            tiles.Add(new Tile
            {
                Position = new Position(0, col),
                Owner = TileOwner.Player,
                IsRevealed = col == 0,
                RevealedBy = col == 0 ? PlayerType.Player : null
            });
        }
        var state = new GameState
        {
            Board = new Board { Width = 3, Height = 1, Tiles = tiles },
            CurrentLevelId = "level1"
        };

        var newState = CardEffectSystem.ExecuteBrat(state, new[] { new Position(0, 0) }, CardDefinitions.Brat);

        Assert.Equal(state.Copper, newState.Copper);
    }

    [Fact]
    public void Ramble_Enhanced_AddsFourDistractions()
    {
        // Pre-seed intent points so AddDistractionPoint has candidates.
        var state = BlankLevel1State() with
        {
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 5 }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        var enhanced = CardDefinitions.Ramble with { Enhanced = true };

        var newState = CardEffectSystem.ExecuteRamble(state, enhanced, new Random(7));

        Assert.Equal(sumBefore + 4, newState.RivalIntentPoints.Values.Sum());
    }

    [Fact]
    public void Ramble_Base_AddsTwoDistractions()
    {
        var state = BlankLevel1State() with
        {
            RivalIntentPoints = new Dictionary<Position, int> { [new Position(0, 0)] = 5 }
        };
        var sumBefore = state.RivalIntentPoints.Values.Sum();
        var newState = CardEffectSystem.ExecuteRamble(state, CardDefinitions.Ramble, new Random(7));
        Assert.Equal(sumBefore + 2, newState.RivalIntentPoints.Values.Sum());
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Read_StackCount(bool enhanced, int expected)
    {
        var state = BlankLevel1State();
        var card = enhanced ? CardDefinitions.Read with { Enhanced = true } : CardDefinitions.Read;
        var newState = CardEffectSystem.ExecuteRead(state, card);
        Assert.Equal(expected, newState.ReadStacks);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Hydrate_StackCount(bool enhanced, int expected)
    {
        var state = BlankLevel1State();
        var card = enhanced ? CardDefinitions.Hydrate with { Enhanced = true } : CardDefinitions.Hydrate;
        var newState = CardEffectSystem.ExecuteHydrate(state, card);
        Assert.Equal(expected, newState.HydrateStacks);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Adopt_StackCount(bool enhanced, int expected)
    {
        var state = BlankLevel1State();
        var card = enhanced ? CardDefinitions.Adopt with { Enhanced = true } : CardDefinitions.Adopt;
        var newState = CardEffectSystem.ExecuteAdopt(state, card);
        Assert.Equal(expected, newState.AdoptStacks);
    }
}
