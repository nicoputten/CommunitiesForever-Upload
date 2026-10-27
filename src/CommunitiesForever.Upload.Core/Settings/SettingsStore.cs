using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CommunitiesForever.Upload.Core.Settings;

/// <summary>Reads and writes the raw settings file (Windows: settings.json in the app's data folder).</summary>
public interface ISettingsFile
{
    /// <summary>The file's text, or null when there is none.</summary>
    string? Read();

    /// <summary>Replaces the file in one step (temporary file, then move).</summary>
    void Write(string text);

    /// <summary>Keeps a damaged file next to the new one as <c>settings.json.bad</c>.</summary>
    void MoveAsideAsDamaged();
}

public abstract record SettingsLoad
{
    private SettingsLoad()
    {
    }

    /// <summary>First run: no settings yet.</summary>
    public sealed record Missing : SettingsLoad;

    public sealed record Loaded(AppSettings Settings) : SettingsLoad;

    /// <summary>The file could not be read; it was kept as settings.json.bad.</summary>
    public sealed record Damaged : SettingsLoad;

    /// <summary>Written by a newer version of the app; left untouched and never overwritten.</summary>
    public sealed record FromNewerVersion(int Version) : SettingsLoad;
}

/// <summary>
/// Loads settings of any earlier version by migrating them step by step (CLAUDE.md rule 10), and saves the current one.
/// </summary>
public sealed class SettingsStore(ISettingsFile file)
{
    // Migration steps: the step at key N turns a version N file into version N + 1. Version 1 is the first.
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> Migrations = new()
    {
        // 1 → 2 (UP-10): "install updates when WoW is closed", on by default.
        [1] = json =>
        {
            json["version"] = 2;
            json["installUpdatesWhenWowClosed"] = true;
            return json;
        },
    };

    private bool _tooNew;

    public SettingsLoad Load()
    {
        string? text = file.Read();
        if (text is null)
        {
            return new SettingsLoad.Missing();
        }

        try
        {
            if (JsonNode.Parse(text) is not JsonObject json || json["version"]?.GetValue<int>() is not int version || version < 1)
            {
                return Damaged();
            }

            if (version > AppSettings.CurrentVersion)
            {
                _tooNew = true;
                return new SettingsLoad.FromNewerVersion(version);
            }

            for (; version < AppSettings.CurrentVersion; version++)
            {
                json = Migrations[version](json);
            }

            var settings = json.Deserialize(SettingsJson.Default.SettingsFileJson);
            return settings is null ? Damaged() : new SettingsLoad.Loaded(settings.ToSettings());
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            return Damaged();
        }
    }

    /// <summary>Saves as the current version. Refuses to overwrite a file from a newer version.</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_tooNew)
        {
            throw new InvalidOperationException("The settings file is from a newer version of the app and is not overwritten.");
        }

        file.Write(JsonSerializer.Serialize(SettingsFileJson.From(settings), SettingsJson.Default.SettingsFileJson));
    }

    private SettingsLoad.Damaged Damaged()
    {
        file.MoveAsideAsDamaged();
        return new SettingsLoad.Damaged();
    }
}

internal sealed record WatchedAccountJson(string InstallPath, string Account);

internal sealed record SettingsFileJson(
    int Version,
    WatchedAccountJson[]? Accounts,
    bool UploadAutomatically,
    bool StartWithWindows,
    bool CloseHidesToTray,
    NotificationLevel Notifications,
    string? SiteUrl,
    bool InstallUpdatesWhenWowClosed = true)
{
    public static SettingsFileJson From(AppSettings s) => new(
        AppSettings.CurrentVersion,
        [.. s.Accounts.Select(a => new WatchedAccountJson(a.InstallPath, a.Account))],
        s.UploadAutomatically,
        s.StartWithWindows,
        s.CloseHidesToTray,
        s.Notifications,
        s.SiteUri?.AbsoluteUri,
        s.InstallUpdatesWhenWowClosed);

    public AppSettings ToSettings() => new()
    {
        Accounts = [.. (Accounts ?? []).Select(a => new WatchedAccount(a.InstallPath, a.Account))],
        UploadAutomatically = UploadAutomatically,
        StartWithWindows = StartWithWindows,
        CloseHidesToTray = CloseHidesToTray,
        Notifications = Notifications,
        InstallUpdatesWhenWowClosed = InstallUpdatesWhenWowClosed,
        SiteUri = SiteUrl is null ? null : new Uri(SiteUrl, UriKind.Absolute),
    };
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    RespectRequiredConstructorParameters = false)]
[JsonSerializable(typeof(SettingsFileJson))]
internal sealed partial class SettingsJson : JsonSerializerContext;
