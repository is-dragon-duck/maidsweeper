namespace Maidsweeper.Core.Models;

public record LevelConfig
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int PlayerCount { get; init; }
    public required int RivalCount { get; init; }
    public required int NeutralCount { get; init; }
    public required int NobleCount { get; init; }
}

public static class LevelConfigs
{
    /// <summary>
    /// Level 1: 6x5, 12 player / 10 rival / 8 neutral / 0 nobles.
    /// Simple intro floor — no nobles, no special tiles.
    /// </summary>
    public static readonly LevelConfig Level1 = new()
    {
        Width = 6,
        Height = 5,
        PlayerCount = 12,
        RivalCount = 10,
        NeutralCount = 8,
        NobleCount = 0
    };
}
