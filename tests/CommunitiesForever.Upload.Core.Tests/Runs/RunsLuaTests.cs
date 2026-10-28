using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunitiesForever.Upload.Core.Runs;

namespace CommunitiesForever.Upload.Core.Tests.Runs;

/// <summary>
/// Runs.lua from the site's <c>GET runs</c> answer (UP-12). Realistic, busy answers stay far below the caps and come
/// back unchanged; the caps are a safety net (Nico, 2026-10-28). Made-up names and BattleTags (CLAUDE.md rule 12).
/// </summary>
public sealed class RunsLuaTests
{
    private const long Monday = 1793660400; // 2026-11-02 20:00 UTC

    private static readonly string[] Dungeons = ["rfc", "wc", "dm", "sfk", "bfd", "stocks", "gnomer", "rfk", "smgy", "smlib", "smarm", "smcath", "rfd", "ulda", "zf", "mara", "st", "brd", "lbrs", "ubrs", "scholo", "strat", "dme"];

    // Long names as the game allows them: first and last name, accents, up to the addon's caps (48 characters).
    private static readonly string[] Friends = ["Ada#1001", "Bo#2002", "Cyrielle#3003", "Dagny#4004", "Esmé#5005", "Faramund#6006", "Gwendolyn#7007", "Hrothgar#8008", "Isolde#9009", "Jöran#1010", "Kestrel#1111", "Lioranthe#1212"];

    private static readonly string[] Names = ["Ægwynn Ørnhjelm-Starfallowmere", "Bo Stonebreaker", "Cyrielle Moonwhisper-Dawnsong", "Dagny Frostmantle", "Esmé Thornwhisper", "Faramund Ironhide-Grimbrew", "Gwendolyn Lightbringer", "Hrothgar Doombringer", "Isolde Ravencrest", "Jöran Emberforge", "Kestrel Windrunner", "Lioranthe Sunstrider-Vale"];

    private static readonly string[] Fits = ["fits", "+3 over", "carry · +12", "4 below"];

    private static readonly string[] Roles = ["tank", "heal", "dps", "dps", "dps"];

    /// <summary>
    /// A busy week as the site sends it: <paramref name="runs"/> runs (open to cancelled, from the site, Discord and the
    /// game), each with <paramref name="answers"/> answers (in, maybe, can't, reserves, fits), notes at the maximum
    /// length with quotes and backslashes, and <paramref name="received"/> received ids.
    /// </summary>
    internal static JsonObject BusyWeek(int runs = 35, int answers = 12, int received = 200)
    {
        var list = new JsonArray();
        for (int r = 0; r < runs; r++)
        {
            var answerList = new JsonArray();
            for (int a = 0; a < answers; a++)
            {
                int f = (r + a) % Friends.Length;
                var answer = new JsonObject
                {
                    ["battleTag"] = Friends[f],
                    ["guid"] = $"Player-4619-0{f:X3}{r:X4}",
                    ["name"] = Names[f],
                    ["answer"] = (a % 6) switch { 4 => "maybe", 5 => "cant", _ => "in" },
                    ["role"] = Roles[a % Roles.Length],
                    ["source"] = (a % 3) switch { 0 => "site", 1 => "discord", _ => "game" },
                    ["at"] = Monday - 86400 + (r * 600) + a,
                    ["fit"] = Fits[(r + a) % Fits.Length],
                };
                if (a >= 8)
                {
                    answer["reserve"] = true;
                    answer["answer"] = "maybe";
                }

                answerList.Add(answer);
            }

            list.Add(new JsonObject
            {
                ["id"] = $"r-{1000 + r}",
                ["dungeon"] = Dungeons[r % Dungeons.Length],
                ["dungeonName"] = "Scarlet Monastery: Armory",
                ["startsAt"] = Monday + ((r / 5) * 86400) + ((r % 5) * 3600),
                ["state"] = (r % 7) switch { 0 => "full", 1 => "started", 2 => "cancelled", _ => "open" },
                ["source"] = (r % 3) switch { 0 => "site", 1 => "discord", _ => "game" },
                ["note"] = Note(r),
                ["url"] = $"https://wow.example.com/runs/{1000 + r}",
                ["organiser"] = new JsonObject
                {
                    ["battleTag"] = Friends[r % Friends.Length],
                    ["guid"] = $"Player-4619-0{r % Friends.Length:X3}{r:X4}",
                    ["name"] = Names[r % Names.Length],
                    ["utcOffset"] = r % 2 == 0 ? 3600 : -18000,
                },
                ["answers"] = answerList,
            });
        }

        var ids = new JsonArray();
        for (int i = 0; i < received; i++)
        {
            ids.Add($"k3f9c0a7e21b-{Monday - i}-{i}");
        }

        return new JsonObject
        {
            ["v"] = 1,
            ["updatedAt"] = Monday - 600,
            ["reminderMinutes"] = 30,
            ["received"] = ids,
            ["runs"] = list,
        };
    }

