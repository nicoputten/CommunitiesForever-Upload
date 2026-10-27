using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.ViewModels;

namespace CommunitiesForever.Upload.App.Views;

/// <summary>Settings (board <c>settings</c>): the menu on the left jumps to a section and shows which one you're in.</summary>
public sealed partial class SettingsView : UserControl
{
    private readonly (Button Nav, Control Section, SettingsSection Kind)[] _sections;

    public SettingsView()
    {
        InitializeComponent();
        _sections =
        [
            (NavBehaviour, SectionBehaviour, SettingsSection.Behaviour),
            (NavGameFolders, SectionGameFolders, SettingsSection.GameFolders),
            (NavConnection, SectionConnection, SettingsSection.Connection),
            (NavAbout, SectionAbout, SettingsSection.About),
        ];
        foreach (var (nav, _, _) in _sections)
        {
            nav.Click += OnNavClick;
        }

        Page.ScrollChanged += OnScrolled;
    }

    /// <summary>Jumps to a section (also where the page opens).</summary>
    public void ShowSection(SettingsSection kind)
    {
        var section = _sections.First(s => s.Kind == kind).Section;
        Page.Offset = new Vector(0, Math.Max(0, section.Bounds.Top));
        Mark(kind);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel vm)
        {
            // Once laid out the sections have their place: open at the one asked for.
            Mark(vm.Section);
            Dispatcher.UIThread.Post(() => ShowSection(vm.Section), DispatcherPriority.Loaded);
        }
    }

    private void OnNavClick(object? sender, RoutedEventArgs e) => ShowSection(_sections.First(s => s.Nav == sender).Kind);

    // The section at the top of the page is the one marked in the menu.
    private void OnScrolled(object? sender, ScrollChangedEventArgs e)
    {
        var current = _sections.LastOrDefault(s => s.Section.Bounds.Top <= Page.Offset.Y + 1);
        Mark(current.Nav is null ? SettingsSection.Behaviour : current.Kind);
    }

    private void Mark(SettingsSection kind)
    {
        foreach (var (nav, _, k) in _sections)
        {
            nav.Classes.Set("on", k == kind);
        }
    }
}
