using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickyNotes;

/// <summary>
/// User preferences. Always stored in %APPDATA%\RhyhoStickyNotes\settings.json so the app can find
/// the notes even when they're kept somewhere else.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultDataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RhyhoStickyNotes");

    public static string SettingsPath { get; } = Path.Combine(DefaultDataFolder, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public string DefaultColor { get; set; } = NoteColors.Default.Name;

    /// <summary>Folder holding notes.json; null or empty means <see cref="DefaultDataFolder"/>.</summary>
    public string? DataFolder { get; set; }

    [JsonIgnore]
    public string ResolvedDataFolder =>
        string.IsNullOrWhiteSpace(DataFolder) ? DefaultDataFolder : DataFolder;

    [JsonIgnore]
    public bool UsesCustomFolder => !string.IsNullOrWhiteSpace(DataFolder) && !IsSameFolder(DataFolder, DefaultDataFolder);

    /// <summary>Loads settings; returns false if none exist yet (first run).</summary>
    public static bool Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return false;
            Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
            return true;
        }
        catch (Exception)
        {
            // Unreadable settings: fall back to defaults and let the user run setup again.
            Current = new AppSettings();
            return false;
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DefaultDataFolder);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tmp, SettingsPath, overwrite: true);
        Current = settings;
    }

    public static bool IsSameFolder(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
}
