using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
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

    [Fact]
    public void Docs_WritesTheReference_OfTheTestersStepsAndThePlugins()
    {
        var output = Path.Combine(_folder.Path, "reference.md");

        Assert.Equal(0, Run("docs", "--output", output, "--plugin", _modPath));
        var markdown = File.ReadAllText(output);
        Assert.Contains("`sample.compile`", markdown);
        Assert.Contains("`wait`", markdown);
        Assert.Matches(@"^Wrote \d+ steps and \d+ states to .*reference\.md\.$", Lines()[0]);
    }

    [Fact]
    public void Docs_WithoutOutput_WritesStepsMd_WithTheConfiguredPlugin()
    {
        Write("zeepkist-test.json", $$"""{ "testPlugin": {{System.Text.Json.JsonSerializer.Serialize(_modPath)}} }""");

        Assert.Equal(0, RunIn(_folder.Path, "docs"));
        Assert.Contains("`sample.compile`", File.ReadAllText(Path.Combine(_folder.Path, "steps.md")));
    }

    [Fact]
    public void Docs_AFolderItCantWriteTo_FailsWithoutAStackTrace()
    {
        Assert.Equal(1, Run("docs", "--output", Path.Combine(_folder.Path, "missing", "steps.md")));
        Assert.StartsWith("error: Can't write ", _output.ToString());
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("--nope")]
    [InlineData("steps.md")]
    public void Docs_ABadOption_PrintsUsageAndFails(string option)
    {
        Assert.Equal(1, Run("docs", option));
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    [Fact]
    public void Schema_WritesItNextToTheConfig_WithTheConfiguredPluginsSteps()
    {
        WriteConfig();
        var current = Directory.CreateDirectory(Path.Combine(_folder.Path, "deep", "down")).FullName;

        Assert.Equal(0, RunIn(current, "schema"));
        var schema = Path.Combine(_folder.Path, ".ztest", "schema.json");
        Assert.Contains("\"sample.compile\"", File.ReadAllText(schema));
        Assert.Equal($"Wrote {schema}.", Lines()[0]);
    }

    [Fact]
    public void Schema_NoConfig_WritesItInTheCurrentFolder_WithThePluginsGiven()
    {
        Assert.Equal(0, RunIn(_folder.Path, "schema", "--plugin", _modPath));
        Assert.Contains("\"sample.compile\"", File.ReadAllText(Path.Combine(_folder.Path, ".ztest", "schema.json")));
    }

    [Fact]
    public void Schema_APluginThatCantBeRead_FailsAndWritesNothing()
    {
        Assert.Equal(1, RunIn(_folder.Path, "schema", "--plugin", Path.Combine(_folder.Path, "nope.dll")));
        Assert.StartsWith("error: Can't read the plugins:", _output.ToString());
        Assert.False(Directory.Exists(Path.Combine(_folder.Path, ".ztest")));
    }

    [Fact]
    public void Schema_ABadOption_PrintsUsageAndFails()
    {
        Assert.Equal(1, Run("schema", "--nope"));
        Assert.Contains("Usage: ztest", _output.ToString());
    }

    [Fact]
    public void Check_WithAConfig_AlsoWritesTheSchema_SoItNeverGoesStale()
    {
        WriteConfig();
        Write(Path.Combine("tests", "a.ztest.yaml"), "steps:\n  - sample.compile: lamp\n");

        Assert.Equal(0, RunIn(_folder.Path, "check"));
        Assert.Contains("\"sample.compile\"", File.ReadAllText(Path.Combine(_folder.Path, ".ztest", "schema.json")));
    }

    [Fact]
    public void Check_WithoutAConfig_WritesNoSchema()
    {
        var file = Write("a.ztest.yaml", "steps:\n  - sample.compile: lamp\n");

        Assert.Equal(0, RunIn(_folder.Path, "check", file, "--plugin", _modPath));
        Assert.False(Directory.Exists(Path.Combine(_folder.Path, ".ztest")));
    }

    [Fact]
    public void Init_NoSettings_WritesThemWithTheSchemaForScenarios_NextToTheConfig()
    {
        WriteConfig();
        var current = Directory.CreateDirectory(Path.Combine(_folder.Path, "deep")).FullName;

        Assert.Equal(0, RunIn(current, "init"));
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(_folder.Path, ".vscode", "settings.json")));
        Assert.Equal("**/*.ztest.yaml", (string)settings["yaml.schemas"][".ztest/schema.json"]);
    }

    [Fact]
    public void Init_SaysHowOtherEditorsFindTheSchema()
    {
        Assert.Equal(0, RunIn(_folder.Path, "init"));
        Assert.Contains("# yaml-language-server: $schema=", _output.ToString());
        Assert.Contains("# $schema:", _output.ToString());
    }

    [Fact]
    public void Init_KeepsTheOtherSettings_AndOtherSchemas()
    {
        Write(Path.Combine(".vscode", "settings.json"), """
            {
                "editor.tabSize": 2,
                "yaml.schemas": { "other.json": "*.other.yaml", ".ztest/schema.json": "old/*.yaml" },
            }
            """);

        Assert.Equal(0, RunIn(_folder.Path, "init"));
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(_folder.Path, ".vscode", "settings.json")));
        Assert.Equal(2, (int)settings["editor.tabSize"]);
        Assert.Equal("*.other.yaml", (string)settings["yaml.schemas"]["other.json"]);
        Assert.Equal(["old/*.yaml", "**/*.ztest.yaml"], settings["yaml.schemas"][".ztest/schema.json"].AsArray().Select(glob => (string)glob));
    }

    [Fact]
    public void Init_Twice_ChangesNothingTheSecondTime()
    {
        Assert.Equal(0, RunIn(_folder.Path, "init"));
        var path = Path.Combine(_folder.Path, ".vscode", "settings.json");
        var first = File.ReadAllText(path);
        _output.GetStringBuilder().Clear();

        Assert.Equal(0, RunIn(_folder.Path, "init"));
        Assert.Equal(first, File.ReadAllText(path));
        Assert.Contains("already", Lines()[0]);
    }

    [Fact]
    public void Init_SettingsWithComments_AreLeftAlone_AndItSaysWhatToAdd()
    {
        var path = Write(Path.Combine(".vscode", "settings.json"), "{\n    // mine\n    \"editor.tabSize\": 2\n}\n");

        Assert.Equal(1, RunIn(_folder.Path, "init"));
        Assert.Equal("{\n    // mine\n    \"editor.tabSize\": 2\n}\n", File.ReadAllText(path));
        Assert.Contains("\"yaml.schemas\": { \".ztest/schema.json\": \"**/*.ztest.yaml\" }", _output.ToString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{ \"yaml.schemas\": 1 }")]
    [InlineData("{ nope")]
    public void Init_SettingsItCantAddTo_Fail(string settings)
    {
        var path = Write(Path.Combine(".vscode", "settings.json"), settings);

        Assert.Equal(1, RunIn(_folder.Path, "init"));
        Assert.StartsWith("error: ", _output.ToString());
        Assert.Equal(settings, File.ReadAllText(path));
    }

    [Fact]
    public void Init_AnArgument_PrintsUsageAndFails()
    {
        Assert.Equal(1, Run("init", "--nope"));
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

    private void WriteConfig()
    {
        Write("zeepkist-test.json", $$"""{ "scenarios": "tests", "testPlugin": {{System.Text.Json.JsonSerializer.Serialize(_modPath)}} }""");
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
