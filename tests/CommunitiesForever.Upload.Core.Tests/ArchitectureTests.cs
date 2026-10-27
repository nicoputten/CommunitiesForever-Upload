using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace CommunitiesForever.Upload.Core.Tests;

/// <summary>
/// Keeps Core UI-free and OS-free (CLAUDE.md rule 2): it may not reference Avalonia, Windows APIs,
/// the other projects of this solution, or touch the file system, processes, the registry or sockets
/// directly. Those go through interfaces implemented in the Windows project.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void CoreUsesNoUiOrOsApis()
    {
        var violations = CoreRules.FindViolations(typeof(ContentHash).Assembly);

        Assert.Empty(violations);
    }

    [Fact]
    public void RulesCatchForbiddenApis()
    {
        // This test assembly deliberately uses forbidden APIs (ForbiddenApiSamples), so the rules must report them.
        var violations = CoreRules.FindViolations(typeof(ArchitectureTests).Assembly);

        Assert.Contains("type System.IO.File", violations);
        Assert.Contains("type System.Diagnostics.Process", violations);
        Assert.Contains("P/Invoke ForbiddenApiSamples.GetTickCount", violations);
    }
}

internal static class CoreRules
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Avalonia",
        "Microsoft.Win32",
        "Microsoft.Windows",
        "System.Diagnostics.Process",
        "System.IO.FileSystem.Watcher",
        "System.Net.Sockets",
        "System.Security.Cryptography.ProtectedData",
        "CommunitiesForever.Upload.Windows",
        "CommunitiesForever.Upload.App",
    ];

    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "Avalonia",
        "Microsoft.Win32",
        "Windows.",
        "System.Net.Sockets",
        "System.Management",
    ];

    private static readonly HashSet<string> ForbiddenTypes =
    [
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileStream",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "System.IO.DriveInfo",
        "System.IO.FileSystemWatcher",
        "System.Diagnostics.Process",
        "System.Net.Http.HttpClientHandler",
        "System.Net.Http.SocketsHttpHandler",
    ];

    public static List<string> FindViolations(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var violations = new List<string>();

        foreach (var handle in reader.AssemblyReferences)
        {
            string name = reader.GetString(reader.GetAssemblyReference(handle).Name);
            if (ForbiddenAssemblyPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            {
                violations.Add($"assembly {name}");
            }
        }

        foreach (var handle in reader.TypeReferences)
        {
            var type = reader.GetTypeReference(handle);
            string fullName = $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}";
            if (ForbiddenTypes.Contains(fullName)
                || ForbiddenNamespacePrefixes.Any(p => fullName.StartsWith(p, StringComparison.Ordinal)))
            {
                violations.Add($"type {fullName}");
            }
        }

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
                violations.Add($"P/Invoke {reader.GetString(declaringType.Name)}.{reader.GetString(method.Name)}");
            }
        }

        return violations;
    }
}
