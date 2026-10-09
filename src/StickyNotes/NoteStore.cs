using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StickyNotes;

public static class NoteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const string FileName = "notes.json";

    private static string Folder => AppSettings.Current.ResolvedDataFolder;

    private static string FilePath => FilePathIn(Folder);

    public static string FilePathIn(string folder) => Path.Combine(folder, FileName);

    /// <summary>Checks the folder exists (creating it if needed) and that a file can be written there.</summary>
    public static void EnsureWritable(string folder)
    {
        Directory.CreateDirectory(folder);
        var probe = Path.Combine(folder, ".write-test-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(probe, string.Empty);
        File.Delete(probe);
    }

    public static List<NoteData> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<NoteData>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<NoteData>>(json, JsonOptions)?.Where(n => n != null).ToList()
                   ?? new List<NoteData>();
        }
        catch (Exception)
        {
            // Keep a copy of an unreadable file rather than silently overwriting it.
            try { File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
            return new List<NoteData>();
        }
    }

    public static void Save(IEnumerable<NoteData> notes)
    {
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(notes.ToList(), JsonOptions);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
