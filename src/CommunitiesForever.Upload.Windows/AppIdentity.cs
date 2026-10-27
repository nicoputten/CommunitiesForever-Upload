namespace CommunitiesForever.Upload.Windows;

/// <summary>How Windows knows the app: the notification sender, the app link, the Run entry, the single-instance names.</summary>
public static class AppIdentity
{
    public const string AppId = "CommunitiesForever.Uploader";
    public const string DisplayName = "CommunitiesForever Uploader";

    /// <summary>App links from notifications: <c>communitiesforever-uploader://open</c>, <c>…://reconnect</c>.</summary>
    public const string Scheme = "communitiesforever-uploader";

    public static readonly Uri Open = new($"{Scheme}://open");
    public static readonly Uri Reconnect = new($"{Scheme}://reconnect");
}
