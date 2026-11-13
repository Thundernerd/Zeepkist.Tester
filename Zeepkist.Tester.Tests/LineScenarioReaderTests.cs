using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Tests.Zls;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class LineScenarioReaderTests
{
    [Fact]
    public void ALine_IsItsStep_WithItsPrimaryArgumentAsTheRestOfTheLine()
    {
        var step = Assert.Single(Steps("expect-text Placed  3 blocks\n"));

        Assert.Equal("zls.expect-text", step.Step.FullName);
        Assert.Equal("Placed  3 blocks", Text(step.Arguments, "text"));
        Assert.Equal(1, step.Location.Line);
    }

    [Theory]
    [InlineData("place-block lamp", "lamp", null)]
    [InlineData("place-block lamp as my-lamp", "lamp", "my-lamp")]
    public void ALineSyntax_ReadsEachArgument_AndCanLeaveOutWhatsInBrackets(string line, string function, string name)
    {
        var step = Assert.Single(Steps(line));

        Assert.Equal("zls.place-block", step.Step.FullName);
        Assert.Equal(function, Text(step.Arguments, "function"));
        Assert.Equal(name, step.Arguments.Values.TryGetValue("as", out var value) ? ((ScalarArgument)value).Text : null);
    }

    [Fact]
    public void ARestOfWords_TakesAsManyAsLeaveTheRestOfThePatternItsOwn()
    {
        var step = Assert.Single(Steps("name-group-block Lights  logic gate as g"));

        Assert.Equal("Lights  logic", Text(step.Arguments, "script"));
        Assert.Equal("gate", Text(step.Arguments, "function"));
        Assert.Equal("g", Text(step.Arguments, "as"));
    }

    [Fact]
    public void AListArgument_TakesEachWordAsAnItem()
    {
        var step = Assert.Single(Steps("expect-reference-headings Inputs Libraries:closed"));

        var headings = Assert.IsType<ListArgument>(step.Arguments.Values["headings"]);
        Assert.Equal(["Inputs", "Libraries:closed"], headings.Items.Cast<ScalarArgument>().Select(item => item.Text));
    }

    [Fact]
    public void AStepOnlyTheTesterHas_IsTheTesters()
    {
        var steps = Steps("wait 0.5\nundo\n");

        Assert.Equal(["wait", "undo"], steps.Select(step => step.Step.FullName));
        Assert.Equal("0.5", Text(steps[0].Arguments, "seconds"));
        Assert.Empty(steps[1].Arguments.Values);
    }

    [Fact]
    public void CommentsAndBlankLines_AreKeptInPlace()
    {
        var entries = Read("# Opens it\nopen-window\n\n  # then closes it\nclose-window\n").Entries;

        Assert.Equal([" Opens it", null, null, " then closes it", null], entries.Select(entry => entry.Comment));
        Assert.Equal([false, true, false, false, true], entries.Select(entry => entry.Step != null));
    }

    [Fact]
    public void AUidPlaceholder_BecomesASubstitution_AndALiteralDollarBraceIsEscaped()
    {
        var step = Assert.Single(Steps("expect-text placed(\"{uid:lamp}\") costs ${5}"));

        Assert.Equal("placed(\"${lamp}\") costs $${5}", Text(step.Arguments, "text"));
    }

    [Theory]
    [InlineData("  nope", 1, 3, "There's no step 'nope'.")]
    [InlineData("open-window now", 1, 1, "open-window takes nothing after it, not 'now'.")]
    [InlineData("expect-text", 1, 1, "expect-text needs the argument 'text'.")]
    [InlineData("place-block lamp named x", 1, 1, "place-block takes <function> [as <as>], not 'lamp named x'.")]
    [InlineData("set-window-height tall", 1, 1, "'value' of zls.set-window-height takes a whole number, not 'tall'.")]
    public void ALineThatDoesntFit_FailsWithWhereItIs(string line, int lineNumber, int column, string problem)
    {
        var exception = Assert.Throws<ScenarioException>(() => Read(line));

        Assert.Equal(lineNumber, exception.Location.Line);
        Assert.Equal(column, exception.Location.Column);
        Assert.Equal(problem, exception.Problem);
    }

    [Fact]
    public void SetText_TakesTheLinesUpToEnd_WithoutTheirSharedIndent()
    {
        var steps = Steps("set-text\n    a = placed(\"{uid:lamp}\")   \n\n      function f()\n      end\nend\nundo\n");

        Assert.Equal(["zls.set-text", "undo"], steps.Select(step => step.Step.FullName));
        Assert.Equal("a = placed(\"${lamp}\")\n\n  function f()\n  end", Text(steps[0].Arguments, "value"));
        Assert.Equal(1, steps[0].Location.Line);
        Assert.Equal(7, steps[1].Location.Line);
    }

    [Theory]
    [InlineData("drag-window-edge 300", false, "300", null)]
    [InlineData("drag-window-edge -1000 over 10 then 200 over 2", false, "-1000", "10", "200", "2")]
    [InlineData("drag-window-edge-sideways -50", true, "-50", null)]
    public void DragWindowEdge_IsAListOfMoves_AndSidewaysIsAnArgument(string line, bool sideways, params string[] moves)
    {
        var step = Assert.Single(Steps(line));

        Assert.Equal("zls.drag-window-edge", step.Step.FullName);
        var written = Assert.IsType<ListArgument>(step.Arguments.Values["moves"]).Items.Cast<ObjectArgument>().ToList();
        Assert.Equal(moves.Length / 2, written.Count);
        for (var i = 0; i < written.Count; i++)
        {
            Assert.Equal(moves[i * 2], Text(written[i], "by"));
            Assert.Equal(moves[(i * 2) + 1], written[i].Values.TryGetValue("over", out var over) ? ((ScalarArgument)over).Text : null);
        }

        Assert.Equal(sideways, step.Arguments.Values.ContainsKey("sideways"));
        if (sideways)
            Assert.Equal("true", Text(step.Arguments, "sideways"));
    }

    [Fact]
    public void ExpectError_SplitsItsPositionIntoLineAndColumn()
    {
        var step = Assert.Single(Steps("expect-error 2:15 'nope' isn't defined"));

        Assert.Equal("2", Text(step.Arguments, "line"));
        Assert.Equal("15", Text(step.Arguments, "column"));
        Assert.Equal("'nope' isn't defined", Text(step.Arguments, "message"));
    }

    [Theory]
    [InlineData("set-text now\nend", 1, "set-text takes nothing after it: the text goes on the lines below it.")]
    [InlineData("undo\nset-text\n  a", 2, "set-text has no end.")]
    [InlineData("undo\nend", 2, "end without set-text.")]
    [InlineData("drag-window-edge 10 then", 1, "drag-window-edge takes the pixels to drag, and optionally 'over <frames>', with 'then' before each move after the first, not '10 then'.")]
    [InlineData("expect-error 2 nope", 1, "expect-error takes a line and column, such as 2:5, then the text to look for, not '2 nope'.")]
    public void AnOldFormThatDoesntFit_Fails(string text, int line, string problem)
    {
        var exception = Assert.Throws<ScenarioException>(() => Read(text));

        Assert.Equal(line, exception.Location.Line);
        Assert.Equal(problem, exception.Problem);
    }

    private static List<StepNode> Steps(string text)
    {
        return Read(text).Entries.Select(entry => entry.Step).Where(step => step != null).ToList();
    }

    private static LineScenario Read(string text)
    {
        return LineScenarioReader.Read("old.zlstest", new StringReader(text), ZlsSteps.Registry);
    }

    private static string Text(ObjectArgument arguments, string name)
    {
        return Assert.IsType<ScalarArgument>(arguments.Values[name]).Text;
    }
}
