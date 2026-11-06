using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What <c>ztest check</c>'s tests read fixtures with: named values from <see cref="TestSteps"/>, and steps that change states the way the tester's and a mod's will.
/// </summary>
public static class CheckSteps
{
    public static readonly StepRegistry Registry = new StepDescriptorBuilder { TesterAssemblyName = typeof(CheckSteps).Assembly.GetName().Name }.BuildRegistry(
        new[]
        {
            typeof(TestSteps.Wait),
            typeof(TestSteps.Undo),
            typeof(TestSteps.PlaceBlock),
            typeof(TestSteps.Named),
            typeof(OpenWindow),
            typeof(CloseWindow),
            typeof(NewScript),
            typeof(Compile),
            typeof(GoToTestMode),
            typeof(BackToEditor),
            typeof(NeedsEither),
            typeof(MakeThing),
            typeof(MakeSpecial),
            typeof(UseThing),
            typeof(UseSpecial),
            typeof(MakeAuto),
            typeof(UseAuto),
        },
        Type.EmptyTypes);

    [ValueKind("thing", Substitute = nameof(Text))]
    public class Thing
    {
        public string Text { get; set; }
    }

    [ValueKind("special-thing")]
    public sealed class SpecialThing : Thing
    {
    }

    public abstract class EmptyState : IState
    {
        public bool IsMet(IStepContext context)
        {
            return true;
        }
    }

    public abstract class EmptyStep : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    [State("editor", Hint = "Start with `start: editor`, or add `back-to-editor`.")]
    public sealed class InEditor : EmptyState
    {
    }

    [State("test-mode", Hint = "Add `test-mode` first.")]
    public sealed class InTestMode : EmptyState
    {
    }

    [State("window-open", Namespace = "zls", Within = typeof(InEditor), Hint = "Add `open-window` first.")]
    public sealed class WindowOpen : EmptyState
    {
    }

    [State("script-selected", Namespace = "zls", Within = typeof(WindowOpen), Hint = "Add `new-script` first.")]
    public sealed class ScriptSelected : EmptyState
    {
    }

    [Step("open-window")]
    [Requires(typeof(InEditor))]
    [Enters(typeof(WindowOpen))]
    public sealed class OpenWindow : EmptyStep
    {
    }

    [Step("close-window")]
    [Leaves(typeof(WindowOpen))]
    public sealed class CloseWindow : EmptyStep
    {
    }

    [Step("new-script")]
    [Requires(typeof(WindowOpen))]
    [Enters(typeof(ScriptSelected))]
    public sealed class NewScript : EmptyStep
    {
    }

    [Step("compile")]
    [Requires(typeof(ScriptSelected))]
    public sealed class Compile : EmptyStep
    {
    }

    [Step("test-mode")]
    [Requires(typeof(InEditor))]
    [Leaves(typeof(InEditor))]
    [Enters(typeof(InTestMode))]
    public sealed class GoToTestMode : EmptyStep
    {
    }

    [Step("back-to-editor")]
    [Requires(typeof(InTestMode))]
    [Leaves(typeof(InTestMode))]
    [Enters(typeof(InEditor))]
    public sealed class BackToEditor : EmptyStep
    {
    }

    [Step("needs-either")]
    [RequiresAny(typeof(ScriptSelected), typeof(InTestMode))]
    public sealed class NeedsEither : EmptyStep
    {
    }

    [Step("make-thing")]
    public sealed class MakeThing : EmptyStep
    {
        [Arg(Primary = true)]
        public Name<Thing> Defines { get; set; }
    }

    [Step("make-special")]
    public sealed class MakeSpecial : EmptyStep
    {
        [Arg(Primary = true)]
        public Name<SpecialThing> Defines { get; set; }
    }

    [Step("use-thing")]
    public sealed class UseThing : EmptyStep
    {
        [Arg(Primary = true)]
        public Ref<Thing> Uses { get; set; }
    }

    [Step("use-special")]
    public sealed class UseSpecial : EmptyStep
    {
        [Arg(Primary = true)]
        public Ref<SpecialThing> Uses { get; set; }
    }

    [Step("make-auto")]
    public sealed class MakeAuto : EmptyStep
    {
        [Arg(Default = "auto")]
        public Name<Thing>? Defines { get; set; }
    }

    [Step("use-auto")]
    public sealed class UseAuto : EmptyStep
    {
        [Arg(Default = "auto")]
        public Ref<Thing>? Uses { get; set; }
    }
}
