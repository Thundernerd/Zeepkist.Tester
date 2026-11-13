using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// The JSON Schema (draft-07) that editors check and complete scenarios with: the header keys, the control nodes, and a
/// <c>oneOf</c> branch per step. It's as strict as the parser where an editor can be, and more lenient where it can't
/// know what the parser does: the parser reads every scalar as text, so a text argument also takes a number.
/// </summary>
internal static class StepSchema
{
    private const string Steps = "#/definitions/steps";

    /// <summary>
    /// What <see cref="Seconds"/> takes as text, as the parser reads it: a number, with or without a unit.
    /// </summary>
    private const string SecondsText = "^[0-9]+(\\.[0-9]+)?(ms|s)?$";

    private static readonly JsonSerializerOptions _indented = new JsonSerializerOptions
    {
        WriteIndented = true,

        // A file for people to read, and no page to put it in, so backticks and quotes stay as they are
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string _namePattern = Name<object>.Pattern;

    /// <summary>
    /// A <c>${name}</c> anywhere in the text.
    /// </summary>
    private static readonly string _substitution = "\\$\\{" + _namePattern.TrimStart('^').TrimEnd('$') + "\\}";

    public static string Write(IEnumerable<StepDescriptor> steps)
    {
        var all = steps.ToList();
        var nodes = all.SelectMany(step => StepBranches(step.FullName, [step]))
            .Concat(UnprefixedSteps(all).SelectMany(group => StepBranches(group.Key, [.. group])))
            .Concat([ForNode(), RepeatNode(), RunNode()]);

        var schema = new JsonObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = "Zeepkist Tester scenario",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["description"] = Documented(Text(), "What the scenario checks, for the results."),
                ["use"] = Use(all),
                ["start"] = Start(),
                ["input"] = Documented(new JsonObject { ["enum"] = Array("in-process", "os") }, "How input reaches the game: `in-process`, the default, or `os`, as a player's keyboard and mouse do."),
                ["steps"] = new JsonObject { ["$ref"] = Steps },
            },
            ["required"] = Array("steps"),
            ["additionalProperties"] = false,
            ["definitions"] = new JsonObject
            {
                ["steps"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["items"] = new JsonObject { ["oneOf"] = new JsonArray([.. nodes]) },
                },
            },
        };

