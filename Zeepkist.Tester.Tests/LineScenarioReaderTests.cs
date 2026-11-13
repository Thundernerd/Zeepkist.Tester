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
