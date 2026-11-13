using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Tests.Zls;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class LineScenarioConverterTests
{
    [Fact]
    public void AScenario_BecomesAListOfSteps_WithItsCommentsAndUseForTheModsSteps()
    {
        var yaml = RoundTrip("# Opens the window\nopen-window\nnew-script Lights\n\n# Waits\nwait 2\nundo\n");

        Assert.Equal(
            "use: [zls]\nsteps:\n  # Opens the window\n  - open-window\n  - new-script: Lights\n\n  # Waits\n  - wait: 2\n  - undo\n",
            yaml);
    }

    [Theory]
    [InlineData("expect-text Placed 3 blocks", "  - expect-text: Placed 3 blocks")]
    [InlineData("expect-text 'nope' isn't defined", "  - expect-text: '''nope'' isn''t defined'")]
    [InlineData("expect-text a: b", "  - expect-text: 'a: b'")]
    [InlineData("expect-text 2:5", "  - expect-text: '2:5'")]
    [InlineData("expect-text a # b", "  - expect-text: 'a # b'")]
    [InlineData("expect-text {x}", "  - expect-text: '{x}'")]
    [InlineData("expect-text [x]", "  - expect-text: '[x]'")]
    [InlineData("expect-text *x", "  - expect-text: '*x'")]
    [InlineData("expect-text - x", "  - expect-text: '- x'")]
    [InlineData("expect-text ? x", "  - expect-text: '? x'")]
    [InlineData("expect-text \"x\"", "  - expect-text: '\"x\"'")]
    [InlineData("expect-text a, b", "  - expect-text: a, b")]
    [InlineData("expect-text yes", "  - expect-text: 'yes'")]
    [InlineData("expect-text Off", "  - expect-text: 'Off'")]
    [InlineData("expect-text {uid:lamp}", "  - expect-text: '{uid:lamp}'")]
    [InlineData("expect-text ${lamp}", "  - expect-text: $${lamp}")]
    [InlineData("startup-warning on", "  - startup-warning: 'on'")]
    [InlineData("press ctrl+space", "  - press: ctrl+space")]
    [InlineData("press ,", "  - press: ','")]
    [InlineData("hover f(a, b)", "  - hover: f(a, b)")]
    public void AValue_IsPlainWhenYamlReadsItBackAsWritten_AndQuotedOtherwise(string line, string step)
    {
        Assert.Equal(step, RoundTrip(line).Split('\n')[2]);
    }

    [Theory]
    [InlineData("set-window-size 800 600", "  - set-window-size: { width: 800, height: 600 }")]
    [InlineData("place-block lamp", "  - zls.place-block: lamp")]
    [InlineData("place-block lamp as lamp", "  - zls.place-block: { function: lamp, as: lamp }")]
    [InlineData("set-block-setting t mode global_velocity", "  - zls.set-block-setting: { block: t, setting: mode, value: global_velocity }")]
    [InlineData("expect-blocks 2", "  - expect-blocks: 2")]
    [InlineData("expect-error 2:15 'nope' isn't defined", "  - expect-error: { line: 2, column: 15, message: '''nope'' isn''t defined' }")]
    [InlineData("expect-reference-headings Inputs Libraries:closed", "  - expect-reference-headings: [Inputs, 'Libraries:closed']")]
    [InlineData("expect-reference-headings none", "  - expect-reference-headings: none")]
    [InlineData("expect-completion print", "  - expect-completion: print")]
    [InlineData("expect-completion print (text) -> void", "  - expect-completion: { label: print, detail: (text) -> void }")]
    [InlineData("drag-window-edge 300", "  - drag-window-edge: 300")]
    [InlineData("drag-window-edge -1000 over 10", "  - drag-window-edge: { by: -1000, over: 10 }")]
    [InlineData("drag-window-edge -1000 over 10 then 200", "  - drag-window-edge: [{ by: -1000, over: 10 }, 200]")]
    [InlineData("drag-window-edge-sideways -50", "  - drag-window-edge: { moves: [-50], sideways: true }")]
    public void ArgumentsBeyondThePrimary_AreAFlowMap_AndAStepTheTesterAlsoHasKeepsItsNamespace(string line, string step)
    {
        Assert.Equal(step, RoundTrip(line).Split('\n')[2]);
    }

    [Fact]
    public void SetText_IsABlock_ThatKeepsItsLinesAndHasNoLineBreakAtItsEnd()
    {
        var yaml = RoundTrip("set-text\n    a = placed(\"{uid:lamp}\")\n\n    function f()\n        b()\n    end\nend\n");

        Assert.Equal("use: [zls]\nsteps:\n  - set-text: |-\n      a = placed(\"${lamp}\")\n\n      function f()\n          b()\n      end\n", yaml);
    }

    [Theory]
    [InlineData("set-text\n  indented first\nnot\nend\n")]
    [InlineData("set-text\none\n\nend\n")]
    [InlineData("set-text\nend\n")]
    [InlineData("set-text\nonly # line\nend\n")]
    public void SetText_ThatABlockCantHold_IsQuoted(string text)
    {
        RoundTrip(text);
    }

    /// <summary>
    /// Converts the text, then checks that parsing the YAML gives the steps reading the old text gave.
    /// </summary>
    internal static string RoundTrip(string text)
    {
        var old = LineScenarioReader.Read("old.zlstest", new StringReader(text), ZlsSteps.Registry);
        var yaml = LineScenarioConverter.Write(old, ZlsSteps.Registry);
        var scenario = ScenarioParser.Parse("new.ztest.yaml", new StringReader(yaml), ZlsSteps.Registry);

        var expected = old.Entries.Select(entry => entry.Step).Where(step => step != null).ToList();
        var actual = scenario.Steps.Cast<StepNode>().ToList();
        Assert.Equal(expected.Select(step => step.Step.FullName), actual.Select(step => step.Step.FullName));
        for (var i = 0; i < expected.Count; i++)
            Assert.True(Same(expected[i].Arguments, actual[i].Arguments), $"Step {i + 1}, {expected[i].Step.FullName}, has other arguments in:\n{yaml}");

        return yaml;
    }

    private static bool Same(ArgumentValue expected, ArgumentValue actual)
    {
        return (expected, actual) switch
        {
            (ScalarArgument one, ScalarArgument other) => one.Text == other.Text,
            (ListArgument one, ListArgument other) => one.Items.Count == other.Items.Count && one.Items.Zip(other.Items).All(pair => Same(pair.First, pair.Second)),
            (ObjectArgument one, ObjectArgument other) => one.Values.Count == other.Values.Count
                && one.Values.All(pair => other.Values.TryGetValue(pair.Key, out var value) && Same(pair.Value, value)),
            _ => false,
        };
    }
}
