using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Where to find what a test plugin references, on a machine that built it but may not have the game.
/// </summary>
internal static class ReferenceFolders
{
    private const string ReferenceAssembliesPackage = "microsoft.netframework.referenceassemblies.net472";
    private static readonly string[] _gamePackages = ["zeepkist.gamelibs", "zeepsdk", "bepinex.baselib", "harmonyx"];

    /// <summary>
    /// In order: the assemblies' own folders, .NET Framework's reference assemblies, the game's packages in the NuGet
    /// cache, and what's always there: this runtime's assemblies, and the SDK next to the CLI.
    /// </summary>
    /// <remarks>
    /// The reference assemblies go before the game's packages, which have an mscorlib of their own, so that mscorlib and
    /// the facades that forward to it come from one place. Each package is taken at the version the first assembly with
    /// a restore beside it built against, and at the newest version in the cache otherwise.
    /// </remarks>
    /// <param name="assemblyPaths">The assemblies to read, test plugins first.</param>
    /// <param name="environmentVariable">Reads an environment variable. Defaults to this process's.</param>
    public static IEnumerable<string> For(IEnumerable<string> assemblyPaths, Func<string, string> environmentVariable = null)
    {
        environmentVariable ??= Environment.GetEnvironmentVariable;
        var paths = assemblyPaths.Select(Path.GetFullPath).ToList();
        var ownFolders = paths.Select(Path.GetDirectoryName).Distinct();
        var restore = paths.Select(Restore.Beside).FirstOrDefault(found => found != null);
        var cache = restore?.PackageFolders.FirstOrDefault() ?? NuGetCache(paths, environmentVariable);

        var referenceAssemblies = Path.Combine(PackageFolder(restore, cache, ReferenceAssembliesPackage), "build", ".NETFramework", "v4.7.2");

        return ownFolders
            .Append(referenceAssemblies)
            .Append(Path.Combine(referenceAssemblies, "Facades"))
            .Concat(_gamePackages.SelectMany(package => LibFolders(PackageFolder(restore, cache, package))))
            .Append(RuntimeEnvironment.GetRuntimeDirectory())
            .Append(AppContext.BaseDirectory);
    }

    /// <summary>
    /// The global packages folder as NuGet finds it: <c>NUGET_PACKAGES</c>, then the <c>globalPackagesFolder</c> of the
    /// nearest <c>nuget.config</c> above the first assembly, then that of the user's, then the default.
    /// </summary>
    private static string NuGetCache(IReadOnlyList<string> assemblyPaths, Func<string, string> environmentVariable)
    {
        var configured = environmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(configured))
            return configured;

