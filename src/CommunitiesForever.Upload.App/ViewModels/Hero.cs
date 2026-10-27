using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>The colour of the header's status line (board <c>states</c>).</summary>
public enum Tone
{
    Fresh,
    Gold,
    Playing,
    Dim,
    Warn,
}

[Flags]
public enum HeroActions
{
    None = 0,
    UploadNow = 1,
    OpenSite = 2,
    Skip = 4,
    TryNow = 8,
    Reconnect = 16,
    FindGame = 32,
    Resume = 64,
    Report = 128,
}

/// <summary>What the main window's status header shows: line, title, text, buttons (boards <c>main-window</c> and <c>states</c>).</summary>
public sealed record Hero(Tone Tone, string Status, string Title, string Line, HeroActions Actions, Uri? Report = null, bool Uploading = false)
{
    /// <summary>"Uploaded" stays in the header this long after an upload (docs/DESIGN-NOTES.md).</summary>
    public static readonly TimeSpan UploadedShownFor = TimeSpan.FromHours(1);

    public bool IsWarn => Tone == Tone.Warn;

    public bool ShowUploadNow => Actions.HasFlag(HeroActions.UploadNow);

    public bool ShowOpenSite => Actions.HasFlag(HeroActions.OpenSite);

    public bool ShowSkip => Actions.HasFlag(HeroActions.Skip);

    public bool ShowTryNow => Actions.HasFlag(HeroActions.TryNow);

    public bool ShowReconnect => Actions.HasFlag(HeroActions.Reconnect);

    public bool ShowFindGame => Actions.HasFlag(HeroActions.FindGame);

    public bool ShowResume => Actions.HasFlag(HeroActions.Resume);

    public bool ShowReport => Actions.HasFlag(HeroActions.Report) && Report is not null;

    /// <summary>The rings around the shield pulse only while all is well.</summary>
    public bool IsCalm => Tone is Tone.Fresh or Tone.Playing or Tone.Gold;

    /// <summary>The header for the most urgent account. <paramref name="name"/> prefixes the text when several accounts are watched.</summary>
    public static Hero For(AccountStatus? status, string? name, DateTimeOffset now, TimeZoneInfo zone, string host, DateTimeOffset? pausedUntil)
    {
        string For(string text) => name is null ? text : $"{name} · {text}";
        DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone);
        var localNow = Local(now);
        var last = status?.LastUpload;
        string lastLine = last is null ? "No upload yet" : $"Last upload {Formats.MomentAt(Local(last.UploadedAt), localNow)}";

