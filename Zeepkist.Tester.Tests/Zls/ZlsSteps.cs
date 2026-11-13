using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.Zls;

/// <summary>
/// ZeepLogicScript's steps as its <c>StepList</c> has them, ported the way the step registry design says, for reading its <c>.zlstest</c> files.
/// Their types are only what a line needs; they run nothing.
/// </summary>
#pragma warning disable SA1402 // The steps are only a fixture, kept together
public static class ZlsSteps
{
    /// <summary>
    /// The tester's own steps, read from its DLL as <c>ztest convert</c> reads them, and ZeepLogicScript's.
    /// </summary>
    public static readonly IReadOnlyList<StepDescriptor> Registry =
    [
        .. OfflineRegistry.Read([], typeof(GameHost).Assembly.Location).Steps,
        .. new StepDescriptorBuilder().BuildAll(typeof(ZlsSteps).GetNestedTypes().Where(type => type.GetCustomAttributes(typeof(StepAttribute), false).Length > 0)),
    ];

    public enum Tab
    {
        Scripts,
        Libraries,
    }

    public enum BlocksTab
    {
        Placed,
        Reference,
    }

    public enum OnOff
    {
        On,
        Off,
    }

    public enum Prompt
    {
        None,
        New,
        Rename,
        Delete,
    }

    public enum PromptButton
    {
        Ok,
        Cancel,
        Delete,
        KeepBlocks,
    }

    public abstract class NoArguments : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    public abstract class TakesText : NoArguments
    {
        [Arg(Primary = true)]
        public required string Text { get; set; }
    }

    public abstract class Takes<T> : NoArguments
    {
        [Arg(Primary = true)]
        public required T Value { get; set; }
    }

    [Step("open-window", Namespace = "zls")]
    public sealed class OpenWindow : NoArguments
    {
    }

    [Step("close-window", Namespace = "zls")]
    public sealed class CloseWindow : NoArguments
    {
    }

    [Step("open-new", Namespace = "zls")]
    public sealed class OpenNew : NoArguments
    {
    }

    [Step("expect-footer-in-view", Namespace = "zls")]
    public sealed class ExpectFooterInView : NoArguments
    {
    }

    [Step("center-window", Namespace = "zls")]
    public sealed class CenterWindow : NoArguments
    {
    }

    [Step("expect-window-centered", Namespace = "zls")]
    public sealed class ExpectWindowCentered : NoArguments
    {
    }

    [Step("remember-window-rect", Namespace = "zls")]
    public sealed class RememberWindowRect : NoArguments
    {
    }

    [Step("expect-window-rect", Namespace = "zls")]
    public sealed class ExpectWindowRect : NoArguments
    {
    }

    [Step("expect-window-start-rect", Namespace = "zls")]
    public sealed class ExpectWindowStartRect : NoArguments
    {
    }

    [Step("expect-editor-in-view", Namespace = "zls")]
    public sealed class ExpectEditorInView : NoArguments
    {
    }

    [Step("compile", Namespace = "zls")]
    public sealed class Compile : NoArguments
    {
    }

    [Step("expect-no-errors", Namespace = "zls")]
    public sealed class ExpectNoErrors : NoArguments
    {
    }

    [Step("caret-at-start", Namespace = "zls")]
    public sealed class CaretAtStart : NoArguments
    {
    }

    [Step("caret-at-end", Namespace = "zls")]
    public sealed class CaretAtEnd : NoArguments
    {
    }

    [Step("complete", Namespace = "zls")]
    public sealed class Complete : NoArguments
    {
    }

    [Step("expect-call-help-no-active", Namespace = "zls")]
    public sealed class ExpectCallHelpNoActive : NoArguments
    {
    }

    [Step("expect-no-completion", Namespace = "zls")]
    public sealed class ExpectNoCompletion : NoArguments
    {
    }

    [Step("expect-no-call-help", Namespace = "zls")]
    public sealed class ExpectNoCallHelp : NoArguments
    {
    }

    [Step("expect-no-hover", Namespace = "zls")]
    public sealed class ExpectNoHover : NoArguments
    {
    }

