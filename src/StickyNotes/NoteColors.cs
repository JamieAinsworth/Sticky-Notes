using System.Linq;
using System.Windows.Media;

namespace StickyNotes;

public sealed record NoteColor(string Name, Color Body, Color Header)
{
    public SolidColorBrush BodyBrush { get; } = Freeze(new SolidColorBrush(Body));
    public SolidColorBrush HeaderBrush { get; } = Freeze(new SolidColorBrush(Header));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}

public static class NoteColors
{
    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static readonly NoteColor[] All =
    {
        new("Yellow", Hex("#FFF9B1"), Hex("#F5E77A")),
        new("Orange", Hex("#FFE0B8"), Hex("#F9C68A")),
        new("Pink",   Hex("#FFD6EA"), Hex("#F7A8CF")),
        new("Purple", Hex("#E8DBFF"), Hex("#C9AEF5")),
        new("Blue",   Hex("#D3ECFF"), Hex("#9FD2FA")),
        new("Green",  Hex("#D6F5D8"), Hex("#A8E6AC")),
        new("Grey",   Hex("#EDEDED"), Hex("#CFCFCF")),
    };

    public static NoteColor Default => All[0];

    public static NoteColor Get(string? name) => All.FirstOrDefault(c => c.Name == name) ?? Default;
}
