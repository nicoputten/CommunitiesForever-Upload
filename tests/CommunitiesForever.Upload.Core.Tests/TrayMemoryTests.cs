using Microsoft.Extensions.Time.Testing;

namespace CommunitiesForever.Upload.Core.Tests;

/// <summary>Giving memory back in the tray (UP-14).</summary>
public sealed class TrayMemoryTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly TrayMemory _memory;
    private int _compactions;

    public TrayMemoryTests() => _memory = new TrayMemory(_time, () => _compactions++);

    public void Dispose() => _memory.Dispose();

    [Fact]
    public void CompactsOnceAFewSecondsAfterHiding()
    {
        _memory.Hidden();
        _time.Advance(TrayMemory.Delay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, _compactions);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _compactions);

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, _compactions);
    }

    [Fact]
    public void ShowingTheWindowAgainCancelsIt()
    {
        _memory.Hidden();
        _time.Advance(TimeSpan.FromSeconds(1));
        _memory.Shown();
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, _compactions);
    }

    [Fact]
    public void HidingAgainStartsTheDelayOver()
    {
        _memory.Hidden();
        _time.Advance(TimeSpan.FromSeconds(2));
        _memory.Hidden();
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, _compactions);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _compactions);
    }

    [Fact]
    public void NothingRunsAfterDispose()
    {
        _memory.Hidden();
        _memory.Dispose();
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, _compactions);
    }

    [Fact]
    public void TheRealCompactionRuns()
    {
        // The production action: a full compacting collection, without throwing.
        using var memory = new TrayMemory(_time);
        int before = GC.CollectionCount(GC.MaxGeneration);
        memory.Hidden();
        _time.Advance(TrayMemory.Delay);
        Assert.True(GC.CollectionCount(GC.MaxGeneration) > before);
    }
}
