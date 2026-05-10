namespace Maidsweeper.Core.Models;

public record SpecialTileConfig
{
    public required SpecialTileType Type { get; init; }
    public required int Count { get; init; }
    /// <summary>
    /// How to pick positions for this special. Defaults to Owners (existing behavior).
    /// </summary>
    public PlacementStrategy Strategy { get; init; } = PlacementStrategy.Owners;
    /// <summary>
    /// Used when Strategy = Owners. Restricts placement to tiles whose Owner is in this list.
    /// </summary>
    public IReadOnlyList<TileOwner> EligibleOwners { get; init; } = [];
    /// <summary>
    /// Used when Strategy = Explicit. The exact positions to mark with this flag.
    /// </summary>
    public IReadOnlyList<Position> ExplicitPositions { get; init; } = [];
}

public record UponFinishConfig
{
    public bool CardReward { get; init; }
    public bool UpgradeReward { get; init; }
    public bool EquipmentReward { get; init; }
    public bool Shop { get; init; }
    public string? NextLevelId { get; init; }
}

public record LevelConfig
{
    public string LevelId { get; init; } = "";
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int PlayerCount { get; init; }
    public required int RivalCount { get; init; }
    public required int NeutralCount { get; init; }
    public required int NobleCount { get; init; }
    public IReadOnlyList<Position> UnusedLocations { get; init; } = [];
    /// <summary>
    /// Alpha alternative to <see cref="UnusedLocations"/>: when &gt; 0,
    /// <see cref="Systems.BoardSystem"/> picks this many random board positions
    /// to mark as holes (used by L12/L14/L15/L16+ where the alpha config
    /// supplies <c>"unusedLocations": 20</c>). Ignored when
    /// <see cref="UnusedLocations"/> is non-empty.
    /// </summary>
    public int RandomUnusedCount { get; init; }
    public IReadOnlyList<SpecialTileConfig> SpecialTiles { get; init; } = [];
    public AdjacencyRule AdjacencyRule { get; init; } = AdjacencyRule.King;
    public AiType RivalAi { get; init; } = AiType.Random;
    public int InitialRivalReveal { get; init; }
    /// <summary>
    /// Maps to the alpha config's `rivalNeverMines`. When true, the rival's AI
    /// must never reveal a noble (regular or lounging). Read by Conservative
    /// and Reasoning AIs to filter their candidate pool.
    /// </summary>
    public bool RivalNeverNobles { get; init; }
    /// <summary>
    /// Maps to the alpha config's `rivalPlacesMines`. After every rival turn,
    /// places this many lounging-noble overlays on random unrevealed
    /// player/neutral tiles that don't already have one.
    /// </summary>
    public int RivalPlacesMines { get; init; }
    /// <summary>
    /// Maps to the alpha config's `rivalMineProtection`. Initial number of times
    /// the rival can safely reveal a noble (regular or lounging) without ending
    /// the floor. Each protected reveal awards 5 copper.
    /// </summary>
    public int RivalMineProtection { get; init; }
    public UponFinishConfig? UponFinish { get; init; }
}

/// <summary>
/// Level definitions ported verbatim from the alpha's <c>levels-config.json</c>.
/// JSON uses <c>[col, row]</c> tuples; we convert each to <c>Position(row, col)</c>.
/// </summary>
public static class LevelConfigs
{
    /// <summary>
    /// Builds a Position list from <c>[col, row]</c> pairs (alpha JSON convention).
    /// </summary>
    private static IReadOnlyList<Position> CR(params (int col, int row)[] coords) =>
        coords.Select(p => new Position(p.row, p.col)).ToArray();

    /// <summary>
    /// Level 1 ("intro"): 6×5, 12P/10R/8N/0Noble. Higher player ratio, no nobles —
    /// can't lose by guessing. Card reward on completion.
    /// </summary>
    public static readonly LevelConfig Level1 = new()
    {
        LevelId = "level1",
        Width = 6, Height = 5,
        PlayerCount = 12, RivalCount = 10, NeutralCount = 8, NobleCount = 0,
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level2" }
    };

