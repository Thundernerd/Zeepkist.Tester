using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Builds <see cref="StepDescriptor"/>s and <see cref="StateDescriptor"/>s from step and state types, and enforces the registry's rules while it does.
/// </summary>
/// <remarks>
/// It only reads types and <see cref="CustomAttributeData"/>, and compares types by name, so it also works on types
/// from a MetadataLoadContext: those can't make attribute instances, and aren't equal to our own types.
/// </remarks>
public sealed class StepDescriptorBuilder
{
    // Also a different type on each runtime: the BCL's on .NET, the SDK's polyfill on net472
    private const string RequiredMemberAttributeName = "System.Runtime.CompilerServices.RequiredMemberAttribute";

    private static readonly Regex _kebabCase = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$");

    // A scenario reads these as control nodes, so a step with one of these names would be hidden when it's written unprefixed
    private static readonly string[] _controlNodes = ["for", "repeat", "run"];

    private static readonly Dictionary<string, ArgumentType> _scalarTypes = new Dictionary<string, ArgumentType>()
    {
        [typeof(string).FullName] = new ArgumentType { Kind = ArgumentKind.String },
        [typeof(TextBlock).FullName] = new ArgumentType { Kind = ArgumentKind.TextBlock },
        [typeof(int).FullName] = new ArgumentType { Kind = ArgumentKind.Int },
        [typeof(float).FullName] = new ArgumentType { Kind = ArgumentKind.Float },
        [typeof(bool).FullName] = new ArgumentType { Kind = ArgumentKind.Bool },
        [typeof(Seconds).FullName] = new ArgumentType { Kind = ArgumentKind.Seconds, Pattern = Seconds.Pattern },
        [typeof(Pixels).FullName] = new ArgumentType { Kind = ArgumentKind.Pixels },
        [typeof(Keys).FullName] = new ArgumentType { Kind = ArgumentKind.Keys, Pattern = Keys.Pattern },
        [typeof(FileName).FullName] = new ArgumentType { Kind = ArgumentKind.FileName, Pattern = FileName.Pattern },
    };

    /// <summary>
    /// The only assembly whose steps and states may use the empty namespace.
    /// </summary>
    internal string TesterAssemblyName { get; init; } = "Zeepkist.Tester";

    /// <summary>
    /// Finds a member's <c>&lt;summary&gt;</c> by its XML doc id, such as <c>T:Mod.Compile</c>. There are no docs without it.
    /// </summary>
    public Func<string, string> Summaries { get; init; } = _ => null;

    /// <summary>
    /// The types in an assembly with <see cref="StepAttribute"/>.
    /// </summary>
    internal static IEnumerable<Type> FindSteps(Assembly assembly)
    {
        return assembly.GetTypes().Where(type => FindAttribute<StepAttribute>(type) != null);
    }

    /// <summary>
    /// The types in an assembly with <see cref="StateAttribute"/>.
    /// </summary>
    internal static IEnumerable<Type> FindStates(Assembly assembly)
    {
        return assembly.GetTypes().Where(type => FindAttribute<StateAttribute>(type) != null);
    }

    /// <summary>
    /// Whether a type in the assembly has <see cref="TestPluginAttribute"/>.
    /// </summary>
    internal static bool IsTestPlugin(Assembly assembly)
    {
        return TestPlugins(assembly).Any();
    }

    public StepDescriptor Build(Type stepType)
    {
        var stepAttribute = FindAttribute<StepAttribute>(stepType)
            ?? throw new StepDefinitionException($"{stepType.FullName} has no [Step] attribute.");

        if (!DerivesFromStep(stepType))
            throw new StepDefinitionException($"{stepType.FullName} has [Step] but doesn't derive from Step.");

        var (name, stepNamespace) = ReadName(stepAttribute, stepType, "Step");
        if (_controlNodes.Contains(name))
            throw new StepDefinitionException($"Step name '{name}' ({stepType.FullName}) is reserved for a scenario's control nodes: for, repeat and run.");

        return new StepDescriptor
        {
            Name = name,
            Namespace = stepNamespace,
            Requires =
            [
                .. DeclaredStates<RequiresAttribute>(stepType).SelectMany(states => states).Select(state => new[] { BuildState(state).FullName }),
                .. DeclaredStates<RequiresAnyAttribute>(stepType).Select(states => StateNames(states)),
            ],
            Enters = StateNames(DeclaredStates<EntersAttribute>(stepType).SelectMany(states => states)),
            Leaves = StateNames(DeclaredStates<LeavesAttribute>(stepType).SelectMany(states => states)),
            Documentation = Summaries($"T:{DocName(stepType)}"),
            Examples = [.. FindAttributes(stepType, typeof(ExampleAttribute).FullName).Select(ConstructorValue<string>)],
            Arguments = BuildArguments(stepType, [stepType]),
        };
    }

