using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.App.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void OpensAtTheBoardSize()
    {
        using var services = App.ConfigureServices();
        var window = services.GetRequiredService<MainWindow>();

        window.Show();

        Assert.True(window.IsVisible);
        Assert.Equal(App.DefaultName, window.Title);
        Assert.Equal(900, window.Width);
        Assert.Equal(620, window.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void ClosedWindowIsCollected()
    {
        // Long-running app (CLAUDE.md rule 11): a closed window must not be kept alive.
        var window = OpenAndClose();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(window.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndClose()
    {
        using var services = App.ConfigureServices();
        var window = services.GetRequiredService<MainWindow>();
        window.Show();
        window.Close();
        return new WeakReference(window);
    }
}
