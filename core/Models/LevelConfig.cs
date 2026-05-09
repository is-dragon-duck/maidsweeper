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

public static class LevelConfigs
{
    /// <summary>
    /// Level 1: 6x5, 12 player / 10 rival / 8 neutral / 0 nobles.
    /// Simple intro floor — no nobles, no special tiles.
    /// </summary>
    public static readonly LevelConfig Level1 = new()
    {
        LevelId = "level1",
        Width = 6,
        Height = 5,
        PlayerCount = 12,
        RivalCount = 10,
        NeutralCount = 8,
        NobleCount = 0,
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level2" }
    };

    /// <summary>
    /// Level 2: 6x5, 10P/9R/8N/1Noble, 2 holes, 1 ExtraDirty on player/neutral.
    /// </summary>
    public static readonly LevelConfig Level2 = new()
    {
        LevelId = "level2",
        Width = 6,
        Height = 5,
        PlayerCount = 10,
        RivalCount = 9,
        NeutralCount = 8,
        NobleCount = 1,
        UnusedLocations = [new Position(0, 0), new Position(4, 5)],
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
            CardReward = true,
            UpgradeReward = true,
            NextLevelId = "level3"
        }
    };

    /// <summary>
    /// Level 3: 6x6, 11P/10R/8N/3Noble, 4-hole center block, 3 ExtraDirty on player/neutral.
    /// </summary>
    public static readonly LevelConfig Level3 = new()
    {
        LevelId = "level3",
        Width = 6,
        Height = 6,
        PlayerCount = 11,
        RivalCount = 10,
        NeutralCount = 8,
        NobleCount = 3,
        UnusedLocations =
        [
            new Position(2, 2), new Position(2, 3),
            new Position(3, 2), new Position(3, 3)
        ],
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
    /// Level 4: 7x6, Manhattan-2 adjacency. 3 ExtraDirty, 3 nobles.
    /// </summary>
    public static readonly LevelConfig Level4 = new()
    {
        LevelId = "level4",
        Width = 7,
        Height = 6,
        PlayerCount = 14,
        RivalCount = 12,
        NeutralCount = 11,
        NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        UnusedLocations = [new Position(0, 0), new Position(5, 6)],
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 3,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { Shop = true, NextLevelId = "level5" }
    };

    /// <summary>
    /// Level 5: 7x7 with complex holes, King adjacency. 4 nobles, 6 ExtraDirty.
    /// Card + Equipment reward.
    /// </summary>
    public static readonly LevelConfig Level5 = new()
    {
        LevelId = "level5",
        Width = 7,
        Height = 7,
        PlayerCount = 12,
        RivalCount = 11,
        NeutralCount = 9,
        NobleCount = 4,
        UnusedLocations =
        [
            // Diamond-shaped hole pattern in center
            new Position(2, 3),
            new Position(3, 2), new Position(3, 3), new Position(3, 4),
            new Position(4, 3),
            // Corner holes
            new Position(0, 0), new Position(0, 6),
            new Position(6, 0), new Position(6, 6),
            // Edge indentations
            new Position(0, 3), new Position(6, 3),
            new Position(3, 0), new Position(3, 6)
        ],
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 6,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true,
            EquipmentReward = true,
            NextLevelId = "level6"
        }
    };

    /// <summary>
    /// Level 6: 7x7, Manhattan-2 adjacency. Initial rival reveal.
    /// </summary>
    public static readonly LevelConfig Level6 = new()
    {
        LevelId = "level6",
        Width = 7,
        Height = 7,
        PlayerCount = 15,
        RivalCount = 13,
        NeutralCount = 11,
        NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 1,
        UnusedLocations =
        [
            new Position(0, 0), new Position(0, 6),
            new Position(3, 3),
            new Position(6, 0), new Position(6, 6),
            new Position(1, 1), new Position(5, 5)
        ],
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 5,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level7" }
    };

    /// <summary>
    /// Level 7: 7x7, Manhattan-2 adjacency. Initial rival reveal. Card + Upgrade.
    /// </summary>
    public static readonly LevelConfig Level7 = new()
    {
        LevelId = "level7",
        Width = 7,
        Height = 7,
        PlayerCount = 13,
        RivalCount = 11,
        NeutralCount = 9,
        NobleCount = 3,
        AdjacencyRule = AdjacencyRule.Manhattan2,
        InitialRivalReveal = 1,
        UnusedLocations =
        [
            // Cross-shaped holes
            new Position(0, 3),
            new Position(3, 0), new Position(3, 3), new Position(3, 6),
            new Position(6, 3),
            // Corner indentations
            new Position(0, 0), new Position(0, 6),
            new Position(6, 0), new Position(6, 6),
            // Additional holes
            new Position(1, 5), new Position(5, 1),
            new Position(2, 0), new Position(4, 6)
        ],
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig
        {
            CardReward = true,
            UpgradeReward = true,
            NextLevelId = "level8"
        }
    };

    /// <summary>
    /// Level 8: 8x7, King adjacency. Final floor. Initial rival reveal.
    /// </summary>
    public static readonly LevelConfig Level8 = new()
    {
        LevelId = "level8",
        Width = 8,
        Height = 7,
        PlayerCount = 17,
        RivalCount = 15,
        NeutralCount = 12,
        NobleCount = 4,
        InitialRivalReveal = 1,
        UnusedLocations =
        [
            new Position(0, 0), new Position(0, 7),
            new Position(3, 3), new Position(3, 4),
            new Position(6, 0), new Position(6, 7),
            new Position(1, 4), new Position(5, 3)
        ],
        SpecialTiles =
        [
            new SpecialTileConfig
            {
                Type = SpecialTileType.ExtraDirty,
                Count = 4,
                EligibleOwners = [TileOwner.Player, TileOwner.Neutral]
            }
        ],
        UponFinish = new UponFinishConfig { CardReward = false }
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
        _ => null
    };
}
