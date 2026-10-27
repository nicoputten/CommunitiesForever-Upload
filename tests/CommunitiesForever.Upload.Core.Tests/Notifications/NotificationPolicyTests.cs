using CommunitiesForever.Upload.Core.Notifications;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Tests.Notifications;

public sealed class NotificationPolicyTests
{
    private static readonly WatchedAccount Nico = new(@"C:\WoW\_forever_", "NICO");
    private static readonly HistoryEntry Earlier = Upload(41, UploadStatus.Ok);

    private readonly NotificationPolicy _policy = new();

    [Fact]
    public void TheFirstLookIsOnlyTheBaseline()
    {
        Assert.Empty(_policy.Update([Status(AccountState.WaitingToSend, Earlier)], NotificationLevel.EveryUpload));
    }

    [Fact]
    public void ANewUploadWithSomethingInItNotifies()
    {
        _policy.Update([Status(AccountState.Watching, Earlier)], NotificationLevel.EveryUpload);

        var events = _policy.Update([Status(AccountState.Watching, Upload(42, UploadStatus.Ok))], NotificationLevel.EveryUpload);

        var e = Assert.Single(events);
        Assert.Equal(NotificationKind.Uploaded, e.Kind);
        Assert.Equal(42, e.Upload!.UploadId);
    }

    [Fact]
    public void NothingNewStaysQuiet()
    {
        _policy.Update([Status(AccountState.Watching, Earlier)], NotificationLevel.EveryUpload);

        Assert.Empty(_policy.Update([Status(AccountState.Watching, Upload(42, UploadStatus.NothingNew))], NotificationLevel.EveryUpload));
    }

    [Theory]
    [InlineData(AccountState.WaitingToSend, NotificationKind.Offline)]
    [InlineData(AccountState.NotConnected, NotificationKind.NotConnected)]
    [InlineData(AccountState.NotUploaded, NotificationKind.NotUploaded)]
    public void AProblemNotifiesOnceWhenItStarts(AccountState state, NotificationKind kind)
    {
        _policy.Update([Status(AccountState.Watching, Earlier)], NotificationLevel.EveryUpload);

        var first = _policy.Update([Status(state, Earlier)], NotificationLevel.EveryUpload);
        var again = _policy.Update([Status(state, Earlier)], NotificationLevel.EveryUpload);

        Assert.Equal(kind, Assert.Single(first).Kind);
        Assert.Empty(again);
    }

    [Fact]
    public void ARejectedUploadNotifiesOnceAsNotUploaded()
    {
        _policy.Update([Status(AccountState.Uploading, Earlier)], NotificationLevel.EveryUpload);
        var rejected = Upload(43, UploadStatus.Rejected);

        var events = _policy.Update([Status(AccountState.NotUploaded, rejected) with { Rejection = "newer_format", SchemaVersion = 3 }], NotificationLevel.EveryUpload);

        var e = Assert.Single(events);
        Assert.Equal(NotificationKind.NotUploaded, e.Kind);
        Assert.Equal("newer_format", e.Rejection);
        Assert.Equal(3, e.SchemaVersion);
    }

    [Fact]
    public void OnlyProblemsDropsUploadsAndOffDropsAll()
    {
        _policy.Update([Status(AccountState.Watching, Earlier)], NotificationLevel.OnlyProblems);
        Assert.Empty(_policy.Update([Status(AccountState.Watching, Upload(42, UploadStatus.Ok))], NotificationLevel.OnlyProblems));
        Assert.Single(_policy.Update([Status(AccountState.WaitingToSend, Upload(42, UploadStatus.Ok))], NotificationLevel.OnlyProblems));

        Assert.Empty(_policy.Update([Status(AccountState.NotConnected, Upload(42, UploadStatus.Ok))], NotificationLevel.Off));
    }

    [Fact]
    public void AnAccountNoLongerWatchedIsForgotten()
    {
        var other = new WatchedAccount(@"C:\WoW\_forever_", "OTHER");
        _policy.Update([Status(AccountState.Watching, Earlier), Status(AccountState.Watching, Earlier) with { Account = other }], NotificationLevel.EveryUpload);
        _policy.Update([Status(AccountState.Watching, Earlier)], NotificationLevel.EveryUpload);

        // Seen again: a new baseline, not a change.
        Assert.Empty(_policy.Update([Status(AccountState.Watching, Earlier), Status(AccountState.WaitingToSend, Earlier) with { Account = other }], NotificationLevel.EveryUpload));
    }

    private static AccountStatus Status(AccountState state, HistoryEntry last) => new(Nico, state, LastUpload: last);

    private static HistoryEntry Upload(long id, UploadStatus status) =>
        new(id, DateTimeOffset.UnixEpoch, status, null, null, "5 levels gained", [], new Uri($"https://wow.vestras.nl/upload/{id}"));
}
