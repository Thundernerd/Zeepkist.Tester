using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioCheckTests
{
    private static readonly string _folder = Path.Combine("Scenarios", "Check");

    private const string NotDefined = "isn't defined by an earlier step.";

    public static TheoryData<string, string[]> Problems => new TheoryData<string, string[]>
    {
        { "name-before-defined", ["2:11: error: The name 'lamp' " + NotDefined] },
        { "ref-before-defined", ["2:20: error: The name 'lamp' " + NotDefined] },
        { "same-step-uses-what-it-defines", ["2:35: error: The name 'lamp' " + NotDefined] },
        { "ref-of-another-kind", ["3:20: error: The name 'main' is a window, not a block."] },
        { "ref-of-a-for-variable", ["4:24: error: The name 'b' is a text, not a block."] },
        { "kind-with-no-substitute", ["3:18: error: ${main} can't be written: a window has no Substitute member."] },
        { "name-defined-twice", ["3:23: error: The name 'lamp' is already defined, as a block."] },
        { "for-variable-after-body", ["5:11: error: The name 'h' " + NotDefined] },
        { "name-after-body", ["5:20: error: The name 'lamp' " + NotDefined, "6:18: error: The name 'lamp' " + NotDefined] },
        { "for-variable-already-defined", ["3:5: error: The name 'h' is already defined, as a block."] },
        { "for-inside-its-own-variable", ["4:9: error: The name 'h' is already defined, as a text."] },
        { "body-defines-an-outer-name", ["5:27: error: The name 'lamp' is already defined, as a block."] },
        { "ref-through-a-for-value", ["5:24: error: The name 'gate' " + NotDefined] },
        { "name-through-a-for-value", ["5:24: error: The name 'one' " + NotDefined] },
        { "name-only-known-when-it-runs", [] },
        { "run-missing", ["2:5: error: run: can't find 'shared/nope.ztest.yaml' from this file."] },
        { "with-uses-an-unknown-name", ["3:19: error: The name 'nope' " + NotDefined] },
        { "run-itself", ["2:5: error: run: 'run-itself.ztest.yaml' is already running here, so it would never end."] },
        { "run-names-end-with-it", ["4:18: error: The name 'lamp' " + NotDefined, "5:18: error: The name 'block' " + NotDefined] },
        { "needs-a-state", ["2:5: warning: compile needs zls.window-open: Add open-window first."] },
        { "start-state", ["3:5: warning: open-window needs editor: Start with start: editor, or add back-to-editor."] },
        { "start-editor", [] },
        { "enters-then-needs", [] },
        { "leaving-leaves-what-is-within", ["6:5: warning: compile needs zls.script-selected: Add new-script first."] },
        {
            "leaving-the-outer-state",
            [
                "5:5: warning: compile needs editor: Start with start: editor, or add back-to-editor.",
                "7:5: warning: compile needs zls.window-open: Add open-window first.",
            ]
        },
        { "needs-any", ["2:5: warning: needs-either needs zls.window-open or test-mode: Add open-window first. Add test-mode first."] },
        { "through-a-run", ["5:5: warning: new-script needs zls.window-open: Add open-window first."] },
        { "repeat-breaks-itself", ["5:9: warning: new-script needs zls.window-open: Add open-window first."] },
        { "repeat-once-does-not", [] },
        { "for-breaks-itself", ["5:9: warning: new-script needs zls.window-open: Add open-window first."] },
        { "for-with-one-value-does-not", [] },
        { "body-state-goes-on-after-it", [] },
        { "clean", [] },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void AScenario_ReportsWhatsWrong_WithItsFileLineAndColumn(string fixture, string[] problems)
    {
        var file = Fixture(fixture);

        var lines = ScenarioCheck.Run([file], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(problems.Select(problem => $"{file}:{problem}"), lines);
    }

    [Fact]
    public void AProblemInARunFile_IsReportedThere_NamingWhatRanIt()
    {
        var file = Fixture("run-sees-only-its-with");
        var shared = Path.Combine(_folder, "shared", "needs-lamp.ztest.yaml");

        var problem = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal($"{shared}:2:18: error: The name 'lamp' {NotDefined} (run from {file}:3:5)", problem.ToString());
    }

    [Fact]
    public void APreconditionInARunFile_FollowsTheStateOfWhatRanIt()
    {
        var file = Fixture("into-a-run");
        var shared = Path.Combine(_folder, "shared", "compiles.ztest.yaml");

        var problem = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal($"{shared}:2:5: warning: compile needs zls.window-open: Add open-window first. (run from {file}:2:5)", problem.ToString());
    }

    [Fact]
    public void ABrokenRunFile_IsReportedOnce_AtItsOwnLine()
    {
        var shared = Path.Combine(_folder, "shared", "broken.ztest.yaml");

        var problem = Assert.Single(ScenarioCheck.Run([Fixture("run-of-a-broken-file")], CheckSteps.Registry));

        Assert.Equal($"{shared}:2:5: error: There's no step 'nope'.", problem.ToString());
    }

    [Fact]
    public void ACycleBetweenFiles_IsAnError_EvenWhenBothFilesAreGiven()
    {
        var a = Fixture("run-cycle-a");
        var b = Fixture("run-cycle-b");

        var problem = Assert.Single(ScenarioCheck.Run([a, b], CheckSteps.Registry));

        Assert.Equal($"{b}:2:5: error: run: 'run-cycle-a.ztest.yaml' is already running here, so it would never end. (run from {a}:2:5)", problem.ToString());
    }

    [Fact]
    public void AFileAnotherGivenFileRuns_IsOnlyCheckedThroughIt()
    {
        var problems = ScenarioCheck.Run([Fixture("clean"), Path.Combine(_folder, "shared", "uses-what-it-is-given.ztest.yaml")], CheckSteps.Registry);

        Assert.Empty(problems);
    }

    [Fact]
    public void AFileThatCantBeRead_IsAnError()
    {
        var file = Fixture("nope");

        var problem = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.True(problem.IsError);
        Assert.StartsWith($"{file}:1:1: error: Can't read the file:", problem.ToString());
    }

    [Theory]
    [InlineData("syntax-error", 3, 5, "While parsing a block mapping, did not find expected key.")]
    [InlineData("unknown-step", 3, 5, "There's no step 'nope'.")]
    [InlineData("unknown-argument", 2, 36, "place-block has no argument 'colour'. It takes: function, as, how-many, offset.")]
    [InlineData("missing-required-argument", 2, 18, "place-block needs the argument 'function'.")]
    public void WhatTheParserFinds_IsReportedTheSameWay(string fixture, int line, int column, string problem)
    {
        var file = Path.Combine("Scenarios", "Errors", $"{fixture}.ztest.yaml");

        var reported = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal($"{file}:{line}:{column}: error: {problem}", reported.ToString());
    }

    [Theory]
    [InlineData("steps:\n  - wait: soon\n", "2:11: error: 'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    [InlineData("steps:\n  - place-block: { function: lamp, how-many: 9 }\n", "2:46: error: 'how-many' of place-block has to be between 1 and 8, not '9'.")]
    [InlineData("steps:\n  - place-block: LAMP\n", "2:18: error: 'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.")]
    [InlineData("steps:\n  - for: { h: [soon] }\n    steps:\n      - wait: ${h}\n", "4:15: error: 'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    public void AValueThatDoesntFit_IsReportedWithoutARun(string yaml, string problem)
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "value.ztest.yaml");
        File.WriteAllText(file, yaml);

        var reported = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal($"{file}:{problem}", reported.ToString());
    }

    private static string Fixture(string name)
    {
        return Path.Combine(_folder, $"{name}.ztest.yaml");
    }
}
