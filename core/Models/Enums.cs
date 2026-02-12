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

public enum PlayerType
{
    Player,
    Rival
}

public enum CardEffectType
{
    Spritz,  // Is it safe or dangerous?
    Recall,  // Bag-draw clue pips (Recall - Imperious, Recall - Vague, etc.)
    Scurry,  // Reveal the safer tile
    Tingle,  // Sense a random rival/noble tile
    Twirl    // Gain copper
}
