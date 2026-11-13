using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNRD.Zeepkist.Tester.Cli;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioCheckTests
{
    private const string NotDefined = "isn't defined by an earlier step.";

    private static readonly string _folder = Path.Combine("Scenarios", "Check");

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
        { "start-level-missing", ["2:3: error: start: editor can't find 'shared/nope.zeeplevel' from this file."] },
        { "start-level-found", [] },
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
        { "default-name-then-ref", [] },
        { "default-ref-before-its-name", ["2:5: error: The name 'auto' " + NotDefined] },
        { "default-name-twice", ["3:5: error: The name 'auto' is already defined, as a thing."] },
        { "ref-of-a-base-kind", [] },
        { "ref-of-a-derived-kind", ["3:18: error: The name 'plain' is a thing, not a special-thing."] },
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
    public void AFileWhoseCallerIsBroken_IsNotBlamedForWhatItWouldHaveBeenGiven()
    {
        using var folder = new TemporaryFolder();
        var caller = Write(folder, "a.ztest.yaml", "steps:\n  - run: b.ztest.yaml\n    with: { x: 1 }\n  - nope\n");
        var called = Write(folder, "b.ztest.yaml", "steps:\n  - wait: ${x}\n");

        var problem = Assert.Single(ScenarioCheck.Run([caller, called], CheckSteps.Registry));

        Assert.Equal($"{caller}:4:5: error: There's no step 'nope'.", problem.ToString());
    }

    [Fact]
    public void ABrokenFile_DoesntHideTheProblemsOfAFileItDoesntMention()
    {
        using var folder = new TemporaryFolder();
        var broken = Write(folder, "a.ztest.yaml", "steps:\n  - nope\n");
        var other = Write(folder, "b.ztest.yaml", "steps:\n  - wait: ${x}\n");

        var problems = ScenarioCheck.Run([broken, other], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal([$"{broken}:2:5: error: There's no step 'nope'.", $"{other}:2:11: error: The name 'x' {NotDefined}"], problems);
    }

    [Fact]
    public void AFileInACycle_IsNotBlamedForWhatItsCallerGivesIt()
    {
        using var folder = new TemporaryFolder();
        var a = Write(folder, "a.ztest.yaml", "steps:\n  - run: b.ztest.yaml\n    with: { x: 1 }\n");
        var b = Write(folder, "b.ztest.yaml", "steps:\n  - wait: ${x}\n  - run: a.ztest.yaml\n");

        var problem = Assert.Single(ScenarioCheck.Run([b, a], CheckSteps.Registry));

        Assert.Contains("is already running here", problem.ToString());
    }

    [Fact]
    public void ANameFromALiteralWith_IsKnown_SoATypoNextToItIsStillFound()
    {
        using var folder = new TemporaryFolder();
        var caller = Write(folder, "a.ztest.yaml", "steps:\n  - run: b.ztest.yaml\n    with: { n: lamp }\n");
        var called = Write(folder, "b.ztest.yaml", "steps:\n  - named: { defines: \"${n}\", uses: typo }\n  - named: { uses: lamp }\n  - named: { uses: other }\n");

        var problems = ScenarioCheck.Run([caller], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(
            [
                $"{called}:2:37: error: The name 'typo' {NotDefined} (run from {caller}:2:5)",
                $"{called}:4:20: error: The name 'other' {NotDefined} (run from {caller}:2:5)",
            ],
            problems);
    }

    [Fact]
    public void ANameThatIsntKnown_MayMakeLaterUsesRight_ButNotThoseInItsOwnStep()
    {
        using var folder = new TemporaryFolder();
        var caller = Write(folder, "a.ztest.yaml", "steps:\n  - named: { defines: lamp }\n  - run: b.ztest.yaml\n    with: { n: \"${lamp}\" }\n");
        var called = Write(folder, "b.ztest.yaml", "steps:\n  - named: { defines: \"${n}\", uses: typo }\n  - named: { uses: anything }\n");

        var problem = Assert.Single(ScenarioCheck.Run([caller], CheckSteps.Registry));

        Assert.Equal($"{called}:2:37: error: The name 'typo' {NotDefined} (run from {caller}:3:5)", problem.ToString());
    }

    [Fact(Timeout = 60000)]
    public async Task ManyNestedRepeats_StopBeingFollowed_AndSaySo()
    {
        using var folder = new TemporaryFolder();
        var yaml = new StringBuilder("steps:\n");
        var indent = "  ";
        for (var i = 0; i < 40; i++)
        {
            yaml.Append($"{indent}- repeat: 2\n{indent}  steps:\n");
            indent += "    ";
        }

        yaml.Append($"{indent}- undo\n");
        var file = Write(folder, "a.ztest.yaml", yaml.ToString());

        var problems = await Task.Run(() => ScenarioCheck.Run([file], CheckSteps.Registry));

        var problem = Assert.Single(problems);
        Assert.Equal(CheckSeverity.Warning, problem.Severity);
        Assert.Contains("stops following it here", problem.Message);
    }

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"a\\0b\"", "a\0b")]
    public void ARunOfAPathThatCantExist_IsAMissingFile(string written, string text)
    {
        using var folder = new TemporaryFolder();
        var file = Write(folder, "a.ztest.yaml", $"steps:\n  - run: {written}\n");

        var problem = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal($"{file}:2:5: error: run: can't find '{text}' from this file.", problem.ToString());
    }

    [Fact]
    public void AFileThatCantBeRead_IsAnError()
    {
        var file = Fixture("nope");

        var problem = Assert.Single(ScenarioCheck.Run([file], CheckSteps.Registry));

        Assert.Equal(CheckSeverity.Error, problem.Severity);
        Assert.StartsWith($"{file}:1:1: error: Can't read the file:", problem.ToString());
    }

    [Theory]
    [InlineData("syntax-error", 3, 5, "While parsing a block mapping, did not find expected key.")]
    [InlineData("unknown-step", 3, 5, "There's no step 'nope'.")]
    [InlineData("unknown-argument", 2, 36, "place-block has no argument 'colour'. It takes: function, as, how-many, offset.")]
    [InlineData("missing-required-argument", 2, 18, "place-block needs the argument 'function'.")]
    [InlineData("nested-too-deep", 1, 107, "Lists and maps are nested more than 100 deep here.")]
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

    [Fact]
    public void BadValuesInSeveralSteps_AreEachReported_InOneCheck()
    {
        using var folder = new TemporaryFolder();
        var file = Write(folder, "a.ztest.yaml", "steps:\n  - wait: soon\n  - place-block: LAMP\n  - wait: later\n");

        var problems = ScenarioCheck.Run([file], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(
            [
                $"{file}:2:11: error: 'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.",
                $"{file}:3:18: error: 'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.",
                $"{file}:4:11: error: 'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'later'.",
            ],
            problems);
    }

    [Fact]
    public void BadValuesInOneStep_AreEachReported()
    {
        using var folder = new TemporaryFolder();
        var file = Write(folder, "a.ztest.yaml", "steps:\n  - place-block: { function: LAMP, how-many: 9 }\n");

        var problems = ScenarioCheck.Run([file], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(
            [
                $"{file}:2:30: error: 'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.",
                $"{file}:2:46: error: 'how-many' of place-block has to be between 1 and 8, not '9'.",
            ],
            problems);
    }

    [Fact]
    public void AnUnknownArgument_DoesntHideABadValueNextToIt()
    {
        using var folder = new TemporaryFolder();
        var file = Write(folder, "a.ztest.yaml", "steps:\n  - place-block: { colour: red, function: LAMP }\n");

        var problems = ScenarioCheck.Run([file], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(
            [
                $"{file}:2:20: error: place-block has no argument 'colour'. It takes: function, as, how-many, offset.",
                $"{file}:2:43: error: 'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.",
            ],
            problems);
    }

    [Fact]
    public void AProblemInTheHeader_DoesntHideTheSteps()
    {
        using var folder = new TemporaryFolder();
        var file = Write(folder, "a.ztest.yaml", "input: keyboard\nstart: nowhere\nsteps:\n  - wait: soon\n");

        var problems = ScenarioCheck.Run([file], CheckSteps.Registry).Select(problem => problem.ToString());

        Assert.Equal(
            [
                $"{file}:1:8: error: input: takes in-process or os, not 'keyboard'.",
                $"{file}:2:8: error: start: takes main-menu, editor or free-play, not 'nowhere'.",
                $"{file}:4:11: error: 'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.",
            ],
            problems);
    }

    private static string Write(TemporaryFolder folder, string name, string yaml)
    {
        var file = Path.Combine(folder.Path, name);
        File.WriteAllText(file, yaml);
        return file;
    }

    private static string Fixture(string name)
    {
        return Path.Combine(_folder, $"{name}.ztest.yaml");
    }
}
