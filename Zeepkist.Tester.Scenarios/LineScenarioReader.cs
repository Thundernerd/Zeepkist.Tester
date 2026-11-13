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
    // The old forms a LineSyntax can't describe, each read by its own method here
    private const string SetText = "set-text";
    private const string End = "end";
    private const string DragWindowEdge = "drag-window-edge";
    private const string DragWindowEdgeSideways = "drag-window-edge-sideways";
    private const string ExpectError = "expect-error";

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

        // A scenario needs steps:, and the old format had no file without them either
        if (!entries.Any(entry => entry.Step != null))
            throw new ScenarioException(new SourceLocation(_file, 1, 1), "The file has no steps.");

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
        switch (name)
        {
            case End:
                throw new ScenarioException(location, $"{End} without {SetText}.");
            case SetText:
                return ReadSetText(lines, ref index, location, argument);
            case DragWindowEdge:
                return ReadDragWindowEdge(location, argument, false);
            case DragWindowEdgeSideways:
                return ReadDragWindowEdge(location, argument, true);
            case ExpectError:
                return ReadExpectError(location, argument);
        }

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

    /// <summary>
    /// The lines after it up to an <c>end</c> at the start of a line, without their shared indent, as ZeepLogicScript read them.
    /// </summary>
    private StepNode ReadSetText(string[] lines, ref int index, SourceLocation location, string argument)
    {
        if (argument.Length > 0)
            throw new ScenarioException(location, $"{SetText} takes nothing after it: the text goes on the lines below it.");

        var start = index + 1;
        var end = Array.FindIndex(lines, start, line => line.TrimEnd() == End);
        if (end < 0)
            throw new ScenarioException(location, $"{SetText} has no {End}.");

        index = end;
        var body = lines.Skip(start).Take(end - start).Select(line => line.TrimEnd()).ToList();
        var indent = body.Where(line => line.Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        var text = string.Join("\n", body.Select(line => line.Length == 0 ? line : line.Substring(indent)));

        var step = Resolve(SetText, location);
        return Checked(step, location, new Dictionary<string, ArgumentValue> { [Primary(step, location).Name] = Scalar(location, text) });
    }

    /// <summary>
    /// <c>&lt;pixels&gt; [over &lt;frames&gt;]</c>, with <c>then</c> before each move after the first, into a list of moves.
    /// <c>drag-window-edge-sideways</c> is the same step with <c>sideways</c>.
    /// </summary>
    private StepNode ReadDragWindowEdge(SourceLocation location, string argument, bool sideways)
    {
        var step = Resolve(DragWindowEdge, location);
        var moves = Primary(step, location);
        var move = moves.Type.Kind == ArgumentKind.List ? moves.Type.ItemType : moves.Type;
        var by = move.Arguments.FirstOrDefault(each => each.IsPrimary)
            ?? throw new ScenarioException(location, $"{step.FullName}'s moves have no primary argument for the pixels.");

        var items = new List<ArgumentValue>();
        foreach (var written in string.Join(" ", argument.Split([' '], StringSplitOptions.RemoveEmptyEntries)).Split([" then "], StringSplitOptions.None))
        {
            var words = written.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (!(words.Length == 1 || (words.Length == 3 && words[1] == "over")))
            {
                var name = sideways ? DragWindowEdgeSideways : DragWindowEdge;
                throw new ScenarioException(location, $"{name} takes the pixels to drag, and optionally 'over <frames>', with 'then' before each move after the first, not '{argument}'.");
            }

            var values = new Dictionary<string, ArgumentValue> { [by.Name] = Scalar(location, words[0]) };
            if (words.Length == 3)
                values.Add("over", Scalar(location, words[2]));

            items.Add(new ObjectArgument(location, values));
        }

        var arguments = new Dictionary<string, ArgumentValue> { [moves.Name] = new ListArgument(location, items) };
        if (sideways)
            arguments.Add("sideways", Scalar(location, "true"));

        return Checked(step, location, arguments);
    }

    /// <summary>
    /// <c>&lt;line&gt;:&lt;column&gt; &lt;text&gt;</c>, where the text is the rest of the line.
    /// </summary>
    private StepNode ReadExpectError(SourceLocation location, string argument)
    {
        var space = argument.IndexOf(' ');
        var position = space < 0 ? [] : argument.Substring(0, space).Split(':');
        if (position.Length != 2)
            throw new ScenarioException(location, $"{ExpectError} takes a line and column, such as 2:5, then the text to look for, not '{argument}'.");

        return Checked(Resolve(ExpectError, location), location, new Dictionary<string, ArgumentValue>
        {
            ["line"] = Scalar(location, position[0]),
            ["column"] = Scalar(location, position[1]),
            ["message"] = Scalar(location, argument.Substring(space + 1).Trim()),
        });
    }

    private static ArgumentDescriptor Primary(StepDescriptor step, SourceLocation location)
    {
        return step.Arguments.FirstOrDefault(argument => argument.IsPrimary)
            ?? throw new ScenarioException(location, $"{step.FullName} has no primary argument to take what the old line wrote.");
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
