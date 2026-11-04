using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Builds <see cref="StepDescriptor"/>s from step types, and enforces the registry's rules while it does.
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

    private static readonly Dictionary<string, ArgumentType> _scalarTypes = new Dictionary<string, ArgumentType>()
    {
        [typeof(string).FullName] = new ArgumentType { Kind = ArgumentKind.String },
        [typeof(Text).FullName] = new ArgumentType { Kind = ArgumentKind.Text },
        [typeof(int).FullName] = new ArgumentType { Kind = ArgumentKind.Int },
        [typeof(float).FullName] = new ArgumentType { Kind = ArgumentKind.Float },
        [typeof(bool).FullName] = new ArgumentType { Kind = ArgumentKind.Bool },
        [typeof(Seconds).FullName] = new ArgumentType { Kind = ArgumentKind.Seconds, Pattern = Seconds.Pattern },
        [typeof(Pixels).FullName] = new ArgumentType { Kind = ArgumentKind.Pixels },
        [typeof(Keys).FullName] = new ArgumentType { Kind = ArgumentKind.Keys, Pattern = Keys.Pattern },
        [typeof(FileName).FullName] = new ArgumentType { Kind = ArgumentKind.FileName, Pattern = FileName.Pattern },
    };

    /// <summary>
    /// The only assembly whose steps may use the empty namespace.
    /// </summary>
    internal string TesterAssemblyName { get; init; } = "Zeepkist.Tester";

    /// <summary>
    /// Finds a member's <c>&lt;summary&gt;</c> by its XML doc id, such as <c>T:Mod.Compile</c>. There are no docs without it.
    /// </summary>
    public Func<string, string> Summaries { get; init; } = _ => null;

    public StepDescriptor Build(Type stepType)
    {
        var stepAttribute = FindAttribute<StepAttribute>(stepType)
            ?? throw new StepDefinitionException($"{stepType.FullName} has no [Step] attribute.");

        if (!DerivesFromStep(stepType))
            throw new StepDefinitionException($"{stepType.FullName} has [Step] but doesn't derive from Step.");

        var name = ConstructorValue<string>(stepAttribute);
        RequireKebabCase($"Step name '{name}'", name, stepType);

        var stepNamespace = NamedValue<string>(stepAttribute, nameof(StepAttribute.Namespace)) ?? TestPluginNamespace(stepType.Assembly) ?? string.Empty;
        if (stepNamespace.Length > 0)
            RequireKebabCase($"Namespace '{stepNamespace}'", stepNamespace, stepType);
        else if (stepType.Assembly.GetName().Name != TesterAssemblyName)
            throw new StepDefinitionException($"Step '{name}' ({stepType.FullName}) has no namespace, which is reserved for the tester's own steps. Set Namespace on [Step] or [TestPlugin].");

        return new StepDescriptor
        {
            Name = name,
            Namespace = stepNamespace,
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
            var range = FindAttribute<RangeAttribute>(property);
            arguments.Add(new ArgumentDescriptor
            {
                Name = name,
                Type = type,
                Documentation = Summaries($"P:{DocName(property.DeclaringType)}.{property.Name}"),
                IsPrimary = NamedValue<bool>(argAttribute, nameof(ArgAttribute.Primary)),
                IsRequired = FindAttribute(property, RequiredMemberAttributeName) != null,
                Default = WrittenDefault(argAttribute),
                Pattern = ConstructorValue<string>(FindAttribute<PatternAttribute>(property)) ?? type.Pattern,
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

    private ArgumentType BuildType(Type type, string argument, List<Type> enclosing)
    {
        if (IsGeneric(type, "System.Nullable`1"))
            return BuildType(type.GetGenericArguments()[0], argument, enclosing);

        if (_scalarTypes.TryGetValue(type.FullName, out var scalar))
            return scalar;

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
        return assembly.GetTypes()
            .Select(type => FindAttribute<TestPluginAttribute>(type))
            .Where(plugin => plugin != null)
            .Select(plugin => NamedValue<string>(plugin, nameof(TestPluginAttribute.Namespace)))
            .FirstOrDefault();
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
        return member.GetCustomAttributesData().Where(data => data.AttributeType.FullName == attributeName);
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
