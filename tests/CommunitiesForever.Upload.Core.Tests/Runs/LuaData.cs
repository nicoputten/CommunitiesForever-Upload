using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace CommunitiesForever.Upload.Core.Tests.Runs;

/// <summary>
/// Reads Runs.lua back as data, strictly: comments, one <c>CF_RUNS = </c>, then only tables, quoted strings, numbers,
/// booleans and nil. Anything else (a call, a name, an unclosed string) fails, so the tests prove the file is data only
/// and lossless.
/// </summary>
internal sealed class LuaData
{
    private readonly string _text;
    private int _at;

    private LuaData(string text) => _text = text;

    /// <summary>The value of CF_RUNS as JSON, and the deepest table nesting and longest string seen.</summary>
    public static (JsonNode? Value, int Depth, int LongestString) Read(byte[] file)
    {
        var reader = new LuaData(Encoding.UTF8.GetString(file));
        while (reader.Peek("--"))
        {
            reader._at = reader._text.IndexOf('\n', reader._at) + 1;
        }

        reader.Expect("CF_RUNS = ");
        var value = reader.Value(1);
        reader.Expect("\n");
        Assert.Equal(reader._text.Length, reader._at);
        return (value, reader._depth, reader._longest);
    }

    private int _depth;
    private int _longest;

    private JsonNode? Value(int depth)
    {
        if (Peek("{"))
        {
            return Table(depth);
        }

        if (Peek("\""))
        {
            string s = String();
            _longest = Math.Max(_longest, s.Length);
            return JsonValue.Create(s);
        }

        if (Take("true"))
        {
            return JsonValue.Create(true);
        }

        if (Take("false"))
        {
            return JsonValue.Create(false);
        }

        if (Take("nil"))
        {
            return null;
        }

        int start = _at;
        while (_at < _text.Length && "-+.0123456789eE".Contains(_text[_at], StringComparison.Ordinal))
        {
            _at++;
        }

        string number = _text[start.._at];
        Assert.NotEmpty(number);
        return long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)
            ? JsonValue.Create(whole)
            : JsonValue.Create(double.Parse(number, CultureInfo.InvariantCulture));
    }

    private JsonNode Table(int depth)
    {
        _depth = Math.Max(_depth, depth);
        if (Take("{}"))
        {
            return new JsonObject();
        }

        Expect("{\n");
        JsonObject? obj = null;
        JsonArray? list = null;
        while (true)
        {
            Spaces();
            if (Take("}"))
            {
                return (JsonNode?)obj ?? (JsonNode?)list ?? new JsonObject();
            }

            if (Take("["))
            {
                string key = String();
                Expect("] = ");
                obj ??= [];
                Assert.Null(list);
                obj[key] = Value(depth + 1);
            }
            else
            {
                list ??= [];
                Assert.Null(obj);
                list.Add(Value(depth + 1));
            }

            Expect(",\n");
        }
    }

    private string String()
    {
        Expect("\"");
        var s = new StringBuilder();
        while (true)
        {
            Assert.True(_at < _text.Length, "unclosed string");
            char c = _text[_at++];
            if (c == '"')
            {
                return s.ToString();
            }

            Assert.True(c >= ' ' && c != '\x7f', "raw control character in a string");
            if (c != '\\')
            {
                s.Append(c);
                continue;
            }

            char next = _text[_at];
            if (next is '\\' or '"')
            {
                s.Append(next);
                _at++;
            }
            else
            {
                Assert.True(char.IsAsciiDigit(next) && char.IsAsciiDigit(_text[_at + 1]) && char.IsAsciiDigit(_text[_at + 2]), "unknown escape");
                s.Append((char)int.Parse(_text.AsSpan(_at, 3), CultureInfo.InvariantCulture));
                _at += 3;
            }
        }
    }

    private void Spaces()
    {
        while (_at < _text.Length && _text[_at] == ' ')
        {
            _at++;
        }
    }

    private bool Peek(string s) => string.CompareOrdinal(_text, _at, s, 0, s.Length) == 0;

    private bool Take(string s)
    {
        if (!Peek(s))
        {
            return false;
        }

        _at += s.Length;
        return true;
    }

    private void Expect(string s) => Assert.True(Take(s), $"expected {s.Replace("\n", "\\n", StringComparison.Ordinal)} at {_at}");
}
