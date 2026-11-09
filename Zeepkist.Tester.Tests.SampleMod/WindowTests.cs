using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>
/// C# tests of a mod's, as the CLI reads them without running them.
/// </summary>
public sealed class WindowTests
{
    /// <summary>The window keeps its footer at the smallest height.</summary>
    [InGameTest]
    public IEnumerator MinimumHeight(IStepContext context)
    {
        yield return context.Run(new Compile("main"));
    }

    [InGameTest(Start = Start.MainMenu)]
    public IEnumerator FromTheMenu(IStepContext context)
    {
        yield break;
    }

    public IEnumerator NotATest(IStepContext context)
    {
        yield break;
    }
}
