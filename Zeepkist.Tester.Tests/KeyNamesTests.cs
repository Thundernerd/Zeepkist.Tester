using System.Linq;
using TNRD.Zeepkist.Tester.InputDrivers;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class KeyNamesTests
{
    [Theory]
    [InlineData("enter", KeyCode.Return, '\n')]
    [InlineData("space", KeyCode.Space, ' ')]
    [InlineData("tab", KeyCode.Tab, '\t')]
    [InlineData("escape", KeyCode.Escape, '\0')]
    [InlineData("backspace", KeyCode.Backspace, '\0')]
    [InlineData("delete", KeyCode.Delete, '\0')]
    [InlineData("up", KeyCode.UpArrow, '\0')]
    [InlineData("down", KeyCode.DownArrow, '\0')]
    [InlineData("left", KeyCode.LeftArrow, '\0')]
    [InlineData("right", KeyCode.RightArrow, '\0')]
    [InlineData("home", KeyCode.Home, '\0')]
    [InlineData("end", KeyCode.End, '\0')]
    [InlineData("pageup", KeyCode.PageUp, '\0')]
    [InlineData("pagedown", KeyCode.PageDown, '\0')]
    [InlineData("a", KeyCode.A, 'a')]
    [InlineData("7", KeyCode.Alpha7, '7')]
    [InlineData("/", KeyCode.Slash, '/')]
    [InlineData("\\", KeyCode.Backslash, '\\')]
    [InlineData(".", KeyCode.Period, '.')]
    [InlineData(",", KeyCode.Comma, ',')]
    [InlineData(";", KeyCode.Semicolon, ';')]
    [InlineData("'", KeyCode.Quote, '\'')]
    [InlineData("[", KeyCode.LeftBracket, '[')]
    [InlineData("]", KeyCode.RightBracket, ']')]
    [InlineData("-", KeyCode.Minus, '-')]
    [InlineData("=", KeyCode.Equals, '=')]
    [InlineData("`", KeyCode.BackQuote, '`')]
    public void AKeysName_GivesItsKeyAndCharacter(string name, KeyCode key, char character)
    {
        Assert.Matches(Keys.Pattern, name);

        var press = Assert.Single(KeyNames.Parse(name));

        Assert.Equal(key, press.Key);
        Assert.Equal(character, press.Character);
        Assert.Equal(EventModifiers.None, press.Modifiers);
    }

    [Fact]
    public void Modifiers_AddUp()
    {
        var press = Assert.Single(KeyNames.Parse("ctrl+shift+alt+cmd+space"));

        Assert.Equal(KeyCode.Space, press.Key);
        Assert.Equal(EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt | EventModifiers.Command, press.Modifiers);
    }

    [Theory]
    [InlineData("shift+a", 'A')]
    [InlineData("shift+/", '/')]
    public void ShiftMakesALetterUpperCase_AndLeavesOtherCharactersAsTheyAre(string name, char character)
    {
        Assert.Equal(character, Assert.Single(KeyNames.Parse(name)).Character);
    }

    [Theory]
    [InlineData("ctrl+a")]
    [InlineData("alt+a")]
    [InlineData("cmd+a")]
    [InlineData("ctrl+shift+a")]
    [InlineData("ctrl+space")]
    [InlineData("ctrl+enter")]
    public void ACommandChord_TypesNothing(string name)
    {
        Assert.Equal('\0', Assert.Single(KeyNames.Parse(name)).Character);
    }

    [Fact]
    public void KeysAreInTheOrderTheyreWritten()
    {
        var presses = KeyNames.Parse("ctrl+space escape a");

        Assert.Equal([KeyCode.Space, KeyCode.Escape, KeyCode.A], presses.Select(press => press.Key));
    }

    [Theory]
    [InlineData("nosuchkey", "nosuchkey")]
    [InlineData("hyper+a", "hyper+a")]
    [InlineData("a !", "!")]
    public void AKeyItDoesntKnow_FailsTheStep_NamingIt(string keys, string named)
    {
        var failure = Assert.Throws<StepFailedException>(() => KeyNames.Parse(keys));

        Assert.Equal($"There's no key '{named}'.", failure.Message);
    }

    [Fact]
    public void EightKeys_FitInAFrame()
    {
        Assert.Matches(Keys.Pattern, "a b c d e f g h");
        Assert.Equal(8, KeyNames.Parse("a b c d e f g h").Count);
    }

    [Fact]
    public void NineKeys_DontFitInAFrame()
    {
        Assert.DoesNotMatch(Keys.Pattern, "a b c d e f g h i");

        var failure = Assert.Throws<StepFailedException>(() => KeyNames.Parse("a b c d e f g h i"));

        Assert.Equal("At most 8 keys fit in a frame, and this is 9: 'a b c d e f g h i'.", failure.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void NoKeys_FailTheStep(string keys)
    {
        Assert.Throws<StepFailedException>(() => KeyNames.Parse(keys));
    }

    [Theory]
    [InlineData('a', KeyCode.A, EventModifiers.None)]
    [InlineData('A', KeyCode.A, EventModifiers.Shift)]
    [InlineData('5', KeyCode.Alpha5, EventModifiers.None)]
    [InlineData(' ', KeyCode.Space, EventModifiers.None)]
    [InlineData('/', KeyCode.Slash, EventModifiers.None)]
    [InlineData('\n', KeyCode.Return, EventModifiers.None)]
    [InlineData('\t', KeyCode.Tab, EventModifiers.None)]
    public void ACharacter_ComesWithTheKeyThatTypesIt(char character, KeyCode key, EventModifiers modifiers)
    {
        var press = KeyNames.ForCharacter(character);

        Assert.Equal(key, press.Key);
        Assert.Equal(character, press.Character);
        Assert.Equal(modifiers, press.Modifiers);
    }

    [Theory]
    [InlineData('(')]
    [InlineData('é')]
    public void ACharacterWithoutAKeyOfItsOwn_ComesAsItsCharacterAlone(char character)
    {
        var press = KeyNames.ForCharacter(character);

        Assert.Equal(KeyCode.None, press.Key);
        Assert.Equal(character, press.Character);
    }

    [Fact]
    public void ThePatternStopsAtWhatKeyNamesReads()
    {
        Assert.DoesNotMatch(Keys.Pattern, "!");
        Assert.DoesNotMatch(Keys.Pattern, "Enter");
    }
}
