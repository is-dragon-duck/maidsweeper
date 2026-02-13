namespace Maidsweeper.Core.Models;

public record SpecialTileConfig
{
    public required SpecialTileType Type { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<TileOwner> EligibleOwners { get; init; }
}

public record UponFinishConfig
{
    public bool CardReward { get; init; }
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
        UponFinish = new UponFinishConfig { CardReward = true, NextLevelId = "level3" }
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
        UponFinish = new UponFinishConfig { CardReward = false }
    };

    public static LevelConfig? GetById(string levelId) => levelId switch
    {
        "level1" => Level1,
        "level2" => Level2,
        "level3" => Level3,
        _ => null
    };
}
