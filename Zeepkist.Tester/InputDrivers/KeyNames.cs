using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Reads <see cref="Keys"/> as a scenario writes them, such as <c>enter</c>, <c>ctrl+space</c> or <c>ctrl+/</c>, and finds
/// the key that types a character. Characters are those of a US keyboard.
/// </summary>
internal static class KeyNames
{
    private static readonly Dictionary<string, EventModifiers> _modifiers = new Dictionary<string, EventModifiers>
    {
        ["shift"] = EventModifiers.Shift,
        ["ctrl"] = EventModifiers.Control,
        ["alt"] = EventModifiers.Alt,
        ["cmd"] = EventModifiers.Command,
    };

    private static readonly Dictionary<string, (KeyCode Key, char Character)> _named = new Dictionary<string, (KeyCode Key, char Character)>
    {
        ["space"] = (KeyCode.Space, ' '),
        ["enter"] = (KeyCode.Return, '\n'),
        ["tab"] = (KeyCode.Tab, '\t'),
        ["escape"] = (KeyCode.Escape, '\0'),
        ["backspace"] = (KeyCode.Backspace, '\0'),
        ["delete"] = (KeyCode.Delete, '\0'),
        ["up"] = (KeyCode.UpArrow, '\0'),
        ["down"] = (KeyCode.DownArrow, '\0'),
        ["left"] = (KeyCode.LeftArrow, '\0'),
        ["right"] = (KeyCode.RightArrow, '\0'),
        ["home"] = (KeyCode.Home, '\0'),
        ["end"] = (KeyCode.End, '\0'),
        ["pageup"] = (KeyCode.PageUp, '\0'),
        ["pagedown"] = (KeyCode.PageDown, '\0'),
    };

    private static readonly Dictionary<char, KeyCode> _symbols = new Dictionary<char, KeyCode>
    {
        [' '] = KeyCode.Space,
        ['\n'] = KeyCode.Return,
        ['\t'] = KeyCode.Tab,
        ['/'] = KeyCode.Slash,
        ['\\'] = KeyCode.Backslash,
        ['.'] = KeyCode.Period,
        [','] = KeyCode.Comma,
        [';'] = KeyCode.Semicolon,
        ['\''] = KeyCode.Quote,
        ['['] = KeyCode.LeftBracket,
        [']'] = KeyCode.RightBracket,
        ['-'] = KeyCode.Minus,
        ['='] = KeyCode.Equals,
        ['`'] = KeyCode.BackQuote,
    };

    /// <summary>
    /// Shift with a letter types it in upper case, and Ctrl, Alt or Cmd with a key types nothing, as a field would otherwise
    /// put the letter of <c>ctrl+a</c> over what that selects. A step's argument has passed <see cref="Keys.Pattern"/> already, but
    /// a step can also build its own.
    /// </summary>
    /// <exception cref="StepFailedException">There are no keys, too many for a frame, or one it doesn't know.</exception>
    public static IReadOnlyList<KeyPress> Parse(Keys keys)
    {
        var names = (keys.Value ?? string.Empty).Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0)
            throw new StepFailedException("There are no keys to press.");

        if (names.Length > Keys.MaxPerFrame)
            throw new StepFailedException($"At most {Keys.MaxPerFrame} keys fit in a frame, and this is {names.Length}: '{keys.Value}'.");

        return names.Select(ParseOne).ToList();
    }

    /// <summary>
    /// The key that types the character, with Shift for an upper-case letter. A character without a key of its own here,
    /// such as <c>(</c>, comes as its character event alone.
    /// </summary>
    public static KeyPress ForCharacter(char character)
    {
        var lower = char.ToLowerInvariant(character);
        var shift = lower != character ? EventModifiers.Shift : EventModifiers.None;
        return new KeyPress(FindKey(lower) ?? KeyCode.None, character, shift);
    }

    private static KeyPress ParseOne(string written)
    {
        var parts = written.ToLowerInvariant().Split('+');
        var modifiers = EventModifiers.None;
        foreach (var part in parts.Take(parts.Length - 1))
        {
            if (!_modifiers.TryGetValue(part, out var modifier))
                throw new StepFailedException($"There's no key '{written}'.");

            modifiers |= modifier;
        }

        var name = parts[parts.Length - 1];
        var typesNothing = (modifiers & (EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != 0;
        if (_named.TryGetValue(name, out var named))
            return new KeyPress(named.Key, typesNothing ? '\0' : named.Character, modifiers);

        if (name.Length != 1 || FindKey(name[0]) is not { } key)
            throw new StepFailedException($"There's no key '{written}'.");

        var character = (modifiers & EventModifiers.Shift) != 0 ? char.ToUpperInvariant(name[0]) : name[0];
        return new KeyPress(key, typesNothing ? '\0' : character, modifiers);
    }

    /// <summary>
    /// Null for a character without a key of its own here.
    /// </summary>
    private static KeyCode? FindKey(char character)
    {
        return character switch
        {
            >= 'a' and <= 'z' => KeyCode.A + (character - 'a'),
            >= '0' and <= '9' => KeyCode.Alpha0 + (character - '0'),
            _ => _symbols.TryGetValue(character, out var key) ? key : null,
        };
    }
}
