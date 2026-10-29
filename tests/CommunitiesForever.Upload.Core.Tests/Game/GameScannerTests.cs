using CommunitiesForever.Upload.Core.Game;

namespace CommunitiesForever.Upload.Core.Tests.Game;

public sealed class GameScannerTests
{
    private const string Wow = @"C:\Program Files (x86)\World of Warcraft";
    private const string Forever = Wow + @"\_forever_";
    private const string Accounts = Forever + @"\WTF\Account";
    private const string Retail = Wow + @"\_retail_";

    private static readonly DateTimeOffset Saved = new(2026, 10, 27, 21, 12, 0, TimeSpan.Zero);

    private readonly MemoryDisk _disk = new();

    [Fact]
    public void FindsTheInstallAndEveryAccount()
    {
        _disk.AddAccount(Accounts, "123456789#1", Saved);
        _disk.AddAccount(Accounts, "123456789#2", null);
        _disk.AddDirectory(Accounts + @"\SavedVariables");

        var install = Assert.Single(Scanner(Wow).Scan());

        Assert.Equal(Forever, install.ForeverPath);
        Assert.Collection(
            install.Accounts,
            a =>
            {
                Assert.Equal("123456789#1", a.Name);
                Assert.Equal(Saved, a.SavedAt);
                Assert.Equal(Accounts + @"\123456789#1\SavedVariables\CommunitiesForever.lua", a.AddonFilePath);
            },
            a =>
            {
                Assert.Equal("123456789#2", a.Name);
                Assert.False(a.HasAddonFile);
            });
    }

    [Theory]
    [InlineData(Wow)]
    [InlineData(Wow + @"\_retail_")]
    [InlineData(Forever)]
    [InlineData(Forever + @"\")]
    [InlineData(Accounts)]
    [InlineData(Accounts + @"\123456789#1")]
    [InlineData(Accounts + @"\123456789#1\SavedVariables")]
    public void ChosenFolderLeadsToTheInstall(string chosen)
    {
        _disk.AddAccount(Accounts, "123456789#1", Saved);
        _disk.AddDirectory(Wow + @"\_retail_");

        var install = Scanner().Inspect(chosen);

        Assert.Equal(Forever, install?.ForeverPath);
    }

    [Fact]
    public void FolderWithoutForeverIsNothing()
    {
        _disk.AddDirectory(Wow + @"\_retail_");

        Assert.Null(Scanner().Inspect(Wow));
        Assert.Null(Scanner().Inspect(""));
    }

    [Fact]
    public void FreshInstallHasNoAccountsYet()
    {
        _disk.AddDirectory(Forever);

        Assert.Empty(Scanner().Inspect(Forever)!.Accounts);
    }

    [Fact]
    public void EachInstallOnceEvenWhenSeveralHintsLeadThere()
    {
        _disk.AddAccount(Accounts, "A", Saved);
        _disk.AddAccount(@"D:\Games\World of Warcraft\_forever_\WTF\Account", "B", Saved);

        var installs = Scanner(Wow, Wow + @"\_retail_", Forever.ToUpperInvariant(), "C:/Program Files (x86)/World of Warcraft", @"E:\nothing", @"D:\Games\World of Warcraft").Scan();

        Assert.Equal([Forever, @"D:\Games\World of Warcraft\_forever_"], installs.Select(i => i.ForeverPath));
    }

    [Fact]
    public void ForeverIsTheDefault()
    {
        Assert.False(GameFolderOptions.Forever.IsRetailTest);
        Assert.Equal("_forever_", GameFolderOptions.Forever.VersionFolder);
    }

    [Theory]
    [InlineData(Wow)]
    [InlineData(Forever)]
    [InlineData(Retail)]
    [InlineData(Retail + @"\WTF\Account\ACCOUNT\SavedVariables")]
    public void RetailTestModeLeadsToRetail(string chosen)
    {
        _disk.AddAccount(Accounts, "ACCOUNT", Saved);
        _disk.AddAccount(Retail + @"\WTF\Account", "ACCOUNT", Saved);

        var install = RetailScanner().Inspect(chosen);

        Assert.Equal(Retail, install?.ForeverPath);
        Assert.Equal(Retail + @"\WTF\Account\ACCOUNT\SavedVariables\CommunitiesForever.lua", Assert.Single(install!.Accounts).AddonFilePath);
    }

    [Fact]
    public void RetailTestModeScansOnlyRetail()
    {
        _disk.AddAccount(Accounts, "ACCOUNT", Saved);
        _disk.AddAccount(Retail + @"\WTF\Account", "ACCOUNT", Saved);

        var install = Assert.Single(new GameScanner(_disk, new Hints([Wow, Forever]), GameFolderOptions.RetailTest).Scan());

        Assert.Equal(Retail, install.ForeverPath);
        Assert.Equal(Forever, Assert.Single(Scanner(Wow, Retail).Scan()).ForeverPath);
    }

    [Fact]
    public void RetailTestModeWithoutRetailIsNothing()
    {
        _disk.AddAccount(Accounts, "ACCOUNT", Saved);

        Assert.Null(RetailScanner().Inspect(Wow));
    }

    private GameScanner Scanner(params string[] hints) => new(_disk, new Hints(hints));

    private GameScanner RetailScanner() => new(_disk, new Hints([]), GameFolderOptions.RetailTest);

    private sealed class Hints(string[] folders) : IInstallHints
    {
        public IEnumerable<string> CandidateFolders() => folders;
    }

    // Folders and file times only; the interface has no way to read contents, which is the point (CLAUDE.md rule 1).
    private sealed class MemoryDisk : IFileSystem
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _files = new(StringComparer.OrdinalIgnoreCase);

        public void AddDirectory(string path)
        {
            for (string? dir = path; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                _directories.Add(Path.TrimEndingDirectorySeparator(dir));
            }
        }

        public void AddAccount(string accounts, string name, DateTimeOffset? saved)
        {
            string variables = Path.Combine(accounts, name, "SavedVariables");
            AddDirectory(variables);
            if (saved is { } time)
            {
                _files[Path.Combine(variables, GameScanner.AddonFileName)] = time;
            }
        }

        public bool DirectoryExists(string path) => _directories.Contains(Path.TrimEndingDirectorySeparator(path));

        public IEnumerable<string> GetDirectories(string path) =>
            _directories.Where(d => string.Equals(Path.GetDirectoryName(d), Path.TrimEndingDirectorySeparator(path), StringComparison.OrdinalIgnoreCase)).ToList();

        public DateTimeOffset? GetLastWriteTime(string path) => _files.TryGetValue(path, out var time) ? time : null;
    }
}