    /// <summary>
    /// Builds several steps, which can't share a name within a namespace.
    /// </summary>
    public IReadOnlyList<StepDescriptor> BuildAll(IEnumerable<Type> stepTypes)
    {
        var descriptors = new List<StepDescriptor>();
        var typesByName = new Dictionary<string, Type>();
        foreach (var stepType in stepTypes)
        {
            var descriptor = Build(stepType);
            if (typesByName.TryGetValue(descriptor.FullName, out var other))
                throw new StepDefinitionException($"{other.FullName} and {stepType.FullName} are both the step '{descriptor.FullName}'.");

            typesByName.Add(descriptor.FullName, stepType);
            descriptors.Add(descriptor);
        }

        return descriptors;
    }

    public StateDescriptor BuildState(Type stateType)
    {
        return BuildState(stateType, []);
    }

    /// <summary>
    /// Builds steps and states: <paramref name="stateTypes"/>, and those the steps and states name, which can't share a name within a namespace.
    /// </summary>
    public StepRegistry BuildRegistry(IEnumerable<Type> stepTypes, IEnumerable<Type> stateTypes)
    {
        var stepTypeList = stepTypes.ToList();
        var steps = BuildAll(stepTypeList);

        var states = new List<StateDescriptor>();
        var typesByName = new Dictionary<string, Type>();
        var pending = new Queue<Type>(stateTypes.Concat(stepTypeList.SelectMany(DeclaredStates)));
        while (pending.Count > 0)
        {
            var stateType = pending.Dequeue();
            var state = BuildState(stateType);
            if (typesByName.TryGetValue(state.FullName, out var other))
            {
                if (other.FullName == stateType.FullName)
                    continue;

                throw new StepDefinitionException($"{other.FullName} and {stateType.FullName} are both the state '{state.FullName}'.");
            }

            typesByName.Add(state.FullName, stateType);
            states.Add(state);

            var within = NamedValue<Type>(FindAttribute<StateAttribute>(stateType), nameof(StateAttribute.Within));
            if (within != null)
                pending.Enqueue(within);
        }

        return new StepRegistry { Steps = steps, States = states };
    }

    private StateDescriptor BuildState(Type stateType, List<Type> inner)
    {
        var stateAttribute = FindAttribute<StateAttribute>(stateType)
            ?? throw new StepDefinitionException($"{stateType.FullName} has no [State] attribute.");

        if (inner.Any(other => other.FullName == stateType.FullName))
            throw new StepDefinitionException($"{stateType.FullName} is within itself, through {inner.Last().FullName}.");

        if (!stateType.GetInterfaces().Any(type => type.FullName == typeof(IState).FullName))
            throw new StepDefinitionException($"{stateType.FullName} has [State] but doesn't implement IState.");

        if (stateType.IsAbstract || stateType.ContainsGenericParameters || stateType.GetConstructor(Type.EmptyTypes) == null)
            throw new StepDefinitionException($"{stateType.FullName} has [State] but can't be made: it has to be a concrete, non-generic class with a public constructor that takes nothing.");

        var (name, stateNamespace) = ReadName(stateAttribute, stateType, "State");

        var within = NamedValue<Type>(stateAttribute, nameof(StateAttribute.Within));
        if (within != null)
            RequireState(within, $"Within on {stateType.FullName}");

        return new StateDescriptor
        {
            Name = name,
            Namespace = stateNamespace,
            Within = within == null ? null : BuildState(within, [.. inner, stateType]).FullName,
            Hint = NamedValue<string>(stateAttribute, nameof(StateAttribute.Hint)),
            Documentation = Summaries($"T:{DocName(stateType)}"),
        };
    }

