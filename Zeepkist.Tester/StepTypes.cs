using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The steps scenarios can use: how the parser sees each, and the type that runs it.
/// </summary>
internal sealed class StepTypes
{
    private readonly Dictionary<string, Type> _types;

    private StepTypes(IReadOnlyList<StepDescriptor> descriptors, Dictionary<string, Type> types)
    {
        Descriptors = descriptors;
        _types = types;
    }

    public IReadOnlyList<StepDescriptor> Descriptors { get; }

    /// <exception cref="StepDefinitionException">A step or a state it names breaks one of the registry's rules.</exception>
    public static StepTypes Build(IEnumerable<Type> types)
    {
        var list = types.ToList();
        var descriptors = new StepDescriptorBuilder().BuildRegistry(list, []).Steps;
        return new StepTypes(descriptors, descriptors.Zip(list, (descriptor, type) => (descriptor.FullName, type)).ToDictionary(pair => pair.FullName, pair => pair.type));
    }

    public Type TypeOf(StepDescriptor step)
    {
        return _types[step.FullName];
    }
}
