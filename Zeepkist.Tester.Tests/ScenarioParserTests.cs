using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioParserTests
{
    public static TheoryData<string, int, int, string> Errors => new TheoryData<string, int, int, string>
    {
        { "syntax-error", 3, 5, "While parsing a block mapping, did not find expected key." },
        { "duplicate-key", 3, 1, "Duplicate key steps" },
        { "empty-file", 1, 1, "The file has no scenario in it." },
        { "two-documents", 4, 1, "A scenario is one YAML document, but this is another." },
        { "root-is-a-list", 1, 1, "A scenario is a map with a steps: key, not a list." },
        { "key-is-a-list", 1, 3, "A key has to be a single value, not a list." },
        { "unknown-header-key", 1, 1, "A scenario has no key 'descriptoin'. It takes: description, use, start, input, steps." },
        { "no-steps", 1, 1, "A scenario needs steps:." },
        { "empty-steps", 1, 8, "steps: has no steps in it." },
        { "steps-is-a-map", 2, 3, "steps: takes a list of steps, not a map." },
        { "description-is-a-list", 1, 14, "description takes a single value, not a list." },
        { "use-is-a-scalar", 1, 6, "use: takes a list of namespaces, such as [zls]." },
        { "use-unknown-namespace", 3, 5, "use: lists 'nope', but no step has that namespace." },
        { "start-unknown", 1, 8, "start: takes main-menu, editor or free-play, not 'garage'." },
        { "start-unknown-argument", 3, 5, "start: editor has no argument 'file'. It takes: level." },
        { "start-missing-argument", 1, 8, "start: free-play needs the argument 'uid'." },
        { "start-two-keys", 3, 3, "start: takes one state, but this is another." },
        { "start-argument-with-a-name", 3, 12, "'level' of start: editor has to be a literal, not '${nope}'." },
        { "input-unknown", 1, 8, "input: takes in-process or os, not 'telepathy'." },
        { "unknown-step", 3, 5, "There's no step 'nope'." },
        { "unknown-prefixed-step", 2, 5, "There's no step 'zls.nope'." },
        { "mod-step-without-use", 2, 5, "There's no step 'compile'. Write zls.compile or other.compile, or list the namespace under use:." },
        { "clash-with-core", 3, 5, "'wait' could be the tester's own step or clashing.wait. Write clashing.wait for the mod's, or take clashing out of use: for the tester's." },
        { "clash-between-mods", 3, 5, "'compile' could be zls.compile or other.compile. Write the one you mean with its prefix." },
        { "step-is-a-list", 2, 5, "A step is a name or a map with its name as the key, not a list." },
        { "step-two-keys", 3, 5, "A step has one key, but this is another. Is it indented too far, or missing its - ?" },
        { "unknown-argument", 2, 36, "place-block has no argument 'colour'. It takes: function, as, how-many, offset." },
        { "unknown-nested-argument", 2, 37, "'moves' of zls.drag-window-edge has no argument 'under'. It takes: by, over." },
        { "no-primary-argument", 2, 12, "named can't take a plain value. Write a map of its arguments: defines, maybe-defines, uses, uses-each." },
        { "no-arguments", 2, 11, "undo takes no arguments, so write it as a bare string." },
        { "missing-required-argument", 2, 18, "place-block needs the argument 'function'." },
        { "missing-required-bare", 2, 5, "wait needs the argument 'seconds'." },
        { "empty-value", 2, 10, "'seconds' of wait has no value." },
        { "list-for-a-single-value", 2, 18, "'function' of place-block takes a single value, not a list." },
        { "map-for-a-single-value", 2, 25, "'keys' of every-type takes a single value, not a map." },
        { "single-value-for-a-list", 2, 26, "'times' of every-type takes a list." },
        { "substitution-for-a-list", 3, 14, "'times' of every-type takes a list. A ${name} only stands in for a single value, never a whole list or map." },
        { "substitution-for-a-map", 3, 15, "'nested' of written can't take a plain value. Write a map of its arguments: line, details. A ${name} only stands in for a single value, never a whole list or map." },
        { "for-two-variables", 2, 20, "A for has one variable, but this is another. Nest two fors for every combination." },
        { "for-is-a-scalar", 2, 10, "for: takes a variable and its values, such as { h: [64, 120] }." },
        { "for-values-are-a-scalar", 2, 15, "for: takes a list of values for 'h'." },
        { "for-no-values", 2, 15, "for: has no values for 'h'." },
        { "for-value-is-a-list", 2, 20, "A value of for: takes a single value, not a list." },
        { "for-value-is-an-expression", 3, 15, "A value of for: has to be a literal, not '${x}'." },
        { "for-bad-variable", 2, 12, "'a b' can't be a variable's name: it has to match ^[A-Za-z0-9_-]+$." },
        { "for-no-steps", 2, 5, "for: needs steps:." },
        { "for-unknown-key", 3, 5, "for: has no key 'with'. It takes: for, steps." },
        { "for-empty-body", 3, 12, "steps: has no steps in it." },
        { "repeat-not-a-number", 2, 13, "repeat: takes a whole number of at least 1, not 'often'." },
        { "repeat-zero", 2, 13, "repeat: takes a whole number of at least 1, not '0'." },
        { "repeat-no-steps", 2, 5, "repeat: needs steps:." },
        { "repeat-unknown-key", 3, 5, "repeat: has no key 'times'. It takes: repeat, steps." },
        { "error-in-a-body", 6, 13, "There's no step 'nope'." },
        { "run-is-a-map", 2, 10, "run: takes a single value, not a map." },
        { "run-is-an-expression", 2, 10, "run: has to be a literal, not '${file}'." },
        { "run-unknown-key", 3, 5, "run: has no key 'steps'. It takes: run, with." },
        { "with-is-a-scalar", 3, 11, "with: takes a map of names and values." },
        { "with-value-is-a-list", 3, 16, "'b' of with: takes a single value, not a list." },
        { "body-contains-itself", 1, 8, "steps: is used a second time through an alias. Use run: to share steps." },
        { "body-used-twice", 3, 12, "steps: is used a second time through an alias. Use run: to share steps." },
        { "tagged-duplicate-key", 3, 1, "A scenario has the key 'steps' twice." },
        { "tagged-duplicate-with", 5, 7, "with: has the name 'a' twice." },
        { "step-is-an-empty-map", 2, 5, "A step is a name or a map with its name as the key, but this map is empty." },
        { "no-arguments-map", 2, 11, "undo takes no arguments, so write it as a bare string." },
        { "unquoted-substitution-in-flow", 2, 31, "While parsing a flow mapping, did not find expected ',' or '}'. Inside { } or [ ], quote a value with ${ or a colon in it." },
        { "with-bad-name", 3, 13, "'b c' can't be a name: it has to match ^[A-Za-z0-9_-]+$." },
    };

    [Fact]
    public void TheHeader_IsReadAsWritten()
    {
        var scenario = ScenarioSteps.Parse("step-forms");

        Assert.Equal("Each way to write a step", scenario.Description);
        Assert.Equal(["zls"], scenario.Use);
        Assert.Same(StartState.Editor, scenario.Start.State);
        Assert.Equal("in-process", scenario.Input);
    }

    [Fact]
    public void ABareString_IsAStepWithNoArguments_AndKnowsWhereItIs()
    {
        var step = StepAt("step-forms", 0);

        Assert.Equal("undo", step.Step.FullName);
        Assert.Empty(step.Arguments.Values);
        Assert.Equal(7, step.Location.Line);
        Assert.Equal(5, step.Location.Column);
    }

    [Fact]
    public void AScalar_IsThePrimaryArgument_AsTheTextItWasWritten()
    {
        var step = StepAt("step-forms", 1);

        Assert.Equal("place-block", step.Step.FullName);
        Assert.Equal("on", Text(step.Arguments, "function"));
    }

    [Fact]
    public void AMap_IsTheStepsArguments()
    {
        var step = StepAt("step-forms", 3);

        Assert.Equal("lamp", Text(step.Arguments, "function"));
        Assert.Equal("my-lamp", Text(step.Arguments, "as"));
        Assert.Equal("2", Text(step.Arguments, "how-many"));
    }

    [Fact]
    public void APrimaryList_TakesOneScalarItem_ThroughTheItemsOwnPrimary()
    {
        var moves = Items(StepAt("step-forms", 4), "moves");

        var move = Assert.IsType<ObjectArgument>(Assert.Single(moves));
        Assert.Equal("300", Text(move, "by"));
    }

    [Fact]
    public void APrimaryList_TakesOneMapItem_WhenNoneOfItsKeysAreTheStepsOwn()
    {
        var moves = Items(StepAt("step-forms", 5), "moves");

        var move = Assert.IsType<ObjectArgument>(Assert.Single(moves));
        Assert.Equal("50", Text(move, "by"));
        Assert.Equal("3", Text(move, "over"));
    }

    [Fact]
    public void APrimaryList_TakesAList_OfEachItemForm_AndUseLetsItGoUnprefixed()
    {
        var step = StepAt("step-forms", 6);
        var moves = Items(step, "moves");

        Assert.Equal("zls.drag-window-edge", step.Step.FullName);
        Assert.Equal(2, moves.Count);
        Assert.Equal("10", Text((ObjectArgument)moves[0], "over"));
        Assert.Equal("200", Text((ObjectArgument)moves[1], "by"));
    }

    [Fact]
    public void AMap_CanGiveAPrimaryListInFull_NextToOtherArguments()
    {
        var step = StepAt("step-forms", 7);

        Assert.Equal("-50", Text((ObjectArgument)Items(step, "moves")[0], "by"));
        Assert.Equal("true", Text(step.Arguments, "sideways"));
    }

    [Fact]
    public void ABlockScalar_KeepsItsLines_WithTheIndentRemoved()
    {
        Assert.Equal("first\n  second\n", Text(StepAt("step-forms", 8).Arguments, "text"));
    }

    [Fact]
    public void APrefixedStep_NeedsNoUse_AndClashesWithNothing()
    {
        var steps = ScenarioSteps.Parse("namespaces").Steps.Cast<StepNode>().Select(step => step.Step.FullName);

        Assert.Equal(["zls.compile", "other.compile", "clashing.wait", "undo"], steps);
    }

    [Theory]
    [InlineData("main-menu", "main-menu", null, null)]
    [InlineData("editor", "editor", null, null)]
    [InlineData("{ editor: { level: a.zeeplevel } }", "editor", "level", "a.zeeplevel")]
    [InlineData("{ free-play: { uid: abc } }", "free-play", "uid", "abc")]
    public void TheStart_TakesEachForm(string written, string name, string argument, string value)
    {
        var start = ScenarioParser.Parse("start.ztest.yaml", new StringReader($"start: {written}\nsteps:\n  - undo\n"), ScenarioSteps.Descriptors).Start;

        Assert.Equal(name, start.State.Name);
        Assert.Equal(value, start.Value);
        if (argument == null)
            Assert.Empty(start.Arguments);
        else
            Assert.Equal(value, start.Arguments[argument]);
    }

    [Fact]
    public void AHeaderWithoutAStart_StartsOnANewLevelInTheEditor()
    {
        var start = ScenarioParser.Parse("start.ztest.yaml", new StringReader("steps:\n  - undo\n"), ScenarioSteps.Descriptors).Start;

        Assert.Same(StartState.Editor, start.State);
        Assert.Null(start.LevelPath("start.ztest.yaml"));
    }

    [Theory]
    [InlineData("{ editor: { level: a.zeeplevel } }", "a.zeeplevel")]
    [InlineData("{ editor: { level: levels/a.zeeplevel } }", "levels/a.zeeplevel")]
    [InlineData("editor", null)]
    [InlineData("{ free-play: { uid: levels/a.zeeplevel } }", null)]
    public void ALevelFile_IsFoundFromTheScenario(string written, string expected)
    {
        var scenario = Path.Combine("scenarios", "start.ztest.yaml");
        var start = ScenarioParser.Parse(scenario, new StringReader($"start: {written}\nsteps:\n  - undo\n"), ScenarioSteps.Descriptors).Start;

        Assert.Equal(expected == null ? null : Path.Combine("scenarios", expected.Replace('/', Path.DirectorySeparatorChar)), start.LevelPath(scenario));
    }

    [Fact]
    public void AFor_HasOneVariable_WithItsValuesAsWritten_AndABody()
    {
        var node = Assert.IsType<ForNode>(ScenarioSteps.Parse("control-nodes").Steps[0]);

        Assert.Equal("h", node.Variable);
        Assert.Equal(["64", "on", "240"], node.Values);
        Assert.Equal(2, node.Location.Line);
        Assert.Equal("${h}", Text(Assert.IsType<StepNode>(node.Steps[0]).Arguments, "string"));
        Assert.Equal("undo", Assert.IsType<StepNode>(Assert.IsType<ForNode>(node.Steps[1]).Steps[0]).Step.FullName);
    }

    [Fact]
    public void ARepeat_HasACount_AndABody()
    {
        var node = Assert.IsType<RepeatNode>(ScenarioSteps.Parse("control-nodes").Steps[1]);

        Assert.Equal(3, node.Count);
        Assert.Equal("undo", Assert.IsType<StepNode>(Assert.Single(node.Steps)).Step.FullName);
    }

    [Fact]
    public void ARun_HasAFile_AndNoWith()
    {
        var node = Assert.IsType<RunNode>(ScenarioSteps.Parse("control-nodes").Steps[2]);

        Assert.Equal("shared/open-lights.ztest.yaml", node.File);
        Assert.Empty(node.With);
    }

    [Fact]
    public void ARun_KeepsItsWithValues_AsTheTextTheyWereWritten()
    {
        var node = Assert.IsType<RunNode>(ScenarioSteps.Parse("control-nodes").Steps[3]);

        Assert.Equal("lamp", node.With["function"].Text);
        Assert.Equal("${lamp}", node.With["uid"].Text);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public void AFileThatCantBeRead_FailsWithItsFileLineAndColumn(string fixture, int line, int column, string problem)
    {
        var exception = Assert.Throws<ScenarioException>(() => ScenarioSteps.Parse(Path.Combine("Errors", fixture)));

        Assert.Equal($"{Path.Combine("Scenarios", "Errors", fixture)}.ztest.yaml:{line}:{column}: {problem}", exception.Message);
    }

    private static StepNode StepAt(string fixture, int index)
    {
        return Assert.IsType<StepNode>(ScenarioSteps.Parse(fixture).Steps[index]);
    }

    private static string Text(ObjectArgument arguments, string name)
    {
        return Assert.IsType<ScalarArgument>(arguments.Values[name]).Text;
    }

    private static IReadOnlyList<ArgumentValue> Items(StepNode step, string name)
    {
        return Assert.IsType<ListArgument>(step.Arguments.Values[name]).Items;
    }
}
