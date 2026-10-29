namespace CommunitiesForever.Upload.Core.Game;

/// <summary>
/// The version folder of the World of Warcraft install the app works in: <c>_forever_</c>, or <c>_retail_</c> in retail
/// test mode (started with <c>--retail</c>, for testing the addon on the retail client). The addon file and Runs.lua
/// follow the watched install, so both directions use this folder.
/// </summary>
public sealed record GameFolderOptions(string VersionFolder)
{
    public const string RetailFolder = "_retail_";

    public static GameFolderOptions Forever { get; } = new(GameScanner.ForeverFolder);

    public static GameFolderOptions RetailTest { get; } = new(RetailFolder);

    public bool IsRetailTest => string.Equals(VersionFolder, RetailFolder, StringComparison.OrdinalIgnoreCase);
}
