using System.Text;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.Windows.Tests;

public sealed class LoopAdapterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cfu-tests-" + Guid.NewGuid().ToString("N"));

    public LoopAdapterTests() => Directory.CreateDirectory(_directory);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WatcherTellsWhenTheFileChanges()
    {
        string file = Path.Combine(_directory, "CommunitiesForever.lua");
        using var changed = new SemaphoreSlim(0);
        using (new FileWatcherFactory().Watch(_directory, "CommunitiesForever.lua", () => changed.Release()))
        {
            await File.WriteAllTextAsync(file, "v1", Ct);

            Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        }

        // Disposed: no more calls.
        while (changed.CurrentCount > 0)
        {
            await changed.WaitAsync(Ct);
        }

        await File.WriteAllTextAsync(file, "v2", Ct);
        Assert.False(await changed.WaitAsync(TimeSpan.FromMilliseconds(500), Ct));
    }

    [Fact]
    public async Task ReadsTheFileWhileTheGameHoldsItOpen()
    {
        string file = Path.Combine(_directory, "CommunitiesForever.lua");
        await File.WriteAllTextAsync(file, "CommunitiesForeverDB = {}", Ct);
        var reader = new AddonFileReader();

        // The game keeps it open for writing (with sharing, as WoW does).
        await using var game = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        byte[] bytes = await reader.ReadAllAsync(file, Ct);

        Assert.Equal("CommunitiesForeverDB = {}", Encoding.UTF8.GetString(bytes));
        Assert.Equal(bytes.Length, reader.GetStamp(file)!.Value.Size);
        Assert.Null(reader.GetStamp(Path.Combine(_directory, "missing.lua")));
        Assert.True(reader.FolderExists(_directory));
    }

    [Fact]
    public void LogFilesRollAndStayBounded()
    {
        string logs = Path.Combine(_directory, "logs");
        using (var provider = new RollingFileLoggerProvider(logs))
        {
            var logger = provider.CreateLogger("CommunitiesForever.Upload.Core.Uploading.UploadLoop");
            string line = new('x', 1000);
            for (int i = 0; i < 10_000; i++)
            {
                logger.LogInformation("{Line}", line);
            }
        }

        string[] files = Directory.GetFiles(logs);
        Assert.Equal(RollingFileLoggerProvider.MaxFiles, files.Length);
        Assert.All(files, f => Assert.True(new FileInfo(f).Length <= RollingFileLoggerProvider.MaxFileBytes + 2048));
        Assert.Contains("Information UploadLoop: xxx", File.ReadAllText(Path.Combine(logs, "uploader.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void NoGameRunningFromAnotherFolder() =>
        Assert.False(new GameProcesses().IsRunning(Path.Combine(_directory, "_forever_")));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
