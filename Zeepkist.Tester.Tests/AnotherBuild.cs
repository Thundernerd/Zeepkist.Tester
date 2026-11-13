using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Copies of an assembly that are another build of it, as a Debug and a Release build are: one name, another module version id.
/// </summary>
internal static class AnotherBuild
{
    /// <returns>The copy's path, in <paramref name="folder"/> under the assembly's own file name.</returns>
    public static string Of(string assemblyPath, string folder)
    {
        var bytes = File.ReadAllBytes(assemblyPath);
        using (var pe = new PEReader(new MemoryStream(bytes)))
        {
            var metadata = pe.GetMetadataReader();
            var mvidIndex = MetadataTokens.GetHeapOffset(metadata.GetModuleDefinition().Mvid);
            var offset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Guid) + ((mvidIndex - 1) * 16);
            Guid.NewGuid().ToByteArray().CopyTo(bytes, offset);
        }

        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, Path.GetFileName(assemblyPath));
        File.WriteAllBytes(copy, bytes);
        return copy;
    }
}
