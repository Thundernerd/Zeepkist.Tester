using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class RunResultsTests
{
    [Fact]
    public void ANewRun_IsWrittenAtOnce_AsNotDone()
    {
        using var folder = new TemporaryFolder();
        var outFolder = Path.Combine(folder.Path, "out");

        _ = new RunResults("abc", outFolder, []);

        var written = Read(outFolder);
        Assert.Equal("abc", (string)written["RunId"]);
        Assert.False((bool)written["Done"]);
        Assert.False((bool)written["Passed"]);
    }

    [Fact]
    public void AStep_IsWrittenAsItsAdded_WithAllItsDetails()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);

        results.Add(new RunStep
        {
            Scenario = "smoke",
            Line = 3,
            Name = "expect-blocks 3",
            Passed = false,
            Detail = "wrong count",
            Expected = "3",
            Actual = "2",
            Seconds = 0.25,
        });

        var step = Assert.Single(Read(folder.Path)["Steps"]);
        Assert.Equal("smoke", (string)step["Scenario"]);
        Assert.Equal(3, (int)step["Line"]);
        Assert.Equal("expect-blocks 3", (string)step["Name"]);
        Assert.False((bool)step["Passed"]);
        Assert.Equal("wrong count", (string)step["Detail"]);
        Assert.Equal("3", (string)step["Expected"]);
        Assert.Equal("2", (string)step["Actual"]);
        Assert.Equal(0.25, (double)step["Seconds"]);
    }

    [Fact]
    public void AStepInARunFile_NamesBothFilesAndLines_AndItsPass()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);
        var run = new RunStep { Scenario = "smoke", Name = "wait", Passed = true, File = "shared.ztest.yaml", Line = 3, RanFrom = "smoke.ztest.yaml:7", Passes = "h=64" };

        results.Add(run);

        var step = Assert.Single(Read(folder.Path)["Steps"]);
        Assert.Equal("shared.ztest.yaml", (string)step["File"]);
        Assert.Equal(3, (int)step["Line"]);
        Assert.Equal("smoke.ztest.yaml:7", (string)step["RanFrom"]);
        Assert.Equal("h=64", (string)step["Passes"]);
        Assert.Equal("smoke shared.ztest.yaml:3 wait [h=64] (run from smoke.ztest.yaml:7) PASS", run.ToString());
    }

    [Fact]
    public void Finishing_WithEveryStepPassed_Passes()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);
        results.Add(RunStep.Pass("one"));

        results.Finish();

        var written = Read(folder.Path);
        Assert.True((bool)written["Done"]);
        Assert.True((bool)written["Passed"]);
    }

    [Fact]
    public void Finishing_WithAFailedStep_Fails()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);
        results.Add(RunStep.Pass("one"));
        results.Add(RunStep.Fail("two", "why"));

        results.Finish();

        var written = Read(folder.Path);
        Assert.True((bool)written["Done"]);
        Assert.False((bool)written["Passed"]);
    }

    [Fact]
    public void Finishing_WithoutAnyStep_Fails()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);

        results.Finish();

        Assert.False((bool)Read(folder.Path)["Passed"]);
    }

    [Fact]
    public void Screenshots_AreListed_AndABlankOneWithItsColor()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);

        results.AddScreenshot("editor");
        results.AddScreenshot("dark");
        results.MarkBlank("dark", "#00000000");
        results.Finish();

        var written = Read(folder.Path);
        Assert.Equal(["editor.png", "dark.png"], written["Screenshots"].Select(name => (string)name));
        Assert.Equal("#00000000", (string)written["BlankScreenshots"]["dark.png"]);
    }

    [Fact]
    public void AScreenshotsPath_DoesntListIt_UntilItsSaved()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);

        var path = results.ScreenshotPath("editor");
        results.Finish();

        Assert.Equal(Path.Combine(folder.Path, "editor.png"), path);
        Assert.Empty(Read(folder.Path)["Screenshots"]);
    }

    [Fact]
    public void TheDisplayAndExceptions_AreWrittenWithTheNextStep()
    {
        using var folder = new TemporaryFolder();
        var exceptions = new ExceptionLog();
        var results = new RunResults("abc", folder.Path, exceptions.Logged);

        results.Display = "1920×1080 px, Windowed";
        exceptions.StartScenario("smoke");
        exceptions.Record("Exception: a", "Foo.Bar ()\n");
        results.Add(RunStep.Pass("one"));

        var written = Read(folder.Path);
        Assert.Equal("1920×1080 px, Windowed", (string)written["Display"]);
        var exception = Assert.Single(written["Exceptions"]);
        Assert.Equal("smoke", (string)exception["Scenario"]);
        Assert.Equal("Exception: a", (string)exception["Message"]);
        Assert.Equal("Foo.Bar ()", (string)exception["Frame"]);
        Assert.Equal(1, (int)exception["Count"]);
    }

    [Fact]
    public void Writing_LeavesNoTemporaryFile()
    {
        using var folder = new TemporaryFolder();
        var results = new RunResults("abc", folder.Path, []);

        results.Add(RunStep.Pass("one"));

        Assert.Equal(["results.json"], Directory.GetFiles(folder.Path).Select(Path.GetFileName));
    }

    private static JObject Read(string folder)
    {
        return JObject.Parse(File.ReadAllText(Path.Combine(folder, "results.json")));
    }
}
