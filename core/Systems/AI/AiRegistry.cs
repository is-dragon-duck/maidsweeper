namespace Maidsweeper.Core.Systems.AI;

using Maidsweeper.Core.Models;

/// <summary>
/// Factory for AI implementations. Maps AiType to concrete IRivalAi instances.
/// </summary>
public static class AiRegistry
{
    public static IRivalAi Get(AiType type) => type switch
    {
        AiType.Random => new RandomAi(),
        AiType.NoGuess => new NoGuessAi(),
        // M37 / M42 fall back to Random until implemented
        AiType.Conservative => new RandomAi(),
        AiType.Reasoning => new RandomAi(),
        _ => new RandomAi()
    };
}
