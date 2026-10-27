using System.Diagnostics;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Windows;

/// <summary>Opens a page in the default browser.</summary>
internal sealed class WindowsBrowser : IBrowser
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
