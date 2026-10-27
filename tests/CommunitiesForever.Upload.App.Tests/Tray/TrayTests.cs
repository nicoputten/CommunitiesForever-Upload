using CommunitiesForever.Upload.App.Tray;
using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;
using CommunitiesForever.Upload.Windows;

namespace CommunitiesForever.Upload.App.Tests.Tray;

public sealed class TrayTests
{
    private static readonly WatchedAccount Nico = new(@"C:\WoW\_forever_", "NICO");
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 21, 30, 0, TimeSpan.Zero);
    private static readonly HistoryEntry Last = new(
        42, new DateTimeOffset(2026, 10, 27, 21, 14, 0, TimeSpan.Zero), UploadStatus.Ok, null, null,
        "5 levels gained · 4 skill gains · 1 new character", ["Ves 37 → 39", "Tailoring 150 → 187"], new Uri("https://wow.vestras.nl/upload/42"));

    [Theory]
    [InlineData(AccountState.Watching, TrayIconKind.Watching, "Doombringers · last upload 21:14", "● Watching · last upload 21:14")]
    [InlineData(AccountState.Playing, TrayIconKind.Watching, "Doombringers · last upload 21:14", "● Playing · last upload 21:14")]
    [InlineData(AccountState.Uploading, TrayIconKind.Uploading, "Uploading CommunitiesForever.lua…", "● Uploading")]
    [InlineData(AccountState.NotConnected, TrayIconKind.Attention, "Upload token no longer works", "● Not connected")]
    [InlineData(AccountState.WaitingToSend, TrayIconKind.Attention, "Can't reach wow.vestras.nl", "● Waiting to send")]
    [InlineData(AccountState.NotWatching, TrayIconKind.Attention, "Where is World of Warcraft?", "● Not watching")]
    public void IconTooltipAndStatusFollowTheBoard(AccountState state, TrayIconKind icon, string tooltip, string status)
    {
        var model = TrayModel.For([new AccountStatus(Nico, state, LastUpload: Last)], "Doombringers", "wow.vestras.nl", Now, TimeZoneInfo.Utc, null);

        Assert.Equal(icon, model.Icon);
        Assert.Equal(tooltip, model.Tooltip);
        Assert.Equal(status, model.StatusLine);
    }

    [Fact]
    public void PausedSaysUntilWhen()
    {
        var model = TrayModel.For([new AccountStatus(Nico, AccountState.Paused)], "Doombringers", "wow.vestras.nl", Now, TimeZoneInfo.Utc, Now.AddMinutes(60));

        Assert.Equal(TrayIconKind.Paused, model.Icon);
        Assert.Equal("Paused until 22:30", model.Tooltip);
    }

    [Fact]
    public void TooltipFitsWindowsLimit()
    {
        var model = TrayModel.For([new AccountStatus(Nico, AccountState.Watching)], new string('D', 200), "wow.vestras.nl", Now, TimeZoneInfo.Utc, null);

        Assert.Equal(TrayModel.TooltipMax, model.Tooltip.Length);
    }

    [Fact]
    public void UploadedNotificationAsOnTheBoard()
    {
        var n = NotificationTexts.For(new NotificationEvent(NotificationKind.Uploaded, Nico, Last), "wow.vestras.nl");

        Assert.Equal("Uploaded · 5 levels gained", n.Title);
        Assert.Equal("Ves 37 → 39 · Tailoring 150 → 187", n.Line);
        Assert.Equal(AppIdentity.Open, n.Launch);
        var button = Assert.Single(n.Buttons);
        Assert.Equal("See what changed", button.Label);
        Assert.Equal(Last.ReportUrl, button.Target);
    }

    [Fact]
    public void TokenNotificationOffersReconnectAndLater()
    {
        var n = NotificationTexts.For(new NotificationEvent(NotificationKind.NotConnected, Nico), "wow.vestras.nl");

        Assert.Equal("Upload token no longer works", n.Title);
        Assert.Equal(["Reconnect", "Later"], n.Buttons.Select(b => b.Label));
        Assert.Equal(AppIdentity.Reconnect, n.Buttons[0].Target);
        Assert.Null(n.Buttons[1].Target);
    }

    [Fact]
    public void OfflineAndRejectedNotifications()
    {
        var offline = NotificationTexts.For(new NotificationEvent(NotificationKind.Offline, Nico), "wow.vestras.nl");
        var rejected = NotificationTexts.For(new NotificationEvent(NotificationKind.NotUploaded, Nico, Last, "newer_format", 3), "wow.vestras.nl");

        Assert.Equal("No connection: will try again", offline.Title);
        Assert.Equal("Your file is saved and goes up as soon as wow.vestras.nl is reachable.", offline.Line);
        Assert.Equal("Not uploaded · The site can't read this file yet", rejected.Title);
        Assert.Equal("See why", Assert.Single(rejected.Buttons).Label);
    }
}
