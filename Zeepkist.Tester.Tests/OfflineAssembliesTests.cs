using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        using var assemblies = WithoutSampleCore(folder);
        var step = LoadCopy(assemblies, folder, stepType);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().Build(step));

        Assert.Equal($"{stepType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Fact]
    public void BuildState_StateWithinAStateThatCantBeFound_NamesTheStateAndWhatsMissing()
    {
        var stateType = typeof(UnfoundSteps.WithinElsewhere);
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var state = LoadCopy(assemblies, folder, stateType);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildState(state));

        Assert.Equal($"{stateType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Theory]
    [InlineData(typeof(UnfoundSteps.RequiresElsewhere))]
    [InlineData(typeof(UnfoundSteps.RequiresAnyElsewhere))]
    [InlineData(typeof(UnfoundSteps.EntersElsewhere))]
    [InlineData(typeof(UnfoundSteps.LeavesElsewhere))]
    public void BuildRegistry_StepNamingAStateThatCantBeFound_NamesTheStepAndWhatsMissing(Type stepType)
    {
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var step = LoadCopy(assemblies, folder, stepType);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildRegistry([step], []));

        Assert.Equal($"{stepType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Theory]
    [InlineData(typeof(UnfoundSteps.StateBaseElsewhere))]
    [InlineData(typeof(UnfoundSteps.ConstructorElsewhere))]
    public void BuildRegistry_StateWithATypeThatCantBeFound_NamesTheStateAndWhatsMissing(Type stateType)
    {
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var state = LoadCopy(assemblies, folder, stateType);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildRegistry([], [state]));

        Assert.Equal($"{stateType.FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Fact]
    public void BuildRegistry_StepNamingAStateBasedOnATypeThatCantBeFound_NamesTheState()
    {
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var step = LoadCopy(assemblies, folder, typeof(UnfoundSteps.RequiresAStateBasedElsewhere));

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildRegistry([step], []));

        Assert.Equal($"{typeof(UnfoundSteps.StateBaseElsewhere).FullName} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Theory]
    [InlineData(typeof(UnfoundSteps.TestConstructorElsewhere))]
    [InlineData(typeof(UnfoundSteps.TestParameterElsewhere))]
    public void BuildTest_TestWithATypeThatCantBeFound_NamesTheTestAndWhatsMissing(Type testClass)
    {
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var test = LoadCopy(assemblies, folder, testClass).GetMethod(nameof(UnfoundSteps.TestParameterElsewhere.Test), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildTest(test));

        Assert.Equal($"{testClass.FullName}.{nameof(UnfoundSteps.TestParameterElsewhere.Test)} uses a type from Zeepkist.Tester.Tests.SampleCore, which can't be found.", exception.Message);
    }

    [Fact]
    public void FindStepsStatesAndTests_PluginWithoutOneOfItsReferences_FindsThemWithoutReadingWhatsMissing()
    {
        // Only building one reads what it uses, and that names the step, state or test
        using var folder = new TemporaryFolder();
        using var assemblies = WithoutSampleCore(folder);
        var plugin = assemblies.Load(Path.Combine(folder.Path, Path.GetFileName(typeof(UnfoundSteps).Assembly.Location)));

        Assert.False(StepDescriptorBuilder.IsTestPlugin(plugin));
        Assert.Contains(typeof(UnfoundSteps.BaseElsewhere).FullName, StepDescriptorBuilder.FindSteps(plugin).Select(type => type.FullName));
        Assert.Contains(typeof(UnfoundSteps.StateBaseElsewhere).FullName, StepDescriptorBuilder.FindStates(plugin).Select(type => type.FullName));
        Assert.Contains(typeof(UnfoundSteps.TestParameterElsewhere).FullName, StepDescriptorBuilder.FindTests(plugin).Select(method => method.DeclaringType.FullName));
    }

    /// <summary>
    /// Copies this assembly and the SDK to <paramref name="folder"/>, and reads from there, where Zeepkist.Tester.Tests.SampleCore, which this assembly references, can't be found.
    /// </summary>
    private static OfflineAssemblies WithoutSampleCore(TemporaryFolder folder)
    {
        foreach (var assembly in new[] { typeof(UnfoundSteps).Assembly, typeof(Step).Assembly })
            File.Copy(assembly.Location, Path.Combine(folder.Path, Path.GetFileName(assembly.Location)));

        return new OfflineAssemblies([folder.Path, RuntimeEnvironment.GetRuntimeDirectory()], "System.Runtime");
    }

    /// <summary>
    /// <paramref name="type"/> as read from the copy of its assembly in <paramref name="folder"/>.
    /// </summary>
    private static Type LoadCopy(OfflineAssemblies assemblies, TemporaryFolder folder, Type type)
    {
        return assemblies.Load(Path.Combine(folder.Path, Path.GetFileName(type.Assembly.Location))).GetType(type.FullName);
    }

    private static IEnumerable<Type> StepsIn(IEnumerable<Type> types)
    {
        return types.Where(type => type.GetCustomAttributesData().Any(data => data.AttributeType.FullName == typeof(StepAttribute).FullName));
    }
}