    // 200 characters: what the site allows, with the characters a writer must escape.
    private static string Note(int r)
    {
        string text = $"Run {r}: bring \"water\" & food, C:\\path\\to\\nothing; meet at the gate — müssen wir? ";
        var note = new StringBuilder();
        while (note.Length < 200)
        {
            note.Append(text);
        }

        return note.ToString(0, 200);
    }

    private static JsonElement Element(JsonNode node) => JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();

    [Fact]
    public void A_busy_week_stays_far_below_the_caps_and_comes_back_unchanged()
    {
        var week = BusyWeek();
        var result = RunsLua.Write(Element(week));

        Assert.NotNull(result.Content);
        Assert.Equal(0, result.Trimmed);
        var (value, depth, longest) = LuaData.Read(result.Content!);
        Assert.True(JsonNode.DeepEquals(week, value), "the file reads back as the site's answer");
        Assert.True(result.Content!.Length < RunsLua.MaxBytes / 4, $"{result.Content.Length} bytes");
        Assert.True(depth <= RunsLua.MaxDepth / 2, $"depth {depth}");
        Assert.True(longest <= RunsLua.MaxStringLength / 2, $"longest string {longest}");
    }

    [Fact]
    public void The_sites_own_maximum_still_fits_with_nothing_dropped()
    {
        // WEB-44's caps: 50 runs, 40 answers each, 200 received ids.
        var most = BusyWeek(runs: 50, answers: 40, received: 200);
        var result = RunsLua.Write(Element(most));

        Assert.NotNull(result.Content);
        Assert.Equal(0, result.Trimmed);
        Assert.True(JsonNode.DeepEquals(most, LuaData.Read(result.Content!).Value));
        Assert.True(result.Content!.Length < RunsLua.MaxBytes / 2, $"{result.Content.Length} bytes");
    }

    [Theory]
    [InlineData("\"}, os.execute(\"format c:\") --")]
    [InlineData("]] .. os.exit() .. [[")]
    [InlineData("line one\nline two\r\tand a bell \a")]
    [InlineData("back\\slash \\\" \\ddd \\n")]
    [InlineData("|cffff0000red|r and |Hlink|h")]
    public void A_string_that_tries_to_break_out_stays_a_string(string text)
    {
        var answer = new JsonObject { ["v"] = 1, ["runs"] = new JsonArray(new JsonObject { ["note"] = text, [text] = text }) };
        var result = RunsLua.Write(Element(answer));

        var (value, _, _) = LuaData.Read(result.Content!);
        Assert.Equal(text, value!["runs"]![0]!["note"]!.GetValue<string>());
        Assert.Equal(text, value["runs"]![0]![text]!.GetValue<string>());
        Assert.DoesNotContain("\n\n", Encoding.UTF8.GetString(result.Content!), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_numbers_booleans_and_leaves_out_null()
    {
        var answer = JsonNode.Parse("""{ "v": 1, "updatedAt": 1793120000, "ratio": 0.25, "neg": -18000, "on": true, "off": false, "gone": null, "list": [1, null, 3] }""")!;
        string text = Encoding.UTF8.GetString(RunsLua.Write(Element(answer)).Content!);

        Assert.StartsWith(RunsLua.Header + "CF_RUNS = {\n", text, StringComparison.Ordinal);
        Assert.Contains("[\"updatedAt\"] = 1793120000,", text, StringComparison.Ordinal);
        Assert.Contains("[\"ratio\"] = 0.25,", text, StringComparison.Ordinal);
        Assert.Contains("[\"neg\"] = -18000,", text, StringComparison.Ordinal);
        Assert.Contains("[\"on\"] = true,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("gone", text, StringComparison.Ordinal);
        Assert.Contains("nil,", text, StringComparison.Ordinal); // keeps the list's positions
    }

    [Fact]
    public void The_caps_cut_what_no_real_answer_reaches()
    {
        JsonNode deep = new JsonObject { ["x"] = 1 };
        for (int i = 0; i < RunsLua.MaxDepth + 2; i++)
        {
            deep = new JsonObject { ["in"] = deep };
        }

        var result = RunsLua.Write(Element(new JsonObject { ["deep"] = deep, ["long"] = new string('x', RunsLua.MaxStringLength + 10), ["huge"] = JsonNode.Parse("1e400") }));
        Assert.Equal(3, result.Trimmed);
        var (value, depth, longest) = LuaData.Read(result.Content!);
        Assert.True(depth <= RunsLua.MaxDepth + 1);
        Assert.Equal(RunsLua.MaxStringLength, longest);
        Assert.Null(value!["huge"]);

        var tooBig = new JsonObject { ["runs"] = new JsonArray([.. Enumerable.Range(0, 10000).Select(_ => (JsonNode)new string('y', 480))]) };
        Assert.Null(RunsLua.Write(Element(tooBig)).Content);
    }

    [Fact]
    public void An_answer_that_is_not_an_object_writes_nil()
    {
        string text = Encoding.UTF8.GetString(RunsLua.Write(JsonDocument.Parse("[1,2]").RootElement).Content!);
        Assert.EndsWith("CF_RUNS = nil\n", text, StringComparison.Ordinal);
    }
}