    /// <summary>
    /// The name and namespace of a step or state, whose <see cref="StepAttribute"/> and <see cref="StateAttribute"/> follow one set of rules.
    /// </summary>
    private (string Name, string Namespace) ReadName(CustomAttributeData attribute, Type owner, string kind)
    {
        var name = ConstructorValue<string>(attribute);
        RequireKebabCase($"{kind} name '{name}'", name, owner);

        var ownNamespace = NamedValue<string>(attribute, nameof(StepAttribute.Namespace)) ?? TestPluginNamespace(owner.Assembly) ?? string.Empty;
        if (ownNamespace.Length > 0)
            RequireKebabCase($"Namespace '{ownNamespace}'", ownNamespace, owner);
        else if (owner.Assembly.GetName().Name != TesterAssemblyName)
            throw new StepDefinitionException($"{kind} '{name}' ({owner.FullName}) has no namespace, which is reserved for the tester's own {kind.ToLowerInvariant()}s. Set Namespace on [{kind}] or [TestPlugin].");

        return (name, ownNamespace);
    }

    private List<string> StateNames(IEnumerable<Type> stateTypes)
    {
        return [.. stateTypes.Select(state => BuildState(state).FullName)];
    }

    private static IEnumerable<Type> DeclaredStates(Type stepType)
    {
        return DeclaredStates<RequiresAttribute>(stepType)
            .Concat(DeclaredStates<RequiresAnyAttribute>(stepType))
            .Concat(DeclaredStates<EntersAttribute>(stepType))
            .Concat(DeclaredStates<LeavesAttribute>(stepType))
            .SelectMany(states => states);
    }

    /// <summary>
    /// What each of a step's <typeparamref name="TAttribute"/>s names.
    /// </summary>
    private static IEnumerable<List<Type>> DeclaredStates<TAttribute>(Type stepType)
        where TAttribute : Attribute
    {
        var what = $"[{typeof(TAttribute).Name.Replace(nameof(Attribute), string.Empty)}] on {stepType.FullName}";
        foreach (var attribute in FindAttributes(stepType, typeof(TAttribute).FullName))
        {
            var written = attribute.ConstructorArguments[0].Value as IEnumerable<CustomAttributeTypedArgument> ?? [];
            var states = written.Select(state => (Type)state.Value).ToList();
            if (states.Count == 0)
                throw new StepDefinitionException($"{what} names no states.");

            foreach (var state in states)
                RequireState(state, what);

            yield return states;
        }
    }

    private static void RequireState(Type type, string what)
    {
        if (type == null)
            throw new StepDefinitionException($"{what} names null, which isn't a state.");

        if (FindAttribute<StateAttribute>(type) == null)
            throw new StepDefinitionException($"{what} names {type.FullName}, which has no [State] attribute.");
    }

    private IReadOnlyList<ArgumentDescriptor> BuildArguments(Type owner, List<Type> enclosing)
    {
        var arguments = new List<ArgumentDescriptor>();
        foreach (var property in owner.GetProperties())
        {
            var argAttribute = FindAttribute<ArgAttribute>(property);
            if (argAttribute == null)
                continue;

            var name = NamedValue<string>(argAttribute, nameof(ArgAttribute.Name)) ?? ToKebabCase(property.Name);
            RequireKebabCase($"Argument name '{name}'", name, owner);
            if (arguments.Any(argument => argument.Name == name))
                throw new StepDefinitionException($"{owner.FullName} has more than one argument named '{name}'.");

            var type = BuildType(property.PropertyType, name, enclosing);
            var range = FindAttribute<InRangeAttribute>(property);
            var pattern = ConstructorValue<string>(FindAttribute<PatternAttribute>(property));
            if (pattern != null)
                RequireRegularExpression(pattern, name, owner);

            arguments.Add(new ArgumentDescriptor
            {
                Name = name,
                PropertyName = property.Name,
                Type = type,
                Documentation = Summaries($"P:{DocName(property.DeclaringType)}.{property.Name}"),
                IsPrimary = NamedValue<bool>(argAttribute, nameof(ArgAttribute.Primary)),
                IsRequired = FindAttribute(property, RequiredMemberAttributeName) != null,
                Default = WrittenDefault(argAttribute),
                Pattern = pattern ?? type.Pattern,
                Minimum = ConstructorValue<double?>(range),
                Maximum = (double?)range?.ConstructorArguments[1].Value,
                MinItems = ConstructorValue<int?>(FindAttribute<MinItemsAttribute>(property)),
            });
        }

        var primaries = arguments.Where(argument => argument.IsPrimary).Select(argument => $"'{argument.Name}'").ToList();
        if (primaries.Count > 1)
            throw new StepDefinitionException($"{owner.FullName} has more than one Primary argument: {string.Join(" and ", primaries)}.");

        return arguments;
    }

