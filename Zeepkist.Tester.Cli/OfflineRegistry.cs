using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Reads the steps, states and C# tests of the tester and of test plugins from their built DLLs, without running them or the game.
/// </summary>
internal static class OfflineRegistry
{
    /// <summary>
    /// The tester's steps and states, then those of each assembly in <paramref name="assemblyPaths"/> that has a <see cref="TestPluginAttribute"/>.
    /// </summary>
    /// <param name="assemblyPaths">Built DLLs. Those that aren't test plugins are skipped.</param>
    /// <param name="testerPath">The tester's plugin. Defaults to the copy that ships next to the CLI.</param>
    public static StepRegistry Read(IEnumerable<string> assemblyPaths, string testerPath = null)
    {
        testerPath ??= Path.Combine(AppContext.BaseDirectory, "Zeepkist.Tester.dll");
        var paths = assemblyPaths.ToList();

        using var assemblies = new OfflineAssemblies(ReferenceFolders.For([.. paths, testerPath]));
        var tester = assemblies.Load(testerPath);
        var testPlugins = paths.Select(assemblies.Load).Where(StepDescriptorBuilder.IsTestPlugin).Where(assembly => assembly != tester).Distinct();
        var withSteps = testPlugins.Prepend(tester).ToList();

        var builder = new StepDescriptorBuilder
        {
            TesterAssemblyName = tester.GetName().Name,
            Summaries = XmlDocs.Read(withSteps.Select(assembly => assembly.Location)),
        };

        return builder.BuildRegistry(
            withSteps.SelectMany(StepDescriptorBuilder.FindSteps),
            withSteps.SelectMany(StepDescriptorBuilder.FindStates),
            withSteps.SelectMany(StepDescriptorBuilder.FindTests));
    }
}
