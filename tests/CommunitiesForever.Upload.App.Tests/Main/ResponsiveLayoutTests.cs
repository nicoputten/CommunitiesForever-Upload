using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.Tests.Main;

/// <summary>
/// The main window at every size it allows (down to AppWindowMinW × AppWindowMinH), in every hero state:
/// the hero's text keeps a readable width and never breaks inside a word, nothing sticks out of the hero,
/// and nothing covers the status bar.
/// </summary>
public sealed class ResponsiveLayoutTests
{
    private static readonly double[] Widths = [640, 680, 737, 738, 760, 800, 818, 822, 860, 900, 1200];
    private static readonly double[] Heights = [480, 620];

    public static TheoryData<string> States() =>
    [
        .. Enum.GetNames<AccountState>(),
        "Uploaded",
        "UpdateNeeded",
    ];

    [AvaloniaTheory]
    [MemberData(nameof(States))]
    public async Task FitsAtEverySize(string state)
    {
        var h = await Arrange(state);
        var vm = h.Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await SetupHarness.Until(() => vm.Identity is not null);
        vm.Refresh();

        double textMin = (double)window.FindResource("AppHeroTextMinW")!;
        var problems = new List<string>();
        foreach (double height in Heights)
        {
            foreach (double width in Widths)
            {
                window.Width = width;
                window.Height = height;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                problems.AddRange(Check(window, textMin).Select(p => $"{width} x {height}: {p}"));
            }
        }

        window.Close();
        Assert.Empty(problems);
    }

    private static IEnumerable<string> Check(MainWindow window, double textMin)
    {
        var hero = window.FindControl<Border>("Hero")!;
        var text = window.FindControl<StackPanel>("HeroText")!;
        var actions = window.FindControl<StackPanel>("HeroActions")!;
        var main = window.FindControl<ScrollViewer>("Main")!;
        var statusBar = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("statusbar"));

        if (text.Bounds.Width < textMin)
        {
            yield return $"hero text {text.Bounds.Width:0} wide";
        }

        foreach (var (name, control) in new (string, Control)[] { ("text", text), ("buttons", actions) })
        {
            var box = Box(control, hero);
            if (box.Right > hero.Bounds.Width - hero.Padding.Right + 0.5 || box.Bottom > hero.Bounds.Height - hero.Padding.Bottom + 0.5)
            {
                yield return $"hero {name} sticks out ({box} in {hero.Bounds.Size})";
            }
        }

        foreach (var block in text.GetVisualDescendants().OfType<TextBlock>().Where(b => b.IsVisible && b.Text is { Length: > 0 }))
        {
            string content = block.Text!;
            foreach (var line in block.TextLayout.TextLines.Skip(1))
            {
                int start = line.FirstTextSourceIndex;
                if (start > 0 && start < content.Length && !char.IsWhiteSpace(content[start - 1]))
                {
                    yield return $"word broken: '{content[Math.Max(0, start - 10)..start]}|{content[start..Math.Min(content.Length, start + 10)]}'";
                }
            }
        }

        if (Box(main, window).Bottom > Box(statusBar, window).Top + 0.5)
        {
            yield return "content covers the status bar";
        }
    }

    private static Rect Box(Visual control, Visual relativeTo) =>
        new(control.TranslatePoint(default, relativeTo)!.Value, control.Bounds.Size);

    private static async Task<MainHarness> Arrange(string state)
    {
        var h = new MainHarness(MainHarness.First);
        h.BoardHistory(MainHarness.First);
        switch (state)
        {
            case "Uploaded":
                h.Status(MainHarness.First, AccountState.Watching);
                break;
            case "UpdateNeeded":
                h.Updates.Site.Status = SiteResult.Ok(new UpdateStatus("1.1.0", null, "1.1.0", new Uri("https://wow.vestras.nl/uploader/changes#1.1.0")));
                h.Updates.Updates.Newer = new UpdateOffer("1.1.0", 6_600_000, "Fixes");
                await h.Updates.Checker.CheckNowAsync();
                h.Status(MainHarness.First, AccountState.Paused);
                break;
            default:
                var account = Enum.Parse<AccountState>(state);
                h.Time.Advance(TimeSpan.FromHours(1));
                h.Status(
                    MainHarness.First,
                    account,
                    settlesAt: h.Now.AddSeconds(4),
                    nextTryAt: h.Now.AddMinutes(1),
                    rejection: account == AccountState.NotUploaded ? "newer_format" : null,
                    schema: account == AccountState.NotUploaded ? 3 : null);
                break;
        }

        return h;
    }
}
