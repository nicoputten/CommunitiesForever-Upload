using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CommunitiesForever.Upload.Core.Updates;

/// <summary>Reads and writes the text of updates.json.</summary>
public interface IUpdatesFile
{
    string? Read();

    void Write(string text);
}

/// <summary>
/// updates.json (UP-10): the version handed to the installer, "Updated to …" until it is closed, the last check.
/// Versioned like the settings (CLAUDE.md rule 10); an unreadable or newer file just starts afresh (nothing in it matters much).
/// </summary>
public sealed class UpdateMemoryStore(IUpdatesFile file) : IUpdateMemoryFile
{
    public const int CurrentVersion = 1;

    public UpdateMemory Load()
    {
        try
        {
            if (file.Read() is not { } text || JsonNode.Parse(text) is not JsonObject json
                || json["version"]?.GetValue<int>() != CurrentVersion)
            {
                return new UpdateMemory();
            }

            var read = json.Deserialize(UpdatesJson.Default.UpdatesFileJson);
            return read is null ? new UpdateMemory() : new UpdateMemory(read.HandedOver, read.ShowUpdatedTo, read.LastCheck);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return new UpdateMemory();
        }
    }

    public void Save(UpdateMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        file.Write(JsonSerializer.Serialize(
            new UpdatesFileJson(CurrentVersion, memory.HandedOver, memory.ShowUpdatedTo, memory.LastCheck),
            UpdatesJson.Default.UpdatesFileJson));
    }
}

internal sealed record UpdatesFileJson(int Version, string? HandedOver, string? ShowUpdatedTo, DateTimeOffset? LastCheck);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(UpdatesFileJson))]
internal sealed partial class UpdatesJson : JsonSerializerContext;
