using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;

namespace StickyNotes;

public partial class NoteWindow : Window
{
    private const double ResizeBorder = 6;

    private readonly NoteManager _manager;
    private IntPtr _hwnd;
    private bool _formatUpdateQueued;

    public NoteData Data { get; }

    /// <summary>True when the user deleted the note (as opposed to the window being torn down externally).</summary>
    public bool IsDeleted { get; set; }

    public NoteWindow(NoteManager manager, NoteData data)
    {
        InitializeComponent();
        _manager = manager;
        Data = data;

        Left = data.Left;
        Top = data.Top;
        Width = data.Width;
        Height = data.Height;

        LoadContent();
        ApplyColor(NoteColors.Get(data.Color));
        ApplyPinned(data.Pinned);

        DataObject.AddPastingHandler(Editor, OnPaste);
        Editor.TextChanged += Editor_TextChanged;
        Editor.SelectionChanged += (_, _) => UpdateFormatButtons();
        // Formatting commands (buttons or Ctrl+B etc.) with no selection change nothing visible yet, so refresh after them too.
        Editor.AddHandler(CommandManager.ExecutedEvent, new ExecutedRoutedEventHandler((_, _) => QueueFormatButtonsUpdate()), true);

        LocationChanged += (_, _) => _manager.RequestSave();
        SizeChanged += (_, _) => _manager.RequestSave();
        Activated += (_, _) => Toolbar.Visibility = Visibility.Visible;
        Deactivated += (_, _) =>
        {
            Toolbar.Visibility = Visibility.Collapsed;
            SendToBottom();
        };
        StateChanged += (_, _) =>
        {
            // Notes live on the desktop; never minimise/maximise them.
            if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // Tool window: no taskbar button and not listed in Alt+Tab.
        long exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle = (exStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        // Owning the note by the desktop window keeps it on the desktop layer and visible after "Show desktop" (Win+D).
        var progman = NativeMethods.GetDesktopProgman();
        if (progman != IntPtr.Zero)
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_HWNDPARENT, progman);

        SendToBottom();
    }

    private void SendToBottom()
    {
        if (_hwnd == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Ignore Alt+F4 etc.; notes are removed with the delete button or hidden by exiting from the tray.
        if (!IsDeleted && !_manager.AllowClose) e.Cancel = true;
        base.OnClosing(e);
    }

    public void FocusEditor()
    {
        Editor.Focus();
        Keyboard.Focus(Editor);
        Editor.CaretPosition = Editor.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
    }

    public NoteData CaptureData()
    {
        if (!double.IsNaN(Left)) Data.Left = Left;
        if (!double.IsNaN(Top)) Data.Top = Top;
        if (!double.IsNaN(Width)) Data.Width = Width;
        if (!double.IsNaN(Height)) Data.Height = Height;

        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        Data.PlainText = range.Text.TrimEnd('\r', '\n');
        try
        {
            using var ms = new MemoryStream();
            range.Save(ms, DataFormats.Xaml);
            Data.Content = Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            Data.Content = string.Empty;
        }
        return Data;
    }

    private void LoadContent()
    {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        if (!string.IsNullOrEmpty(Data.Content))
        {
            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(Data.Content));
                range.Load(ms, DataFormats.Xaml);
                return;
            }
            catch
            {
                // Fall back to plain text below.
            }
        }
        if (!string.IsNullOrEmpty(Data.PlainText)) range.Text = Data.PlainText;
    }

    private bool IsEmpty =>
        string.IsNullOrWhiteSpace(new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text);