        var startFolder = assemblyPaths.Select(Path.GetDirectoryName).FirstOrDefault();
        return ConfigFiles(startFolder).Select(GlobalPackagesFolder).FirstOrDefault(folder => folder != null)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    /// <summary>
    /// The <c>nuget.config</c> files that apply in <paramref name="startFolder"/>, nearest first, and then the user's.
    /// </summary>
    private static IEnumerable<string> ConfigFiles(string startFolder)
    {
        for (var folder = startFolder; folder != null; folder = Path.GetDirectoryName(folder))
        {
            foreach (var name in (string[])["nuget.config", "NuGet.config", "NuGet.Config"])
            {
                var file = Path.Combine(folder, name);
                if (File.Exists(file))
                {
                    yield return file;
                    break;
                }
            }
        }

        var userFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "NuGet");
        var userConfig = Path.Combine(userFolder, "NuGet.Config");
        if (File.Exists(userConfig))
            yield return userConfig;
    }

    /// <summary>
    /// The <c>globalPackagesFolder</c> <paramref name="configFile"/> sets, relative to its own folder, or
    /// <see langword="null"/> when it sets none or can't be read.
    /// </summary>
    private static string GlobalPackagesFolder(string configFile)
    {
        try
        {
            var value = XDocument.Load(configFile).Root?.Element("config")?.Elements("add")
                .LastOrDefault(add => string.Equals((string)add.Attribute("key"), "globalPackagesFolder", StringComparison.OrdinalIgnoreCase))
                ?.Attribute("value")?.Value;
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value), Path.GetDirectoryName(configFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The folder of the version the restore took, when it's on disk, or else of the newest version in the cache.
    /// </summary>
    private static string PackageFolder(Restore restore, string cache, string package)
    {
        return restore?.Find(package) ?? NewestVersion(Path.Combine(cache, package));
    }

    /// <summary>
    /// GameLibs has its DLLs right in <c>lib</c>, the others per target framework.
    /// </summary>
    private static IEnumerable<string> LibFolders(string versionFolder)
    {
        var lib = Path.Combine(versionFolder, "lib");
        if (!Directory.Exists(lib))
            return [];

        return Directory.GetDirectories(lib).Order(StringComparer.Ordinal).Prepend(lib);
    }

    /// <summary>
    /// The folder of the package's newest version, or the package's own when it isn't in the cache. A stable version is
    /// newer than its prereleases.
    /// </summary>
    private static string NewestVersion(string packageFolder)
    {
        if (!Directory.Exists(packageFolder))
            return packageFolder;

        return Directory.GetDirectories(packageFolder)
            .Select(versionFolder => (Folder: versionFolder, Version: Path.GetFileName(versionFolder).Split('+')[0]))
            .OrderBy(version => ParseVersion(version.Version))
            .ThenBy(version => !version.Version.Contains('-'))
            .ThenBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
            .Select(version => version.Folder)
            .LastOrDefault() ?? packageFolder;
    }

    private static Version ParseVersion(string version)
    {
        return Version.TryParse(version.Split('-')[0], out var parsed) ? parsed : new Version();
    }

    /// <summary>
    /// What a project's restore took: its <c>obj/project.assets.json</c>.
    /// </summary>
    private sealed class Restore
    {
        private readonly Dictionary<string, string> _packagePaths;

        private Restore(IReadOnlyList<string> packageFolders, Dictionary<string, string> packagePaths)
        {
            PackageFolders = packageFolders;
            _packagePaths = packagePaths;
        }

        /// <summary>
        /// Where the restore looked for packages: the global packages folder first, then any fallback folders.
        /// </summary>
        public IReadOnlyList<string> PackageFolders { get; }

        /// <summary>
        /// The restore of the project that built <paramref name="assemblyPath"/>: the first <c>obj/project.assets.json</c>
        /// in a folder above it, or <see langword="null"/> when there's none or it can't be read.
        /// </summary>
        public static Restore Beside(string assemblyPath)
        {
            for (var folder = Path.GetDirectoryName(assemblyPath); folder != null; folder = Path.GetDirectoryName(folder))
            {
                var assetsFile = Path.Combine(folder, "obj", "project.assets.json");
                if (File.Exists(assetsFile))
                    return Read(assetsFile);
            }

            return null;
        }

        /// <summary>
        /// The folder of the version the restore took of <paramref name="package"/>, or <see langword="null"/> when it
        /// took none or that version isn't on disk.
        /// </summary>
        public string Find(string package)
        {
            if (!_packagePaths.TryGetValue(package, out var path))
                return null;

            return PackageFolders.Select(packageFolder => Path.Combine(packageFolder, path)).FirstOrDefault(Directory.Exists);
        }

        private static Restore Read(string assetsFile)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(assetsFile));
                var root = document.RootElement;

                var packageFolders = root.TryGetProperty("packageFolders", out var folders) && folders.ValueKind == JsonValueKind.Object
                    ? folders.EnumerateObject().Select(folder => folder.Name).ToList()
                    : [];

                var packagePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty("libraries", out var libraries) && libraries.ValueKind == JsonValueKind.Object)
                {
                    foreach (var library in libraries.EnumerateObject())
                    {
                        if (library.Value.TryGetProperty("type", out var type) && type.GetString() == "package"
                            && library.Value.TryGetProperty("path", out var path) && path.GetString() is { } packagePath)
                        {
                            packagePaths[library.Name.Split('/')[0]] = packagePath;
                        }
                    }
                }

                return new Restore(packageFolders, packagePaths);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
