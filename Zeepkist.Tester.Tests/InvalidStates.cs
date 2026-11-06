using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// States, and steps using them, that each break one of the registry's rules.
/// </summary>
public static class InvalidStates
{
    public abstract class Base : IState
    {
        public bool IsMet(IStepContext context)
        {
            return true;
        }
    }

    public abstract class StepBase : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    public sealed class NoStateAttribute : Base
    {
    }

    [State("good", Namespace = "zls")]
    public sealed class Good : Base
    {
    }

    [State("Bad_Name", Namespace = "zls")]
    public sealed class NameNotKebab : Base
    {
    }

    [State("good", Namespace = "Bad_Namespace")]
    public sealed class NamespaceNotKebab : Base
    {
    }

    [State("no-namespace")]
    public sealed class NoNamespace : Base
    {
    }

    [State("not-a-state", Namespace = "zls")]
    public sealed class NotAState
    {
    }

    [State("abstract", Namespace = "zls")]
    public abstract class Abstract : Base
    {
    }

    [State("takes-arguments", Namespace = "zls")]
    public sealed class TakesArguments : Base
    {
        public TakesArguments(int argument)
        {
        }
    }

    [State("open-generic", Namespace = "zls")]
    public sealed class OpenGeneric<T> : Base
    {
    }

    [State("within-unknown", Namespace = "zls", Within = typeof(NoStateAttribute))]
    public sealed class WithinUnknown : Base
    {
    }

    [State("within-itself", Namespace = "zls", Within = typeof(WithinItself))]
    public sealed class WithinItself : Base
    {
    }

    [State("cycle-one", Namespace = "zls", Within = typeof(CycleTwo))]
    public sealed class CycleOne : Base
    {
    }

    [State("cycle-two", Namespace = "zls", Within = typeof(CycleThree))]
    public sealed class CycleTwo : Base
    {
    }

    [State("cycle-three", Namespace = "zls", Within = typeof(CycleOne))]
    public sealed class CycleThree : Base
    {
    }

    [State("into-cycle", Namespace = "zls", Within = typeof(CycleOne))]
    public sealed class IntoCycle : Base
    {
    }

    [State("good", Namespace = "zls")]
    public sealed class SameNameAsGood : Base
    {
    }

    [State("within-same-name", Namespace = "zls", Within = typeof(SameNameAsGood))]
    public sealed class WithinSameName : Base
    {
    }

    [Step("requires-unknown", Namespace = "zls")]
    [Requires(typeof(Good), typeof(NoStateAttribute))]
    public sealed class RequiresUnknown : StepBase
    {
    }

    [Step("requires-any-unknown", Namespace = "zls")]
    [RequiresAny(typeof(NoStateAttribute), typeof(Good))]
    public sealed class RequiresAnyUnknown : StepBase
    {
    }

    [Step("enters-unknown", Namespace = "zls")]
    [Enters(typeof(NoStateAttribute))]
    public sealed class EntersUnknown : StepBase
    {
    }

    [Step("leaves-unknown", Namespace = "zls")]
    [Leaves(typeof(NoStateAttribute))]
    public sealed class LeavesUnknown : StepBase
    {
    }

    [Step("requires-null", Namespace = "zls")]
    [Requires(typeof(Good), null)]
    public sealed class RequiresNull : StepBase
    {
    }

    [Step("requires-nothing", Namespace = "zls")]
    [Requires]
    public sealed class RequiresNothing : StepBase
    {
    }

    [Step("requires-any-of-nothing", Namespace = "zls")]
    [RequiresAny]
    public sealed class RequiresAnyOfNothing : StepBase
    {
    }

    [Step("requires-broken", Namespace = "zls")]
    [Requires(typeof(CycleOne))]
    public sealed class RequiresBroken : StepBase
    {
    }

    [Step("requires-good", Namespace = "zls")]
    [Requires(typeof(Good))]
    public sealed class RequiresGood : StepBase
    {
    }

    [Step("requires-same-name", Namespace = "zls")]
    [Requires(typeof(WithinSameName))]
    public sealed class RequiresSameName : StepBase
    {
    }
}
