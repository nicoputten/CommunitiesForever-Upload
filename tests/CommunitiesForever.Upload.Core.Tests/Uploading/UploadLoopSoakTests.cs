using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Core.Tests.Uploading;

/// <summary>
/// The app runs for weeks (CLAUDE.md rule 11): 10,000 saves over three accounts must leave the ledger, the history,
/// the watchers and memory where they were. Runs alone: the memory check reads the whole process's heap, so another
/// test class loading its statics (the contract examples, JSON metadata) in the meantime would count as growth.
/// </summary>
[Collection(nameof(UploadLoopSoakTests))]
public sealed class UploadLoopSoakTests
{
    private const int Saves = 10_000;

    [Fact]
    public async Task TenThousandSavesKeepEverythingBounded()
    {
        await using var h = new LoopHarness("A", "B", "C");
        await h.StartAsync();
        await h.AdvanceUntil(() => h.Loop.Statuses.All(s => s.State == AccountState.NotWatching));

        long baseline = 0;
        for (int i = 1; i <= Saves; i++)
        {
            var account = h.Accounts[i % h.Accounts.Count];
            h.Save(account, $"{account.Account} save {i}");
            if (i % 100 == 0)
            {
                // Every hundredth save takes the normal way: settle on the clock.
                await h.AdvanceUntil(() => h.Site.Count == i);
            }
            else
            {
                // The rest skip the wait, so the test stays fast.
                h.Loop.UploadNow(account);
                await LoopHarness.Settle(() => h.Site.Count == i, TimeSpan.FromSeconds(5));
                Assert.Equal(i, h.Site.Count);
            }

            if (i == 1_000)
            {
                baseline = Memory();
            }
        }

        h.Site.Uploaded.Clear();
        long growth = Memory() - baseline;

        Assert.All(h.Accounts, a =>
        {
            Assert.Equal(UploadHistory.LedgerSize, h.History.LedgerCount(a));
            Assert.Equal(UploadHistory.HistorySize, h.History.Uploads(a).Count);
        });
        Assert.Equal(3, h.Watchers.Active);
        Assert.Equal(3, h.Loop.Statuses.Count);
        Assert.True(growth < 4 * 1024 * 1024, $"Memory grew by {growth / 1024} KB over {Saves - 1_000} saves");
    }

    private static long Memory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }
}

[CollectionDefinition(nameof(UploadLoopSoakTests), DisableParallelization = true)]
public sealed class UploadLoopSoakRunsAlone;
