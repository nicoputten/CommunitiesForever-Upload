using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using CommunitiesForever.Upload.App.Styles;
using Microsoft.Extensions.DependencyInjection;

namespace CommunitiesForever.Upload.App.Tests;

public sealed class StyleTests
{
    [AvaloniaFact]
    public void TokensResolveAsTypedResources()
    {
        var app = Application.Current!;

        Assert.True(app.TryFindResource("Gold", out object? gold));
        Assert.Equal(Color.Parse("#D4AF6A"), gold);
        Assert.True(app.TryFindResource("S4", out object? space));
        Assert.Equal(16d, space);
        Assert.True(app.TryFindResource("DurFast", out object? duration));
        Assert.Equal(TimeSpan.FromMilliseconds(120), duration);
        Assert.True(app.TryFindResource("FontUi", out object? font));
        Assert.IsType<FontFamily>(font);
    }

    [AvaloniaFact]
    public void LetterSpacingFollowsTheFontSize()
    {
        var text = new TextBlock { FontSize = 20 };

        Typography.SetLetterSpacingEm(text, 0.05);
        Assert.Equal(1, text.LetterSpacing, 3);

        text.FontSize = 40;
        Assert.Equal(2, text.LetterSpacing, 3);
    }

    [AvaloniaFact]
    public void ReducedMotionRemovesTransitions()
    {
        var app = (App)Application.Current!;
        var button = new Button();
        var window = new Window { Content = button };
        window.Show();

        Assert.NotNull(button.Transitions);
        app.ApplyMotion(animationsEnabled: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(button.Transitions);
        app.ApplyMotion(animationsEnabled: true);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(button.Transitions);

        window.Close();
    }

    [AvaloniaFact]
    public void GalleryOpensAndIsCollectedAfterClose()
    {
        // Long-running app (CLAUDE.md rule 11): the title bar must not keep the window alive.
        var window = OpenAndCloseGallery();
        // Let Avalonia finish the queued close and render work, which still holds the window.
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(window.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndCloseGallery()
    {
        using var services = App.ConfigureServices();
        var window = services.GetRequiredService<GalleryWindow>();
        window.Show();
        Assert.True(window.IsVisible);
        window.Close();
        return new WeakReference(window);
    }
}
