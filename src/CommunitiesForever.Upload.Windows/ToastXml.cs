using System.Globalization;
using System.Security;
using System.Text;
using CommunitiesForever.Upload.Core.Notifications;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// The toast as Windows' XML: title, line, our shield as app logo, buttons. Every click is a link (protocol activation):
/// a page opens in the browser, an app link reaches the running app; no COM server needed.
/// </summary>
internal static class ToastXml
{
    public static string Build(AppNotification notification, string? logoPath)
    {
        var xml = new StringBuilder();
        xml.Append(CultureInfo.InvariantCulture, $"<toast launch=\"{Escape(notification.Launch.AbsoluteUri)}\" activationType=\"protocol\">");
        xml.Append("<visual><binding template=\"ToastGeneric\">");
        xml.Append(CultureInfo.InvariantCulture, $"<text>{Escape(notification.Title)}</text>");
        if (notification.Line.Length > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<text>{Escape(notification.Line)}</text>");
        }

        if (logoPath is not null)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<image placement=\"appLogoOverride\" src=\"{Escape(new Uri(logoPath).AbsoluteUri)}\"/>");
        }

        xml.Append("</binding></visual>");
        if (notification.Buttons.Count > 0)
        {
            xml.Append("<actions>");
            foreach (var button in notification.Buttons)
            {
                xml.Append(button.Target is { } target
                    ? $"<action content=\"{Escape(button.Label)}\" activationType=\"protocol\" arguments=\"{Escape(target.AbsoluteUri)}\"/>"
                    : $"<action content=\"{Escape(button.Label)}\" activationType=\"system\" arguments=\"dismiss\"/>");
            }

            xml.Append("</actions>");
        }

        xml.Append("</toast>");
        return xml.ToString();
    }

    private static string Escape(string text) => SecurityElement.Escape(text);
}
