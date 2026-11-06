using System.IO;
using System.Linq;
using TNRD.Zeepkist.Tester.Scenarios;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepBinderTests
{
    private readonly ScenarioValues _values = new ScenarioValues();

    [Theory]
    [InlineData("2", 2f)]
    [InlineData("0.5", 0.5f)]
    [InlineData("500ms", 0.5f)]
    [InlineData("1.5s", 1.5f)]
    public void Seconds_TakeANumberOrAUnit(string written, float expected)
    {
        var step = Bind<TestSteps.Wait>($"wait: {written}");

        Assert.Equal(expected, step.Seconds.Value);
    }

    [Fact]
    public void AMap_SetsEachArgument_ByItsName()
    {
        var step = Bind<TestSteps.PlaceBlock>("place-block: { function: lamp, as: my-lamp, how-many: 2, offset: -40 }");

        Assert.Equal("lamp", step.Function);
        Assert.Equal("my-lamp", step.As);
        Assert.Equal(2, step.Count);
        Assert.Equal(-40, step.Offset.Value.Value);
    }

    [Fact]
    public void AnArgumentLeftOut_GetsItsDefault_OrStaysUnset()
    {
        var step = Bind<TestSteps.PlaceBlock>("place-block: lamp");

        Assert.Equal(1, step.Count);
        Assert.Null(step.As);
        Assert.Null(step.Offset);
    }

    [Fact]
    public void EachScalarType_ConvertsFromItsText()
    {
        var step = Bind<TestSteps.EveryType>("every-type: { axis: page-up, maybe-axis: global_velocity, text: a, float: 1.5, bool: true, keys: ctrl+space, file-name: shot-1, timeout: 250ms }");

        Assert.Equal(TestSteps.Axis.PageUp, step.Axis);
        Assert.Equal(TestSteps.Axis.GlobalVelocity, step.MaybeAxis);
        Assert.Equal("a", step.Text.Value);
        Assert.Equal(1.5f, step.Float);
        Assert.True(step.Bool);
        Assert.Equal("ctrl+space", step.Keys.Value);
        Assert.Equal("shot-1", step.FileName.Value);
        Assert.Equal(0.25f, step.Timeout.Value);
    }

    [Fact]
    public void AnEnumDefault_IsTheWrittenValue_AndAListHoldsConvertedItems()
    {
        var step = Bind<TestSteps.EveryType>("every-type: { times: [1, 500ms], chords: [a, b] }");

        Assert.Equal(TestSteps.Axis.GlobalVelocity, step.Axis);
        Assert.Equal([1f, 0.5f], step.Times.Select(time => time.Value));
        Assert.Equal(["a", "b"], step.Chords.Select(chord => chord.Value));
    }

    [Fact]
    public void NamesAndRefs_HoldTheirName()
    {
        var step = Bind<TestSteps.Named>("named: { defines: a, maybe-defines: b, uses: c, uses-each: [d, e] }");

        Assert.Equal("a", step.Defines.Value);
        Assert.Equal("b", step.MaybeDefines.Value.Value);
        Assert.Equal("c", step.Uses.Value);
        Assert.Equal(["d", "e"], step.UsesEach.Select(item => item.Value));
    }

    [Theory]
    [InlineData("zls.drag-window-edge: 300", 300, 1)]
    [InlineData("zls.drag-window-edge: { by: 50, over: 3 }", 50, 3)]
    [InlineData("zls.drag-window-edge: { moves: [-50], sideways: true }", -50, 1)]
    public void NestedArguments_AreBuilt_WithTheirOwnDefaults(string written, int by, int over)
    {
        var move = Assert.Single(Bind<TestSteps.DragWindowEdge>(written).Moves);

        Assert.Equal(by, move.By.Value);
        Assert.Equal(over, move.Over);
    }

    [Fact]
    public void ASubstitution_WorksInASingleValue_OfAnyType()
    {
        _values.Define("h", new TestSteps.BlockValue { Uid = "64" });
        _values.Define("mode", new TestSteps.BlockValue { Uid = "page-up" });

        Assert.Equal(64f, Bind<TestSteps.Wait>("wait: ${h}").Seconds.Value);
        Assert.Equal(TestSteps.Axis.PageUp, Bind<TestSteps.EveryType>("every-type:\n      axis: ${mode}").Axis);
        Assert.Equal("shot-64", Bind<TestSteps.EveryType>("every-type:\n      file-name: shot-${h}").FileName.Value);
        Assert.Equal(64, Assert.Single(Bind<TestSteps.DragWindowEdge>("zls.drag-window-edge: ${h}").Moves).By.Value);
    }

    [Fact]
    public void BindingAgain_StartsFromTheWrittenText()
    {
        var node = Read("written:\n      string: a $${b} ${c}");

        _values.Define("c", new TestSteps.BlockValue { Uid = "one" });
        var first = (TestSteps.Written)StepBinder.Bind(node, typeof(TestSteps.Written), _values);
        var other = new ScenarioValues();
        other.Define("c", new TestSteps.BlockValue { Uid = "two" });
        var second = (TestSteps.Written)StepBinder.Bind(node, typeof(TestSteps.Written), other);

        Assert.Equal("a ${b} one", first.String);
        Assert.Equal("a ${b} two", second.String);
    }

    [Fact]
    public void EachStepOfAFixture_BindsToItsOwnType()
    {
        var steps = ScenarioSteps.Parse("step-forms").Steps.Cast<StepNode>()
            .Where(node => node.Step.FullName != "wait")
            .Select(node => StepBinder.Bind(node, ScenarioSteps.TypeOf(node), _values))
            .ToList();

        Assert.IsType<TestSteps.Undo>(steps[0]);
        Assert.Equal("my-lamp", Assert.IsType<TestSteps.PlaceBlock>(steps[2]).As);
        Assert.Equal(2, Assert.IsType<TestSteps.DragWindowEdge>(steps[5]).Moves.Count);
        Assert.Equal("first\n  second\n", Assert.IsType<TestSteps.Written>(steps[7]).Text.Value);
    }

    [Theory]
    [InlineData("wait: soon", "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    [InlineData("wait: -1", "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not '-1'.")]
    [InlineData("place-block: { function: LAMP }", "'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.")]
    [InlineData("place-block: { function: lamp, how-many: two }", "'how-many' of place-block takes a whole number, not 'two'.")]
    [InlineData("place-block: { function: lamp, how-many: 1.5 }", "'how-many' of place-block takes a whole number, not '1.5'.")]
    [InlineData("place-block: { function: lamp, how-many: 9 }", "'how-many' of place-block has to be between 1 and 8, not '9'.")]
    [InlineData("place-block: { function: lamp, offset: far }", "'offset' of place-block takes a whole number, not 'far'.")]
    [InlineData("every-type: { float: much }", "'float' of every-type takes a number, not 'much'.")]
    [InlineData("every-type: { float: Infinity }", "'float' of every-type takes a number, not 'Infinity'.")]
    [InlineData("every-type: { bool: on }", "'bool' of every-type takes true or false, not 'on'.")]
    [InlineData("every-type: { axis: diagonal }", "'axis' of every-type takes horizontal, global_velocity or page-up, not 'diagonal'.")]
    [InlineData("every-type: { keys: ctrl+ }", "'keys' of every-type has to match")]
    [InlineData("every-type: { file-name: a/b }", "'file-name' of every-type has to match ^[A-Za-z0-9_-]+$, not 'a/b'.")]
    [InlineData("named: { defines: \"a b\" }", "'defines' of named has to match ^[A-Za-z0-9_-]+$, not 'a b'.")]
    [InlineData("zls.drag-window-edge: { moves: [] }", "'moves' of zls.drag-window-edge needs at least 1 item, not 0.")]
    [InlineData("zls.drag-window-edge: [{ by: 1, over: 700 }]", "'over' of 'moves' of zls.drag-window-edge has to be between 1 and 600, not '700'.")]
    [InlineData("wait: ${nope}", "The name 'nope' isn't defined by an earlier step.")]
    [InlineData("wait: ${", "A ${ has no name and } after it. Write $${ for a literal ${.")]
    public void AValueThatDoesntFit_FailsTheStep_WithItsLocationAndWhatsWrong(string written, string problem)
    {
        var node = Read(written);

        var exception = Assert.Throws<StepFailedException>(() => StepBinder.Bind(node, ScenarioSteps.TypeOf(node), _values));

        Assert.StartsWith("bind.ztest.yaml:2:", exception.Message);
        Assert.Contains($": {problem}", exception.Message);
    }

    private T Bind<T>(string written)
        where T : Step
    {
        return (T)StepBinder.Bind(Read(written), typeof(T), _values);
    }

    private static StepNode Read(string written)
    {
        var yaml = $"steps:\n  - {written}\n";
        return (StepNode)ScenarioParser.Parse("bind.ztest.yaml", new StringReader(yaml), ScenarioSteps.Descriptors).Steps.Single();
    }
}
