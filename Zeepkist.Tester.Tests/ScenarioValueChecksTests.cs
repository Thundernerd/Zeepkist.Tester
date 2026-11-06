using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNRD.Zeepkist.Tester.Scenarios;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioValueChecksTests
{
    [Theory]
    [InlineData("wait: soon", 11, "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    [InlineData("wait: -1", 11, "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not '-1'.")]
    [InlineData("wait: ٣", 11, "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not '٣'.")]
    [InlineData("place-block: { function: LAMP }", 30, "'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.")]
    [InlineData("place-block: { function: lamp, how-many: two }", 46, "'how-many' of place-block takes a whole number, not 'two'.")]
    [InlineData("place-block: { function: lamp, how-many: 1.5 }", 46, "'how-many' of place-block takes a whole number, not '1.5'.")]
    [InlineData("place-block: { function: lamp, how-many: 9 }", 46, "'how-many' of place-block has to be between 1 and 8, not '9'.")]
    [InlineData("place-block: { function: lamp, offset: far }", 44, "'offset' of place-block takes a whole number, not 'far'.")]
    [InlineData("every-type: { float: much }", 26, "'float' of every-type takes a number, not 'much'.")]
    [InlineData("every-type: { float: Infinity }", 26, "'float' of every-type takes a number, not 'Infinity'.")]
    [InlineData("every-type: { bool: on }", 25, "'bool' of every-type takes true or false, not 'on'.")]
    [InlineData("every-type: { axis: diagonal }", 25, "'axis' of every-type takes horizontal, global_velocity or page-up, not 'diagonal'.")]
    [InlineData("every-type: { keys: ctrl+ }", 25, "'keys' of every-type has to match")]
    [InlineData("every-type: { chords: [ctrl+] }", 28, "'chords' of every-type has to match")]
    [InlineData("every-type: { file-name: a/b }", 30, "'file-name' of every-type has to match ^[A-Za-z0-9_-]+$, not 'a/b'.")]
    [InlineData("named: { defines: \"a b\" }", 23, "'defines' of named has to match ^[A-Za-z0-9_-]+$, not 'a b'.")]
    [InlineData("named: { uses-each: [\"a b\"] }", 26, "'uses-each' of named has to match ^[A-Za-z0-9_-]+$, not 'a b'.")]
    [InlineData("zls.drag-window-edge: { moves: [] }", 36, "'moves' of zls.drag-window-edge needs at least 1 item, not 0.")]
    [InlineData("zls.drag-window-edge: [{ by: 1, over: 700 }]", 43, "'over' of 'moves' of zls.drag-window-edge has to be between 1 and 600, not '700'.")]
    [InlineData("wait: ${", 11, "A ${ has no name and } after it. Write $${ for a literal ${.")]
    public void ALiteralThatDoesntFit_FailsWhenTheScenarioLoads(string written, int column, string problem)
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse($"steps:\n  - {written}\n"));

        Assert.StartsWith($"check.ztest.yaml:2:{column}: {problem}", exception.Message);
    }

    [Fact]
    public void SecondsTooLongForAFloat_FailWhenTheScenarioLoads()
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse($"steps:\n  - wait: {new string('9', 400)}\n"));

        Assert.Contains("takes a length of time", exception.Message);
    }

    [Fact]
    public void AValueWithALineBreakAtItsEnd_DoesntMatchAPattern()
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse("steps:\n  - every-type:\n      file-name: |\n        shot\n"));

        Assert.Contains("'file-name' of every-type has to match", exception.Message);
    }

    [Theory]
    [InlineData("wait: ${h}")]
    [InlineData("every-type: { axis: \"${h}\", bool: \"${h}\", float: \"${h}\" }")]
    [InlineData("place-block: { function: \"${h}\", how-many: \"${h}\" }")]
    [InlineData("named: { defines: \"${h}\", uses: \"${h}\" }")]
    public void AValueWithAName_WaitsUntilItRuns(string written)
    {
        Parse($"steps:\n  - {written}\n");
    }

    [Fact]
    public void AnEscape_IsCheckedAsTheTextItWrites()
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse("steps:\n  - place-block: $${a}\n"));

        Assert.Contains("'function' of place-block has to match ^[a-z_]+$, not '${a}'.", exception.Message);
    }

    [Fact]
    public void AWithValue_HasToBeWrittenProperly_ButIsNotOtherwiseChecked()
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse("steps:\n  - run: a.ztest.yaml\n    with: { a: \"${\" }\n"));

        Assert.Equal("check.ztest.yaml:3:16: A ${ has no name and } after it. Write $${ for a literal ${.", exception.Message);
        Parse("steps:\n  - run: a.ztest.yaml\n    with: { a: soon, b: \"${c}\" }\n");
    }

    [Theory]
    [InlineData("{ h: [1, soon] }", "wait: ${h}", 6, 15, "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    [InlineData("{ a: [lamp, LAMP] }", "place-block: ${a}", 6, 22, "'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.")]
    [InlineData("{ n: [1, 9] }", "place-block: { function: lamp, how-many: \"${n}\" }", 6, 50, "'how-many' of place-block has to be between 1 and 8, not '9'.")]
    [InlineData("{ a: [x, \"y z\"] }", "every-type: { file-name: \"shot-${a}\" }", 6, 34, "'file-name' of every-type has to match ^[A-Za-z0-9_-]+$, not 'shot-y z'.")]
    public void AForValue_IsTriedAgainstEachArgumentItsVariableIsUsedIn(string variable, string step, int line, int column, string problem)
    {
        var exception = Assert.Throws<ScenarioException>(() => Parse($"steps:\n  - undo\n  - for: {variable}\n    steps:\n      - undo\n      - {step}\n"));

        Assert.Equal($"check.ztest.yaml:{line}:{column}: {problem}", exception.Message);
    }

    [Fact]
    public void ANestedFor_TriesEachCombinationItsValueUses()
    {
        const string Yaml = """
            steps:
              - for: { a: [x, "y z"] }
                steps:
                  - for: { b: [1, 2] }
                    steps:
                      - every-type: { file-name: "${a}-${b}" }
            """;

        var exception = Assert.Throws<ScenarioException>(() => Parse(Yaml));

        Assert.EndsWith("not 'y z-1'.", exception.Message);
    }

    [Fact]
    public void AForValue_IsNotTried_WhereItsVariableEnded_OrWhereAnotherNameIsInTheSameValue()
    {
        const string Yaml = """
            steps:
              - for: { h: [soon] }
                steps:
                  - undo
                  - every-type: { file-name: "${h}-${other}" }
              - wait: ${h}
            """;

        Parse(Yaml);
    }

    [Fact(Timeout = 20000)]
    public async Task ManyNestedForsInOneValue_StillLoadInGoodTime()
    {
        var values = "[0, 1, 2, 3, 4, 5, 6, 7, 8, 9]";
        var yaml = new StringBuilder("steps:\n");
        var indent = "  ";
        for (var i = 0; i < 12; i++)
        {
            yaml.Append($"{indent}- for: {{ v{i}: {values} }}\n{indent}  steps:\n");
            indent += "    ";
        }

        var names = string.Concat(Enumerable.Range(0, 12).Select(i => $"${{v{i}}}"));
        yaml.Append($"{indent}- every-type: {{ file-name: \"{names}\" }}\n");

        await Task.Run(() => Parse(yaml.ToString()));
    }

    private static void Parse(string yaml)
    {
        ScenarioParser.Parse("check.ztest.yaml", new StringReader(yaml), ScenarioSteps.Descriptors);
    }
}
