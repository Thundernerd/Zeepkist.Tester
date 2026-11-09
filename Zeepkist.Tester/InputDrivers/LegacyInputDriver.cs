using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// Hands keys and a mouse to Unity's own <c>Input</c>, which the level editor's mouse, Escape checks and ZeepSDK's
/// hotkeys read. Harmony prefixes on <c>Input</c> ask it first, so what it holds comes on top of the player's input.
/// Input it's handed in a frame is seen from the next, as the game's <c>Update</c> has read that one's already.
/// </summary>
internal sealed class LegacyInputDriver : IInputDriver
{
    private static readonly (EventModifiers Modifier, KeyCode Key)[] _modifierKeys =
    [
        (EventModifiers.Shift, KeyCode.LeftShift),
        (EventModifiers.Control, KeyCode.LeftControl),
        (EventModifiers.Alt, KeyCode.LeftAlt),
        (EventModifiers.Command, KeyCode.LeftCommand),
    ];

    /// <summary>
    /// Unity's names for the keys this can hold.
    /// </summary>
    private static readonly Dictionary<string, KeyCode> _names = NameKeys();

    /// <summary>
    /// The game's Input Manager's axes that keys push, by name.
    /// </summary>
    private static readonly Dictionary<string, (KeyCode[] Positive, KeyCode[] Negative)> _keyAxes = new Dictionary<string, (KeyCode[] Positive, KeyCode[] Negative)>
    {
        ["Horizontal"] = ([KeyCode.RightArrow, KeyCode.D], [KeyCode.LeftArrow, KeyCode.A]),
        ["Vertical"] = ([KeyCode.UpArrow, KeyCode.W], [KeyCode.DownArrow, KeyCode.S]),
    };

    /// <summary>
    /// The game's Input Manager's mouse axes, by name. It has them both in lower case and capitalised, with different sensitivities.
    /// </summary>
    private static readonly Dictionary<string, (bool Sideways, float Sensitivity)> _mouseAxes = new Dictionary<string, (bool Sideways, float Sensitivity)>
    {
        ["mouse x"] = (true, 1),
        ["mouse y"] = (false, 1),
        ["Mouse X"] = (true, 0.1f),
        ["Mouse Y"] = (false, 0.1f),
    };

    private readonly Func<int> _frame;
    private readonly Dictionary<KeyCode, int> _heldKeys = [];
    private readonly Dictionary<KeyCode, int> _downFrames = [];
    private readonly Dictionary<KeyCode, int> _upFrames = [];
    private (int Frame, float X, float Y) _mouseDelta;

    /// <param name="frame">Unity's <c>Time.frameCount</c>.</param>
    public LegacyInputDriver(Func<int> frame)
    {
        _frame = frame;
    }

    public bool PressKeys(IReadOnlyList<KeyPress> keys)
    {
        var taken = false;
        foreach (var key in keys)
        {
            foreach (var code in KeysOf(key))
            {
                Press(code);
                taken = true;
            }
        }

        return taken;
    }

    public bool ReleaseKeys(IReadOnlyList<KeyPress> keys)
    {
        foreach (var key in keys)
        {
            foreach (var code in KeysOf(key))
                Release(code);
        }

        return true;
    }

    public bool MoveMouse(Vector2 screenPoint)
    {
        // Its fields are set one by one because the unit tests' UnityEngine has no method bodies
        MousePosition = new Vector3 { x = screenPoint.x, y = screenPoint.y };
        return true;
    }

    public bool PressButton(MouseButton button)
    {
        Press(KeyOf(button));
        return true;
    }

    /// <remarks>Unity's <c>Input</c> has no drag of its own: the button stays held from <see cref="PressButton"/>.</remarks>
    public bool DragMouse(Vector2 delta, MouseButton button)
    {
        // The game's Update reads this frame's in the next
        var seenIn = _frame() + 1;
        _mouseDelta = _mouseDelta.Frame == seenIn
            ? (seenIn, _mouseDelta.X + delta.x, _mouseDelta.Y + delta.y)
            : (seenIn, delta.x, delta.y);

        var from = MousePosition ?? default;
        MousePosition = new Vector3 { x = from.x + delta.x, y = from.y + delta.y };
        return true;
    }

    public bool ReleaseButton(MouseButton button)
    {
        Release(KeyOf(button));
        return true;
    }

    public void GiveBack()
    {
        // What the game hasn't seen go down never does, so giving back after a press isn't a click
        foreach (var key in _heldKeys.Keys.ToList())
        {
            _heldKeys.Remove(key);
            if (_downFrames[key] > _frame())
                _downFrames.Remove(key);
            else
                _upFrames[key] = _frame() + 1;
        }

        _mouseDelta = default;
        MousePosition = null;
    }

    /// <summary>
    /// A key or mouse button is held, as Unity counts them.
    /// </summary>
    public bool AnyKey => _downFrames.Keys.Any(GetKey);

    public bool AnyKeyDown => _downFrames.Keys.Any(GetKeyDown);

    /// <summary>
    /// Where the mouse is, in screen pixels counted from the bottom left, or null while it's the player's.
    /// </summary>
    public Vector3? MousePosition { get; private set; }

    /// <param name="button">0 for the left button, as Unity counts them.</param>
    public bool GetMouseButton(int button)
    {
        return GetKey(KeyCode.Mouse0 + button);
    }

    /// <param name="button">0 for the left button, as Unity counts them.</param>
    public bool GetMouseButtonDown(int button)
    {
        return GetKeyDown(KeyCode.Mouse0 + button);
    }

