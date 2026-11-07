using System;
using System.Collections;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Steps as they're written in the tester and in mods, to build descriptors from.
/// </summary>
public static class TestSteps
{
    /// <summary>Waits, such as for the level to run a little before a screenshot.</summary>
    [Step("wait")]
    public sealed class Wait : Step
    {
        /// <summary>How long to wait.</summary>
        [Arg(Primary = true)]
        public required Seconds Seconds { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>Places a block of the Block Function's type.</summary>
    [Step("place-block")]
    [Example("- place-block: lamp")]
    [Example("- place-block: { function: lamp, as: my-lamp }")]
    public sealed class PlaceBlock : Step
    {
        /// <summary>The Block Function.</summary>
        [Arg(Primary = true)]
        [Pattern("^[a-z_]+$")]
        public required string Function { get; set; }

        /// <summary>A name for the block.</summary>
        [Arg]
        public string As { get; set; }

        [Arg(Default = 1, Name = "how-many")]
        [InRange(1, 8)]
        public int Count { get; set; }

        [Arg]
        public Pixels? Offset { get; set; }

        public int NotAnArgument { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>One move of a drag.</summary>
    public sealed class DragMove
    {
        /// <summary>Pixels down, or up for a number below 0.</summary>
        [Arg(Primary = true)]
        public required Pixels By { get; set; }

        /// <summary>Frames the move takes.</summary>
        [Arg(Default = 1)]
        [InRange(1, 600)]
        public int Over { get; set; }
    }

    /// <summary>Drags the window's resize handle.</summary>
    [Step("drag-window-edge", Namespace = "zls")]
    public sealed class DragWindowEdge : Step
    {
        /// <summary>The moves, made in turn before letting go.</summary>
        [Arg(Primary = true)]
        [MinItems(1)]
        public required List<DragMove> Moves { get; set; }

        [Arg(Default = false)]
        public bool Sideways { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    public enum Axis
    {
        Horizontal,
        [ArgValue("global_velocity")]
        GlobalVelocity,
        PageUp,
    }

    /// <summary>A step with an argument of each remaining type.</summary>
    [Step("every-type")]
    public sealed class EveryType : Step
    {
        [Arg(Default = Axis.GlobalVelocity)]
        public Axis Axis { get; set; }

        [Arg]
        public Axis? MaybeAxis { get; set; }

        [Arg]
        public TextBlock Text { get; set; }

        [Arg]
        public float Float { get; set; }

        [Arg]
        public bool Bool { get; set; }

        [Arg]
        public Keys Keys { get; set; }

        [Arg]
        public FileName FileName { get; set; }

        [Arg]
        public List<Seconds> Times { get; set; }

        [Arg]
        public List<Keys> Chords { get; set; }

        [Arg]
        public Seconds Timeout { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>A block placed by a step, by its UID.</summary>
    [ValueKind("block", Substitute = nameof(Uid))]
    public sealed class BlockValue
    {
        public string Uid { get; set; }
    }

    /// <summary>A kind whose values can't be written into text.</summary>
    [ValueKind("window")]
    public sealed class WindowValue
    {
    }

    /// <summary>A step with each way to define and use a named value.</summary>
    [Step("named")]
    public sealed class Named : Step
    {
        [Arg]
        public Name<BlockValue> Defines { get; set; }

        [Arg]
        public Name<WindowValue>? MaybeDefines { get; set; }

        [Arg]
        public Ref<BlockValue> Uses { get; set; }

        [Arg]
        public List<Ref<WindowValue>> UsesEach { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>One item of a nested argument, with text of its own.</summary>
    public sealed class Message
    {
        [Arg]
        public string Line { get; set; }

        [Arg]
        public List<TextBlock> Details { get; set; }
    }

    /// <summary>A step with each type of argument that can hold <c>${name}</c>, and others that look like they could.</summary>
    [Step("written")]
    public sealed class Written : Step
    {
        [Arg]
        public string String { get; set; }

        [Arg]
        public string NoString { get; set; }

        [Arg]
        public TextBlock Text { get; set; }

        [Arg]
        public TextBlock? MaybeText { get; set; }

        [Arg]
        public TextBlock? NoText { get; set; }

        [Arg]
        public List<string> Strings { get; set; }

        [Arg]
        public Message Nested { get; set; }

        [Arg]
        public List<Message> NestedItems { get; set; }

        [Arg]
        public FileName FileName { get; set; }

        [Arg]
        public Keys Keys { get; set; }

        [Arg]
        public Name<BlockValue> Defines { get; set; }

        [Arg]
        public Ref<BlockValue> Uses { get; set; }

        [Arg]
        public int GetOnly => 1;

        public string NotAnArgument { get; set; }

        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>Undoes the last change.</summary>
    [Step("undo")]
    public sealed class Undo : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    /// <summary>What the states and steps below run against: which states hold, and which steps ran.</summary>
    public sealed class StatesContext : BareStepContext
    {
        public HashSet<Type> Met { get; } = [];

        public List<Type> Ran { get; } = [];
    }

    /// <summary>A state that holds when its <see cref="StatesContext"/> says so.</summary>
    public abstract class ContextState : IState
    {
        public bool IsMet(IStepContext context)
        {
            return ((StatesContext)context).Met.Contains(GetType());
        }
    }

    /// <summary>A step that tells its <see cref="StatesContext"/> it ran.</summary>
    public abstract class ContextStep : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            ((StatesContext)context).Ran.Add(GetType());
            yield return "ran";
        }
    }

    /// <summary>The level editor, not in test mode.</summary>
    [State("editor", Namespace = "zls", Hint = "Start with `start: editor`.")]
    public sealed class InEditor : ContextState
    {
    }

    /// <summary>The Script window is open.</summary>
    [State("window-open", Namespace = "zls", Within = typeof(InEditor), Hint = "Add `zls.open-window` first.")]
    public sealed class WindowOpen : ContextState
    {
    }

    /// <summary>A Script is selected.</summary>
    [State("script-selected", Namespace = "zls", Within = typeof(WindowOpen), Hint = "Add `zls.new-script` or `zls.select-script` first.")]
    public sealed class ScriptSelected : ContextState
    {
    }

    /// <summary>A text field has the focus.</summary>
    [State("field-focused", Namespace = "zls", Within = typeof(WindowOpen), Hint = "Add `zls.caret-at-end` first.")]
    public sealed class FieldFocused : ContextState
    {
    }

    /// <summary>A state with nothing to say about how to get into it.</summary>
    [State("no-hint", Namespace = "zls")]
    public sealed class NoHint : ContextState
    {
    }

    [Step("compile", Namespace = "zls")]
    [Requires(typeof(ScriptSelected))]
    public sealed class Compile : ContextStep
    {
    }

    [Step("open-window", Namespace = "zls")]
    [Requires(typeof(InEditor), typeof(NoHint))]
    [Enters(typeof(WindowOpen))]
    public sealed class OpenWindow : ContextStep
    {
    }

    [Step("close-window", Namespace = "zls")]
    [Leaves(typeof(WindowOpen))]
    public sealed class CloseWindow : ContextStep
    {
    }

    [Step("needs-either", Namespace = "zls")]
    [Requires(typeof(InEditor))]
    [RequiresAny(typeof(ScriptSelected), typeof(FieldFocused))]
    public sealed class NeedsEither : ContextStep
    {
    }

    [Step("needs-no-hint", Namespace = "zls")]
    [Requires(typeof(NoHint))]
    public sealed class NeedsNoHint : ContextStep
    {
    }
}
