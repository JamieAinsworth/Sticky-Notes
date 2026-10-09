using System;
using Microsoft.Win32;

namespace StickyNotes;

/// <summary>Toggles launching the app at sign-in via the per-user Run key.</summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StickyNotes";
    private const string LegacyValueName = "RhyhoStickyNotes";

    // Launched at sign-in: stay in the tray rather than opening an empty note when there are none.
    private static string Command => $"{App.LaunchCommand} {App.BackgroundArg}";

    public static bool IsEnabled => IsOurs(ReadValue());

    private static string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) as string;
    }

    private static bool IsOurs(string? value) =>
        value != null && (string.Equals(value, App.LaunchCommand, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(App.LaunchCommand + " ", StringComparison.OrdinalIgnoreCase));

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            if (IsOurs(key.GetValue(LegacyValueName) as string))
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Upgrades entries written by older versions, which didn't pass the background flag.</summary>
    public static void RefreshIfEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
            {
                if (key != null) MigrateLegacyEntry(key);
            }
            var value = ReadValue();
            if (IsOurs(value) && !string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                SetEnabled(true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Couldn't update the startup setting:\n" + ex.Message,
                "Sticky Notes", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    internal static void MigrateLegacyEntry(RegistryKey key)
    {
        if (!IsOurs(key.GetValue(LegacyValueName) as string)) return;
        if (key.GetValue(ValueName) == null)
            key.SetValue(ValueName, Command);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }
}
