using System;
using Microsoft.Win32;

namespace StickyNotes;

/// <summary>Toggles launching the app at sign-in via the per-user Run key.</summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RhyhoStickyNotes";

    // Launched at sign-in: stay in the tray rather than opening an empty note when there are none.
    private static string Command => $"{App.LaunchCommand} {App.BackgroundArg}";

    public static bool IsEnabled => IsOurs(ReadValue());

    private static string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) as string;
    }

    private static bool IsOurs(string? value) =>
        value != null && value.StartsWith(App.LaunchCommand, StringComparison.OrdinalIgnoreCase);

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Upgrades entries written by older versions, which didn't pass the background flag.</summary>
    public static void RefreshIfEnabled()
    {
        try
        {
            var value = ReadValue();
            if (IsOurs(value) && !string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                SetEnabled(true);
        }
        catch
        {
            // Not critical.
        }
    }
}
