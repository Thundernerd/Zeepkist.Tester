using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// Writes a <c>.zlstest</c> file that was read as YAML: each step in its shortest form, its comments where they were,
/// and its mods' namespaces under <c>use:</c>.
/// </summary>
internal static class LineScenarioConverter
{
    private const string StepIndent = "  ";

    // Each of these reads as something other than text to some YAML reader
    private static readonly string[] _keywords = ["true", "false", "yes", "no", "on", "off", "y", "n", "null", "~"];

    /// <param name="scenario">The file as it was read.</param>
    /// <param name="steps">What it was read against, which tells which names need their namespace.</param>
    public static string Write(LineScenario scenario, IReadOnlyList<StepDescriptor> steps)
    {
        var yaml = new StringBuilder();
        var written = scenario.Entries.Where(entry => entry.Step != null).Select(entry => entry.Step.Step).Distinct().ToList();
        var use = written.Where(step => step.Namespace.Length > 0).Select(step => step.Namespace).Distinct().ToList();
        if (use.Count > 0)
            yaml.Append("use: [").Append(string.Join(", ", use)).Append("]\n");

        yaml.Append("steps:\n");
        var lastWasBlank = true;
        foreach (var entry in TrimBlanks(scenario.Entries))
        {
            if (entry.Step != null)
                WriteStep(yaml, entry.Step, NameFor(entry.Step.Step, steps, use));
            else if (entry.Comment != null)
                yaml.Append(StepIndent).Append('#').Append(entry.Comment).Append('\n');
            else if (!lastWasBlank)
                yaml.Append('\n');

            lastWasBlank = entry == LineEntry.Blank;
        }

        return yaml.ToString();
    }

    private static IEnumerable<LineEntry> TrimBlanks(IReadOnlyList<LineEntry> entries)
    {
        var last = entries.Count - 1;
        while (last >= 0 && entries[last] == LineEntry.Blank)
            last--;

        return entries.Take(last + 1);
    }

    /// <summary>
    /// A mod's step is written without its namespace unless that would mean the tester's step or another of <paramref name="use"/>'s.
    /// </summary>
    private static string NameFor(StepDescriptor step, IReadOnlyList<StepDescriptor> steps, IReadOnlyList<string> use)
    {
        if (step.Namespace.Length == 0)
            return step.Name;

        var clashes = steps.Any(other => other != step && other.Name == step.Name && (other.Namespace.Length == 0 || use.Contains(other.Namespace)));
        return clashes ? step.FullName : step.Name;
    }

    private static void WriteStep(StringBuilder yaml, StepNode node, string name)
    {
        yaml.Append(StepIndent).Append("- ").Append(name);
        var values = node.Arguments.Values;
        if (values.Count == 0)
        {
            yaml.Append('\n');
            return;
        }

        yaml.Append(": ");
        var primary = node.Step.Arguments.FirstOrDefault(argument => argument.IsPrimary);
        if (values.Count == 1 && primary != null && values.TryGetValue(primary.Name, out var value))
        {
            // A primary list of one item is written as the item
            if (primary.Type.Kind == ArgumentKind.List && value is ListArgument { Items.Count: 1 } list)
                WriteBlockValue(yaml, primary.Type.ItemType, list.Items[0]);
            else
                WriteBlockValue(yaml, primary.Type, value);
        }
        else
        {
            yaml.Append(FlowObject(node.Step.Arguments, node.Arguments));
        }

        yaml.Append('\n');
    }

    /// <summary>
    /// A step's value after its name: text of several lines as a <c>|-</c> block, and anything else in flow style.
    /// </summary>
    private static void WriteBlockValue(StringBuilder yaml, ArgumentType type, ArgumentValue value)
    {
        if (value is ScalarArgument scalar && CanBeABlock(scalar.Text))
        {
            yaml.Append("|-");
            foreach (var line in scalar.Text.Split('\n'))
                yaml.Append('\n').Append(line.Length == 0 ? string.Empty : StepIndent + "    " + line);

            return;
        }

        yaml.Append(value is ScalarArgument single ? Scalar(type, single.Text, false) : Flow(type, value));
    }

    /// <summary>
    /// A <c>|-</c> block keeps each line as it is, but can't start with an indented line or end with a blank one.
    /// </summary>
    private static bool CanBeABlock(string text)
    {
        return text.Contains('\n') && !text.StartsWith(' ') && !text.EndsWith('\n') && !text.Any(c => char.IsControl(c) && c != '\n');
    }

    private static string Flow(ArgumentType type, ArgumentValue value)
    {
        var itemType = type.Kind == ArgumentKind.List ? type.ItemType : type;
        return value switch
        {
            ListArgument list => type.Kind == ArgumentKind.List
                ? $"[{string.Join(", ", list.Items.Select(item => Flow(itemType, item)))}]"
                : Flow(itemType, list.Items[0]),
            ObjectArgument nested => FlowNested(type.Arguments, nested),
            _ => Scalar(type, ((ScalarArgument)value).Text, true),
        };
    }

    /// <summary>
    /// A nested class with only its primary argument is written as that argument.
    /// </summary>
    private static string FlowNested(IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument nested)
    {
        var primary = arguments.FirstOrDefault(argument => argument.IsPrimary);
        if (primary != null && nested.Values.Count == 1 && nested.Values.TryGetValue(primary.Name, out var value) && primary.Type.Kind != ArgumentKind.List)
            return Flow(primary.Type, value);

        return FlowObject(arguments, nested);
    }

    private static string FlowObject(IReadOnlyList<ArgumentDescriptor> arguments, ObjectArgument values)
    {
        var entries = arguments.Where(argument => values.Values.ContainsKey(argument.Name)).Select(argument => $"{argument.Name}: {Flow(argument.Type, values.Values[argument.Name])}");
        return $"{{ {string.Join(", ", entries)} }}";
    }

    /// <summary>
    /// Plain when every YAML reader reads it back as this text, and quoted otherwise.
    /// </summary>
    /// <param name="type">Its argument's type: a bool's own true and false stay plain.</param>
    /// <param name="text">The value.</param>
    /// <param name="inFlow">Whether it's inside <c>{ }</c> or <c>[ ]</c>, where <c>, [ ] { }</c> also end a plain value.</param>
    private static string Scalar(ArgumentType type, string text, bool inFlow)
    {
        if (IsPlain(text, inFlow) && (type.Kind == ArgumentKind.Bool || !_keywords.Contains(text.ToLowerInvariant())))
            return text;

        return text.Any(char.IsControl) ? DoubleQuoted(text) : $"'{text.Replace("'", "''")}'";
    }

    private static bool IsPlain(string text, bool inFlow)
    {
        if (IsNegativeNumber(text))
            return true;

        return text.Length > 0
            && text.Trim() == text
            && "-?:,[]{}#&*!|>'\"%@`".IndexOf(text[0]) < 0
            && text.IndexOfAny([':', '#', '"', '\'']) < 0
            && (!inFlow || text.IndexOfAny([',', '[', ']', '{', '}']) < 0)
            && !text.Any(char.IsControl);
    }

    private static bool IsNegativeNumber(string text)
    {
        return text.StartsWith('-') && double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
    }

    private static string DoubleQuoted(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\t' => "\\t",
                _ when char.IsControl(c) => $"\\x{(int)c:x2}",
                _ => c.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }
}
