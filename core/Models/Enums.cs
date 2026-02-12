namespace Maidsweeper.Core.Models;

public enum TileOwner
{
    Player,
    Rival,
    Neutral,
    Mine
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
    Scout,        // Spritz — is it safe or dangerous?
    Instructions, // Imperious Instructions — bag-draw clue pips
    Scurry,       // Scurry — reveal the safer tile
    Tingle,       // Tingle — sense a random rival/mine tile
    Twirl         // Twirl — gain copper
}
