using Avalonia.Threading;
using CommunitiesForever.Upload.App.ViewModels;
using CommunitiesForever.Upload.Core.Game;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.App.Tests.Setup;

/// <summary>
/// A setup view model over fakes: a scripted site, an in-memory disk with one install and two accounts, fixed time
/// (27 Oct 2026 21:30 UTC). Shared with the design checks (linked into that project).
/// </summary>
internal sealed class SetupHarness
{
    public const string Forever = @"C:\Program Files (x86)\World of Warcraft\_forever_";

    public static readonly AccessToken Token = new("cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy");
    public static readonly SiteIdentity Nico = new("Nico#2201", "Doombringers", "DESKTOP-NICO", "1.0.0", DateTimeOffset.UnixEpoch, null);

    public SetupHarness()
    {
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Disk.AddAccount(Forever, "123456789#1", new DateTimeOffset(2026, 10, 27, 21, 12, 0, TimeSpan.Zero));
        Disk.AddAccount(Forever, "123456789#2", null);
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 27, 21, 30, 0, TimeSpan.Zero));

    public FakeSite Site { get; } = new();

    public MemoryTokens Tokens { get; } = new();

    public MemorySettingsFile SettingsFile { get; } = new();

    public MemoryDisk Disk { get; } = new();

    public List<Uri> Opened { get; } = [];

    public SetupViewModel Create() => new(
        new ConnectFlow(Site, Tokens, new Browser(Opened), new Device(), Time),
        new GameScanner(Disk, new Hints(Disk)),
        Site,
        Tokens,
        new SettingsStore(SettingsFile),
        new SiteOptions(new Uri("https://wow.vestras.nl/"), "1.0.0"),
        Time);

    /// <summary>Runs the UI jobs (and background scans) until <paramref name="done"/>, at most a few seconds.</summary>
    public static async Task Until(Func<bool> done)
    {
        for (int i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done(), "Timed out waiting for the setup to get there");
    }

    private sealed class Browser(List<Uri> opened) : IBrowser
    {
        public void Open(Uri uri) => opened.Add(uri);
    }

    internal sealed class Device : IDeviceInfo
    {
        public string DeviceName => "DESKTOP-NICO";
    }

    private sealed class Hints(MemoryDisk disk) : IInstallHints
    {
        public IEnumerable<string> CandidateFolders() => disk.Roots;
    }
}

internal sealed class FakeSite : ISiteClient
{
    public SiteResult<DeviceAuthorization> DeviceCode { get; set; } = SiteResult.Ok(new DeviceAuthorization(
        new DeviceCode("kq3V0dXyPZ8m1nR2c9WbTg"),
        "KXRT-7QMN",
        new Uri("https://wow.vestras.nl/uploader"),
        new Uri("https://wow.vestras.nl/uploader?code=KXRT-7QMN"),
        TimeSpan.FromSeconds(600),
        TimeSpan.FromSeconds(5)));

    /// <summary>Answers to the polls in order; pending once empty.</summary>
    public Queue<SiteResult<TokenPoll>> Polls { get; } = new();

    public SiteResult<SiteIdentity> Identity { get; set; } = SiteResult.Ok(SetupHarness.Nico);

    public Task<SiteResult<DeviceAuthorization>> RequestDeviceCodeAsync(string deviceName, CancellationToken cancellationToken = default) =>
        Task.FromResult(DeviceCode);

    public Task<SiteResult<TokenPoll>> PollTokenAsync(DeviceCode deviceCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Polls.Count > 0 ? Polls.Dequeue() : SiteResult.Ok(new TokenPoll(PollStatus.Pending)));

    public Task<SiteResult<SiteIdentity>> GetIdentityAsync(AccessToken token, CancellationToken cancellationToken = default) =>
        Task.FromResult(Identity);

    public Task<SiteResult<UploadReceipt>> UploadAsync(AccessToken token, Stream content, DateTimeOffset? savedAt, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SiteResult<bool>> RevokeAsync(AccessToken token, CancellationToken cancellationToken = default) =>
        Task.FromResult(SiteResult.Ok(true));
}

internal sealed class MemoryTokens : ITokenStore
{
    public AccessToken? Token { get; set; }

    public AccessToken? Load() => Token;

    public void Save(AccessToken token) => Token = token;

    public void Delete() => Token = null;
}

internal sealed class MemorySettingsFile : ISettingsFile
{
    public string? Text { get; set; }

    public string? Read() => Text;

    public void Write(string text) => Text = text;

    public void MoveAsideAsDamaged() => Text = null;
}

internal sealed class MemoryDisk : IFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _files = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Roots { get; } = [];

    public void AddAccount(string forever, string name, DateTimeOffset? saved)
    {
        if (!Roots.Contains(forever))
        {
            Roots.Add(forever);
        }

        string variables = Path.Combine(forever, "WTF", "Account", name, "SavedVariables");
        for (string? dir = variables; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            _directories.Add(dir);
        }

        if (saved is { } time)
        {
            _files[Path.Combine(variables, GameScanner.AddonFileName)] = time;
        }
    }

    public void Clear()
    {
        Roots.Clear();
        _directories.Clear();
        _files.Clear();
    }

    public bool DirectoryExists(string path) => _directories.Contains(Path.TrimEndingDirectorySeparator(path));

    public IEnumerable<string> GetDirectories(string path) =>
        _directories.Where(d => string.Equals(Path.GetDirectoryName(d), Path.TrimEndingDirectorySeparator(path), StringComparison.OrdinalIgnoreCase)).ToList();

    public DateTimeOffset? GetLastWriteTime(string path) => _files.TryGetValue(path, out var time) ? time : null;
}
