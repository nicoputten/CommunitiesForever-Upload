namespace CommunitiesForever.Upload.Windows;

/// <summary>Where the app keeps its files, per Windows user (docs/ARCHITECTURE.md "Storage").</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CommunitiesForever.Upload");
}
