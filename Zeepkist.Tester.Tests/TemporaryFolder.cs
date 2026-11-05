using System;
using System.IO;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// A folder of its own for one test, removed with it.
/// </summary>
internal sealed class TemporaryFolder : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("ztest-").FullName;

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