    [Step("insert-selected", Namespace = "zls")]
    public sealed class InsertSelected : NoArguments
    {
    }

    [Step("new-script", Namespace = "zls")]
    public sealed class NewScript : TakesText
    {
    }

    [Step("select-script", Namespace = "zls")]
    public sealed class SelectScript : TakesText
    {
    }

    [Step("new-library", Namespace = "zls")]
    public sealed class NewLibrary : TakesText
    {
    }

    [Step("select-library", Namespace = "zls")]
    public sealed class SelectLibrary : TakesText
    {
    }

    [Step("open-delete", Namespace = "zls")]
    public sealed class OpenDelete : TakesText
    {
    }

    [Step("open-rename", Namespace = "zls")]
    public sealed class OpenRename : TakesText
    {
    }

    [Step("type-name", Namespace = "zls")]
    public sealed class TypeName : TakesText
    {
    }

    [Step("select-group", Namespace = "zls")]
    public sealed class SelectGroup : TakesText
    {
    }

    [Step("expect-list-result", Namespace = "zls")]
    public sealed class ExpectListResult : TakesText
    {
    }

    [Step("expect-text", Namespace = "zls")]
    public sealed class ExpectText : TakesText
    {
    }

    [Step("expect-result", Namespace = "zls")]
    public sealed class ExpectResult : TakesText
    {
    }

    [Step("expect-call-help", Namespace = "zls")]
    public sealed class ExpectCallHelp : TakesText
    {
    }

    [Step("expect-call-help-active", Namespace = "zls")]
    public sealed class ExpectCallHelpActive : TakesText
    {
    }

    [Step("hover", Namespace = "zls")]
    public sealed class Hover : TakesText
    {
    }

    [Step("expect-hover", Namespace = "zls")]
    public sealed class ExpectHover : TakesText
    {
    }

    [Step("type", Namespace = "zls")]
    public sealed class Type : TakesText
    {
    }

    [Step("set-reference-filter", Namespace = "zls")]
    public sealed class SetReferenceFilter : NoArguments
    {
        [Arg(Primary = true)]
        public string Filter { get; set; }
    }

    [Step("show-tab", Namespace = "zls")]
    public sealed class ShowTab : Takes<Tab>
    {
    }

    [Step("expect-tab", Namespace = "zls")]
    public sealed class ExpectTab : Takes<Tab>
    {
    }

    [Step("show-blocks-tab", Namespace = "zls")]
    public sealed class ShowBlocksTab : Takes<BlocksTab>
    {
    }

    [Step("expect-blocks-tab", Namespace = "zls")]
    public sealed class ExpectBlocksTab : Takes<BlocksTab>
    {
    }

    [Step("press-prompt", Namespace = "zls")]
    public sealed class PressPrompt : Takes<PromptButton>
    {
    }

    [Step("expect-prompt", Namespace = "zls")]
    public sealed class ExpectPrompt : Takes<Prompt>
    {
    }

    [Step("library-read-failed", Namespace = "zls")]
    public sealed class LibraryReadFailed : Takes<OnOff>
    {
    }

    [Step("startup-warning", Namespace = "zls")]
    public sealed class StartupWarning : Takes<OnOff>
    {
    }

    [Step("expect-library-read-failed", Namespace = "zls")]
    public sealed class ExpectLibraryReadFailed : Takes<OnOff>
    {
    }

    [Step("expect-startup-warning", Namespace = "zls")]
    public sealed class ExpectStartupWarning : Takes<OnOff>
    {
    }

    [Step("set-window-height", Namespace = "zls")]
    public sealed class SetWindowHeight : Takes<Pixels>
    {
    }

    [Step("expect-window-height", Namespace = "zls")]
    public sealed class ExpectWindowHeight : Takes<Pixels>
    {
    }

    [Step("expect-editor-in-view-each-frame", Namespace = "zls")]
    public sealed class ExpectEditorInViewEachFrame : Takes<int>
    {
    }

    [Step("expect-errors", Namespace = "zls")]
    public sealed class ExpectErrors : Takes<int>
    {
    }

    [Step("expect-blocks", Namespace = "zls")]
    public sealed class ExpectBlocks : Takes<int>
    {
    }

