using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CommunitiesForever.Upload.Core.Tests;

/// <summary>
/// Waits for work on other threads: polls until <c>done</c>, at most <see cref="Max"/> of real time, and fails with
/// the condition's text instead of letting the test go on in the wrong state. Linked into the app's tests and design
/// checks, so every project waits the same way.
/// </summary>
internal static class Wait
{
    /// <summary>Generous for a busy CI machine; a condition that holds returns at once.</summary>
    public static readonly TimeSpan Max = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);

    /// <summary>Polls <c>done</c>; <c>step</c> runs before every check (the UI jobs, a zero clock advance).</summary>
    public static async Task Until(
        Func<bool> done,
        Action? step = null,
        [CallerArgumentExpression(nameof(done))] string condition = "")
    {
        var watch = Stopwatch.StartNew();
        while (!Check(done, step))
        {
            Assert.True(watch.Elapsed < Max, $"Timed out waiting for {condition}");
            await Task.Delay(Poll);
        }
    }

    /// <summary>The same, for code that can't await.</summary>
    public static void UntilBlocking(
        Func<bool> done,
        Action? step = null,
        [CallerArgumentExpression(nameof(done))] string condition = "")
    {
        var watch = Stopwatch.StartNew();
        while (!Check(done, step))
        {
            Assert.True(watch.Elapsed < Max, $"Timed out waiting for {condition}");
            Thread.Sleep(Poll);
        }
    }

    private static bool Check(Func<bool> done, Action? step)
    {
        step?.Invoke();
        return done();
    }
}
