using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a <c>.zlstest</c> file, ZeepLogicScript's old line format, into the registry's steps. One step per line, the
/// rest of the line read by the step's <see cref="LineSyntaxAttribute"/>, with blank lines and <c>#</c> comments kept.
/// </summary>
internal sealed class LineScenarioReader
{
    private static readonly Regex _oldValues = new Regex(@"\{uid:(?<name>[^}]*)\}|\$\{");

    private readonly string _file;
    private readonly IReadOnlyList<StepDescriptor> _steps;

    private LineScenarioReader(string file, IEnumerable<StepDescriptor> steps)
    {
        _file = file;
        _steps = steps.ToList();
    }

    /// <exception cref="ScenarioException">A line can't be read as a step.</exception>
    public static LineScenario Read(string file, TextReader reader, IEnumerable<StepDescriptor> steps)
    {
        return new LineScenarioReader(file, steps).Read(reader.ReadToEnd());
    }

    private LineScenario Read(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var entries = new List<LineEntry>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                // The split leaves an empty line after the file's last line break
                if (i < lines.Length - 1)
                    entries.Add(LineEntry.Blank);
            }
            else if (line.StartsWith("#", StringComparison.Ordinal))
            {
                entries.Add(LineEntry.ForComment(line.Substring(1)));
            }
            else
            {
                entries.Add(LineEntry.ForStep(ReadStep(lines, ref i)));
            }
        }

        return new LineScenario(_file, entries);
    }

    /// <summary>
    /// Moves <paramref name="index"/> to the step's last line.
    /// </summary>
    private StepNode ReadStep(string[] lines, ref int index)
    {
        var written = lines[index];
        var location = new SourceLocation(_file, index + 1, written.Length - written.TrimStart().Length + 1);
        var line = written.Trim();
        var space = line.IndexOf(' ');
        var name = space < 0 ? line : line.Substring(0, space);
        var argument = space < 0 ? string.Empty : line.Substring(space + 1).Trim();
        var step = Resolve(name, location);

        var syntax = LineSyntax.For(step);
        var matched = syntax.Match(argument)
            ?? throw new ScenarioException(location, $"{name} takes {Describe(step)}, not '{argument}'.");

        var values = new Dictionary<string, ArgumentValue>();
        foreach (var match in matched)
        {
            var descriptor = step.Arguments.First(each => each.Name == match.Key);
            values.Add(match.Key, descriptor.Type.Kind == ArgumentKind.List
                ? new ListArgument(location, [.. match.Value.Words.Select(word => Scalar(location, word))])
                : Scalar(location, match.Value.Text));
        }

        return Checked(step, location, values);
    }

    private StepNode Checked(StepDescriptor step, SourceLocation location, Dictionary<string, ArgumentValue> values)
    {
        var missing = step.Arguments.FirstOrDefault(argument => argument.IsRequired && !values.ContainsKey(argument.Name));
        if (missing != null)
            throw new ScenarioException(location, $"{step.Name} needs the argument '{missing.Name}'.");

        var node = new StepNode(location, step, new ObjectArgument(location, values));
        ValueChecks.Check(node, new Dictionary<string, IReadOnlyList<string>>());
        return node;
    }

    /// <summary>
    /// A mod's step before the tester's: a <c>.zlstest</c> was written against the mod's own list of steps.
    /// </summary>
    private StepDescriptor Resolve(string name, SourceLocation location)
    {
        var mods = _steps.Where(step => step.Name == name && step.Namespace.Length > 0).ToList();
        if (mods.Count > 1)
            throw new ScenarioException(location, $"'{name}' could be {string.Join(" or ", mods.Select(step => step.FullName))}.");

        return mods.FirstOrDefault()
            ?? _steps.FirstOrDefault(step => step.Name == name && step.Namespace.Length == 0)
            ?? throw new ScenarioException(location, $"There's no step '{name}'.");
    }

    /// <summary>
    /// A <c>{uid:x}</c> was a placed block's UID, which <c>${x}</c> is now, and a <c>${</c> written as it is becomes <c>$${</c>.
    /// </summary>
    private static ScalarArgument Scalar(SourceLocation location, string text)
    {
        return new ScalarArgument(location, _oldValues.Replace(text, match => match.Groups["name"].Success ? $"${{{match.Groups["name"].Value}}}" : "$${"));
    }

    private static string Describe(StepDescriptor step)
    {
        if (step.LineSyntax != null)
            return step.LineSyntax;

        var primary = step.Arguments.FirstOrDefault(argument => argument.IsPrimary);
        return primary == null ? "nothing after it" : $"<{primary.Name}...>";
    }
}