    // ---- Header actions ----

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Data.Pinned || e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void NewNote_Click(object sender, RoutedEventArgs e) => _manager.CreateNote(this);

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!IsEmpty)
        {
            var result = MessageBox.Show(this, "Delete this note?", "Sticky Notes",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
        }
        _manager.DeleteNote(this);
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        ApplyPinned(!Data.Pinned);
        _manager.RequestSave();
    }

    private void ApplyPinned(bool pinned)
    {
        Data.Pinned = pinned;
        PinButton.Content = pinned ? "\uE841" : "\uE718";
        PinButton.ToolTip = pinned ? "Unpin (allow moving and resizing)" : "Pin in place";
        Header.Cursor = pinned ? Cursors.Arrow : Cursors.SizeAll;
        ResizeMode = pinned ? ResizeMode.NoResize : ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
            ResizeBorderThickness = new Thickness(pinned ? 0 : ResizeBorder),
        });
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ColorButton, Placement = PlacementMode.Bottom };
        foreach (var color in NoteColors.All)
        {
            var item = new MenuItem
            {
                Header = color.Name,
                IsChecked = color.Name == Data.Color,
                Icon = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(3),
                    Background = color.BodyBrush,
                    BorderBrush = color.HeaderBrush,
                    BorderThickness = new Thickness(2),
                },
            };
            item.Click += (_, _) =>
            {
                ApplyColor(color);
                _manager.RequestSave();
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ApplyColor(NoteColor color)
    {
        Data.Color = color.Name;
        Background = color.BodyBrush;
        Header.Background = color.HeaderBrush;
    }

    // ---- Editor behaviour ----

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        _manager.RequestSave();
        QueueFormatButtonsUpdate();

        // Text was only inserted (not deleted) and the paragraph is now exactly a list marker + space.
        if (e.Changes.Count != 1) return;
        var change = e.Changes.First();
        if (change.AddedLength < 1 || change.RemovedLength != 0) return;

        var paragraph = Editor.Selection.End.Paragraph;
        if (paragraph == null || paragraph.Parent is ListItem) return;

        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        RoutedUICommand? command = text switch
        {
            "- " or "* " => EditingCommands.ToggleBullets,
            "1. " => EditingCommands.ToggleNumbering,
            _ => null,
        };
        if (command == null) return;

        // Don't modify the document from inside its own change notification.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => StartListFromShortcut(paragraph, text, command));
    }

    /// <summary>
    /// WPF drops formatting toggled on (e.g. Ctrl+B) before the first character typed into an empty paragraph,
    /// so insert that first character ourselves and apply the pending formatting to it.
    /// </summary>
    private void Editor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        if (!Editor.Selection.IsEmpty) return;
        var paragraph = Editor.Selection.Start.GetInsertionPosition(LogicalDirection.Forward).Paragraph;
        bool isEmpty = paragraph != null
            ? paragraph.Inlines.All(i => i is Run r && r.Text.Length == 0)
            : new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text.Trim('\r', '\n').Length == 0;
        if (!isEmpty) return;

        var selection = Editor.Selection;
        var weight = selection.GetPropertyValue(TextElement.FontWeightProperty);
        var style = selection.GetPropertyValue(TextElement.FontStyleProperty);
        var decorations = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        bool hasFormatting =
            (weight is FontWeight w && w != FontWeights.Normal) ||
            (style is FontStyle s && s != FontStyles.Normal) ||
            (decorations is TextDecorationCollection d && d.Count > 0);
        if (!hasFormatting) return;

        Editor.BeginChange();
        try
        {
            selection.Text = e.Text;
            if (weight != DependencyProperty.UnsetValue) selection.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
            if (style != DependencyProperty.UnsetValue) selection.ApplyPropertyValue(TextElement.FontStyleProperty, style);
            if (decorations != DependencyProperty.UnsetValue) selection.ApplyPropertyValue(Inline.TextDecorationsProperty, decorations);
            selection.Select(selection.End, selection.End);
        }
        finally
        {
            Editor.EndChange();
        }
        e.Handled = true;
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;

        if (e.Key == Key.T && modifiers == ModifierKeys.Control)
        {
            ToggleStrikethrough();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
        {
            e.Handled = TryExitListOnEmptyItem();
        }
    }

    /// <summary>Typing "- " or "* " starts a bulleted list, "1. " starts a numbered list.</summary>
    private void StartListFromShortcut(Paragraph paragraph, string expectedText, RoutedUICommand command)
    {
        if (paragraph.Parent is ListItem) return;
        var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        if (range.Text != expectedText) return;

        Editor.BeginChange();
        try
        {
            range.Text = string.Empty;
            Editor.CaretPosition = paragraph.ContentStart;
            command.Execute(null, Editor);
        }
        finally
        {
            Editor.EndChange();
        }
    }

    /// <summary>Pressing Enter on an empty list item ends the list, like most editors.</summary>
    private bool TryExitListOnEmptyItem()
    {
        if (!Editor.Selection.IsEmpty) return false;
        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph?.Parent is not ListItem { Parent: List list }) return false;
        if (new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Length != 0) return false;

        var command = list.MarkerStyle == TextMarkerStyle.Decimal
            ? EditingCommands.ToggleNumbering
            : EditingCommands.ToggleBullets;
        command.Execute(null, Editor);
        return true;
    }

    private void Strikethrough_Click(object sender, RoutedEventArgs e) => ToggleStrikethrough();

    private void ToggleStrikethrough()
    {
        var selection = Editor.Selection;
        var current = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        bool hasStrike = HasDecoration(current, TextDecorationLocation.Strikethrough);

        var updated = new TextDecorationCollection();
        if (current != null)
        {
            foreach (var decoration in current.Where(d => d.Location != TextDecorationLocation.Strikethrough))
                updated.Add(decoration.Clone());
        }
        if (!hasStrike)
        {
            foreach (var decoration in TextDecorations.Strikethrough)
                updated.Add(decoration.Clone());
        }

        selection.ApplyPropertyValue(Inline.TextDecorationsProperty, updated);
        Editor.Focus();
        UpdateFormatButtons();
    }

    private void QueueFormatButtonsUpdate()
    {
        if (_formatUpdateQueued) return;
        _formatUpdateQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            _formatUpdateQueued = false;
            UpdateFormatButtons();
        });
    }

    /// <summary>Highlights each formatting button whose style applies to the caret or the whole selection.</summary>
    private void UpdateFormatButtons()
    {
        var selection = Editor.Selection;

        SetActive(BoldButton, selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight &&
                              weight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight());
        SetActive(ItalicButton, selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle style &&
                                style != FontStyles.Normal);

        var decorations = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        SetActive(UnderlineButton, HasDecoration(decorations, TextDecorationLocation.Underline));
        SetActive(StrikeButton, HasDecoration(decorations, TextDecorationLocation.Strikethrough));

        var list = (selection.Start.Paragraph?.Parent as ListItem)?.List;
        bool numbered = list != null && list.MarkerStyle == TextMarkerStyle.Decimal;
        SetActive(BulletsButton, list != null && !numbered);
        SetActive(NumberingButton, numbered);
    }

    private static bool HasDecoration(TextDecorationCollection? decorations, TextDecorationLocation location) =>
        decorations != null && decorations.Any(d => d.Location == location);

    private static void SetActive(Button button, bool active) => button.Tag = active ? "Active" : null;

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        // Keep rich content copied between notes; paste anything else as plain text so the note stays tidy.
        if (e.DataObject.GetDataPresent(DataFormats.Xaml) || e.DataObject.GetDataPresent(DataFormats.XamlPackage))
            return;

        if (e.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            var plain = new DataObject();
            plain.SetData(DataFormats.UnicodeText, text);
            e.DataObject = plain;
        }
        else
        {
            e.CancelCommand();
        }
    }
}
