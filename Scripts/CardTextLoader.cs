using System.Collections.Generic;
using Godot;

#nullable enable

namespace Maidsweeper.Scripts;

/// <summary>
/// Loads card display text from human-editable JSON files in Resources/CardText/.
/// Three variants per card, keyed by card name:
///   - regular.json: text shown on a non-Enhanced card
///   - enhanced.json: text shown on an Enhanced card
///   - help.json: text shown in the right-click help popup
/// Falls back to the supplied fallback string when a card name is missing from a file.
/// </summary>
public static class CardTextLoader
{
    private const string RegularPath = "res://Resources/CardText/regular.json";
    private const string EnhancedPath = "res://Resources/CardText/enhanced.json";
    private const string HelpPath = "res://Resources/CardText/help.json";

    private static Dictionary<string, string>? _regular;
    private static Dictionary<string, string>? _enhanced;
    private static Dictionary<string, string>? _help;

    public static string GetRegular(string cardName, string fallback = "") =>
        Lookup(ref _regular, RegularPath, cardName, fallback);

    public static string GetEnhanced(string cardName, string fallback = "") =>
        Lookup(ref _enhanced, EnhancedPath, cardName, fallback);

    public static string GetHelp(string cardName, string fallback = "") =>
        Lookup(ref _help, HelpPath, cardName, fallback);

    private static string Lookup(ref Dictionary<string, string>? cache, string path, string key, string fallback)
    {
        cache ??= Load(path);
        return cache.TryGetValue(key, out var value) ? value : fallback;
    }

    private static Dictionary<string, string> Load(string path)
    {
        var result = new Dictionary<string, string>();
        if (!FileAccess.FileExists(path))
        {
            GD.PushWarning($"CardTextLoader: file missing at {path}");
            return result;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning($"CardTextLoader: could not open {path}");
            return result;
        }

        var json = new Json();
        var err = json.Parse(file.GetAsText());
        if (err != Error.Ok)
        {
            GD.PushWarning($"CardTextLoader: JSON parse error in {path}: {json.GetErrorMessage()}");
            return result;
        }

        if (json.Data.AsGodotDictionary() is { } dict)
        {
            foreach (var (k, v) in dict)
            {
                result[k.AsString()] = v.AsString();
            }
        }
        return result;
    }
}
