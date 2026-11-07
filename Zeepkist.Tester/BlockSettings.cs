using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// A block's settings as the level file stores them: each under its key, in the dictionary of its type. A new block already
/// has all of its own, so that's what says which type a setting is.
/// </summary>
internal sealed class BlockSettings
{
    private readonly Dictionary<string, int> _whole;
    private readonly Dictionary<string, float> _decimals;
    private readonly Dictionary<string, string> _text;

    /// <summary>
    /// Any of them can be null, as a block's are when it has no setting of that type.
    /// </summary>
    public BlockSettings(Dictionary<string, int> whole, Dictionary<string, float> decimals, Dictionary<string, string> text)
    {
        _whole = whole;
        _decimals = decimals;
        _text = text;
    }

    public static BlockSettings Of(BlockProperties block)
    {
        var properties = block.customBlockEditProperties;
        return new BlockSettings(properties.n, properties.f, properties.t);
    }

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="key"/>. Returns it as <see cref="Read"/> will, or null with why in
    /// <paramref name="problem"/> when the block has no such setting or the value doesn't fit it, which changes nothing.
    /// </summary>
    public string Write(string key, string value, out string problem)
    {
        problem = null;
        if (_whole?.ContainsKey(key) == true)
        {
            if (TryReadWhole(value, out var whole))
            {
                _whole[key] = whole;
                return Read(key);
            }

            problem = $"{key} takes a whole number, true or false, not '{value}'";
        }
        else if (_decimals?.ContainsKey(key) == true)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                _decimals[key] = number;
                return Read(key);
            }

            problem = $"{key} takes a number, not '{value}'";
        }
        else if (_text?.ContainsKey(key) == true)
        {
            _text[key] = value;
            return value;
        }
        else
        {
            var keys = Keys(_whole).Concat(Keys(_decimals)).Concat(Keys(_text)).OrderBy(name => name, StringComparer.Ordinal).ToList();
            problem = $"The block has no setting '{key}'. It has {(keys.Count == 0 ? "none" : string.Join(", ", keys))}.";
        }

        return null;
    }

    /// <summary>
    /// What the block holds under <paramref name="key"/> as text, or null when it holds nothing there.
    /// </summary>
    public string Read(string key)
    {
        if (_whole != null && _whole.TryGetValue(key, out var whole))
            return whole.ToString(CultureInfo.InvariantCulture);

        if (_decimals != null && _decimals.TryGetValue(key, out var number))
            return number.ToString("R", CultureInfo.InvariantCulture);

        return _text != null && _text.TryGetValue(key, out var text) ? text : null;
    }

    private static bool TryReadWhole(string value, out int whole)
    {
        switch (value)
        {
            case "true":
                whole = 1;
                return true;
            case "false":
                whole = 0;
                return true;
            default:
                return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole);
        }
    }

    private static IEnumerable<string> Keys<T>(Dictionary<string, T> dictionary)
    {
        return dictionary?.Keys ?? Enumerable.Empty<string>();
    }
}
