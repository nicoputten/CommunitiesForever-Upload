using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunitiesForever.Upload.Core.Settings;
using CommunitiesForever.Upload.Core.Site;

namespace CommunitiesForever.Upload.Core.Uploading;

/// <summary>What became of a file, by its SHA-256 (the upload ledger, docs/ARCHITECTURE.md "The loop").</summary>
public enum LedgerOutcome
{
    Ok,
    NothingNew,
    Rejected,
    TooLarge,
    EmptyFile,

    /// <summary>"Skip this one": never sent.</summary>
    Skipped,
}

public sealed record LedgerEntry(string Hash, LedgerOutcome Outcome, string? Rejection, DateTimeOffset At);

/// <summary>One upload in the local history: the site's answer as shown in the main window.</summary>
public sealed record HistoryEntry(
    long UploadId,
    DateTimeOffset UploadedAt,
    UploadStatus Status,
    string? Rejection,
    int? SchemaVersion,
    string SummaryLine,
    IReadOnlyList<string> Highlights,
    Uri? ReportUrl);

/// <summary>
/// The upload ledger and history per account, saved as history.json, versioned like the settings (CLAUDE.md rule 10).
/// Bounded (rule 11): <see cref="LedgerSize"/> hashes and <see cref="HistorySize"/> uploads per account.
/// Thread-safe: account workers record from their own tasks.
/// </summary>
public sealed class UploadHistory
{
    public const int CurrentVersion = 1;
    public const int LedgerSize = 300;
    public const int HistorySize = 50;

    // Rejections that a site update can fix: they block the same file only for a while (decided with Nico, UP-06).
    private static readonly Dictionary<string, TimeSpan> Expiring = new(StringComparer.Ordinal)
    {
        [UploadReceipt.Rejections.NewerFormat] = TimeSpan.FromHours(24),
        [UploadReceipt.Rejections.NoCommunityYet] = TimeSpan.FromHours(1),
    };

    // Migration steps: the step at key N turns a version N file into version N + 1.
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> Migrations = [];

    private readonly IHistoryFile _file;
    private readonly Lock _lock = new();
    private readonly Dictionary<WatchedAccount, (List<LedgerEntry> Ledger, List<HistoryEntry> History)> _accounts = [];

    public UploadHistory(IHistoryFile file)
    {
        _file = file;
        Load();
    }

    /// <summary>
    /// Whether this file was already handled. Expiring rejections (<c>newer_format</c> 24 h, <c>no_community_yet</c> 1 h)
    /// stop blocking after their time, and also when they were recorded before this run of the app started.
    /// </summary>
    public bool IsHandled(WatchedAccount account, string hash, DateTimeOffset now, DateTimeOffset runStartedAt)
    {
        lock (_lock)
        {
            if (!_accounts.TryGetValue(account, out var lists) || lists.Ledger.FindLast(e => e.Hash == hash) is not { } entry)
            {
                return false;
            }

            if (entry.Outcome == LedgerOutcome.Rejected && entry.Rejection is { } code && Expiring.TryGetValue(code, out var lifetime))
            {
                return entry.At >= runStartedAt && now - entry.At < lifetime;
            }

            return true;
        }
    }

    public void Record(WatchedAccount account, LedgerEntry entry, HistoryEntry? upload = null)
    {
        lock (_lock)
        {
            var lists = Lists(account);
            lists.Ledger.Add(entry);
            Trim(lists.Ledger, LedgerSize);
            if (upload is not null)
            {
                lists.History.Add(upload);
                Trim(lists.History, HistorySize);
            }

            Save();
        }
    }

    /// <summary>The account's uploads, newest first.</summary>
    public IReadOnlyList<HistoryEntry> Uploads(WatchedAccount account)
    {
        lock (_lock)
        {
            return _accounts.TryGetValue(account, out var lists) ? [.. Enumerable.Reverse(lists.History)] : [];
        }
    }

    public HistoryEntry? LastUpload(WatchedAccount account)
    {
        lock (_lock)
        {
            return _accounts.TryGetValue(account, out var lists) && lists.History.Count > 0 ? lists.History[^1] : null;
        }
    }

    public int LedgerCount(WatchedAccount account)
    {
        lock (_lock)
        {
            return _accounts.TryGetValue(account, out var lists) ? lists.Ledger.Count : 0;
        }
    }

    private static void Trim<T>(List<T> list, int size)
    {
        if (list.Count > size)
        {
            list.RemoveRange(0, list.Count - size);
        }
    }

    private (List<LedgerEntry> Ledger, List<HistoryEntry> History) Lists(WatchedAccount account)
    {
        if (!_accounts.TryGetValue(account, out var lists))
        {
            lists = ([], []);
            _accounts[account] = lists;
        }

        return lists;
    }

    // An unreadable history is started afresh: at worst a file is uploaded once more, which the site handles.
    private void Load()
    {
        try
        {
            if (_file.Read() is not { } text
                || JsonNode.Parse(text) is not JsonObject json
                || json["version"]?.GetValue<int>() is not int version
                || version < 1
                || version > CurrentVersion)
            {
                return;
            }

            for (; version < CurrentVersion; version++)
            {
                json = Migrations[version](json);
            }

            foreach (var account in json.Deserialize(HistoryJsonContext.Default.HistoryV1Json)?.Accounts ?? [])
            {
                _accounts[new WatchedAccount(account.InstallPath, account.Account)] = (
                    [.. account.Ledger ?? []],
                    [.. (account.History ?? []).Select(h => h.ToEntry())]);
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            _accounts.Clear();
        }
    }

    private void Save()
    {
        var json = new HistoryV1Json(
            CurrentVersion,
            [.. _accounts.Select(a => new AccountHistoryJson(
                a.Key.InstallPath,
                a.Key.Account,
                [.. a.Value.Ledger],
                [.. a.Value.History.Select(HistoryEntryJson.From)]))]);
        _file.Write(JsonSerializer.Serialize(json, HistoryJsonContext.Default.HistoryV1Json));
    }
}

internal sealed record HistoryV1Json(int Version, AccountHistoryJson[]? Accounts);

internal sealed record AccountHistoryJson(string InstallPath, string Account, LedgerEntry[]? Ledger, HistoryEntryJson[]? History);

internal sealed record HistoryEntryJson(
    long UploadId,
    DateTimeOffset UploadedAt,
    UploadStatus Status,
    string? Rejection,
    int? SchemaVersion,
    string SummaryLine,
    string[]? Highlights,
    string? ReportUrl)
{
    public static HistoryEntryJson From(HistoryEntry e) =>
        new(e.UploadId, e.UploadedAt, e.Status, e.Rejection, e.SchemaVersion, e.SummaryLine, [.. e.Highlights], e.ReportUrl?.AbsoluteUri);

    public HistoryEntry ToEntry() =>
        new(UploadId, UploadedAt, Status, Rejection, SchemaVersion, SummaryLine, Highlights ?? [], ReportUrl is null ? null : new Uri(ReportUrl));
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(HistoryV1Json))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
