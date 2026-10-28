using System.Runtime;

namespace CommunitiesForever.Upload.Core;

/// <summary>
/// Gives memory back once the window is hidden to the tray (UP-14). The GC keeps the heap it grew while the window
/// was open (about 40 MB committed but unused, measured 2026-10-28) until something forces a full collection, which
/// a quiet tray app never does. A few seconds after hiding, one compacting collection returns it; showing the window
/// again before then cancels it. Rendering in software (Program) is the larger saving; this is the rest.
/// </summary>
public sealed class TrayMemory : IDisposable
{
    /// <summary>Long enough for the hide animation and the last layout pass to finish.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);

    private readonly TimeProvider _time;
    private readonly Action _compact;
    private readonly Lock _gate = new();
    private ITimer? _timer;

    public TrayMemory(TimeProvider time)
        : this(time, CompactHeap)
    {
    }

    internal TrayMemory(TimeProvider time, Action compact)
    {
        _time = time;
        _compact = compact;
    }

    /// <summary>The window went to the tray: compact after <see cref="Delay"/>, once.</summary>
    public void Hidden()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = _time.CreateTimer(_ => Run(), null, Delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The window is back: nothing to give back.</summary>
    public void Shown() => Stop();

    public void Dispose() => Stop();

    private void Run()
    {
        lock (_gate)
        {
            if (_timer is null)
            {
                return;
            }

            _timer.Dispose();
            _timer = null;
        }

        _compact();
    }

    private void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    // A full, blocking, compacting collection, the large object heap included; Aggressive also decommits the
    // memory it frees instead of keeping it for the next allocations.
    private static void CompactHeap()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
