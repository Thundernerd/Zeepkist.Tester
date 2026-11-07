using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// The steps the tester ships with, as the parser, <c>ztest check</c> and the step reference see them.
/// </summary>
public class CoreStepsTests
{
    private static readonly StepRegistry _registry = OfflineRegistry.Read([], typeof(GameHost).Assembly.Location);

    private static readonly string[] _names =
    [
        "wait",
        "log",
        "screenshot",
        "expect-blank-screenshot",
        "expect-no-exceptions",
        "place-start",
        "place-block",
        "set-block-setting",
        "expect-block-count",
        "test-mode",
        "back-to-editor",
        "undo",
        "redo",
    ];

    public static TheoryData<string> Names => new TheoryData<string>(_names);

    [Fact]
    public void TheTesterShips_ExactlyTheseSteps()
    {
        Assert.Equal(_names.Order(), _registry.Steps.Select(step => step.FullName).Order());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EachStep_HasDocs_ForEachOfItsArgumentsToo(string name)
    {
        var step = Step(name);

        Assert.False(string.IsNullOrWhiteSpace(step.Documentation));
        Assert.All(step.Arguments, argument => Assert.False(string.IsNullOrWhiteSpace(argument.Documentation)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EachStep_HasAnExample_ThatTheParserTakes(string name)
    {
        var step = Step(name);

        Assert.NotEmpty(step.Examples);
        foreach (var example in step.Examples)
        {
            var scenario = ScenarioParser.Parse("example", new StringReader($"steps:\n  {example}\n"), _registry.Steps);
            var parsed = Assert.IsType<StepNode>(Assert.Single(scenario.Steps));
            Assert.Equal(name, parsed.Step.FullName);
        }
    }

    [Theory]
    [InlineData("place-start")]
    [InlineData("place-block")]
    [InlineData("set-block-setting")]
    [InlineData("expect-block-count")]
    [InlineData("undo")]
    [InlineData("redo")]
    public void AnEditorStep_NeedsTheEditor_AndStaysThere(string name)
    {
        var step = Step(name);

        Assert.Equal(["editor"], Assert.Single(step.Requires));
        Assert.Empty(step.Enters);
        Assert.Empty(step.Leaves);
    }

    [Fact]
    public void TestMode_GoesFromTheEditorToTestMode()
    {
        var step = Step("test-mode");

        Assert.Equal(["editor"], Assert.Single(step.Requires));
        Assert.Equal(["editor"], step.Leaves);
        Assert.Equal(["test-mode"], step.Enters);
    }

    [Fact]
    public void BackToEditor_GoesFromTestModeToTheEditor()
    {
        var step = Step("back-to-editor");

        Assert.Equal(["test-mode"], Assert.Single(step.Requires));
        Assert.Equal(["test-mode"], step.Leaves);
        Assert.Equal(["editor"], step.Enters);
    }

    [Fact]
    public void TestMode_SaysHowToGetThere()
    {
        var state = _registry.States.Single(state => state.FullName == "test-mode");

        Assert.Contains("`test-mode`", state.Hint);
    }

    [Fact]
    public void TheEditor_SaysHowToGetBackFromTestMode()
    {
        var state = _registry.States.Single(state => state.FullName == "editor");

        Assert.Contains("`back-to-editor`", state.Hint);
    }

    [Fact]
    public void PlaceBlock_DefinesABlock_ThatSetBlockSettingUses()
    {
        var name = Step("place-block").Arguments.Single(argument => argument.Name == "as");
        var block = Step("set-block-setting").Arguments.Single(argument => argument.Name == "block");

        Assert.Equal(ArgumentKind.Name, name.Type.Kind);
        Assert.Equal(ArgumentKind.Ref, block.Type.Kind);
        Assert.Equal("block", name.Type.ValueKind);
        Assert.Equal("block", block.Type.ValueKind);
    }

    private static StepDescriptor Step(string name)
    {
        return _registry.Steps.Single(step => step.FullName == name);
    }
}
