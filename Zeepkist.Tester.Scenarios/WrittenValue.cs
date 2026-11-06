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
    /// What the text says for its type: an int for a whole number or pixels, a float for a number or seconds, a bool, the
    /// index of an enum's value, or else the text itself.
    /// </summary>
    /// <exception cref="ScenarioException">The value doesn't fit its argument.</exception>
    public object Read()
    {
        switch (_type.Kind)
        {
            case ArgumentKind.Int:
            case ArgumentKind.Pixels:
                return ToInt();
            case ArgumentKind.Float:
                return ToFloat();
            case ArgumentKind.Seconds:
                return ToSeconds();
            case ArgumentKind.Bool:
                return ToBool();
            case ArgumentKind.Enum:
                return IndexInEnum();
            default:
                RequirePattern();
                return _text;
        }
    }

    private int ToInt()
    {
        if (!int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            throw Fail($"{_what} takes a whole number, not '{_text}'.");

        RequireRange(number);
        return number;
    }

    private float ToFloat()
    {
        if (!TryParseFloat(_text, out var number))
            throw Fail($"{_what} takes a number, not '{_text}'.");

        RequireRange(number);
        return (float)number;
    }

    private float ToSeconds()
    {
        var match = _seconds.Match(_text);
        if (!match.Success || !TryParseFloat(match.Groups["number"].Value, out var number))
            throw Fail($"{_what} takes a length of time such as 2, 0.5 or 500ms, not '{_text}'.");

        var seconds = match.Groups["unit"].Value == "ms" ? number / 1000 : number;
        RequireRange(seconds);
        return (float)seconds;
    }

    private bool ToBool()
    {
        return _text == "true" || (_text == "false" ? false : throw Fail($"{_what} takes true or false, not '{_text}'."));
    }

    private int IndexInEnum()
    {
        var index = _type.EnumValues.ToList().IndexOf(_text);
        if (index < 0)
            throw Fail($"{_what} takes {Choices(_type.EnumValues)}, not '{_text}'.");

        return index;
    }

    /// <summary>
    /// A list's items have their pattern on their own type, unless the argument gives one.
    /// </summary>
    private void RequirePattern()
    {
        var pattern = _argument.Pattern ?? _type.Pattern;
        if (pattern != null && !Regex.IsMatch(_text, ToEndOfText(pattern)))
            throw Fail($"{_what} has to match {pattern}, not '{_text}'.");
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
