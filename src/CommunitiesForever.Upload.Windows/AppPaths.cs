namespace CommunitiesForever.Upload.Windows;

/// <summary>Where the app keeps its files, per Windows user (docs/ARCHITECTURE.md "Storage").</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CommunitiesForever.Upload");

    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    /// <summary>Settings and history of retail test mode (<c>--retail</c>), apart from the normal ones.</summary>
    public static string RetailTestDirectory { get; } = Path.Combine(DataDirectory, "retail-test");
}
