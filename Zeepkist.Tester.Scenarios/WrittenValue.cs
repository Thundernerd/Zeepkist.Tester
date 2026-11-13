using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// One value's text, with what its checks need from the descriptors to say what's wrong with it. These checks make no
/// instances, so they work on steps whose types can't be loaded, as <c>ztest check</c>'s can't.
/// </summary>
internal sealed class WrittenValue
{
    private static readonly Regex _seconds = new Regex(@"^(?<number>[0-9]+(?:\.[0-9]+)?)(?<unit>ms|s)?\z");

    private readonly ArgumentDescriptor _argument;
    private readonly ArgumentType _type;
    private readonly string _text;
    private readonly SourceLocation _location;
    private readonly string _what;

    /// <remarks>The location is null only inside <see cref="ProblemWith"/>, which drops it.</remarks>
    public WrittenValue(ArgumentDescriptor argument, ArgumentType type, string text, SourceLocation location, string what)
    {
        _argument = argument;
        _type = type;
        _text = text;
        _location = location;
        _what = what;
    }

    /// <summary>
    /// The text that <see cref="ArgAttribute.Default"/> stands for.
    /// </summary>
    public static string DefaultText(object value)
    {
        return value is bool flag ? (flag ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What's wrong with text that no file wrote, such as a default, so it has no location to give.
    /// </summary>
    /// <returns>Null when the text fits.</returns>
    public static string ProblemWith(ArgumentDescriptor argument, ArgumentType type, string text, string what)
    {
        try
        {
            new WrittenValue(argument, type, text, null, what).Check();
            return null;
        }
        catch (ScenarioException exception)
        {
            return exception.Problem;
        }
    }

    /// <summary>
    /// Reads the text as its type would, for what doesn't fit.
    /// </summary>
    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public void Check()
    {
        switch (_type.Kind)
        {
            case ArgumentKind.Int:
            case ArgumentKind.Pixels:
                ReadInt();
                break;
            case ArgumentKind.Float:
                ReadFloat();
                break;
            case ArgumentKind.Seconds:
                ReadSeconds();
                break;
            case ArgumentKind.Bool:
                ReadBool();
                break;
            case ArgumentKind.Enum:
                ReadEnumIndex();
                break;
            default:
                ReadText();
                break;
        }
    }

    /// <summary>
    /// A whole number, or pixels.
    /// </summary>
    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public int ReadInt()
    {
        if (!int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw Fail($"{_what} takes a whole number, not '{_text}'.");

        RequireRange(number);
        return number;
    }

    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public float ReadFloat()
    {
        if (!TryParseFloat(_text, out var number))
            throw Fail($"{_what} takes a number, not '{_text}'.");

        RequireRange(number);
        return (float)number;
    }

    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public float ReadSeconds()
    {
        var match = _seconds.Match(_text);
        if (!match.Success || !TryParseFloat(match.Groups["number"].Value, out var number))
            throw Fail($"{_what} takes a length of time such as 2, 0.5 or 500ms, not '{_text}'.");

        var seconds = match.Groups["unit"].Value == "ms" ? number / 1000 : number;
        RequireRange(seconds);
        return (float)seconds;
    }

    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public bool ReadBool()
    {
        return _text == "true" || (_text == "false" ? false : throw Fail($"{_what} takes true or false, not '{_text}'."));
    }

    /// <summary>
    /// The index of the enum's value, in the order of <see cref="ArgumentType.EnumValues"/>.
    /// </summary>
    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public int ReadEnumIndex()
    {
        var index = _type.EnumValues.ToList().IndexOf(_text);
        if (index < 0)
            throw Fail($"{_what} takes {Choices(_type.EnumValues)}, not '{_text}'.");

        return index;
    }

    /// <summary>
    /// The text itself, for every kind that holds text. A list's items have their pattern on their own type, unless the argument gives one.
    /// </summary>
    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public string ReadText()
    {
        var pattern = _argument.Pattern ?? _type.Pattern;
        if (pattern != null && !Regex.IsMatch(_text, ToEndOfText(pattern)))
            throw Fail($"{_what} has to match {pattern}, not '{_text}'.");

        return _text;
    }

    private void RequireRange(double number)
    {
        if (number < _argument.Minimum || number > _argument.Maximum)
            throw Fail($"{_what} has to be between {_argument.Minimum.Value.ToString(CultureInfo.InvariantCulture)} and {_argument.Maximum.Value.ToString(CultureInfo.InvariantCulture)}, not '{_text}'.");
    }

    private ScenarioException Fail(string problem)
    {
        return new ScenarioException(_location, problem);
    }

    /// <summary>
    /// Reads a float as a double, so a range compares what was written and not the nearest float to it.
    /// </summary>
    private static bool TryParseFloat(string text, out double number)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !float.IsNaN((float)number) && !float.IsInfinity((float)number);
    }

    /// <summary>
    /// A pattern is written for the schema too, where <c>$</c> is the end of the text. Here it also matches before a line break at the end, as a <c>|</c> block has.
    /// </summary>
    private static string ToEndOfText(string pattern)
    {
        return pattern.EndsWith("$", StringComparison.Ordinal) && !pattern.EndsWith(@"\$", StringComparison.Ordinal)
            ? pattern.Substring(0, pattern.Length - 1) + @"\z"
            : pattern;
    }

    private static string Choices(IReadOnlyList<string> values)
    {
        return values.Count < 2 ? string.Join(string.Empty, values) : $"{string.Join(", ", values.Take(values.Count - 1))} or {values[values.Count - 1]}";
    }
}