    private static void RequireRegularExpression(string pattern, string argument, Type owner)
    {
        try
        {
            _ = new Regex(pattern);
        }
        catch (ArgumentException)
        {
            throw new StepDefinitionException($"Argument '{argument}' ({owner.FullName}) has the pattern '{pattern}', which isn't a regular expression.");
        }
    }

    private ArgumentType BuildType(Type type, string argument, List<Type> enclosing)
    {
        if (IsGeneric(type, "System.Nullable`1"))
            return BuildType(type.GetGenericArguments()[0], argument, enclosing);

        if (_scalarTypes.TryGetValue(type.FullName, out var scalar))
            return scalar;

        if (IsGeneric(type, typeof(Name<>).FullName))
            return BuildNamedValue(ArgumentKind.Name, type, argument, enclosing);

        if (IsGeneric(type, typeof(Ref<>).FullName))
            return BuildNamedValue(ArgumentKind.Ref, type, argument, enclosing);

        if (type.IsEnum)
            return new ArgumentType { Kind = ArgumentKind.Enum, EnumValues = [.. EnumMembers(type).Select(WrittenEnumValue)] };

        if (IsGeneric(type, "System.Collections.Generic.List`1"))
            return new ArgumentType { Kind = ArgumentKind.List, ItemType = BuildType(type.GetGenericArguments()[0], argument, enclosing) };

        if (!type.IsClass || !type.GetProperties().Any(property => FindAttribute<ArgAttribute>(property) != null))
            throw new StepDefinitionException($"Argument '{argument}' ({enclosing.Last().FullName}) has the type {type.FullName}, which isn't an argument type.");

        if (enclosing.Any(other => other.FullName == type.FullName))
            throw new StepDefinitionException($"{type.FullName} contains itself through argument '{argument}'.");

        return new ArgumentType
        {
            Kind = ArgumentKind.Object,
            Documentation = Summaries($"T:{DocName(type)}"),
            Arguments = BuildArguments(type, [.. enclosing, type]),
        };
    }

    private static ArgumentType BuildNamedValue(ArgumentKind argumentKind, Type type, string argument, List<Type> enclosing)
    {
        var valueType = type.GetGenericArguments()[0];
        var kindAttribute = FindAttribute<ValueKindAttribute>(valueType)
            ?? throw new StepDefinitionException($"Argument '{argument}' ({enclosing.Last().FullName}) names a {valueType.FullName}, which has no [ValueKind] attribute.");

        var kind = ConstructorValue<string>(kindAttribute);
        RequireKebabCase($"Value kind '{kind}'", kind, valueType);

        var substitute = NamedValue<string>(kindAttribute, nameof(ValueKindAttribute.Substitute));
        if (substitute != null && !IsStringProperty(valueType, substitute))
            throw new StepDefinitionException($"Value kind Substitute '{substitute}' ({valueType.FullName}) isn't a public string property of it.");

        return new ArgumentType { Kind = argumentKind, Pattern = Name<object>.Pattern, ValueKind = kind, BaseValueKinds = BaseKinds(valueType), Substitute = substitute };
    }

