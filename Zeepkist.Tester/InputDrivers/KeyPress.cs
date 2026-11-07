using UnityEngine;

namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// One key as Unity reports it: a key event, then the character it types as an event of its own. A key that types
/// nothing, such as Escape, has <see cref="Character"/> 0; a character with no key of its own has <see cref="Key"/>
/// <see cref="KeyCode.None"/>.
/// </summary>
internal readonly struct KeyPress
{
    public KeyPress(KeyCode key, char character, EventModifiers modifiers)
    {
        Key = key;
        Character = character;
        Modifiers = modifiers;
    }

    public KeyCode Key { get; }

    public char Character { get; }

    public EventModifiers Modifiers { get; }
}
