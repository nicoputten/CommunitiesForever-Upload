using CommunitiesForever.Upload.App.Tests.Setup;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.App.Tests.Main;

/// <summary>
/// A main view model over a scripted loop and the board's sample history, at 27 Oct 2026 21:30 UTC.
/// Shared with the design checks (linked into that project).
/// </summary>
internal sealed class MainHarness
{
    public const string Forever = @"C:\Program Files (x86)\World of Warcraft\_forever_";

    public static readonly WatchedAccount First = new(Forever, "123456789#1");
    public static readonly WatchedAccount Second = new(Forever, "123456789#2");

    public MainHarness(params WatchedAccount[] accounts)
    {
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Accounts = accounts.Length > 0 ? accounts : [First];
        Tokens.Token = SetupHarness.Token;
        new SettingsStore(SettingsFile).Save(new AppSettings { Accounts = Accounts });
        History = new UploadHistory(new MemoryHistory());
        Loop.Statuses = [.. Accounts.Select(a => new AccountStatus(a, AccountState.Watching))];
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero));

    public FakeLoop Loop { get; } = new();

    public FakeSite Site { get; } = new();

    public MemoryTokens Tokens { get; } = new();

    public MemorySettingsFile SettingsFile { get; } = new();

    /// <summary>What "Scan again" finds; empty unless a test adds installs.</summary>
    public MemoryDisk Disk { get; } = new();

    public UploadHistory History { get; }

    public IReadOnlyList<WatchedAccount> Accounts { get; }

    public List<Uri> Opened { get; } = [];

    public DateTimeOffset Now => Time.GetUtcNow();

    public static HistoryEntry Upload(long id, DateTimeOffset at, UploadStatus status, string summary, string[]? highlights = null, string? rejection = null, int? schema = null) =>
        new(id, at, status, rejection, schema, summary, highlights ?? [], new Uri($"https://wow.vestras.nl/upload/{id}"));

    /// <summary>The main-window board's recent uploads, the newest at 21:14 today.</summary>
    public void BoardHistory(WatchedAccount account)
    {
        Record(account, Upload(38, new DateTimeOffset(2026, 10, 21, 22, 5, 0, TimeSpan.Zero), UploadStatus.Ok, "Mira reached 60"));
        Record(account, Upload(39, new DateTimeOffset(2026, 10, 23, 23, 40, 0, TimeSpan.Zero), UploadStatus.Ok, "2 levels gained · 9 sightings"));
        Record(account, Upload(41, new DateTimeOffset(2026, 10, 27, 19, 2, 0, TimeSpan.Zero), UploadStatus.NothingNew, "Nothing new"));
        Record(account, Upload(
            42,
            new DateTimeOffset(2026, 10, 27, 21, 14, 0, TimeSpan.Zero),
            UploadStatus.Ok,
            "5 levels gained · 4 skill gains · 1 new character",
            ["Ves 37 → 39", "Lia 35 → 37", "Tailoring 150 → 187"]));
    }

    public void Record(WatchedAccount account, HistoryEntry upload) =>
        History.Record(account, new LedgerEntry($"h{upload.UploadId}", LedgerOutcome.Ok, null, upload.UploadedAt), upload);

    /// <summary>Sets an account's status as the loop would report it (with its last upload and the file's stamp).</summary>
    public void Status(WatchedAccount account, AccountState state, DateTimeOffset? settlesAt = null, DateTimeOffset? nextTryAt = null, string? rejection = null, int? schema = null)
    {
        var status = new AccountStatus(
            account,
            state,
            settlesAt,
            nextTryAt,
            rejection,
            schema,
            History.LastUpload(account),
            state == AccountState.NotWatching ? null : new FileStamp(48 * 1024, new DateTimeOffset(2026, 10, 27, 21, 12, 0, TimeSpan.Zero)));
        Loop.Statuses = [.. Loop.Statuses.Where(s => s.Account != account).Append(status).OrderBy(s => Accounts.ToList().IndexOf(s.Account))];
        Loop.Changed();
    }

    public static readonly SiteOptions Options = new(new Uri("https://wow.vestras.nl/"), "1.0.0");

    public MainViewModel Create() => new(
        Loop,
        History,
        new SettingsStore(SettingsFile),
        Site,
        Tokens,
        Options,
        new Browser(Opened),
        Time,
        new GameScanner(Disk, new Hints(Disk)),
        CreateSettings);

    /// <summary>The settings page over the same fakes.</summary>
    public SettingsViewModel CreateSettings() => new(
        new SettingsStore(SettingsFile),
        Loop,
        new GameScanner(Disk, new Hints(Disk)),
        new ConnectFlow(Site, Tokens, new Browser(Opened), new SetupHarness.Device(), Time),
        Site,
        Tokens,
        Options,
        new Browser(Opened),
        Time);

    private sealed class Hints(MemoryDisk disk) : IInstallHints
    {
        public IEnumerable<string> CandidateFolders() => disk.Roots;
    }

    private sealed class Browser(List<Uri> opened) : IBrowser
    {
        public void Open(Uri uri) => opened.Add(uri);
    }

    private sealed class MemoryHistory : IHistoryFile
    {
        private string? _text;

        public string? Read() => _text;

        public void Write(string text) => _text = text;
    }
}

internal sealed class FakeLoop : IUploadLoop
{
    public event EventHandler? StatusChanged;

    public IReadOnlyList<AccountStatus> Statuses { get; set; } = [];

    public DateTimeOffset? PausedUntil { get; set; }

    public List<string> Calls { get; } = [];

    public void Changed() => StatusChanged?.Invoke(this, EventArgs.Empty);

    public void UploadNow(WatchedAccount? account = null) => Calls.Add($"UploadNow {account?.Account ?? "all"}");

    public void Skip(WatchedAccount account) => Calls.Add($"Skip {account.Account}");

    public void Pause(DateTimeOffset until)
    {
        Calls.Add($"Pause {until:yyyy-MM-dd HH:mm}");
        PausedUntil = until;
    }

    public void EndPause()
    {
        Calls.Add("EndPause");
        PausedUntil = null;
    }
}
