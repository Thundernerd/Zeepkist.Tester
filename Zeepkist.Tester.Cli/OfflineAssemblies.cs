using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Assemblies opened for their metadata only, so none of their code runs and they can target another runtime.
/// </summary>
internal sealed class OfflineAssemblies : IDisposable
{
    private readonly MetadataLoadContext _context;

    /// <param name="folders">Where to find what the assemblies reference. The first folder with an assembly wins.</param>
    /// <param name="coreAssembly">The assembly with <see cref="object"/>, for the runtime the assemblies target.</param>
    public OfflineAssemblies(IEnumerable<string> folders, string coreAssembly = "mscorlib")
    {
        _context = new MetadataLoadContext(new FolderResolver(folders.ToList()), coreAssembly);
    }

    /// <exception cref="UnreadableAssemblyException">The file isn't a .NET assembly, or another build of it is already loaded.</exception>
    public Assembly Load(string path)
    {
        try
        {
            return _context.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
        catch (BadImageFormatException exception)
        {
            throw new UnreadableAssemblyException($"{path} isn't a .NET assembly.", exception);
        }
        catch (FileLoadException exception)
        {
            var name = AssemblyName.GetAssemblyName(path).Name;
            var other = _context.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == name);
            if (other == null)
                throw;

            throw new UnreadableAssemblyException($"{path} is another build of {name}, which is already read from {other.Location}. Only one build of an assembly can be read.", exception);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Takes the first match instead of the highest version as PathAssemblyResolver does: that one also fails when
    /// two folders hold different builds of one assembly, which a build output and the NuGet cache can.
    /// </summary>
    private sealed class FolderResolver : MetadataAssemblyResolver
    {
        private readonly IReadOnlyList<string> _folders;

        public FolderResolver(IReadOnlyList<string> folders)
        {
            _folders = folders;
        }

        public override Assembly Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            var path = _folders
                .Select(folder => Path.Combine(folder, assemblyName.Name + ".dll"))
                .FirstOrDefault(File.Exists);

            return path == null ? null : context.LoadFromAssemblyPath(path);
        }
    }
}
