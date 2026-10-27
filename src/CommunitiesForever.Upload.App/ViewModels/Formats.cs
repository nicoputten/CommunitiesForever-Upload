using System.Globalization;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>How the app writes times and sizes (English, hardcoded: CLAUDE.md rule 8).</summary>
public static class Formats
{
    /// <summary>"today 21:14", "yesterday 08:05", "20 Oct 19:40" (all local).</summary>
    public static string Moment(DateTimeOffset at, DateTimeOffset now)
    {
        string clock = Clock(at);
        return (now.Date - at.Date).Days switch
        {
            0 => $"today {clock}",
            1 => $"yesterday {clock}",
            _ => at.ToString("d MMM HH:mm", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>"today at 21:14", "yesterday at 08:05", "20 Oct 19:40" (main-window board: "Last upload today at 21:14").</summary>
    public static string MomentAt(DateTimeOffset at, DateTimeOffset now) => (now.Date - at.Date).Days switch
    {
        0 => $"today at {Clock(at)}",
        1 => $"yesterday at {Clock(at)}",
        _ => Moment(at, now),
    };

    /// <summary>Recent uploads: "Today 21:14", "Yesterday 08:05", "Thu 23 Oct, 23:40".</summary>
    public static string Day(DateTimeOffset at, DateTimeOffset now) => (now.Date - at.Date).Days switch
    {
        0 => $"Today {Clock(at)}",
        1 => $"Yesterday {Clock(at)}",
        _ => at.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture),
    };

    public static string Clock(DateTimeOffset at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"48 KB", "1.2 MB".</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{Math.Max(1, (long)Math.Round(bytes / 1024d))} KB",
        _ => (bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB",
    };

    /// <summary>"in 1 min", "in 12 min", "in 2 h".</summary>
    public static string Wait(TimeSpan wait) => wait.TotalMinutes switch
    {
        < 1 => "in under a minute",
        < 90 => $"in {(int)Math.Ceiling(wait.TotalMinutes)} min",
        _ => $"in {(int)Math.Round(wait.TotalHours)} h",
    };
}