    private static List<string> BaseKinds(Type valueType)
    {
        var kinds = new List<string>();
        for (var baseType = valueType.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            var attribute = FindAttribute<ValueKindAttribute>(baseType);
            if (attribute != null)
                kinds.Add(ConstructorValue<string>(attribute));
        }

        return kinds;
    }

    private static bool IsStringProperty(Type type, string name)
    {
        var property = type.GetProperty(name);
        return property != null && property.CanRead && property.PropertyType.FullName == typeof(string).FullName;
    }

    private static bool DerivesFromStep(Type type)
    {
        for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.FullName == typeof(Step).FullName)
                return true;
        }

        return false;
    }

    private static string TestPluginNamespace(Assembly assembly)
    {
        return TestPlugins(assembly)
            .Select(plugin => NamedValue<string>(plugin, nameof(TestPluginAttribute.Namespace)))
            .FirstOrDefault();
    }

    private static IEnumerable<CustomAttributeData> TestPlugins(Assembly assembly)
    {
        return assembly.GetTypes()
            .Select(type => FindAttribute<TestPluginAttribute>(type))
            .Where(plugin => plugin != null);
    }

    private static void RequireKebabCase(string what, string name, Type owner)
    {
        if (!_kebabCase.IsMatch(name))
            throw new StepDefinitionException($"{what} ({owner.FullName}) isn't in kebab case, such as 'place-block'.");
    }

    private static bool IsGeneric(Type type, string definitionName)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition().FullName == definitionName;
    }

    private static FieldInfo[] EnumMembers(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static);
    }

    private static string WrittenEnumValue(FieldInfo member)
    {
        return ConstructorValue<string>(FindAttribute<ArgValueAttribute>(member)) ?? ToKebabCase(member.Name);
    }

    /// <summary>
    /// An enum in an attribute is only its number, so this finds the member it means.
    /// </summary>
    private static object WrittenDefault(CustomAttributeData argAttribute)
    {
        foreach (var argument in argAttribute.NamedArguments)
        {
            if (argument.MemberName != nameof(ArgAttribute.Default))
                continue;

            var value = argument.TypedValue;
            if (!value.ArgumentType.IsEnum)
                return value.Value;

            var member = EnumMembers(value.ArgumentType).FirstOrDefault(field => Equals(field.GetRawConstantValue(), value.Value));
            return member == null ? value.Value : WrittenEnumValue(member);
        }

        return null;
    }

    private static string DocName(Type type)
    {
        return type.FullName.Replace('+', '.');
    }

    private static string ToKebabCase(string name)
    {
        var kebab = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var startsWord = i > 0 && char.IsUpper(name[i]) && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (startsWord)
                kebab.Append('-');

            kebab.Append(char.ToLowerInvariant(name[i]));
        }

        return kebab.ToString();
    }

    private static CustomAttributeData FindAttribute<TAttribute>(MemberInfo member)
        where TAttribute : Attribute
    {
        return FindAttribute(member, typeof(TAttribute).FullName);
    }

    private static CustomAttributeData FindAttribute(MemberInfo member, string attributeName)
    {
        return FindAttributes(member, attributeName).FirstOrDefault();
    }

    private static IEnumerable<CustomAttributeData> FindAttributes(MemberInfo member, string attributeName)
    {
        return member.GetCustomAttributesData().Where(data => AttributeName(data) == attributeName);
    }

    /// <summary>
    /// Null for an attribute a MetadataLoadContext can't find, such as from a package of the mod's: it isn't one we look for, which are all in this assembly or the BCL.
    /// </summary>
    private static string AttributeName(CustomAttributeData attribute)
    {
        try
        {
            return attribute.AttributeType.FullName;
        }
        catch (Exception exception) when (exception is FileNotFoundException or TypeLoadException)
        {
            return null;
        }
    }

    private static T ConstructorValue<T>(CustomAttributeData attribute)
    {
        return attribute == null ? default : (T)attribute.ConstructorArguments[0].Value;
    }

    private static T NamedValue<T>(CustomAttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.MemberName == name)
                return (T)argument.TypedValue.Value;
        }

        return default;
    }
}
