using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CommunitiesForever.Upload.Core.Runs;

/// <summary>Runs.lua's bytes, or why nothing is written; <see cref="Trimmed"/> counts strings cut and values left out.</summary>
public sealed record RunsLuaResult(byte[]? Content, int Trimmed);

/// <summary>
/// Turns the site's <c>GET runs</c> answer into <c>Data\Runs.lua</c> (UP-12, CLAUDE.md rule 1's write exception): one
/// global table <c>CF_RUNS</c> built from the JSON as data only. Every key is written as <c>["key"]</c>, every string is
/// quoted and escaped, numbers are invariant literals, <c>null</c> is left out. Nothing from the site ever becomes Lua
/// code. The whole answer goes in (decided with Nico, 2026-10-28), so new site fields reach the addon without an
/// uploader update; the addon's validator (CF-45) drops what it doesn't know. The caps are a safety net only: a real
/// answer stays far below them (tests with a busy week).
/// </summary>
public static class RunsLua
{
    public const int MaxDepth = 12;
    public const int MaxStringLength = 500;
    public const int MaxBytes = 4 * 1024 * 1024;

    public const string Header = "-- Planned runs from the CommunitiesForever site, written by the CommunitiesForever uploader.\n"
        + "-- Data only. Rewritten when the runs change; the addon reads it at login or /reload.\n";

    /// <summary>The file for <paramref name="root"/> (a JSON object); null content when it would exceed <see cref="MaxBytes"/>.</summary>
    public static RunsLuaResult Write(JsonElement root)
    {
        var text = new StringBuilder(Header);
        text.Append("CF_RUNS = ");
        int trimmed = 0;
        if (root.ValueKind == JsonValueKind.Object)
        {
            Value(text, root, 1, ref trimmed);
        }
        else
        {
            text.Append("nil");
        }

        text.Append('\n');
        byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
        return new RunsLuaResult(bytes.Length > MaxBytes ? null : bytes, trimmed);
    }

    private static void Value(StringBuilder text, JsonElement value, int depth, ref int trimmed)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                Quote(text, value.GetString()!, ref trimmed);
                break;
            case JsonValueKind.Number:
                if (Number(value) is { } number)
                {
                    text.Append(number);
                }
                else
                {
                    trimmed++;
                    text.Append("nil");
                }

                break;
            case JsonValueKind.True:
                text.Append("true");
                break;
            case JsonValueKind.False:
                text.Append("false");
                break;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                Table(text, value, depth, ref trimmed);
                break;
            default:
                text.Append("nil");
                break;
        }
    }

    private static void Table(StringBuilder text, JsonElement table, int depth, ref int trimmed)
    {
        if (depth > MaxDepth)
        {
            trimmed++;
            text.Append("{}");
            return;
        }

        string indent = new(' ', depth * 2);
        text.Append("{\n");
        if (table.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in table.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                text.Append(indent).Append('[');
                Quote(text, property.Name, ref trimmed);
                text.Append("] = ");
                Value(text, property.Value, depth + 1, ref trimmed);
                text.Append(",\n");
            }
        }
        else
        {
            // A null in a list would leave a hole the addon stops at; nil keeps the positions as the site sent them.
            foreach (var item in table.EnumerateArray())
            {
                text.Append(indent);
                Value(text, item, depth + 1, ref trimmed);
                text.Append(",\n");
            }
        }

        text.Append(new string(' ', (depth - 1) * 2)).Append('}');
    }

    // An invariant literal; null for a number Lua can't write (beyond double range).
    private static string? Number(JsonElement value)
    {
        if (value.TryGetInt64(out long whole))
        {
            return whole.ToString(CultureInfo.InvariantCulture);
        }

        return value.TryGetDouble(out double real) && double.IsFinite(real) ? real.ToString("R", CultureInfo.InvariantCulture) : null;
    }

    // A Lua 5.1 string literal: \\ and \" escaped, every control character as \ddd; other characters as UTF-8.
    private static void Quote(StringBuilder text, string value, ref int trimmed)
    {
        if (value.Length > MaxStringLength)
        {
            trimmed++;
            int cut = MaxStringLength;
            if (char.IsHighSurrogate(value[cut - 1]))
            {
                cut--;
            }

            value = value[..cut];
        }

        text.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    text.Append(@"\\");
                    break;
                case '"':
                    text.Append("\\\"");
                    break;
                default:
                    if (c < ' ' || c == '\x7f')
                    {
                        text.Append('\\').Append(((int)c).ToString("000", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        text.Append('"');
    }
}
