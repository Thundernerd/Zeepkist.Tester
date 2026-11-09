using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using static TNRD.Zeepkist.Tester.Tests.RunnerSteps;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// C# tests for the runner's tests, which say what happened through a <see cref="Recorder"/> service.
/// </summary>
public sealed class RunnerTests
{
    [InGameTest]
    public IEnumerator TwoNotes(IStepContext context)
    {
        yield return context.Run(new Note { Text = "a" });
        yield return context.Run(new Note { Text = "b" });
    }

    [InGameTest(Start = Start.MainMenu)]
    public IEnumerator FromTheMenu(IStepContext context)
    {
        yield return context.Run(new Note { Text = "menu" });
    }

    [InGameTest]
    public IEnumerator FailsHalfway(IStepContext context)
    {
        yield return context.Run(new Note { Text = "before" });
        yield return context.Run(new Fail { Why = "why" });
        context.Get<Recorder>().Events.Add("after");
        yield return context.Run(new Note { Text = "after" });
    }

    [InGameTest]
    public IEnumerator Throws(IStepContext context)
    {
        yield return context.Run(new Note { Text = "before" });
        throw new InvalidOperationException("boom");
    }

    [InGameTest]
    public IEnumerator FailsItself(IStepContext context)
    {
        context.Fail("not right");
        yield break;
    }

    [InGameTest]
    public IEnumerator StopsItself(IStepContext context)
    {
        yield return null;
        throw new StepFailedException("stopped");
    }

    [InGameTest]
    public IEnumerator ChecksItself(IStepContext context)
    {
        yield return context.Run(new Note { Text = "checked" });
        context.Pass("adds up", "3", "3");
    }

    [InGameTest]
    public IEnumerator Empty(IStepContext context)
    {
        yield break;
    }

    [InGameTest]
    public IEnumerator NamedValues(IStepContext context)
    {
        yield return context.Run(new DefineText { As = "lamp" });
        yield return context.Run(new UseText { Value = "lamp" });
        yield return context.Run(new Raw { Text = "${lamp} $${" });
        context.Detail = context.Values.Substitute("${lamp}");
    }

    [InGameTest]
    public IEnumerator FailsItselfThenAStep(IStepContext context)
    {
        context.Fail("first its own");
        yield return context.Run(new Fail { Why = "why" });
    }

    [InGameTest]
    public IEnumerator NeedsAState(IStepContext context)
    {
        yield return context.Run(new NeedsAState());
    }
}
