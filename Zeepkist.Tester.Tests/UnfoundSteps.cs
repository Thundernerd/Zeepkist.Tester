using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleCore;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Steps, states and C# tests that use a type from Zeepkist.Tester.Tests.SampleCore, for reading them offline where it can't be found.
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

    [Step("requires-elsewhere", Namespace = "zls")]
    [Requires(typeof(CoreState))]
    public sealed class RequiresElsewhere : InvalidSteps.Base
    {
    }

    [Step("requires-any-elsewhere", Namespace = "zls")]
    [RequiresAny(typeof(CoreState), typeof(WithinElsewhere))]
    public sealed class RequiresAnyElsewhere : InvalidSteps.Base
    {
    }

    [Step("enters-elsewhere", Namespace = "zls")]
    [Enters(typeof(CoreState))]
    public sealed class EntersElsewhere : InvalidSteps.Base
    {
    }

    [Step("leaves-elsewhere", Namespace = "zls")]
    [Leaves(typeof(CoreState))]
    public sealed class LeavesElsewhere : InvalidSteps.Base
    {
    }

    [State("base-elsewhere", Namespace = "zls")]
    public sealed class StateBaseElsewhere : CoreStateBase
    {
    }

    [State("constructor-elsewhere", Namespace = "zls")]
    public sealed class ConstructorElsewhere : IState
    {
        // The constructor the harness makes it with, so the overload below is the only problem
        public ConstructorElsewhere()
        {
        }

        public ConstructorElsewhere(CoreState outer)
        {
        }

        public bool IsMet(IStepContext context)
        {
            return true;
        }
    }

    [Step("requires-a-state-based-elsewhere", Namespace = "zls")]
    [Requires(typeof(StateBaseElsewhere))]
    public sealed class RequiresAStateBasedElsewhere : InvalidSteps.Base
    {
    }

    /// <summary>A C# test in a class with a constructor that takes a type from the missing assembly.</summary>
    public sealed class TestConstructorElsewhere
    {
        // The constructor the harness makes it with, so the overload below is the only problem
        public TestConstructorElsewhere()
        {
        }

        public TestConstructorElsewhere(CoreState state)
        {
        }

        [InGameTest]
        public IEnumerator Test(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>A C# test that takes a type from the missing assembly.</summary>
    public sealed class TestParameterElsewhere
    {
        [InGameTest]
        public IEnumerator Test(CoreState state)
        {
            yield break;
        }
    }
}
