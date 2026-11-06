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

        Assert.Equal(["wait", "place-block", "zls.drag-window-edge", "every-type", "named", "written", "undo", "zls.compile", "zls.open-window", "zls.close-window", "zls.needs-either", "zls.no-hint"], offline.Select(step => step.FullName));
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

    private static IEnumerable<Type> StepsIn(IEnumerable<Type> types)
    {
        return types.Where(type => type.GetCustomAttributesData().Any(data => data.AttributeType.FullName == typeof(StepAttribute).FullName));
    }
}