        return schema.ToJsonString(_indented);
    }

    /// <summary>
    /// A mod's steps a scenario may write without their prefix, once <c>use:</c> lists the namespace, by that name. The
    /// schema can't see <c>use:</c>, so it takes them with or without it, except a name the tester's own steps have.
    /// Several mods' steps can have the name, and <c>use:</c> picks one of them.
    /// </summary>
    private static IEnumerable<IGrouping<string, StepDescriptor>> UnprefixedSteps(List<StepDescriptor> steps)
    {
        var core = steps.Where(step => step.Namespace.Length == 0).Select(step => step.Name).ToHashSet();
        return steps.Where(step => step.Namespace.Length > 0 && !core.Contains(step.Name)).GroupBy(step => step.Name);
    }

    /// <summary>
    /// How a scenario can write the step that <paramref name="name"/> stands for, which is one of <paramref name="steps"/>:
    /// a bare string when it needs no arguments, and a map with the name as its key when it takes any.
    /// </summary>
    private static IEnumerable<JsonNode> StepBranches(string name, IReadOnlyList<StepDescriptor> steps)
    {
        var documentation = steps.Count == 1
            ? steps[0].Documentation
            : string.Join("\n\n", steps.Select(step => string.IsNullOrEmpty(step.Documentation) ? $"`{step.FullName}`" : $"`{step.FullName}`: {step.Documentation}"));

        if (steps.Any(step => step.Arguments.All(argument => !argument.IsRequired)))
            yield return Documented(new JsonObject { ["const"] = name }, documentation);

        var values = steps.Where(step => step.Arguments.Count > 0).Select(step => ObjectValue(step.Arguments)).ToList();
        if (values.Count == 0)
            yield break;

        var value = values.Count == 1 ? values[0] : new JsonObject { ["anyOf"] = new JsonArray([.. values]) };
        yield return Documented(SingleKey(name, Documented(value, documentation)), documentation);
    }

    /// <summary>
    /// The value of a step or of a nested class: a map of its arguments, or its primary argument alone.
    /// </summary>
    private static JsonObject ObjectValue(IReadOnlyList<ArgumentDescriptor> arguments)
    {
        var properties = new JsonObject();
        foreach (var argument in arguments)
            properties[argument.Name] = ArgumentValue(argument);

        var map = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };

        var required = arguments.Where(argument => argument.IsRequired).Select(argument => argument.Name).ToArray();
        if (required.Length > 0)
            map["required"] = Array(required);

        var primary = arguments.FirstOrDefault(argument => argument.IsPrimary);
        if (primary == null)
            return map;

        var alone = Value(primary, primary.Type);
        if (primary.Type.Kind == ArgumentKind.List)
            alone = AnyOf(Value(primary, primary.Type.ItemType), alone);

        return AnyOf(alone, map);
    }

    private static JsonObject ArgumentValue(ArgumentDescriptor argument)
    {
        var value = Documented(Value(argument, argument.Type), argument.Documentation);
        if (argument.Default != null)
            value["default"] = JsonSerializer.SerializeToNode(argument.Default);

        return value;
    }

    /// <summary>
    /// A value of <paramref name="type"/>, which is the argument's own type or its list's items, with the argument's checks.
    /// </summary>
    private static JsonObject Value(ArgumentDescriptor argument, ArgumentType type)
    {
        switch (type.Kind)
        {
            case ArgumentKind.List:
                var list = new JsonObject { ["type"] = "array", ["items"] = Value(argument, type.ItemType) };
                if (argument.MinItems != null)
                    list["minItems"] = argument.MinItems;

                return list;
            case ArgumentKind.Object:
                return Documented(ObjectValue(type.Arguments), type.Documentation);
            case ArgumentKind.Int:
            case ArgumentKind.Pixels:
                return OrSubstituted(Ranged(argument, new JsonObject { ["type"] = "integer" }));
            case ArgumentKind.Float:
                return OrSubstituted(Ranged(argument, new JsonObject { ["type"] = "number" }));
            case ArgumentKind.Seconds:
                var number = Ranged(argument, new JsonObject { ["type"] = "number", ["minimum"] = 0 });
                return OrSubstituted(AnyOf(number, new JsonObject { ["type"] = "string", ["pattern"] = SecondsText }));
            case ArgumentKind.Bool:
                return OrSubstituted(new JsonObject { ["type"] = "boolean" });
            case ArgumentKind.Enum:
                return OrSubstituted(new JsonObject { ["enum"] = Array([.. type.EnumValues]) });
            default:
                var pattern = argument.Pattern ?? type.Pattern;
                if (pattern == null)
                    return Text();

                var text = Text();
                text["pattern"] = pattern;
                return OrSubstituted(text);
        }
    }

    private static JsonObject Ranged(ArgumentDescriptor argument, JsonObject number)
    {
        if (argument.Minimum != null)
            number["minimum"] = argument.Minimum;

        if (argument.Maximum != null)
            number["maximum"] = argument.Maximum;

        return number;
    }

    /// <summary>
    /// A value of a type other than text also takes a <c>${name}</c>, which the parser substitutes before it converts it.
    /// </summary>
    private static JsonObject OrSubstituted(JsonObject value)
    {
        return AnyOf(value, new JsonObject { ["type"] = "string", ["pattern"] = _substitution });
    }

    private static JsonObject Use(List<StepDescriptor> steps)
    {
        var namespaces = steps.Select(step => step.Namespace).Where(space => space.Length > 0).Distinct().ToArray();
        var use = new JsonObject { ["type"] = "array" };
        use["items"] = namespaces.Length > 0 ? new JsonObject { ["enum"] = Array(namespaces) } : false;
        return Documented(use, "Namespaces whose steps this scenario writes without their prefix, such as `[zls]`.");
    }

    private static JsonObject Start()
    {
        var bare = StartState.All.Where(state => !state.NeedsArgument).Select(state => state.Name).ToArray();
        var forms = new List<JsonNode> { new JsonObject { ["enum"] = Array(bare) } };
        foreach (var state in StartState.All)
        {
            var arguments = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false };
            if (state.Argument != null)
                arguments["properties"][state.Argument] = Literal();

            if (state.NeedsArgument)
                arguments["required"] = Array(state.Argument);

            forms.Add(SingleKey(state.Name, arguments));
        }

        return Documented(new JsonObject { ["oneOf"] = new JsonArray([.. forms]) }, "Where the scenario starts: `main-menu`, `editor` (the default), `editor: { level: file }` or `free-play: { uid: … }`.");
    }

    private static JsonObject ForNode()
    {
        var variable = new JsonObject
        {
            ["type"] = "object",
            ["minProperties"] = 1,
            ["maxProperties"] = 1,
            ["propertyNames"] = new JsonObject { ["pattern"] = _namePattern },
            ["additionalProperties"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["items"] = Literal() },
        };

        return ControlNode(
            "for",
            Documented(variable, "One variable and its values, such as `{ h: [64, 120] }`. The steps run once for each value, with `${h}` standing for it."),
            ["steps"]);
    }

    private static JsonObject RepeatNode()
    {
        return ControlNode("repeat", Documented(new JsonObject { ["type"] = "integer", ["minimum"] = 1 }, "How many times to run the steps."), ["steps"]);
    }

    private static JsonObject RunNode()
    {
        var with = new JsonObject
        {
            ["type"] = "object",
            ["propertyNames"] = new JsonObject { ["pattern"] = _namePattern },
            ["additionalProperties"] = Text(),
        };

        return ControlNode("run", Documented(Literal(), "A scenario file to run here, relative to this one."), [], ("with", Documented(with, "Named values the file can use as `${name}`.")));
    }

    private static JsonObject ControlNode(string name, JsonObject value, string[] withSteps, params (string Name, JsonObject Value)[] others)
    {
        var properties = new JsonObject { [name] = value };
        foreach (var other in others)
            properties[other.Name] = other.Value;

        var required = new List<string> { name };
        foreach (var steps in withSteps)
        {
            properties[steps] = new JsonObject { ["$ref"] = Steps };
            required.Add(steps);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = Array([.. required]),
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject SingleKey(string key, JsonNode value)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { [key] = value },
            ["required"] = Array(key),
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// A single value as the parser reads it, which is always text, though an editor reads <c>2</c> as a number.
    /// </summary>
    private static JsonObject Text()
    {
        return new JsonObject { ["type"] = Array("string", "number", "boolean") };
    }

    /// <summary>
    /// A single value without a <c>${name}</c>, as control nodes and <c>start:</c> take.
    /// </summary>
    private static JsonObject Literal()
    {
        var literal = Text();
        literal["not"] = new JsonObject { ["type"] = "string", ["pattern"] = "\\$\\{" };
        return literal;
    }

    /// <summary>
    /// Either value, with an <c>anyOf</c> that has nothing else beside it opened up into this one.
    /// </summary>
    private static JsonObject AnyOf(JsonObject first, JsonObject second)
    {
        var either = new JsonArray();
        foreach (var value in new[] { first, second })
        {
            if (value.Count == 1 && value["anyOf"] is JsonArray choices)
            {
                foreach (var choice in choices.ToList())
                {
                    choices.Remove(choice);
                    either.Add(choice);
                }
            }
            else
            {
                either.Add(value);
            }
        }

        return new JsonObject { ["anyOf"] = either };
    }

    private static JsonObject Documented(JsonObject value, string documentation)
    {
        if (!string.IsNullOrEmpty(documentation))
        {
            value["description"] = documentation;
            value["markdownDescription"] = documentation;
        }

        return value;
    }

    private static JsonArray Array(params string[] values)
    {
        return new JsonArray([.. values.Select(value => (JsonNode)value)]);
    }
}
