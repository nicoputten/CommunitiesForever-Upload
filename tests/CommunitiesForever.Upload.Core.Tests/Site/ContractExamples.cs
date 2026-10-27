using System.Text.RegularExpressions;

namespace CommunitiesForever.Upload.Core.Tests.Site;

/// <summary>
/// The sample responses of docs/contract/UPLOADER-API.md (the web repo's contract, copied by
/// scripts/refresh-api-contract.ps1). Taking them from there keeps these tests on the contract.
/// </summary>
internal static partial class ContractExamples
{
    private static readonly Lazy<Dictionary<string, string>> Examples = new(Load);

    public static string Get(string name) =>
        Examples.Value.TryGetValue(name, out string? json) ? json : throw new KeyNotFoundException($"No example '{name}' in UPLOADER-API.md");

    public static IReadOnlyCollection<string> Names => Examples.Value.Keys;

    private static Dictionary<string, string> Load()
    {
        string contract = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "contract", "UPLOADER-API.md"));
        return Example().Matches(contract).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CommunitiesForever.Upload.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    [GeneratedRegex(@"<!-- example: ([\w-]+) -->\s*```json\s*(.*?)```", RegexOptions.Singleline)]
    private static partial Regex Example();
}