        switch (status?.State ?? AccountState.Watching)
        {
            case AccountState.Watching or AccountState.Playing
                when last is { Status: not UploadStatus.Rejected } && now - last.UploadedAt < UploadedShownFor:
                return new Hero(
                    Tone.Fresh,
                    $"● Uploaded {Formats.Clock(Local(last.UploadedAt))}",
                    // States board "Done": the first count as title, the highlights below; the full line is in Recent uploads.
                    last.SummaryLine.Split(" · ")[0],
                    For(string.Join(" · ", last.Highlights)),
                    HeroActions.Report | HeroActions.OpenSite,
                    last.ReportUrl);

            case AccountState.Playing:
                return new Hero(
                    Tone.Playing,
                    "● Playing",
                    "Waiting for the game to save",
                    For("World of Warcraft writes the file when you log out, type /reload or quit. Nothing to do."),
                    HeroActions.UploadNow | HeroActions.OpenSite);

            case AccountState.Settling:
                int seconds = Math.Max(0, (int)Math.Ceiling(((status!.SettlesAt ?? now) - now).TotalSeconds));
                return new Hero(
                    Tone.Gold,
                    "● File changed",
                    $"Game saved: uploading in {seconds} s",
                    For("Waiting until the file stops changing, so we never send half a file."),
                    HeroActions.UploadNow | HeroActions.Skip);

            case AccountState.ReadyToUpload:
                return new Hero(
                    Tone.Gold,
                    "● Ready to upload",
                    "The game saved the file",
                    For("Upload automatically is off. Upload it now, or skip this one."),
                    HeroActions.UploadNow | HeroActions.Skip);

            case AccountState.Paused:
                return new Hero(
                    Tone.Dim,
                    "● Paused",
                    pausedUntil is { } until ? $"Paused until {Formats.Clock(Local(until))}" : "Paused",
                    For("Saves are noted; the newest goes up when the pause ends."),
                    HeroActions.Resume);

            case AccountState.Uploading:
                return new Hero(
                    Tone.Gold,
                    "● Uploading",
                    $"Sending to {host}",
                    For(status!.File is { } file ? $"{Formats.Size(file.Size)} · usually under a second" : "Usually under a second"),
                    HeroActions.None,
                    Uploading: true);

            case AccountState.WaitingToSend:
                string next = status!.NextTryAt is { } at ? $"Next try {Formats.Wait(at - now)}." : "It tries again soon.";
                return new Hero(
                    Tone.Warn,
                    "● Waiting to send",
                    $"Can't reach {host}",
                    For($"The file is kept and sent automatically. {next}"),
                    HeroActions.TryNow);

            case AccountState.NotAllowed:
                return new Hero(
                    Tone.Warn,
                    "● Not allowed",
                    "The site doesn't take uploads from you right now",
                    For("Your upload rights are off on the site. It tries again every hour."),
                    HeroActions.TryNow);

            case AccountState.NotConnected:
                return new Hero(
                    Tone.Warn,
                    "● Not connected",
                    "The site doesn't accept this uploader anymore",
                    "Its token was revoked. Connect again; it takes a minute.",
                    HeroActions.Reconnect);

            case AccountState.NotWatching:
                return new Hero(
                    Tone.Warn,
                    "● Not watching",
                    "Where is World of Warcraft?",
                    For("The game folder moved or the addon file is gone. Scan again or pick the folder yourself."),
                    HeroActions.FindGame);

            case AccountState.NotUploaded:
                var (title, line) = Rejected(status!.Rejection, status.SchemaVersion);
                var report = last is { Status: UploadStatus.Rejected } ? last.ReportUrl : null;
                return new Hero(
                    Tone.Warn,
                    "● Not uploaded",
                    title,
                    For(line),
                    (report is null ? HeroActions.None : HeroActions.Report) | HeroActions.OpenSite,
                    report);

            default:
                return new Hero(
                    Tone.Fresh,
                    "● Watching",
                    "All caught up",
                    For(lastLine),
                    HeroActions.UploadNow | HeroActions.OpenSite);
        }
    }

    // One line per reason (UPLOADER-API.md "Uploading"); texts approved with Nico (docs/DESIGN-NOTES.md).
    private static (string Title, string Line) Rejected(string? code, int? schemaVersion) => code switch
    {
        UploadReceipt.Rejections.NewerFormat => (
            "The site can't read this file yet",
            $"Your addon writes data format {schemaVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}; the site can't read it yet. It uploads by itself once the site is updated."),
        UploadReceipt.Rejections.WrongFile => ("This isn't the CommunitiesForever file", "The site expected the addon's saved data."),
        UploadReceipt.Rejections.Damaged => ("The file is damaged", "The game may not have finished writing it. Log out or /reload to save it again."),
        UploadReceipt.Rejections.NotYourFile => ("This file belongs to someone else", "Only your own addon file can be uploaded from this PC."),
        UploadReceipt.Rejections.OtherCommunity => ("This file is from another community", "It belongs to a different site."),
        UploadReceipt.Rejections.NoCommunityYet => ("The site isn't set up yet", "The admin's first upload starts the community. It tries again in an hour."),
        "too_large" => ("The file is too large", "The site takes files up to 25 MB."),
        "empty_file" => ("The file is empty", "Log in once with the addon enabled."),
        _ => ("The site didn't take this file", "See the session report for why."),
    };
}
