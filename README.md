# Sticky Notes
Sticky notes that dont annoy me

A lightweight sticky-notes app for Windows 10/11 built with WPF (.NET 10).

## Features

- Notes live on the desktop: they sit behind your other windows, stay visible on "Show desktop" (Win+D), and have no taskbar button or Alt+Tab entry.
- Drag a note by its header; resize from any edge or corner.
- **Pin** a note to lock its position and size.
- **Colour** picker (yellow, orange, pink, purple, blue, green, grey).
- Text wraps to the note width and scrolls when it overflows.
- Formatting bar (shown while a note is focused): bold, italic, underline, strikethrough, bulleted and numbered lists. A button is highlighted while its style is active at the cursor or selection, whether you turned it on with the button or a shortcut.
- Everything is saved automatically, by default to `%APPDATA%\StickyNotes\notes.json`.
- **First-time setup** on the first launch: choose the default colour for new notes, where to save your notes (any folder, e.g. a OneDrive folder), and whether to start with Windows. Change these later from **Settings…** in the tray menu. Changing the folder moves your notes there; if the folder already has notes, you can switch to them instead. The settings themselves are always kept in `%APPDATA%\StickyNotes\settings.json`.
- On upgrade, existing data from the previous default folder is copied automatically; the originals are retained as a backup. Custom notes folders stay unchanged, and existing Windows startup entries are renamed. Exit the older version before launching the update. If both default folders contain different notes and the new folder has no settings yet, migration stops with an error rather than overwriting either file.
- Tray icon (in the taskbar's hidden icons), drawn in your default note colour: right-click for **New note**, **Show desktop**, **Start with Windows**, **Settings…**, **Exit**. Double-click the icon for a new note. The app keeps running in the tray even when you have no notes, and when it starts with Windows it waits there quietly instead of opening an empty note.

### Keyboard shortcuts

| Action | Shortcut |
| --- | --- |
| Bold / Italic / Underline | Ctrl+B / Ctrl+I / Ctrl+U |
| Strikethrough | Ctrl+T |
| Bulleted list | Ctrl+Shift+L, or type `- ` or `* ` at the start of a line |
| Numbered list | Ctrl+Shift+N, or type `1. ` at the start of a line |
| Leave a list | Press Enter on an empty list item |
| Indent / outdent list item | Tab / Shift+Tab at the start of the item |
| Bigger / smaller text | Ctrl+] / Ctrl+[ |

## Build and run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet run --project src\StickyNotes
```

To publish a copy you can keep running from one place:

```powershell
dotnet publish src\StickyNotes -c Release -o publish
dotnet .\publish\StickyNotes.dll
```

The app is built without a `StickyNotes.exe` launcher (some antivirus tools flag unsigned launchers); it runs through the .NET runtime's own `dotnet.exe` instead. To start it by double-clicking, create a shortcut (right-click the desktop → **New → Shortcut**) with this target, adjusting the path to your `publish` folder:

```
"C:\Program Files\dotnet\dotnet.exe" "C:\Apps\StickyNotes\publish\StickyNotes.dll"
```

Launching the app again while it's running just opens a new note. **Start with Windows** in the tray menu sets up the sign-in launch for you.
