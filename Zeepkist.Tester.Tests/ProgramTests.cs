using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Tests.SampleMod;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public sealed class ProgramTests : IDisposable
{
    private static readonly string _modPath = typeof(Compile).Assembly.Location;

    private readonly TemporaryFolder _folder = new TemporaryFolder();
    private readonly StringWriter _output = new StringWriter();

    public void Dispose()
    {
        _folder.Dispose();
    }

    [Fact]
    public void NoCommand_PrintsUsageAndSucceeds()
    {
        Assert.Equal(0, Run());
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    [Fact]
    public void AnUnknownCommand_PrintsUsageAndFails()
    {
        Assert.Equal(1, Run("nope"));
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    [Fact]
    public void Check_ACleanFile_Succeeds()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - sample.compile: lamp\n");

        Assert.Equal(0, Run("check", file, "--plugin", _modPath));
        Assert.Contains("Checked 1 file: no errors, no warnings.", _output.ToString());
    }

    [Fact]
    public void Check_AnError_PrintsItsFileLineAndColumn_AndFails()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - sample.compile: lamp\n  - nope\n");

        Assert.Equal(1, Run("check", file, "--plugin", _modPath));
        Assert.Equal($"{file}:3:5: error: There's no step 'nope'.", Lines()[0]);
        Assert.Equal("Checked 1 file: 1 error, no warnings.", Lines()[1]);
    }

    [Fact]
    public void Check_WithoutThePlugin_DoesNotKnowItsSteps()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - sample.compile: lamp\n");

        Assert.Equal(1, Run("check", file));
        Assert.Contains("There's no step 'sample.compile'.", _output.ToString());
    }

    [Fact]
    public void Check_OnlyAWarning_PrintsItAndSucceeds()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - sample.swap-panel\n");

        Assert.Equal(0, Run("check", file, "--plugin", _modPath));
        Assert.Equal($"{file}:2:5: warning: sample.swap-panel needs core.core-state: Add core-step first.", Lines()[0]);
        Assert.Equal("Checked 1 file: no errors, 1 warning.", Lines()[1]);
    }

    [Fact]
    public void Check_AFolder_ChecksEachScenarioInIt_AndNotWhatTheyRun()
    {
        Write("a.ztest.yaml", "steps:\n  - run: shared/b.ztest.yaml\n    with: { x: 1 }\n");
        Write(Path.Combine("shared", "b.ztest.yaml"), "steps:\n  - sample.compile: ${x}\n");
        Write("notes.yaml", "not: a scenario\n");

        Assert.Equal(0, Run("check", _folder.Path, "--plugin", _modPath));
        Assert.Contains("Checked 2 files: no errors, no warnings.", _output.ToString());
    }

    [Fact]
    public void Check_NoFiles_UsesTheConfiguredFolderAndPlugin_FromAboveTheCurrentFolder()
    {
        Write("zeepkist-test.json", $$"""{ "scenarios": "tests", "testPlugin": {{System.Text.Json.JsonSerializer.Serialize(_modPath)}} }""");
        Write(Path.Combine("tests", "a.ztest.yaml"), "steps:\n  - sample.compile: lamp\n");
        var current = Directory.CreateDirectory(Path.Combine(_folder.Path, "deep", "down")).FullName;

        Assert.Equal(0, RunIn(current, "check"));
        Assert.Contains("Checked 1 file: no errors, no warnings.", _output.ToString());
    }

    [Fact]
    public void Check_AConfiguredProject_ReadsTheNewestBuildOfIt()
    {
        Write("zeepkist-test.json", """{ "scenarios": "tests", "testPlugin": "Mod/Mod.csproj" }""");
        Write(Path.Combine("Mod", "Mod.csproj"), "<Project />");
        var old = Write(Path.Combine("Mod", "bin", "Debug", "net472", "Mod.dll"), "not an assembly");
        File.SetLastWriteTimeUtc(old, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var built = Path.Combine(_folder.Path, "Mod", "bin", "Release", "net472", "Mod.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(built));
        File.Copy(_modPath, built);
        File.SetLastWriteTimeUtc(built, DateTime.UtcNow);
        Write(Path.Combine("tests", "a.ztest.yaml"), "steps:\n  - sample.compile: lamp\n");

        Assert.Equal(0, RunIn(_folder.Path, "check"));
    }

    [Fact]
    public void Check_AConfiguredProjectThatIsntBuilt_SaysToBuildIt()
    {
        Write("zeepkist-test.json", """{ "scenarios": "tests", "testPlugin": "Mod/Mod.csproj" }""");

        Assert.Equal(1, RunIn(_folder.Path, "check"));
        Assert.Contains("Build Mod/Mod.csproj first, or pass --plugin.", _output.ToString());
    }

    [Fact]
    public void Check_NoFilesAndNoConfig_Fails()
    {
        Assert.Equal(1, RunIn(_folder.Path, "check"));
        Assert.Contains("error: No files given, and no zeepkist-test.json with \"scenarios\" here or above.", _output.ToString());
    }

    [Fact]
    public void Check_ABrokenConfig_Fails()
    {
        Write("zeepkist-test.json", "{ nope");

        Assert.Equal(1, RunIn(_folder.Path, "check"));
        Assert.Contains("error: Can't read ", _output.ToString());
    }

    [Fact]
    public void Check_AFolderWithNoScenarios_Fails()
    {
        Assert.Equal(1, Run("check", _folder.Path));
        Assert.Contains($"error: No .ztest.yaml files in {_folder.Path}.", _output.ToString());
    }

    [Fact]
    public void Check_APluginThatCantBeRead_FailsWithoutAStackTrace()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - undo\n");

        Assert.Equal(1, Run("check", file, "--plugin", Path.Combine(_folder.Path, "nope.dll")));
        Assert.StartsWith("error: Can't read the plugins:", _output.ToString());
    }

    [Fact]
    public void Check_APluginThatIsAFolder_FailsWithoutAStackTrace()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - undo\n");

        Assert.Equal(1, Run("check", file, "--plugin", _folder.Path));
        Assert.StartsWith("error: Can't read the plugins:", _output.ToString());
    }

    [Fact]
    public void Check_AnEmptyPath_IsAnErrorAndNotAStackTrace()
    {
        Assert.Equal(1, Run("check", string.Empty));
        Assert.Contains(":1:1: error: Can't read the file:", _output.ToString());
    }

    [Theory]
    [InlineData("--plugin")]
    [InlineData("--nope")]
    public void Check_ABadOption_PrintsUsageAndFails(string option)
    {
        Assert.Equal(1, Run("check", option));
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    [Fact]
    public void CheckRegistry_ListsSmellsAsWarnings_AndSucceeds()
    {
        Assert.Equal(0, Run("check", "--registry", "--plugin", _modPath));
        Assert.Contains("warning: No step enters the state 'core.core-state', so the offline check never sees it hold.", Lines());
    }

    [Fact]
    public void Convert_WritesTheYamlNextToTheOldFile()
    {
        var file = Write("a.zlstest", "# Compiles\ncompile lamp\nwait 2\n");

        Assert.Equal(0, Run("convert", file, "--plugin", _modPath));
        var converted = Path.Combine(_folder.Path, "a.ztest.yaml");
        Assert.Equal("use: [sample]\nsteps:\n  # Compiles\n  - compile: lamp\n  - wait: 2\n", File.ReadAllText(converted));
        Assert.Equal([$"Wrote {converted}", "Converted 1 file: no errors."], Lines());
    }

    [Fact]
    public void Convert_ALineThatCantBeRead_PrintsWhereAndWritesNothing()
    {
        var file = Write("a.zlstest", "compile lamp\nnope\n");

        Assert.Equal(1, Run("convert", file, "--plugin", _modPath));
        Assert.Equal([$"{file}:2:1: error: There's no step 'nope'.", "Converted no files: 1 error."], Lines());
        Assert.False(File.Exists(Path.Combine(_folder.Path, "a.ztest.yaml")));
    }

    [Fact]
    public void Convert_DoesntWriteOverAScenarioThatsThere()
    {
        var file = Write("a.zlstest", "wait 2\n");
        var converted = Write("a.ztest.yaml", "steps:\n  - undo\n");

        Assert.Equal(1, Run("convert", file));
        Assert.Equal($"error: {converted} is already there. Move it out of the way to convert {file}.", Lines()[0]);
        Assert.Equal("steps:\n  - undo\n", File.ReadAllText(converted));
    }

    [Fact]
    public void Convert_AFolder_ConvertsEachOldFileInIt()
    {
        Write("a.zlstest", "wait 2\n");
        Write(Path.Combine("more", "b.zlstest"), "undo\n");

        Assert.Equal(0, Run("convert", _folder.Path));
        Assert.True(File.Exists(Path.Combine(_folder.Path, "a.ztest.yaml")));
        Assert.True(File.Exists(Path.Combine(_folder.Path, "more", "b.ztest.yaml")));
        Assert.Equal("Converted 2 files: no errors.", Lines()[^1]);
    }

    [Fact]
    public void Convert_AFolderWithoutOldFiles_Fails()
    {
        Write("a.ztest.yaml", "steps:\n  - undo\n");

        Assert.Equal(1, Run("convert", _folder.Path));
        Assert.Equal($"error: No .zlstest files in {_folder.Path}.", Lines()[0]);
    }

    [Theory]
    [InlineData]
    [InlineData("--nope")]
    public void Convert_NoFilesOrABadOption_PrintsUsageAndFails(params string[] arguments)
    {
        Assert.Equal(1, Run(["convert", .. arguments]));
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    private int Run(params string[] arguments)
    {
        return RunIn(Environment.CurrentDirectory, arguments);
    }

    private int RunIn(string folder, params string[] arguments)
    {
        return Program.Run(arguments, _output, folder);
    }

    private string Write(string file, string text)
    {
        var path = Path.Combine(_folder.Path, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
        return path;
    }

    private IReadOnlyList<string> Lines()
    {
        return _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
