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
        _values.Define("h", new TestSteps.PlacedBlock { Uid = "64" });
        _values.Define("mode", new TestSteps.PlacedBlock { Uid = "page-up" });

        Assert.Equal(64f, Bind<TestSteps.Wait>("wait: ${h}").Seconds.Value);
        Assert.Equal(TestSteps.Axis.PageUp, Bind<TestSteps.EveryType>("every-type:\n      axis: ${mode}").Axis);
        Assert.Equal("shot-64", Bind<TestSteps.EveryType>("every-type:\n      file-name: shot-${h}").FileName.Value);
        Assert.Equal(64, Assert.Single(Bind<TestSteps.DragWindowEdge>("zls.drag-window-edge: ${h}").Moves).By.Value);
    }

    [Fact]
    public void BindingAgain_StartsFromTheWrittenText()
    {
        var node = Read("written:\n      string: a $${b} ${c}");

        _values.Define("c", new TestSteps.PlacedBlock { Uid = "one" });
        var first = (TestSteps.Written)StepBinder.Bind(node, typeof(TestSteps.Written), _values);
        var other = new ScenarioValues();
        other.Define("c", new TestSteps.PlacedBlock { Uid = "two" });
        var second = (TestSteps.Written)StepBinder.Bind(node, typeof(TestSteps.Written), other);

        Assert.Equal("a ${b} one", first.String);
        Assert.Equal("a ${b} two", second.String);
    }

    [Fact]
    public void EachStepOfAFixture_BindsToItsOwnType()
    {
        var steps = ScenarioSteps.Parse("step-forms").Steps.Cast<StepNode>()
            .Select(node => StepBinder.Bind(node, ScenarioSteps.TypeOf(node), _values))
            .ToList();

        Assert.IsType<TestSteps.Undo>(steps[0]);
        Assert.Equal("my-lamp", Assert.IsType<TestSteps.PlaceBlock>(steps[3]).As);
        Assert.Equal(2, Assert.IsType<TestSteps.DragWindowEdge>(steps[6]).Moves.Count);
        Assert.Equal("first\n  second\n", Assert.IsType<TestSteps.Written>(steps[8]).Text.Value);
    }

    [Theory]
    [InlineData("wait: ${h}", 11, "'seconds' of wait takes a length of time such as 2, 0.5 or 500ms, not 'soon'.")]
    [InlineData("every-type:\n      axis: ${h}", 13, "'axis' of every-type takes horizontal, global_velocity or page-up, not 'soon'.")]
    [InlineData("place-block: { function: \"${n}\" }", 30, "'function' of place-block has to match ^[a-z_]+$, not 'LAMP'.")]
    [InlineData("wait: ${nope}", 11, "The name 'nope' isn't defined by an earlier step.")]
    public void AValueOnlyKnownWhenItRuns_FailsTheStep_WithItsLocationAndWhatsWrong(string written, int column, string problem)
    {
        _values.Define("h", new TestSteps.PlacedBlock { Uid = "soon" });
        _values.Define("n", new TestSteps.PlacedBlock { Uid = "LAMP" });
        var node = Read(written);

        var exception = Assert.Throws<StepFailedException>(() => StepBinder.Bind(node, ScenarioSteps.TypeOf(node), _values));

        Assert.EndsWith($":{column}: {problem}", exception.Message);
        Assert.StartsWith("bind.ztest.yaml:", exception.Message);
    }

    [Theory]
    [InlineData("ranged: { number: 0.1 }")]
    [InlineData("ranged: { time: 100ms }")]
    public void AValueAtTheEdgeOfItsRange_IsInRange(string written)
    {
        Bind<ScenarioSteps.Ranged>(written);
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
