using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleCore;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Steps and a state that use a type from Zeepkist.Tester.Tests.SampleCore, for reading them offline where it can't be found.
/// </summary>
public static class UnfoundSteps
{
    [Step("base-elsewhere", Namespace = "zls")]
    public sealed class BaseElsewhere : CoreStepBase
    {
    }

    [Step("argument-elsewhere", Namespace = "zls")]
    public sealed class ArgumentElsewhere : InvalidSteps.Base
    {
        [Arg]
        public CoreState State { get; set; }
    }

    [State("within-elsewhere", Namespace = "zls", Within = typeof(CoreState))]
    public sealed class WithinElsewhere : IState
    {
        public bool IsMet(IStepContext context)
        {
            return true;
        }
    }
}
