using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// A test class that can't be made, because its constructor throws.
/// </summary>
public sealed class BrokenRunnerTests
{
    public BrokenRunnerTests()
    {
        throw new InvalidOperationException("no instance");
    }

    [InGameTest]
    public IEnumerator CantBeMade(IStepContext context)
    {
        yield break;
    }
}
