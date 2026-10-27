namespace CommunitiesForever.Upload.Core.Game;

/// <summary>What the scanner may look at on disk: folders, and whether a file exists and when it was saved. Never contents.</summary>
public interface IFileSystem
{
    bool DirectoryExists(string path);

    IEnumerable<string> GetDirectories(string path);

    /// <summary>The file's last write time, or null when it doesn't exist.</summary>
    DateTimeOffset? GetLastWriteTime(string path);
}

/// <summary>Places where World of Warcraft may be installed (Windows: registry, Battle.net config, common folders).</summary>
public interface IInstallHints
{
    IEnumerable<string> CandidateFolders();
}

/// <summary>An account under <c>WTF\Account</c>; <see cref="SavedAt"/> is null while it has no addon file yet.</summary>
public sealed record GameAccount(string Name, string AddonFilePath, DateTimeOffset? SavedAt)
{
    public bool HasAddonFile => SavedAt is not null;
}

/// <summary>A WoW: Forever install: its <c>_forever_</c> folder and accounts.</summary>
public sealed record GameInstall(string ForeverPath, IReadOnlyList<GameAccount> Accounts)
{
    public bool Equals(GameInstall? other) =>
        other is not null && string.Equals(ForeverPath, other.ForeverPath, StringComparison.OrdinalIgnoreCase) && Accounts.SequenceEqual(other.Accounts);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ForeverPath);
}

/// <summary>
/// Finds WoW: Forever and its accounts (UP-05). It only checks folders and the addon file's save time; it never opens
/// the file (CLAUDE.md rule 1).
/// </summary>
public sealed class GameScanner(IFileSystem files, IInstallHints hints)
{
    public const string ForeverFolder = "_forever_";
    public const string AddonFileName = "CommunitiesForever.lua";

    /// <summary>Every install found through the hints, each once.</summary>
    public IReadOnlyList<GameInstall> Scan()
    {
        var installs = new List<GameInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in hints.CandidateFolders())
        {
            if (Inspect(candidate) is { } install && seen.Add(install.ForeverPath))
            {
                installs.Add(install);
            }
        }

        return installs;
    }

    /// <summary>
    /// The install for a folder the user chose: the WoW folder, a version folder next to <c>_forever_</c> (such as
    /// <c>_retail_</c>), <c>_forever_</c> itself, or any folder inside it (WTF, Account, an account).
    /// </summary>
    public GameInstall? Inspect(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        // Normalise "C:/Program Files (x86)" (Battle.net writes forward slashes) and ".." so each install is found once.
        string? forever = FindForever(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Trim())));
        return forever is null ? null : new GameInstall(forever, Accounts(forever));
    }

    private string? FindForever(string folder)
    {
        // Inside _forever_: walk up to it.
        for (string? dir = folder; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (string.Equals(Path.GetFileName(dir), ForeverFolder, StringComparison.OrdinalIgnoreCase))
            {
                return files.DirectoryExists(dir) ? dir : null;
            }
        }

        // The WoW folder, or a sibling of _forever_ (the registry points at _retail_).
        foreach (string? root in new[] { folder, Path.GetDirectoryName(folder) })
        {
            if (root is not null && Path.Combine(root, ForeverFolder) is var candidate && files.DirectoryExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private List<GameAccount> Accounts(string forever)
    {
        string accounts = Path.Combine(forever, "WTF", "Account");
        if (!files.DirectoryExists(accounts))
        {
            return [];
        }

        return files.GetDirectories(accounts)
            .Select(Path.GetFileName)
            .OfType<string>()
            // WTF\Account\SavedVariables holds account-wide data of other addons, not an account.
            .Where(name => !string.Equals(name, "SavedVariables", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                string file = Path.Combine(accounts, name, "SavedVariables", AddonFileName);
                return new GameAccount(name, file, files.GetLastWriteTime(file));
            })
            .ToList();
    }
}
