using System;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.Zls;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public sealed class ScenarioLoaderTests : IDisposable
{
    private readonly TemporaryFolder _folder = new TemporaryFolder();

    public void Dispose()
    {
        _folder.Dispose();
    }

    [Fact]
    public void AnOldFile_LoadsAsItsConvertedYaml_InTheEditorWithItsModsInUse()
    {
        var old = Write("a.zlstest", "# Opens the window\nopen-window\nplace-block lamp as lamp\nset-text\n    a = placed(\"{uid:lamp}\")\nend\nwait 2\n");
        var converted = Write("a.ztest.yaml", LineScenarioConverter.Write(LineScenarioReader.Read(old, new StringReader(File.ReadAllText(old)), ZlsSteps.Registry), ZlsSteps.Registry));

        var scenario = new ScenarioLoader(ZlsSteps.Registry).Load(old);
        var yaml = new ScenarioLoader(ZlsSteps.Registry).Load(converted);

        LineScenarioConverterTests.AssertSameSteps(yaml.Steps.Cast<StepNode>().ToList(), scenario.Steps.Cast<StepNode>().ToList(), File.ReadAllText(converted));
        Assert.Equal(old, scenario.File);
        Assert.Equal(["zls"], scenario.Use);
        Assert.Equal(StartState.Editor, scenario.Start.State);
        Assert.Equal(InputRoute.InProcess, scenario.Input);
        Assert.Null(scenario.Takes);
        Assert.Equal([2, 3, 4, 7], scenario.Steps.Select(step => step.Location.Line));
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder.Path, name);
        File.WriteAllText(path, text);
        return path;
    }
}