    /// <param name="button">0 for the left button, as Unity counts them.</param>
    public bool GetMouseButtonUp(int button)
    {
        return GetKeyUp(KeyCode.Mouse0 + button);
    }

    public bool GetKey(KeyCode key)
    {
        if (!_downFrames.TryGetValue(key, out var down) || _frame() < down)
            return false;

        // Released keys are held until the frame that sees them go up
        return _heldKeys.ContainsKey(key) || _frame() < _upFrames[key];
    }

    public bool GetKeyDown(KeyCode key)
    {
        return _downFrames.TryGetValue(key, out var frame) && frame == _frame();
    }

    public bool GetKeyUp(KeyCode key)
    {
        return _upFrames.TryGetValue(key, out var frame) && frame == _frame();
    }

    /// <summary>
    /// One of the game's axes in its Input Manager, as the player's with what this adds to it. A key axis adds -1, 0 or 1
    /// as it snaps, and stays within 1 either way; a mouse axis adds how far the drags moved the mouse in the frame before,
    /// times the axis's sensitivity. An axis it doesn't know is the player's.
    /// </summary>
    public float Axis(string name, float player)
    {
        name ??= string.Empty;
        if (_keyAxes.TryGetValue(name, out var keys))
        {
            var pushed = (keys.Positive.Any(GetKey) ? 1 : 0) - (keys.Negative.Any(GetKey) ? 1 : 0);
            return Math.Max(-1, Math.Min(1, player + pushed));
        }

        if (!_mouseAxes.TryGetValue(name, out var mouse) || _mouseDelta.Frame != _frame())
            return player;

        return player + ((mouse.Sideways ? _mouseDelta.X : _mouseDelta.Y) * mouse.Sensitivity);
    }

    /// <summary>
    /// The key Unity names as this, such as <c>left shift</c> or <c>page up</c>.
    /// </summary>
    /// <remarks>A name it doesn't know is no key it holds, so Unity's own <c>Input</c> answers, and fails it as before.</remarks>
    public bool GetKey(string name)
    {
        return Named(name) is { } key && GetKey(key);
    }

    public bool GetKeyDown(string name)
    {
        return Named(name) is { } key && GetKeyDown(key);
    }

    public bool GetKeyUp(string name)
    {
        return Named(name) is { } key && GetKeyUp(key);
    }

    private static KeyCode? Named(string name)
    {
        return name != null && _names.TryGetValue(name, out var key) ? key : null;
    }

    /// <summary>
    /// The modifiers' keys, then the key itself. A character without a key of its own has none here.
    /// </summary>
    private static IEnumerable<KeyCode> KeysOf(KeyPress press)
    {
        foreach (var (modifier, key) in _modifierKeys)
        {
            if ((press.Modifiers & modifier) != 0)
                yield return key;
        }

        if (press.Key != KeyCode.None)
            yield return press.Key;
    }

    private static Dictionary<string, KeyCode> NameKeys()
    {
        var names = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = KeyCode.Space,
            ["return"] = KeyCode.Return,
            ["tab"] = KeyCode.Tab,
            ["escape"] = KeyCode.Escape,
            ["backspace"] = KeyCode.Backspace,
            ["delete"] = KeyCode.Delete,
            ["up"] = KeyCode.UpArrow,
            ["down"] = KeyCode.DownArrow,
            ["left"] = KeyCode.LeftArrow,
            ["right"] = KeyCode.RightArrow,
            ["home"] = KeyCode.Home,
            ["end"] = KeyCode.End,
            ["page up"] = KeyCode.PageUp,
            ["page down"] = KeyCode.PageDown,
            ["left shift"] = KeyCode.LeftShift,
            ["left ctrl"] = KeyCode.LeftControl,
            ["left alt"] = KeyCode.LeftAlt,
            ["left cmd"] = KeyCode.LeftCommand,
            ["/"] = KeyCode.Slash,
            ["\\"] = KeyCode.Backslash,
            ["."] = KeyCode.Period,
            [","] = KeyCode.Comma,
            [";"] = KeyCode.Semicolon,
            ["'"] = KeyCode.Quote,
            ["["] = KeyCode.LeftBracket,
            ["]"] = KeyCode.RightBracket,
            ["-"] = KeyCode.Minus,
            ["="] = KeyCode.Equals,
            ["`"] = KeyCode.BackQuote,
        };

        for (var letter = 'a'; letter <= 'z'; letter++)
            names[letter.ToString()] = KeyCode.A + (letter - 'a');

        for (var digit = 0; digit <= 9; digit++)
        {
            names[digit.ToString()] = KeyCode.Alpha0 + digit;
            if (digit <= 6)
                names[$"mouse {digit}"] = KeyCode.Mouse0 + digit;
        }

        return names;
    }

    /// <summary>
    /// Unity's mouse buttons are keys too, from <see cref="KeyCode.Mouse0"/> for the left one.
    /// </summary>
    private static KeyCode KeyOf(MouseButton button)
    {
        return KeyCode.Mouse0 + (int)button;
    }

    private void Press(KeyCode key)
    {
        _heldKeys[key] = _heldKeys.TryGetValue(key, out var count) ? count + 1 : 1;
        _downFrames[key] = _frame() + 1;
    }

    /// <summary>
    /// A key pressed more than once stays held until each press is released.
    /// </summary>
    private void Release(KeyCode key)
    {
        if (!_heldKeys.TryGetValue(key, out var count))
            return;

        if (count > 1)
        {
            _heldKeys[key] = count - 1;
            return;
        }

        _heldKeys.Remove(key);
        _upFrames[key] = _frame() + 1;
    }
}
