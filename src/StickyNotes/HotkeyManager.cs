using System;
using System.Windows.Forms;
using System.Windows.Input;

namespace StickyNotes;

/// <summary>Registers one global hotkey, described as text such as "Ctrl+Alt+N".</summary>
public sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 1;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    private readonly Action _pressed;
    private bool _registered;

    public HotkeyManager(Action pressed)
    {
        _pressed = pressed;
        // Message-only window to receive WM_HOTKEY.
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
    }

    /// <summary>Replaces the current hotkey. Returns false if the text is invalid or the combination is taken.</summary>
    public bool Register(string? text)
    {
        Unregister();
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!TryParse(text, out uint modifiers, out uint vk)) return false;
        _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    private void Unregister()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) _pressed();
        else base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var text = string.Empty;
        if (modifiers.HasFlag(ModifierKeys.Control)) text += "Ctrl+";
        if (modifiers.HasFlag(ModifierKeys.Alt)) text += "Alt+";
        if (modifiers.HasFlag(ModifierKeys.Shift)) text += "Shift+";
        if (modifiers.HasFlag(ModifierKeys.Windows)) text += "Win+";
        return text + key;
    }

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win": modifiers |= MOD_WIN; break;
                default: return false;
            }
        }

        if (!Enum.TryParse(parts[^1], out Key key) || key == Key.None) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }
}
