using CommunitiesForever.Upload.Core.Settings;

namespace CommunitiesForever.Upload.Core.Tests.Settings;

public sealed class SettingsStoreTests
{
    private readonly MemoryFile _file = new();

    [Fact]
    public void FirstRunHasNoSettings() => Assert.IsType<SettingsLoad.Missing>(new SettingsStore(_file).Load());

    [Fact]
    public void SavedSettingsLoadTheSame()
    {
        var settings = new AppSettings
        {
            Accounts = [new WatchedAccount(@"C:\Games\World of Warcraft\_forever_", "123456789#1")],
            UploadAutomatically = false,
            StartWithWindows = true,
            CloseHidesToTray = false,
            Notifications = NotificationLevel.OnlyProblems,
            SiteUri = new Uri("http://localhost:3000/"),
        };

        new SettingsStore(_file).Save(settings);
        var loaded = new SettingsStore(_file).Load();

        Assert.Equal(settings, Assert.IsType<SettingsLoad.Loaded>(loaded).Settings);
        Assert.Contains("\"version\": 2", _file.Text, StringComparison.Ordinal);
        Assert.Contains("\"OnlyProblems\"", _file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Version1FileAsReleasedStillLoads()
    {
        // Rule 10: this exact text is what release 1 writes. Never change it; add a migration instead.
        _file.Text = """
            {
              "version": 1,
              "accounts": [ { "installPath": "C:\\WoW\\_forever_", "account": "NICO" } ],
              "uploadAutomatically": true,
              "startWithWindows": false,
              "closeHidesToTray": true,
              "notifications": "EveryUpload",
              "siteUrl": null
            }
            """;

        var settings = Assert.IsType<SettingsLoad.Loaded>(new SettingsStore(_file).Load()).Settings;

        Assert.Equal(new WatchedAccount(@"C:\WoW\_forever_", "NICO"), Assert.Single(settings.Accounts));
        Assert.False(settings.StartWithWindows);
        Assert.Null(settings.SiteUri);
        // Migrated to version 2 (UP-10): the new setting starts on.
        Assert.True(settings.InstallUpdatesWhenWowClosed);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ }")]
    [InlineData("{ \"version\": \"one\" }")]
    [InlineData("{ \"version\": 0 }")]
    [InlineData("{ \"version\": 1, \"notifications\": \"Loud\" }")]
    public void DamagedFileIsKeptAside(string text)
    {
        _file.Text = text;

        Assert.IsType<SettingsLoad.Damaged>(new SettingsStore(_file).Load());
        Assert.True(_file.MovedAside);
    }

    [Fact]
    public void NewerFileIsNeverOverwritten()
    {
        _file.Text = """{ "version": 99, "somethingNew": true }""";
        var store = new SettingsStore(_file);

        Assert.Equal(99, Assert.IsType<SettingsLoad.FromNewerVersion>(store.Load()).Version);
        Assert.Throws<InvalidOperationException>(() => store.Save(new AppSettings()));
        Assert.Contains("somethingNew", _file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultsMatchTheSetupBoard()
    {
        var settings = new AppSettings();

        Assert.True(settings.UploadAutomatically);
        Assert.True(settings.StartWithWindows);
        Assert.True(settings.CloseHidesToTray);
        Assert.Equal(NotificationLevel.EveryUpload, settings.Notifications);
    }

    private sealed class MemoryFile : ISettingsFile
    {
        public string? Text { get; set; }

        public bool MovedAside { get; private set; }

        public string? Read() => Text;

        public void Write(string text) => Text = text;

        public void MoveAsideAsDamaged()
        {
            MovedAside = true;
            Text = null;
        }
    }
}
