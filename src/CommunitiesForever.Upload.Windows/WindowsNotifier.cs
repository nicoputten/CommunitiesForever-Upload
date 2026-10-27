using System.Runtime.InteropServices;
using CommunitiesForever.Upload.Core.Notifications;
using Microsoft.Extensions.Logging;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace CommunitiesForever.Upload.Windows;

/// <summary>Windows toast notifications for this unpackaged app (registered by <see cref="ShellRegistration"/>).</summary>
internal sealed partial class WindowsNotifier(ILogger<WindowsNotifier> logger) : INotifier
{
    private static readonly string LogoPath = Path.Combine(AppContext.BaseDirectory, "app-icon.png");

    public void Show(AppNotification notification)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml(ToastXml.Build(notification, File.Exists(LogoPath) ? LogoPath : null));
            ToastNotificationManager.CreateToastNotifier(AppIdentity.AppId).Show(new ToastNotification(xml));
        }
        catch (Exception e) when (e is COMException or InvalidOperationException or ArgumentException)
        {
            // A notification must never stop the app (e.g. notifications switched off for it in Windows).
            Log.NotShown(logger, e.Message);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Notification not shown: {Reason}")]
        public static partial void NotShown(ILogger logger, string reason);
    }
}
