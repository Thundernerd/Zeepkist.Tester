using System.Collections;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Steps;

/// <summary>Checks the game logged no exception since the scenario started, or since the last time this step ran.</summary>
[Step("expect-no-exceptions")]
[Example("- expect-no-exceptions")]
public sealed class ExpectNoExceptions : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        // Unity logs an exception from the frame it's thrown in, so one from the step before has been logged by now
        yield return null;
        var logged = context.Exceptions.TakeSinceCheck();
        if (logged.Count == 0)
        {
            context.Pass("No exceptions", "0", "0");
            yield break;
        }

        var count = logged.Sum(exception => exception.Count);
        var listed = string.Join("; ", logged.Select(exception => exception.ToString()));
        context.Fail(count == 1 ? "1 exception logged" : $"{count} exceptions logged", "0", $"{count}: {listed}");
    }
}
