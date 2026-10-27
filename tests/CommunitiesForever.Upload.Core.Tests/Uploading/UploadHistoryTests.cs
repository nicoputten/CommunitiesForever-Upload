using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Tests.Uploading;

public sealed class UploadHistoryTests
{
    private static readonly WatchedAccount Account = new(@"C:\WoW\_forever_", "NICO");
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RunStarted = Now.AddHours(-2);

    private readonly MemoryHistoryFile _file = new();

    [Fact]
    public void HandledFilesBlockForGood()
    {
        var history = new UploadHistory(_file);
        history.Record(Account, Entry("a", LedgerOutcome.Ok));
        history.Record(Account, Entry("b", LedgerOutcome.Rejected, UploadReceipt.Rejections.WrongFile));
        history.Record(Account, Entry("c", LedgerOutcome.Skipped));

        Assert.True(history.IsHandled(Account, "a", Now.AddYears(1), RunStarted));
        Assert.True(history.IsHandled(Account, "b", Now.AddYears(1), RunStarted));
        Assert.True(history.IsHandled(Account, "c", Now.AddYears(1), RunStarted));
        Assert.False(history.IsHandled(Account, "d", Now, RunStarted));
        Assert.False(history.IsHandled(Account with { Account = "OTHER" }, "a", Now, RunStarted));
    }

    [Theory]
    [InlineData("newer_format", 23, true)]
    [InlineData("newer_format", 25, false)]
    [InlineData("no_community_yet", 0.5, true)]
    [InlineData("no_community_yet", 2, false)]
    public void FixableRejectionsBlockOnlyForAWhile(string code, double hoursLater, bool blocked)
    {
        var history = new UploadHistory(_file);
        history.Record(Account, Entry("a", LedgerOutcome.Rejected, code));

        Assert.Equal(blocked, history.IsHandled(Account, "a", Now.AddHours(hoursLater), RunStarted));
    }

    [Fact]
    public void FixableRejectionsAreTriedAgainOnTheNextRun()
    {
        var history = new UploadHistory(_file);
        history.Record(Account, Entry("a", LedgerOutcome.Rejected, UploadReceipt.Rejections.NewerFormat));

        Assert.False(history.IsHandled(Account, "a", Now.AddMinutes(5), runStartedAt: Now.AddMinutes(1)));
    }

    [Fact]
    public void LedgerAndHistoryStayBounded()
    {
        var history = new UploadHistory(_file);
        for (int i = 0; i < 1000; i++)
        {
            history.Record(Account, Entry($"h{i}", LedgerOutcome.Ok), Upload(i));
        }

        Assert.Equal(UploadHistory.LedgerSize, history.LedgerCount(Account));
        Assert.Equal(UploadHistory.HistorySize, history.Uploads(Account).Count);
        Assert.Equal(999, history.Uploads(Account)[0].UploadId);
        Assert.Equal(999, history.LastUpload(Account)!.UploadId);
        Assert.False(history.IsHandled(Account, "h0", Now, RunStarted));
        Assert.True(history.IsHandled(Account, "h999", Now, RunStarted));
    }

    [Fact]
    public void SavedHistoryLoadsTheSame()
    {
        var history = new UploadHistory(_file);
        history.Record(Account, Entry("a", LedgerOutcome.Ok), Upload(42));

        var loaded = new UploadHistory(_file);

        Assert.True(loaded.IsHandled(Account, "a", Now, RunStarted));
        var upload = Assert.Single(loaded.Uploads(Account));
        Assert.Equal(Upload(42) with { Highlights = upload.Highlights }, upload);
        Assert.Equal(["Ves Doombringer 37 → 39"], upload.Highlights);
    }

    [Fact]
    public void Version1FileAsReleasedStillLoads()
    {
        // Rule 10: the text release 1 writes. Never change it; add a migration instead.
        _file.Text = """
            {
              "version": 1,
              "accounts": [
                {
                  "installPath": "C:\\WoW\\_forever_",
                  "account": "NICO",
                  "ledger": [ { "hash": "abc", "outcome": "Rejected", "rejection": "wrong_file", "at": "2026-10-27T19:00:00+00:00" } ],
                  "history": [
                    {
                      "uploadId": 7, "uploadedAt": "2026-10-27T19:00:00+00:00", "status": "Rejected", "rejection": "wrong_file",
                      "schemaVersion": null, "summaryLine": "Not an addon file", "highlights": [], "reportUrl": "https://wow.vestras.nl/upload/7"
                    }
                  ]
                }
              ]
            }
            """;

        var history = new UploadHistory(_file);

        Assert.True(history.IsHandled(Account, "abc", Now, RunStarted));
        Assert.Equal(7, Assert.Single(history.Uploads(Account)).UploadId);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "version": 99, "accounts": [] }""")]
    [InlineData("""{ "version": 1, "accounts": [ { "installPath": 5 } ] }""")]
    public void UnreadableHistoryStartsAfresh(string text)
    {
        _file.Text = text;

        var history = new UploadHistory(_file);

        Assert.False(history.IsHandled(Account, "abc", Now, RunStarted));
        Assert.Empty(history.Uploads(Account));
    }

    private static LedgerEntry Entry(string hash, LedgerOutcome outcome, string? rejection = null) => new(hash, outcome, rejection, Now);

    private static HistoryEntry Upload(long id) =>
        new(id, Now, UploadStatus.Ok, null, null, "5 levels gained", ["Ves Doombringer 37 → 39"], new Uri($"https://wow.vestras.nl/upload/{id}"));
}
