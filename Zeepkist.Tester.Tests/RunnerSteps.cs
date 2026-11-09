using System.Collections;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Steps for the runner's tests, which say what happened through a <see cref="Recorder"/> service.
/// </summary>
public static class RunnerSteps
{
    public sealed class Recorder
    {
        public List<string> Events { get; } = [];
    }

    [ValueKind("thing")]
    public sealed class Thing
    {
    }

    public sealed class Tally
    {
        public int Count { get; set; }
    }

    /// <summary>Holds when nothing says otherwise, for the check before a step.</summary>
    [State("never", Namespace = "t", Hint = "Add `t.note` first.")]
    public sealed class Never : IState
    {
        public bool IsMet(IStepContext context)
        {
            return false;
        }
    }

    [Step("note", Namespace = "t")]
    public sealed class Note : Step
    {
        [Arg(Primary = true)]
        public required string Text { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            context.Get<Recorder>().Events.Add(Text);
            yield break;
        }
    }

    [Step("route", Namespace = "t")]
    public sealed class Route : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Get<Recorder>().Events.Add($"route {context.Input.Route}");
            yield break;
        }
    }

    [Step("switch-to-os", Namespace = "t")]
    public sealed class SwitchToOs : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Input.Route = InputRoute.Os;
            yield break;
        }
    }

    [Step("number", Namespace = "t")]
    public sealed class Number : Step
    {
        [Arg(Primary = true)]
        public required int Value { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            context.Get<Recorder>().Events.Add($"number {Value}");
            yield break;
        }
    }

    [Step("define", Namespace = "t")]
    public sealed class Define : Step
    {
        [Arg(Primary = true)]
        public required Name<Thing> As { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            As.Set(context, new Thing());
            yield break;
        }
    }

    [Step("use", Namespace = "t")]
    public sealed class Use : Step
    {
        [Arg(Primary = true)]
        public required Ref<Thing> Value { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            Value.Get(context);
            yield break;
        }
    }

    [Step("define-text", Namespace = "t")]
    public sealed class DefineText : Step
    {
        [Arg(Primary = true)]
        public required Name<TextValue> As { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            As.Set(context, new TextValue("from a step"));
            yield break;
        }
    }

    [Step("fail", Namespace = "t")]
    public sealed class Fail : Step
    {
        [Arg(Primary = true)]
        public required string Why { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            context.Fail(Why, "expected", "actual");
            yield break;
        }
    }

    [Step("fail-twice", Namespace = "t")]
    public sealed class FailTwice : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Fail("first");
            context.Pass("passed", "a", "a");
            context.Fail("second");
            context.Get<Recorder>().Events.Add("went on after failing");
            yield break;
        }
    }

    [Step("fail-then-detail", Namespace = "t")]
    public sealed class FailThenDetail : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Fail("first");
            context.Detail = "second";
            yield break;
        }
    }

    [Step("check", Namespace = "t")]
    public sealed class Check : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Pass("looks right", "3", "3");
            yield break;
        }
    }

    [Step("throw", Namespace = "t")]
    public sealed class Throw : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield return null;
            throw new System.InvalidOperationException("boom");
        }
    }

    [Step("fail-by-exception", Namespace = "t")]
    public sealed class FailByException : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield return null;
            throw new StepFailedException("stopped");
        }
    }

    [Step("wait-for-nothing", Namespace = "t")]
    public sealed class WaitForNothing : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield return context.WaitUntil(() => false, 2, "the door");
            context.Get<Recorder>().Events.Add("went on after giving up");
        }
    }

    [Step("wait-a-while", Namespace = "t")]
    public sealed class WaitAWhile : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            var yields = new[] { context.WaitSeconds(2), context.WaitFrames(3), context.WaitUntil(() => true, 2, "nothing") };
            foreach (var wait in yields)
                yield return wait;

            context.Get<Recorder>().Events.Add("waited");
        }
    }

    [Step("tally", Namespace = "t")]
    public sealed class AddToTally : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            var tally = context.Scenario<Tally>();
            tally.Count++;
            context.Get<Recorder>().Events.Add($"tally {tally.Count}");
            yield break;
        }
    }

    [Step("needs-a-state", Namespace = "t")]
    [Requires(typeof(Never))]
    public sealed class NeedsAState : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Get<Recorder>().Events.Add("ran without its state");
            yield break;
        }
    }

    [Step("needs-the-editor", Namespace = "t")]
    public sealed class NeedsTheEditor : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            if (context.RequireEditor() is null)
                yield break;

            context.Get<Recorder>().Events.Add("ran without the editor");
        }
    }

    [Step("shot", Namespace = "t")]
    public sealed class Shot : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield return context.Screenshot("view");
        }
    }

    [Step("wants-a-string", Namespace = "t")]
    public sealed class WantsAService : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Get<string>();
            yield break;
        }
    }

    [Step("takes-exceptions", Namespace = "t")]
    public sealed class TakesExceptions : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            context.Get<Recorder>().Events.Add($"exceptions {context.Exceptions.TakeSinceCheck().Count}");
            context.Get<Recorder>().Events.Add($"scene loads {context.Game.ScenesLoaded}");
            context.Get<Recorder>().Events.Add($"scenario {context.ScenarioName}");
            yield break;
        }
    }

    [Step("raw", Namespace = "t")]
    public sealed class Raw : Step
    {
        [Arg(Primary = true)]
        public required string Text { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            context.Detail = Text;
            yield break;
        }
    }

    [Step("runs-a-step", Namespace = "t")]
    public sealed class RunsAStep : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield return context.Run(new Note { Text = "inner" });
        }
    }

    [Step("use-text", Namespace = "t")]
    public sealed class UseText : Step
    {
        [Arg(Primary = true)]
        public required Ref<TextValue> Value { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            Value.Get(context);
            yield break;
        }
    }
}