    [Step("press", Namespace = "zls")]
    public sealed class Press : Takes<Keys>
    {
    }

    [Step("set-text", Namespace = "zls")]
    public sealed class SetText : Takes<TextBlock>
    {
    }

    [Step("expect-window-width", Namespace = "zls")]
    public sealed class ExpectWindowWidth : NoArguments
    {
        [Arg(Primary = true)]
        [Pattern("^([1-9][0-9]*|screen)$")]
        public required string Width { get; set; }
    }

    [Step("set-window-size", Namespace = "zls")]
    [LineSyntax("<width> <height>")]
    public sealed class SetWindowSize : NoArguments
    {
        [Arg]
        public required Pixels Width { get; set; }

        [Arg]
        public required Pixels Height { get; set; }
    }

    [Step("expect-window-size", Namespace = "zls")]
    [LineSyntax("<width> <height>")]
    public sealed class ExpectWindowSize : NoArguments
    {
        [Arg]
        public required Pixels Width { get; set; }

        [Arg]
        public required Pixels Height { get; set; }
    }

    [Step("expect-reference-headings", Namespace = "zls")]
    [LineSyntax("<headings...>")]
    public sealed class ExpectReferenceHeadings : NoArguments
    {
        [Arg(Primary = true)]
        public required List<string> Headings { get; set; }
    }

    [Step("expect-error", Namespace = "zls")]
    public sealed class ExpectError : NoArguments
    {
        [Arg]
        public required int Line { get; set; }

        [Arg]
        public required int Column { get; set; }

        [Arg]
        public required string Message { get; set; }
    }

    [Step("expect-completion", Namespace = "zls")]
    [LineSyntax("<label> [<detail...>]")]
    public sealed class ExpectCompletion : NoArguments
    {
        [Arg(Primary = true)]
        [Pattern(@"^\S+$")]
        public required string Label { get; set; }

        [Arg]
        public string Detail { get; set; }
    }

    [Step("expect-completion-no-detail", Namespace = "zls")]
    public sealed class ExpectCompletionNoDetail : NoArguments
    {
        [Arg(Primary = true)]
        [Pattern(@"^\S+$")]
        public required string Label { get; set; }
    }

    [Step("expect-completion-missing", Namespace = "zls")]
    public sealed class ExpectCompletionMissing : NoArguments
    {
        [Arg(Primary = true)]
        [Pattern(@"^\S+$")]
        public required string Label { get; set; }
    }

    [Step("place-block", Namespace = "zls")]
    [LineSyntax("<function> [as <as>]")]
    public sealed class PlaceBlock : NoArguments
    {
        [Arg(Primary = true)]
        public required string Function { get; set; }

        [Arg]
        public Name<BlockValue>? As { get; set; }
    }

    [Step("set-block-setting", Namespace = "zls")]
    [LineSyntax("<block> <setting> <value>")]
    public sealed class SetBlockSetting : NoArguments
    {
        [Arg]
        public required Ref<BlockValue> Block { get; set; }

        [Arg]
        public required string Setting { get; set; }

        [Arg]
        public required string Value { get; set; }
    }

    [Step("name-group-block", Namespace = "zls")]
    [LineSyntax("<script...> <function> as <as>")]
    public sealed class NameGroupBlock : NoArguments
    {
        [Arg]
        public required string Script { get; set; }

        [Arg]
        public required string Function { get; set; }

        [Arg]
        public required Name<BlockValue> As { get; set; }
    }

    [Step("turn-off-style", Namespace = "zls")]
    [LineSyntax("<function> [<style>]")]
    public sealed class TurnOffStyle : NoArguments
    {
        [Arg(Primary = true)]
        public required string Function { get; set; }

        [Arg]
        public string Style { get; set; }
    }

    [Step("drag-window-edge", Namespace = "zls")]
    public sealed class DragWindowEdge : NoArguments
    {
        [Arg(Primary = true)]
        [MinItems(1)]
        public required List<DragMove> Moves { get; set; }

        [Arg(Default = false)]
        public bool Sideways { get; set; }
    }
}
