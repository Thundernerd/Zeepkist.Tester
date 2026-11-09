using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Steps;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// C# tests in the real game, run after the scenarios. Two of them fail on purpose.
/// </summary>
public sealed class CSharpTests
{
    /// <summary>Core steps and the plugin's from C#, with a loop, a block's UID read by its name, and one step object run twice.</summary>
    [InGameTest]
    public IEnumerator StepsFromCSharp(IStepContext context)
    {
        yield return context.Run(new ExpectEditor());
        foreach (var number in new[] { 64, 120 })
            yield return context.Run(new AddToSum { Number = number });

        yield return context.Run(new ExpectSum { Total = 184 });
        yield return context.Run(new PlaceBlock { Type = 2440, As = "telemetry" });
        yield return context.Run(new ExpectBlockCount { Count = 1 });

        // ${…} is only substituted in scenarios, so the log says it as written
        var uid = context.Values.Get<BlockValue>("telemetry").Uid;
        yield return context.Run(new Log { Message = $"The block's UID is {uid}, and ${{telemetry}} stays as written." });

        var addOne = new AddToSum { Number = 1 };
        yield return context.Run(addOne);
        yield return context.Run(addOne);
        yield return context.Run(new ExpectSum { Total = 186 });

        var substituted = context.Values.Substitute("${telemetry}");
        if (substituted == uid && uid.Length > 0)
            context.Pass("The test read the block's UID by its name.", uid, substituted);
        else
            context.Fail("The block's name didn't give its UID.", uid, substituted);
    }

    /// <summary>A step whose state doesn't hold fails, and stops the test before the step after it.</summary>
    [InGameTest(Start = Start.MainMenu)]
    public IEnumerator AStepFails(IStepContext context)
    {
        yield return context.Run(new Log { Message = "Before the step that fails." });
        yield return context.Run(new ExpectEditor());
        yield return context.Run(new FailOnPurpose { Why = "The test went on after a failed step." });
    }

    /// <summary>A test fails by a check of its own, outside any step.</summary>
    [InGameTest(Start = Start.MainMenu)]
    public IEnumerator ItsOwnCheckFails(IStepContext context)
    {
        yield return null;
        context.Fail("To check a test can fail outside a step.", "1", 2.ToString(CultureInfo.InvariantCulture));
    }
}
