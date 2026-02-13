namespace Maidsweeper.Core.Models;

public enum TileOwner
{
    Player,
    Rival,
    Neutral,
    Noble
}

public enum GameStatus
{
    Playing,
    Won,
    Lost
}

public enum GamePhase
{
    Playing,
    CardReward,
    FloorComplete,
    CampaignVictory
}

public enum PlayerType
{
    Player,
    Rival
}

public enum CardEffectType
{
    Spritz,      // Is it safe or dangerous?
    Recall,      // Bag-draw clue pips (Recall - Imperious, Recall - Vague, etc.)
    Scurry,      // Reveal the safer tile
    Tingle,      // Sense a random rival/noble tile
    Twirl,       // Gain copper
    Brush,       // 3x3 area: annotate to exclude a random non-owner
    Sweep,       // 5x5 area: remove ExtraDirty
    Caffeinate,  // Gain 2 spoons (exhaust)
    Breathe,     // Draw 3 cards
    LockIn,      // Draw 2 cards (exhaust, free)
    Rendezvous   // Reveal random player+rival tiles with swapped adjacency
}

public enum SpecialTileType
{
    ExtraDirty
}
