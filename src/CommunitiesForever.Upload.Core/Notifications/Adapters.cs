namespace CommunitiesForever.Upload.Core.Notifications;

/// <summary>A notification button: opens <see cref="Target"/> (a page or an app link), or just dismisses when null.</summary>
public sealed record NotificationButton(string Label, Uri? Target);

/// <summary>A Windows notification as the app writes it (Windows draws it in its own style; docs/DESIGN-NOTES.md).</summary>
public sealed record AppNotification(string Title, string Line, Uri Launch, IReadOnlyList<NotificationButton> Buttons);

/// <summary>Shows notifications (Windows: toast notifications, registered for this user).</summary>
public interface INotifier
{
    void Show(AppNotification notification);
}

/// <summary>Start with Windows (Windows: the user's Run key, starting hidden in the tray).</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    void Apply(bool enabled);
}
