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
    private readonly IReadOnlyList<ISettings> _types;

    /// <summary>
    /// Any of them can be null, as a block's are when it has no setting of that type.
    /// </summary>
    public BlockSettings(Dictionary<string, int> whole, Dictionary<string, float> decimals, Dictionary<string, string> text)
    {
        _types =
        [
            new Settings<int>(whole, TryReadWhole, number => number.ToString(CultureInfo.InvariantCulture), "a whole number, true or false"),
            new Settings<float>(decimals, TryReadDecimal, number => number.ToString("R", CultureInfo.InvariantCulture), "a number"),
            new Settings<string>(text, TryReadText, text => text, takes: null),
        ];
    }

    /// <summary>
    /// The settings of one type: their values, how text becomes one and back, and what the type takes, for a value that
    /// doesn't fit it.
    /// </summary>
    private interface ISettings
    {
        public IEnumerable<string> Keys { get; }

        public bool Has(string key);

        /// <summary>
        /// Writes <paramref name="value"/> under <paramref name="key"/> when it fits, or gives why it doesn't.
        /// </summary>
        public string Write(string key, string value);

        public string Read(string key);
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
        var type = _types.FirstOrDefault(settings => settings.Has(key));
        if (type == null)
        {
            var keys = _types.SelectMany(settings => settings.Keys).OrderBy(name => name, StringComparer.Ordinal).ToList();
            problem = $"The block has no setting '{key}'. It has {(keys.Count == 0 ? "none" : string.Join(", ", keys))}.";
            return null;
        }

        problem = type.Write(key, value);
        return problem == null ? type.Read(key) : null;
    }

    /// <summary>
    /// What the block holds under <paramref name="key"/> as text, or null when it holds nothing there.
    /// </summary>
    public string Read(string key)
    {
        return _types.FirstOrDefault(settings => settings.Has(key))?.Read(key);
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

    private static bool TryReadDecimal(string value, out float number)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryReadText(string value, out string text)
    {
        text = value;
        return true;
    }

    private delegate bool TryRead<T>(string value, out T read);

    private sealed class Settings<T> : ISettings
    {
        private readonly Dictionary<string, T> _values;
        private readonly TryRead<T> _tryRead;
        private readonly Func<T, string> _format;
        private readonly string _takes;

        public Settings(Dictionary<string, T> values, TryRead<T> tryRead, Func<T, string> format, string takes)
        {
            _values = values;
            _tryRead = tryRead;
            _format = format;
            _takes = takes;
        }

        public IEnumerable<string> Keys => _values?.Keys ?? Enumerable.Empty<string>();

        public bool Has(string key)
        {
            return _values?.ContainsKey(key) == true;
        }

        public string Write(string key, string value)
        {
            if (!_tryRead(value, out var read))
                return $"{key} takes {_takes}, not '{value}'";

            _values[key] = read;
            return null;
        }

        public string Read(string key)
        {
            return _format(_values[key]);
        }
    }
}
