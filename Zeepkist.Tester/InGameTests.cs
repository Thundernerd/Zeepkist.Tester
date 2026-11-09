using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The C# tests a run can choose from: how the CLI sees each, and the method that runs it.
/// </summary>
internal sealed class InGameTests
{
    private readonly List<InGameTest> _tests;

    private InGameTests(List<InGameTest> tests)
    {
        _tests = tests;
    }

    /// <exception cref="StepDefinitionException">A test breaks one of the registry's rules, or two share a name.</exception>
    public static InGameTests Build(IEnumerable<MethodInfo> methods)
    {
        var list = methods.ToList();
        var descriptors = new StepDescriptorBuilder().BuildTests(list);
        return new InGameTests([.. descriptors.Zip(list, (descriptor, method) => new InGameTest(descriptor, method))]);
    }

    /// <summary>
    /// The tests <paramref name="selectors"/> name, each once, in the order they're named and then written.
    /// </summary>
    /// <param name="selectors">Each a test's <see cref="TestDescriptor.Name"/>, or its class's name for each test in it.</param>
    /// <param name="unknown">The selectors that name no test.</param>
    public List<InGameTest> Select(IEnumerable<string> selectors, out List<string> unknown)
    {
        var selected = new List<InGameTest>();
        unknown = [];
        foreach (var selector in selectors)
        {
            var named = _tests.Where(test => test.Descriptor.IsSelectedBy(selector)).ToList();
            if (named.Count == 0)
                unknown.Add(selector);

            selected.AddRange(named.Where(test => !selected.Contains(test)));
        }

        return selected;
    }
}
