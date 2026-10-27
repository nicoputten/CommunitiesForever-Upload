namespace CommunitiesForever.Upload.Core.Site;

/// <summary>Keeps the upload token (Windows: DPAPI, current user; CLAUDE.md rule 4).</summary>
public interface ITokenStore
{
    AccessToken? Load();

    void Save(AccessToken token);

    void Delete();
}

/// <summary>Opens a page in the user's browser.</summary>
public interface IBrowser
{
    void Open(Uri uri);
}

/// <summary>What the site shows for this PC ("DESKTOP-NICO").</summary>
public interface IDeviceInfo
{
    string DeviceName { get; }
}
