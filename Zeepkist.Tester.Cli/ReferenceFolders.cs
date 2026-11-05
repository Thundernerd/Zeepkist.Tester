using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Where to find what a test plugin references, on a machine that built it but may not have the game.
/// </summary>
internal static class ReferenceFolders
{
    private static readonly string[] _gamePackages = ["zeepkist.gamelibs", "zeepsdk", "bepinex.baselib", "harmonyx"];

    /// <summary>
    /// In order: the assemblies' own folders, .NET Framework's reference assemblies, the game's packages in the NuGet
    /// cache, and what's always there: this runtime's assemblies, and the SDK next to the CLI.
    /// </summary>
    /// <remarks>
    /// The reference assemblies go before the game's packages, which have an mscorlib of their own, so that mscorlib and
    /// the facades that forward to it come from one place. Only metadata is read, so the newest version of a package
    /// serves whichever version a plugin built against.
    /// </remarks>
    public static IEnumerable<string> For(IEnumerable<string> assemblyPaths)
    {
        var ownFolders = assemblyPaths.Select(path => Path.GetDirectoryName(Path.GetFullPath(path))).Distinct();
        var cache = NuGetCache();
        var referenceAssemblies = Path.Combine(NewestVersion(Path.Combine(cache, "microsoft.netframework.referenceassemblies.net472")), "build", ".NETFramework", "v4.7.2");

        return ownFolders
            .Append(referenceAssemblies)
            .Append(Path.Combine(referenceAssemblies, "Facades"))
            .Concat(_gamePackages.SelectMany(package => LibFolders(Path.Combine(cache, package))))
            .Append(RuntimeEnvironment.GetRuntimeDirectory())
            .Append(AppContext.BaseDirectory);
    }

    private static string NuGetCache()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        return string.IsNullOrEmpty(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
            : configured;
    }

    /// <summary>
    /// GameLibs has its DLLs right in <c>lib</c>, the others per target framework.
    /// </summary>
    private static IEnumerable<string> LibFolders(string packageFolder)
    {
        var lib = Path.Combine(NewestVersion(packageFolder), "lib");
        if (!Directory.Exists(lib))
            return [];

        return Directory.GetDirectories(lib).Order(StringComparer.Ordinal).Prepend(lib);
    }

    /// <summary>
    /// The folder of the package's newest version, or the package's own when it isn't in the cache.
    /// </summary>
    private static string NewestVersion(string packageFolder)
    {
        if (!Directory.Exists(packageFolder))
            return packageFolder;

        return Directory.GetDirectories(packageFolder).MaxBy(ParseVersion) ?? packageFolder;
    }

    private static Version ParseVersion(string versionFolder)
    {
        return Version.TryParse(Path.GetFileName(versionFolder).Split('-')[0], out var version) ? version : new Version();
    }
}
