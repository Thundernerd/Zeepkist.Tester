using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// What the parser's tests read fixtures with: the tester's steps from <see cref="TestSteps"/>, and mods' steps that clash.
/// </summary>
public static class ScenarioSteps
{
    private static readonly Type[] _types =
    [
        typeof(TestSteps.Wait),
        typeof(TestSteps.Undo),
        typeof(TestSteps.PlaceBlock),
        typeof(TestSteps.DragWindowEdge),
        typeof(TestSteps.EveryType),
        typeof(TestSteps.Named),
        typeof(TestSteps.Written),
        typeof(ClashingWait),
        typeof(ZlsCompile),
        typeof(OtherCompile),
    ];

    public static readonly IReadOnlyList<StepDescriptor> Descriptors = new StepDescriptorBuilder { TesterAssemblyName = typeof(TestSteps).Assembly.GetName().Name }.BuildAll(_types);

    /// <summary>
    /// The type of a step that was read, by the position of its descriptor.
    /// </summary>
    internal static Type TypeOf(StepNode node)
    {
        return _types[Descriptors.ToList().IndexOf(node.Step)];
    }

    internal static Scenario Parse(string fixture)
    {
        var file = Path.Combine("Scenarios", $"{fixture}.ztest.yaml");
        using var reader = File.OpenText(file);
        return ScenarioParser.Parse(file, reader, Descriptors);
    }

    /// <summary>Waits in a way that clashes with the tester's <c>wait</c> once <c>use:</c> lists its namespace.</summary>
    [Step("wait", Namespace = "clashing")]
    public sealed class ClashingWait : EmptyStep
    {
    }

    /// <summary>Clashes with <see cref="OtherCompile"/> once <c>use:</c> lists both.</summary>
    [Step("compile", Namespace = "zls")]
    public sealed class ZlsCompile : EmptyStep
    {
    }

    /// <summary>Clashes with <see cref="ZlsCompile"/> once <c>use:</c> lists both.</summary>
    [Step("compile", Namespace = "other")]
    public sealed class OtherCompile : EmptyStep
    {
    }

    public abstract class EmptyStep : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }
}
