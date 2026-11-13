using System;
using System.IO;
using System.Runtime.CompilerServices;
using Json.Schema;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Tests.SampleMod;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepSchemaTests
{
    private static readonly JsonSchema _schema = JsonSchema.FromText(StepSchema.Write(ScenarioSteps.Descriptors));

    [Theory]
    [InlineData("steps:\n  - undo\n")]
    [InlineData("steps:\n  - wait: 2\n")]
    [InlineData("steps:\n  - wait: 0.5\n")]
    [InlineData("steps:\n  - wait: 500ms\n")]
    [InlineData("steps:\n  - wait: { seconds: 2 }\n")]
    [InlineData("steps:\n  - place-block: lamp\n")]
    [InlineData("steps:\n  - place-block: { function: lamp, as: my-lamp, how-many: 3, offset: -1000 }\n")]
    [InlineData("steps:\n  - every-type: { axis: global_velocity, bool: true, float: 1.5, keys: ctrl+z escape, file-name: shot-1 }\n")]
    [InlineData("steps:\n  - every-type: { text: 42, times: [1, 500ms], chords: [a, ctrl+b] }\n")]
    [InlineData("steps:\n  - wait: ${h}\n")]
    [InlineData("steps:\n  - place-block: { function: \"${f}\", how-many: \"${n}\" }\n")]
    [InlineData("steps:\n  - every-type: { axis: \"${a}\", bool: \"${b}\", keys: \"ctrl+${k}\", file-name: \"shot-${h}\" }\n")]
    [InlineData("steps:\n  - named: { defines: \"${x}\", uses-each: [lamp, \"${y}\"] }\n")]
    [InlineData("use: [zls]\nsteps:\n  - drag-window-edge: 300\n  - zls.drag-window-edge: 300\n")]
    [InlineData("use: [zls]\nsteps:\n  - compile\n")]
    [InlineData("start: { editor: { level: a.zeeplevel } }\nsteps:\n  - undo\n")]
    [InlineData("start: { free-play: { uid: abc } }\nsteps:\n  - undo\n")]
    public void Valid_AsTheParserReadsIt(string yaml)
    {
        Assert.True(IsValid(yaml));
    }

    [Theory]
    [InlineData("steps:\n  - nope\n")]
    [InlineData("steps:\n  - wait\n")]
    [InlineData("steps:\n  - wait: soon\n")]
    [InlineData("steps:\n  - wait: { second: 2 }\n")]
    [InlineData("steps:\n  - undo: 2\n")]
    [InlineData("steps:\n  - place-block: { as: my-lamp }\n")]
    [InlineData("steps:\n  - place-block: { function: lamp, how-many: 9 }\n")]
    [InlineData("steps:\n  - place-block: { function: lamp, how-many: 1.5 }\n")]
    [InlineData("steps:\n  - place-block: Lamp\n")]
    [InlineData("steps:\n  - every-type: { axis: sideways }\n")]
    [InlineData("steps:\n  - every-type: { bool: yes }\n")]
    [InlineData("steps:\n  - every-type: { keys: ctrl+nope }\n")]
    [InlineData("steps:\n  - every-type: { file-name: a.png }\n")]
    [InlineData("steps:\n  - wait: \"${}\"\n")]
    [InlineData("steps:\n  - wait: -1\n")]
    [InlineData("steps:\n  - repeat: ${n}\n    steps:\n      - undo\n")]
    [InlineData("start: { editor: { level: \"${x}\" } }\nsteps:\n  - undo\n")]
    [InlineData("steps:\n  - compile: 1\n")]
    [InlineData("use: [clashing]\nsteps:\n  - wait: true\n")]
    public void Invalid_AsTheParserReadsIt(string yaml)
    {
        Assert.False(IsValid(yaml));
    }

    [Theory]
    [InlineData("step-forms")]
    [InlineData("control-nodes")]
    [InlineData("namespaces")]
    public void ValidFixtures_AreValid(string fixture)
    {
        Assert.True(IsValid(File.ReadAllText(Path.Combine("Scenarios", $"{fixture}.ztest.yaml"))));
    }

    /// <summary>
    /// The parser's errors a schema can see. The others need more than one node to see, such as an alias, a clash with
    /// what <c>use:</c> lists, or a duplicate key, which an editor's YAML reader reports itself.
    /// </summary>
    [Theory]
    [InlineData("description-is-a-list")]
    [InlineData("empty-steps")]
    [InlineData("empty-value")]
    [InlineData("for-bad-variable")]
    [InlineData("for-empty-body")]
    [InlineData("for-is-a-scalar")]
    [InlineData("for-no-steps")]
    [InlineData("for-no-values")]
    [InlineData("for-two-variables")]
    [InlineData("for-unknown-key")]
    [InlineData("for-value-is-a-list")]
    [InlineData("for-value-is-an-expression")]
    [InlineData("for-values-are-a-scalar")]
    [InlineData("input-unknown")]
    [InlineData("list-for-a-single-value")]
    [InlineData("map-for-a-single-value")]
    [InlineData("missing-required-argument")]
    [InlineData("missing-required-bare")]
    [InlineData("no-arguments-map")]
    [InlineData("no-arguments")]
    [InlineData("no-primary-argument")]
    [InlineData("no-steps")]
    [InlineData("repeat-no-steps")]
    [InlineData("repeat-not-a-number")]
    [InlineData("repeat-unknown-key")]
    [InlineData("repeat-zero")]
    [InlineData("run-is-a-map")]
    [InlineData("run-is-an-expression")]
    [InlineData("run-unknown-key")]
    [InlineData("single-value-for-a-list")]
    [InlineData("start-argument-with-a-name")]
    [InlineData("start-missing-argument")]
    [InlineData("start-two-keys")]
    [InlineData("start-unknown-argument")]
    [InlineData("start-unknown")]
    [InlineData("step-is-a-list")]
    [InlineData("step-is-an-empty-map")]
    [InlineData("steps-is-a-map")]
    [InlineData("step-two-keys")]
    [InlineData("substitution-for-a-list")]
    [InlineData("substitution-for-a-map")]
    [InlineData("unknown-argument")]
    [InlineData("unknown-header-key")]
    [InlineData("unknown-nested-argument")]
    [InlineData("unknown-prefixed-step")]
    [InlineData("unknown-step")]
    [InlineData("use-is-a-scalar")]
    [InlineData("use-unknown-namespace")]
    [InlineData("with-bad-name")]
    [InlineData("with-is-a-scalar")]
    [InlineData("with-value-is-a-list")]
    public void ErrorFixtures_AreInvalid(string fixture)
    {
        Assert.False(IsValid(File.ReadAllText(Path.Combine("Scenarios", "Errors", $"{fixture}.ztest.yaml"))));
    }

    [Fact]
    public void Snapshot_EveryKindOfArgument()
    {
        MatchesSnapshot("test-steps", StepSchema.Write(ScenarioSteps.Descriptors));
    }

    [Fact]
    public void Snapshot_TheTestersStepsAndAMods()
    {
        MatchesSnapshot("tester-and-sample-mod", StepSchema.Write(OfflineRegistry.Read([typeof(Compile).Assembly.Location]).Steps));
    }

    /// <summary>
    /// Compares the schema with the file checked in for it. With <c>UPDATE_SNAPSHOTS=1</c> it writes the file instead,
    /// for a change that's meant, which the diff then shows.
    /// </summary>
    private static void MatchesSnapshot(string name, string schema, [CallerFilePath] string testFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(testFile), "Snapshots", $"{name}.schema.json");
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, schema + "\n");
        }

        Assert.Equal(File.ReadAllText(path), schema + "\n");
    }

    private static bool IsValid(string yaml)
    {
        return _schema.Evaluate(EditorYaml.ToJson(yaml)).IsValid;
    }
}