    /// <summary>
    /// Level 2: 6×5, 10P/9R/8N/1Noble. Two corner holes, 1 ExtraDirty (player/neutral).
    /// Card + Upgrade reward.
    /// </summary>
    public static readonly LevelConfig Level2 = new()
    {
        LevelId = "level2",
        Width = 6, Height = 5,
        PlayerCount = 10, RivalCount = 9, NeutralCount = 8, NobleCount = 1,
        UnusedLocations = CR((0, 0), (5, 4)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 1,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, UpgradeReward = true, NextLevelId = "level3"
        }
    };

    /// <summary>
    /// Level 3: 6×6, 11P/10R/8N/3Noble. 4-tile center hole, 3 ExtraDirty (player/neutral).
    /// Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level3 = new()
    {
        LevelId = "level3",
        Width = 6, Height = 6,
        PlayerCount = 11, RivalCount = 10, NeutralCount = 8, NobleCount = 3,
        UnusedLocations = CR(
            (2, 2), (3, 2),
            (2, 3), (3, 3)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 3,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { EquipmentReward = true, NextLevelId = "level4" }
    };

    /// <summary>
    /// Level 4: 9×9 checkerboard, 14P/12R/11N/3Noble. Manhattan-2 adjacency.
    /// 3 ExtraDirty random. Shop on completion.
    /// </summary>
    public static readonly LevelConfig Level4 = new()
    {
        LevelId = "level4",
        Width = 9, Height = 9,
        PlayerCount = 14, RivalCount = 12, NeutralCount = 11, NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        UnusedLocations = CR(
            (0, 0), (2, 0), (4, 0), (6, 0), (8, 0),
                    (1, 1), (3, 1), (5, 1), (7, 1),
            (0, 2), (2, 2), (4, 2), (6, 2), (8, 2),
                    (1, 3), (3, 3), (5, 3), (7, 3),
            (0, 4), (2, 4), (4, 4), (6, 4), (8, 4),
                    (1, 5), (3, 5), (5, 5), (7, 5),
            (0, 6), (2, 6), (4, 6), (6, 6), (8, 6),
                    (1, 7), (3, 7), (5, 7), (7, 7),
            (0, 8), (2, 8), (4, 8), (6, 8), (8, 8)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 3,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level5" }
    };

    /// <summary>
    /// Level 5: 7×7, 12P/11R/9N/4Noble. Diamond+cross hole pattern.
    /// 6 ExtraDirty random. RivalNeverNobles, Conservative AI.
    /// Card + Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level5 = new()
    {
        LevelId = "level5",
        Width = 7, Height = 7,
        PlayerCount = 12, RivalCount = 11, NeutralCount = 9, NobleCount = 4,
        RivalAi = AiType.Conservative,
        RivalNeverNobles = true,
        UnusedLocations = CR(
                            (3, 1),
                    (2, 2), (3, 2), (4, 2),
            (1, 3), (2, 3), (3, 3), (4, 3), (5, 3),
                    (2, 4), (3, 4), (4, 4),
                            (3, 5)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 6,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, EquipmentReward = true, NextLevelId = "level6"
        }
    };

    /// <summary>
    /// Level 6: 9×9 checkerboard with extra holes, 15P/13R/11N/3Noble. Manhattan-2.
    /// Initial rival reveal = 1, Conservative AI. 5 ExtraDirty random. Card reward.
    /// </summary>
    public static readonly LevelConfig Level6 = new()
    {
        LevelId = "level6",
        Width = 9, Height = 9,
        PlayerCount = 15, RivalCount = 13, NeutralCount = 11, NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR(
            (0, 0), (2, 0), (4, 0), (6, 0), (8, 0),
                    (1, 1), (3, 1), (5, 1), (7, 1),
                    (1, 2), (3, 2), (5, 2), (7, 2),
            (0, 3), (2, 3), (4, 3), (6, 3), (8, 3),
                    (1, 4), (3, 4), (5, 4), (7, 4),
                    (1, 5), (3, 5), (5, 5), (7, 5),
            (0, 6), (2, 6), (4, 6), (6, 6), (8, 6),
                    (1, 7), (3, 7), (5, 7), (7, 7),
                    (1, 8), (3, 8), (5, 8), (7, 8)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 5,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level7" }
    };

    /// <summary>
    /// Level 7: 7×7, 13P/11R/9N/3Noble. Same hole pattern as L5. Manhattan-2.
    /// Initial rival reveal = 1, Conservative AI. 4 ExtraDirty random. Card + Upgrade.
    /// </summary>
    public static readonly LevelConfig Level7 = new()
    {
        LevelId = "level7",
        Width = 7, Height = 7,
        PlayerCount = 13, RivalCount = 11, NeutralCount = 9, NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR(
                            (3, 1),
                    (2, 2), (3, 2), (4, 2),
            (1, 3), (2, 3), (3, 3), (4, 3), (5, 3),
                    (2, 4), (3, 4), (4, 4),
                            (3, 5)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, UpgradeReward = true, NextLevelId = "level8"
        }
    };

    /// <summary>
    /// Level 8: 7×7, 17P/15R/12N/4Noble. Single center hole [3,3].
    /// Introduces courtiers (1) and soirées (1, on the empty hole).
    /// Initial rival reveal = 1, Conservative AI. 4 ExtraDirty random. Shop reward.
    /// </summary>
    public static readonly LevelConfig Level8 = new()
    {
        LevelId = "level8",
        Width = 7, Height = 7,
        PlayerCount = 17, RivalCount = 15, NeutralCount = 12, NobleCount = 4,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR((3, 3)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 1,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 1,
                Strategy = PlacementStrategy.Empty
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level9" }
    };

    /// <summary>
    /// Level 9 ("Weirder spacing"): 7×6, 13P/11R/8N/6Noble. Four corner holes.
    /// Manhattan-2, initial rival reveal = 2, RivalNeverNobles, Conservative AI.
    /// 6 ExtraDirty random. Card + Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level9 = new()
    {
        LevelId = "level9",
        Width = 7, Height = 6,
        PlayerCount = 13, RivalCount = 11, NeutralCount = 8, NobleCount = 6,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 2,
        RivalNeverNobles = true,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR(
            (0, 0), (6, 0),
            (0, 5), (6, 5)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 6,
                Strategy = PlacementStrategy.Random
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, EquipmentReward = true, NextLevelId = "level10"
        }
    };

    /// <summary>
    /// Level 10 ("Easy banquets"): 10×9 with frame-shaped hole pattern.
    /// 18P/15R/13N/6Noble. 5 ED random, 2 courtiers (nonmine), 4 soirées at corner holes.
    /// King adjacency. Initial rival reveal = 1, Conservative AI. Card reward.
    /// </summary>
    public static readonly LevelConfig Level10 = new()
    {
        LevelId = "level10",
        Width = 10, Height = 9,
        PlayerCount = 18, RivalCount = 15, NeutralCount = 13, NobleCount = 6,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR(
            // Top + bottom rows fully empty
            (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0),
            // Row 1: cols 0, 4, 5, 9
            (0, 1),                         (4, 1), (5, 1),                         (9, 1),
            // Rows 2-6: cols 0 and 9 only
            (0, 2),                                                                 (9, 2),
            (0, 3),                                                                 (9, 3),
            (0, 4),                                                                 (9, 4),
            (0, 5),                                                                 (9, 5),
            (0, 6),                                                                 (9, 6),
            // Row 7: cols 0, 4, 5, 9
            (0, 7),                         (4, 7), (5, 7),                         (9, 7),
            // Bottom row fully empty
            (0, 8), (1, 8), (2, 8), (3, 8), (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 5,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 2,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 4,
                Strategy = PlacementStrategy.Explicit,
                ExplicitPositions = CR((0, 0), (9, 0), (0, 8), (9, 8))
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level11" }
    };

    /// <summary>
    /// Level 11 ("Hard banquets"): 8×8, 22P/17R/15N/6Noble. 2×2 center hole.
    /// 4 ED random, 4 soirées on the empty holes. King adjacency.
    /// Initial rival reveal = 1, Conservative AI. Card + Upgrade reward.
    /// </summary>
    public static readonly LevelConfig Level11 = new()
    {
        LevelId = "level11",
        Width = 8, Height = 8,
        PlayerCount = 22, RivalCount = 17, NeutralCount = 15, NobleCount = 6,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        UnusedLocations = CR(
            (3, 3), (4, 3),
            (3, 4), (4, 4)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 4,
                Strategy = PlacementStrategy.Empty
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, UpgradeReward = true, NextLevelId = "level12"
        }
    };

    /// <summary>
    /// Level 12 ("Lounging nobility"): 9×9 with 20 random holes. 21P/18R/15N/7Noble.
    /// Introduces lounging nobles. 5 ED random, 1 courtier nonmine, 1 soirée empty,
    /// 4 lounging nobles on player/neutral. King adjacency. Initial rival reveal = 1,
    /// Conservative AI, RivalMineProtection = 1. Shop reward.
    /// </summary>
    public static readonly LevelConfig Level12 = new()
    {
        LevelId = "level12",
        Width = 9, Height = 9,
        PlayerCount = 21, RivalCount = 18, NeutralCount = 15, NobleCount = 7,
        RandomUnusedCount = 20,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        RivalMineProtection = 1,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 5,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 1,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 1,
                Strategy = PlacementStrategy.Empty
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 4,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level13" }
    };

    /// <summary>
    /// Level 13 ("Boss #3"): 10×10 frame layout. 19P/18R/14N/9Noble.
    /// 4 ED random, 2 courtiers nonmine, 4 soirées picked from 8 explicit positions.
    /// Manhattan-2, initial rival reveal = 1, Conservative AI, RivalNeverNobles.
    /// Card + Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level13 = new()
    {
        LevelId = "level13",
        Width = 10, Height = 10,
        PlayerCount = 19, RivalCount = 18, NeutralCount = 14, NobleCount = 9,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 1,
        RivalAi = AiType.Conservative,
        RivalNeverNobles = true,
        UnusedLocations = CR(
            (0, 0), (1, 0),                                                          (8, 0), (9, 0),
            (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1),
                    (1, 2),                                                          (8, 2),
                    (1, 3),                                                          (8, 3),
                    (1, 4),                                                          (8, 4),
                    (1, 5),                                                          (8, 5),
                    (1, 6),                                                          (8, 6),
                    (1, 7),                                                          (8, 7),
            (0, 8), (1, 8), (2, 8), (3, 8), (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8),
            (0, 9), (1, 9),                                                          (8, 9), (9, 9)),
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 2,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 4,
                Strategy = PlacementStrategy.Explicit,
                ExplicitPositions = CR(
                    (2, 1), (7, 1), (1, 2), (8, 2),
                    (1, 7), (8, 7), (2, 8), (7, 8))
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, EquipmentReward = true, NextLevelId = "level14"
        }
    };

    /// <summary>
    /// Level 14 ("Rival mines your tiles"): 9×9, 20 random holes. 21P/19R/15N/6Noble.
    /// First Reasoning AI floor. 5 ED random, 2 lounging nobles on player/neutral.
    /// Initial rival reveal = 2, RivalPlacesMines = 1, RivalMineProtection = 1.
    /// Card reward.
    /// </summary>
    public static readonly LevelConfig Level14 = new()
    {
        LevelId = "level14",
        Width = 9, Height = 9,
        PlayerCount = 21, RivalCount = 19, NeutralCount = 15, NobleCount = 6,
        RandomUnusedCount = 20,
        InitialRivalReveal = 2,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 1,
        RivalMineProtection = 1,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 5,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 2,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level15" }
    };

    /// <summary>
    /// Level 15 ("All but sanctums"): 9×9, 20 random holes. 21P/19R/14N/7Noble.
    /// 6 ED random, 3 lounging nobles, 2 courtiers nonmine, 2 soirées empty.
    /// Reasoning AI, initial rival reveal = 2, RivalPlacesMines = 2, mine protection = 1.
    /// Card + Upgrade reward.
    /// </summary>
    public static readonly LevelConfig Level15 = new()
    {
        LevelId = "level15",
        Width = 9, Height = 9,
        PlayerCount = 21, RivalCount = 19, NeutralCount = 14, NobleCount = 7,
        RandomUnusedCount = 20,
        InitialRivalReveal = 2,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 2,
        RivalMineProtection = 1,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 6,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 3,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 2,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 2,
                Strategy = PlacementStrategy.Empty
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, UpgradeReward = true, NextLevelId = "level16"
        }
    };

    /// <summary>
    /// Level 16 ("sanctum, who dis"): 7×7, 4 random holes. 15P/13R/10N/7Noble.
    /// Introduces sanctums. 4 ED random, 1 sanctum (neutral/mine), 1 courtier nonmine,
    /// 1 soirée empty. Reasoning AI, initial rival reveal = 2, RivalMineProtection = 2.
    /// Shop reward.
    /// </summary>
    public static readonly LevelConfig Level16 = new()
    {
        LevelId = "level16",
        Width = 7, Height = 7,
        PlayerCount = 15, RivalCount = 13, NeutralCount = 10, NobleCount = 7,
        RandomUnusedCount = 4,
        InitialRivalReveal = 2,
        RivalAi = AiType.Reasoning,
        RivalMineProtection = 2,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Sanctum,
                Count = 1,
                EligibleOwners = [TileOwner.Neutral, TileOwner.Noble]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 1,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 1,
                Strategy = PlacementStrategy.Empty
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level17" }
    };

    /// <summary>
    /// Level 17 ("Boss #4: lots of lounging"): 9×9, 20 random holes.
    /// 21P/19R/14N/7Noble. 6 ED, 3 lounging nobles, 3 courtiers, 3 soirées.
    /// Reasoning AI, initial rival reveal = 3, RivalPlacesMines = 3, RivalNeverNobles.
    /// Card + Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level17 = new()
    {
        LevelId = "level17",
        Width = 9, Height = 9,
        PlayerCount = 21, RivalCount = 19, NeutralCount = 14, NobleCount = 7,
        RandomUnusedCount = 20,
        InitialRivalReveal = 3,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 3,
        RivalNeverNobles = true,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 6,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 3,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 3,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 3,
                Strategy = PlacementStrategy.Empty
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, EquipmentReward = true, NextLevelId = "level18"
        }
    };

    /// <summary>
    /// Level 18 ("Several adjacency sanctums"): 10×10, 30 random holes.
    /// 24P/21R/16N/9Noble. 8 ED, 6 sanctums (neutral/noble), 3 courtiers.
    /// Manhattan-2, Reasoning AI, initial rival reveal = 3, RivalMineProtection = 2.
    /// Card reward.
    /// </summary>
    public static readonly LevelConfig Level18 = new()
    {
        LevelId = "level18",
        Width = 10, Height = 10,
        PlayerCount = 24, RivalCount = 21, NeutralCount = 16, NobleCount = 9,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        RandomUnusedCount = 30,
        InitialRivalReveal = 3,
        RivalAi = AiType.Reasoning,
        RivalMineProtection = 2,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 8,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Sanctum,
                Count = 6,
                EligibleOwners = [TileOwner.Neutral, TileOwner.Noble]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 3,
                Strategy = PlacementStrategy.NonMine
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level19" }
    };

    /// <summary>
    /// Level 19 ("Everything but adjacency"): 8×8, 3 random holes.
    /// 20P/18R/15N/8Noble. 3 ED, 3 sanctums, 3 lounging nobles, 3 courtiers, 3 soirées.
    /// King adjacency. Reasoning AI, initial rival reveal = 3, RivalPlacesMines = 3,
    /// RivalMineProtection = 3. Card + Upgrade reward.
    /// </summary>
    public static readonly LevelConfig Level19 = new()
    {
        LevelId = "level19",
        Width = 8, Height = 8,
        PlayerCount = 20, RivalCount = 18, NeutralCount = 15, NobleCount = 8,
        RandomUnusedCount = 3,
        InitialRivalReveal = 3,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 3,
        RivalMineProtection = 3,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 3,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Sanctum,
                Count = 3,
                EligibleOwners = [TileOwner.Neutral, TileOwner.Noble]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 3,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 3,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 3,
                Strategy = PlacementStrategy.Empty
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true, UpgradeReward = true, NextLevelId = "level20"
        }
    };

    /// <summary>
    /// Level 20 ("Gold mine"): 10×10, 30 random holes. 24P/22R/19N/5Noble.
    /// 4 ED, 5 sanctums, 1 courtier, 1 soirée, 1 lounging noble.
    /// Manhattan-2, Reasoning AI, initial rival reveal = 2, RivalPlacesMines = 1,
    /// RivalMineProtection = 3. Shop reward.
    /// </summary>
    public static readonly LevelConfig Level20 = new()
    {
        LevelId = "level20",
        Width = 10, Height = 10,
        PlayerCount = 24, RivalCount = 22, NeutralCount = 19, NobleCount = 5,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        RandomUnusedCount = 30,
        InitialRivalReveal = 2,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 1,
        RivalMineProtection = 3,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Sanctum,
                Count = 5,
                EligibleOwners = [TileOwner.Neutral, TileOwner.Noble]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 1,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 1,
                Strategy = PlacementStrategy.Empty
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 1,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level21" }
    };

    /// <summary>
    /// Level 21 ("Boss #5: the final boss"): 10×10, 25 random holes.
    /// 24P/22R/19N/10Noble. 8 ED, 7 sanctums, 5 courtiers, 5 soirées, 5 lounging nobles.
    /// Manhattan-2, Reasoning AI, initial rival reveal = 4, RivalPlacesMines = 3,
    /// RivalNeverNobles. <c>winTheGame = true</c> → no further levels.
    /// </summary>
    public static readonly LevelConfig Level21 = new()
    {
        LevelId = "level21",
        Width = 10, Height = 10,
        PlayerCount = 24, RivalCount = 22, NeutralCount = 19, NobleCount = 10,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        RandomUnusedCount = 25,
        InitialRivalReveal = 4,
        RivalAi = AiType.Reasoning,
        RivalPlacesMines = 3,
        RivalNeverNobles = true,
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 8,
                Strategy = PlacementStrategy.Random
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Sanctum,
                Count = 7,
                EligibleOwners = [TileOwner.Neutral, TileOwner.Noble]
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Courtier,
                Count = 5,
                Strategy = PlacementStrategy.NonMine
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.Soiree,
                Count = 5,
                Strategy = PlacementStrategy.Empty
            },
            new SpecialTileConfig
            {
                Type = SpecialTileType.LoungingNoble,
                Count = 5,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        // No NextLevelId → CampaignSystem treats this as winTheGame.
        UponFinish = new UponFinishConfig()
    };

    public static LevelConfig? GetById(string levelId) => levelId switch
    {
        "level1" => Level1,
        "level2" => Level2,
        "level3" => Level3,
        "level4" => Level4,
        "level5" => Level5,
        "level6" => Level6,
        "level7" => Level7,
        "level8" => Level8,
        "level9" => Level9,
        "level10" => Level10,
        "level11" => Level11,
        "level12" => Level12,
        "level13" => Level13,
        "level14" => Level14,
        "level15" => Level15,
        "level16" => Level16,
        "level17" => Level17,
        "level18" => Level18,
        "level19" => Level19,
        "level20" => Level20,
        "level21" => Level21,
        _ => null
    };
}
