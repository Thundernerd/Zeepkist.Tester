using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class OfflineAssembliesTests
{
    [Fact]
    public void BuildAll_OfflineAndAtRuntime_GiveEqualDescriptors()
    {
        var testsPath = typeof(TestSteps).Assembly.Location;
        var builder = new StepDescriptorBuilder
        {
            TesterAssemblyName = typeof(TestSteps).Assembly.GetName().Name,
            Summaries = XmlDocs.Read([testsPath]),
        };

        var atRuntime = builder.BuildAll(StepsIn(typeof(TestSteps).GetNestedTypes()));

        // This assembly is on .NET, where a plugin would be on .NET Framework
        using var assemblies = new OfflineAssemblies([RuntimeEnvironment.GetRuntimeDirectory(), Path.GetDirectoryName(testsPath)], "System.Runtime");
        var testSteps = assemblies.Load(testsPath).GetType(typeof(TestSteps).FullName);
        var offline = builder.BuildAll(StepsIn(testSteps.GetNestedTypes()));

        Assert.Equal(["wait", "place-block", "zls.drag-window-edge", "every-type", "named", "written", "undo", "zls.compile", "zls.open-window", "zls.close-window", "zls.needs-either", "zls.needs-no-hint"], offline.Select(step => step.FullName));
        Assert.Equal("Waits, such as for the level to run a little before a screenshot.", offline[0].Documentation);
        Assert.Equal(JsonSerializer.Serialize(atRuntime), JsonSerializer.Serialize(offline));
    }

    [Fact]
    public void BuildState_OfflineAndAtRuntime_GiveEqualDescriptors()
    {
        var testsPath = typeof(TestSteps).Assembly.Location;
        var builder = new StepDescriptorBuilder { Summaries = XmlDocs.Read([testsPath]) };

        var atRuntime = builder.BuildState(typeof(TestSteps.ScriptSelected));

        using var assemblies = new OfflineAssemblies([RuntimeEnvironment.GetRuntimeDirectory(), Path.GetDirectoryName(testsPath)], "System.Runtime");
        var offline = builder.BuildState(assemblies.Load(testsPath).GetType(typeof(TestSteps.ScriptSelected).FullName));

        Assert.Equal("zls.window-open", offline.Within);
        Assert.Equal(JsonSerializer.Serialize(atRuntime), JsonSerializer.Serialize(offline));
    }

    [Theory]
    [InlineData(typeof(UnfoundSteps.BaseElsewhere))]
    [InlineData(typeof(UnfoundSteps.ArgumentElsewhere))]
    public void Build_StepWithATypeThatCantBeFound_NamesTheStepAndWhatsMissing(Type stepType)
    {
        using var folder = new TemporaryFolder();
        using var assemblies = new OfflineAssemblies([CopyWithoutSampleCore(folder), RuntimeEnvironment.GetRuntimeDirectory()], "System.Runtime");
        var step = assemblies.Load(Path.Combine(folder.Path, Path.GetFileName(stepType.Assembly.Location))).GetType(stepType.FullName);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().Build(step));

        Assert.Equal($"{stepType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Fact]
    public void BuildState_StateWithinAStateThatCantBeFound_NamesTheStateAndWhatsMissing()
    {
        var stateType = typeof(UnfoundSteps.WithinElsewhere);
        using var folder = new TemporaryFolder();
        using var assemblies = new OfflineAssemblies([CopyWithoutSampleCore(folder), RuntimeEnvironment.GetRuntimeDirectory()], "System.Runtime");
        var state = assemblies.Load(Path.Combine(folder.Path, Path.GetFileName(stateType.Assembly.Location))).GetType(stateType.FullName);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildState(state));

        Assert.Equal($"{stateType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    /// <returns>A folder with this assembly and the SDK, but not Zeepkist.Tester.Tests.SampleCore, which it references.</returns>
    private static string CopyWithoutSampleCore(TemporaryFolder folder)
    {
        foreach (var assembly in new[] { typeof(UnfoundSteps).Assembly, typeof(Step).Assembly })
            File.Copy(assembly.Location, Path.Combine(folder.Path, Path.GetFileName(assembly.Location)));

        return folder.Path;
    }

    private static IEnumerable<Type> StepsIn(IEnumerable<Type> types)
    {
        return types.Where(type => type.GetCustomAttributesData().Any(data => data.AttributeType.FullName == typeof(StepAttribute).FullName));
    }
}
