using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleCore;
using TNRD.Zeepkist.Tester.Tests.SampleMod;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class OfflineRegistryTests
{
    private static readonly string _modPath = typeof(Compile).Assembly.Location;
    private static readonly string _sampleTesterPath = typeof(CoreStep).Assembly.Location;

    [Fact]
    public void Read_TestPlugin_GivesWhatRuntimeReflectionGives()
    {
        var builder = new StepDescriptorBuilder { TesterAssemblyName = typeof(CoreStep).Assembly.GetName().Name, Summaries = XmlDocs.Read([_modPath, _sampleTesterPath]) };
        var atRuntime = builder.BuildRegistry([typeof(Compile), typeof(GameImports), typeof(NamedValues), typeof(OwnNamespace), typeof(SwapPanel)], [typeof(CoreState), typeof(PanelOpen)]);

        var read = OfflineRegistry.Read([_modPath], _sampleTesterPath);
        var offline = read.Steps.Where(step => step.Namespace.Length > 0).ToList();

        Assert.Equal(["sample.compile", "sample.game-imports", "sample.named-values", "other.own-namespace", "sample.swap-panel"], offline.Select(step => step.FullName));
        Assert.Equal("Its constructors are there to compile on net472: `required` with `SetsRequiredMembersAttribute`.", offline[0].Documentation);
        Assert.Equal("The function to compile.", offline[0].Arguments[0].Documentation);
        Assert.True(offline[0].Arguments[0].IsRequired);
        Assert.Equal(JsonSerializer.Serialize(atRuntime.Steps), JsonSerializer.Serialize(offline));
        Assert.Equal(JsonSerializer.Serialize(atRuntime.States), JsonSerializer.Serialize(read.States));
    }

    [Fact]
    public void Read_InGameTests_GivesWhatRuntimeReflectionGives()
    {
        var builder = new StepDescriptorBuilder { Summaries = XmlDocs.Read([_modPath]) };
        var atRuntime = builder.BuildTests(StepDescriptorBuilder.FindTests(typeof(WindowTests).Assembly));

        var offline = OfflineRegistry.Read([_modPath], _sampleTesterPath).Tests;

        Assert.Equal(["WindowTests.MinimumHeight", "WindowTests.FromTheMenu"], offline.Select(test => test.Name));
        Assert.Equal("WindowTests", offline[0].ClassName);
        Assert.Equal(typeof(WindowTests).FullName, offline[0].TypeName);
        Assert.Equal("editor", offline[0].Start);
        Assert.Equal("main-menu", offline[1].Start);
        Assert.Equal("The window keeps its footer at the smallest height.", offline[0].Documentation);
        Assert.Equal(JsonSerializer.Serialize(atRuntime), JsonSerializer.Serialize(offline));
    }

    [Fact]
    public void Read_StatesOfAModAndOfTheTester_RecordsThemAndWhatStepsDoWithThem()
    {
        var registry = OfflineRegistry.Read([_modPath], _sampleTesterPath);

        Assert.Equal(["core.core-state", "sample.panel-open"], registry.States.Select(state => state.FullName));
        var panelOpen = registry.States[1];
        Assert.Equal("core.core-state", panelOpen.Within);
        Assert.Equal("Add `sample.open-panel` first.", panelOpen.Hint);
        Assert.Equal("The panel is open.", panelOpen.Documentation);

        var step = registry.Steps.Single(descriptor => descriptor.Name == "swap-panel");
        Assert.Equal([["core.core-state"], ["sample.panel-open", "core.core-state"]], step.Requires);
        Assert.Equal(["sample.panel-open"], step.Enters);
        Assert.Equal(["core.core-state"], step.Leaves);
    }

    [Fact]
    public void Read_NamedValues_RecordsWhichArgumentsDefineAndUseThem()
    {
        var step = OfflineRegistry.Read([_modPath], _sampleTesterPath).Steps.Single(descriptor => descriptor.Name == "named-values");

        var defines = step.Arguments[0];
        Assert.Equal(ArgumentKind.Name, defines.Type.Kind);
        Assert.Equal("sample-block", defines.Type.ValueKind);
        Assert.Equal("Uid", defines.Type.Substitute);
        Assert.Equal(ArgumentKind.Ref, step.Arguments[1].Type.Kind);
    }

    [Fact]
    public void Read_StepNextToTheGamesImports_FindsItsInRangeAndTextBlock()
    {
        var step = OfflineRegistry.Read([_modPath], _sampleTesterPath).Steps.Single(descriptor => descriptor.Name == "game-imports");

        Assert.Equal(1, step.Arguments[0].Minimum);
        Assert.Equal(8, step.Arguments[0].Maximum);
        Assert.Equal(ArgumentKind.TextBlock, step.Arguments[1].Type.Kind);
    }

    [Fact]
    public void Read_TheTestersAssembly_GivesItsStepsWithoutATestPlugin()
    {
        var step = Assert.Single(OfflineRegistry.Read([], _sampleTesterPath).Steps);

        Assert.Equal("core-step", step.FullName);
        Assert.Equal(string.Empty, step.Namespace);
        Assert.Equal("A step as the tester ships them: with no namespace, in an assembly with no `TestPluginAttribute`.", step.Documentation);
    }

    [Fact]
    public void Read_AssemblyWithoutATestPlugin_IsSkipped()
    {
        var notAPlugin = typeof(StepDescriptor).Assembly.Location;

        var step = Assert.Single(OfflineRegistry.Read([notAPlugin, _sampleTesterPath], _sampleTesterPath).Steps);

        Assert.Equal("core-step", step.FullName);
    }

    [Fact]
    public void Read_WithoutATesterPath_UsesTheTesterNextToTheCli()
    {
        var names = OfflineRegistry.Read([_modPath]).Steps.Select(step => step.FullName);

        Assert.Contains("sample.compile", names);
    }

    [Fact]
    public void For_Plugin_FindsTheGameAndBepInExInTheNuGetCache()
    {
        using var folder = new TemporaryFolder();
        var tester = CopyTesterTo(folder);

        using var assemblies = new OfflineAssemblies(ReferenceFolders.For([tester]));
        var plugin = assemblies.Load(tester).GetType(typeof(Plugin).FullName);

        Assert.Equal("BepInEx.BaseUnityPlugin", plugin.BaseType.FullName);
        Assert.Equal("UnityEngine.MonoBehaviour", plugin.BaseType.BaseType.FullName);
    }

    [Fact]
    public void For_Plugin_TakesTheCoreTypesFromTheReferenceAssemblies()
    {
        using var assemblies = new OfflineAssemblies(ReferenceFolders.For([_modPath]));
        var step = assemblies.Load(_modPath).GetType(typeof(Compile).FullName).BaseType;

        var core = step.BaseType.Assembly;

        Assert.Equal("System.Object", step.BaseType.FullName);
        Assert.Contains("microsoft.netframework.referenceassemblies.net472", core.Location);
    }

    [Fact]
    public void IsTestPlugin_AttributeFromAnAssemblyThatCannotBeFound_IsNotOne()
    {
        // The plugin has BepInPlugin, and BepInEx isn't in these folders
        using var folder = new TemporaryFolder();
        var tester = CopyTesterTo(folder);
        File.Copy(typeof(StepDescriptor).Assembly.Location, Path.Combine(folder.Path, "Zeepkist.Tester.Sdk.dll"));

        using var assemblies = new OfflineAssemblies([folder.Path, RuntimeEnvironment.GetRuntimeDirectory()]);

        Assert.False(StepDescriptorBuilder.IsTestPlugin(assemblies.Load(tester)));
    }

    private static string CopyTesterTo(TemporaryFolder folder)
    {
        var tester = Path.Combine(folder.Path, "Zeepkist.Tester.dll");
        File.Copy(typeof(Plugin).Assembly.Location, tester);
        return tester;
    }
}
