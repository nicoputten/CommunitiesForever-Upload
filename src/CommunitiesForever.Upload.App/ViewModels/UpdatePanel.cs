using System.Globalization;
using CommunitiesForever.Upload.Core.Updates;

namespace CommunitiesForever.Upload.App.ViewModels;

[Flags]
public enum UpdateButtons
{
    None = 0,
    RestartNow = 1,
    Cancel = 2,
    TryAgain = 4,
    OpenLog = 8,
}

/// <summary>
/// Settings › About &amp; updates (boards <c>settings-updates</c> and <c>update-states</c>): the version, a pill, one
/// line and the buttons for the update state.
/// </summary>
public sealed record UpdatePanel(
    string VersionLine,
    string? Pill,
    Tone PillTone,
    bool Spinning,
    string? Line,
    double? Progress,
    UpdateButtons Buttons,
    string? WhatsNew,
    string? Notes,
    string CheckLine,
    bool CanCheck,
    bool IsDevBuild)
{
    public bool HasPill => Pill is not null;

    public bool HasLine => Line is not null;

    public bool ShowBar => Progress is not null;

    public bool ShowRestartNow => Buttons.HasFlag(UpdateButtons.RestartNow);

    public bool ShowCancel => Buttons.HasFlag(UpdateButtons.Cancel);

    public bool ShowTryAgain => Buttons.HasFlag(UpdateButtons.TryAgain);

    public bool ShowOpenLog => Buttons.HasFlag(UpdateButtons.OpenLog);

    public bool HasWhatsNew => WhatsNew is not null;

    public static UpdatePanel For(UpdateChecker checker, DateTimeOffset now, TimeZoneInfo zone, string host, bool installWhenWowClosed)
    {
        ArgumentNullException.ThrowIfNull(checker);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        string current = checker.CurrentVersion;
        string next = ShortVersion(checker.NewVersion);
        string versionLine = checker.State == UpdateState.DevBuild
            ? $"Version {current} · local build"
            : checker.ReleasedAt is { } released
                ? $"Version {current} · released {TimeZoneInfo.ConvertTime(released, zone).ToString("d MMM", CultureInfo.InvariantCulture)}"
                : $"Version {current}";
        string lastCheck = checker.LastCheck is { } at ? $" · last check {Formats.Moment(TimeZoneInfo.ConvertTime(at, zone), localNow)}" : "";
        string checkLine = $"Automatic at start and every 24 hours{lastCheck}";
        bool canCheck = checker.State is UpdateState.Idle or UpdateState.UpToDate or UpdateState.CouldntCheck or UpdateState.Failed;

        UpdatePanel Panel(string? pill, Tone tone, string? line, UpdateButtons buttons = UpdateButtons.None, bool spinning = false, double? progress = null) =>
            new(versionLine, pill, tone, spinning, line, progress, buttons, null, null, checkLine, canCheck, false);

        switch (checker.State)
        {
            case UpdateState.DevBuild:
                return new UpdatePanel(versionLine, "Dev build", Tone.Dim, false, "Updates are off for builds that aren't installed releases.", null, UpdateButtons.None, null, null, checkLine, false, true);
            case UpdateState.UpToDate:
                return Panel("● Up to date", Tone.Fresh, null);
            case UpdateState.Checking:
                return Panel("Checking…", Tone.Gold, $"Asking {host}", spinning: true);
            case UpdateState.Downloading:
                double fraction = checker.DownloadSize > 0 ? Math.Clamp((double)checker.DownloadedBytes / checker.DownloadSize, 0, 1) : 0;
                return Panel(
                    $"Downloading {next}",
                    Tone.Gold,
                    $"{fraction * 100:0}% · {Megabytes(checker.DownloadedBytes)} of {Megabytes(checker.DownloadSize)} MB · uploads keep working",
                    progress: fraction);
            case UpdateState.Ready:
                string downloaded = checker.LastCheck is { } when ? $"Downloaded in the background {Formats.MomentAt(TimeZoneInfo.ConvertTime(when, zone), localNow)}. " : "";
                string installs = installWhenWowClosed
                    ? "It installs by itself when World of Warcraft is closed and nothing is waiting to upload, or now:"
                    : "It installs when you quit or Windows restarts, or now:";
                return Panel($"Version {next} ready", Tone.Gold, downloaded + installs, UpdateButtons.RestartNow) with
                {
                    WhatsNew = checker.NotesUrl is null ? null : $"What's new in {next}",
                    Notes = checker.NewNotes,
                };
            case UpdateState.WaitingForUpload:
                return Panel("Waiting for upload", Tone.Gold, "Restarting after this upload, a few seconds", UpdateButtons.Cancel, spinning: true);
            case UpdateState.CouldntCheck:
                return Panel("Couldn't check", Tone.Dim, $"No connection to {host}. Tries again in 1 hour by itself.", UpdateButtons.TryAgain);
            case UpdateState.Failed:
                return Panel("Update failed", Tone.Warn, $"Still on {current}, nothing lost. The download will be fetched again.", UpdateButtons.TryAgain | UpdateButtons.OpenLog);
            default:
                return Panel(null, Tone.Dim, null);
        }
    }

    /// <summary>"1.1.0" reads "1.1" (boards); "1.0.3" stays.</summary>
    public static string ShortVersion(string? version) =>
        version is null ? "" : version.EndsWith(".0", StringComparison.Ordinal) && version.Count(c => c == '.') == 2 ? version[..^2] : version;

    private static string Megabytes(long bytes) => (bytes / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture);
}
