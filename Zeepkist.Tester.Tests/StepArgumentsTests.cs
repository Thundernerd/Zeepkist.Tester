using TNRD.Zeepkist.Tester.Sdk;
using Xunit;
using static TNRD.Zeepkist.Tester.Tests.TestSteps;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepArgumentsTests
{
    private const string Lamp = "${lamp}";
    private const string Uid = "uid-1";

    private readonly ScenarioValues _values = new ScenarioValues();

    public StepArgumentsTests()
    {
        _values.Define("lamp", new BlockValue { Uid = Uid });
    }

    [Fact]
    public void Substitute_StringAndTextArguments_WritesTheName()
    {
        var step = new Written { String = Lamp, Text = "a\n${lamp}\nb", MaybeText = Lamp };

        StepArguments.Substitute(step, _values);

        Assert.Equal(Uid, step.String);
        Assert.Equal($"a\n{Uid}\nb", step.Text.Value);
        Assert.Equal(Uid, step.MaybeText.Value.Value);
    }

    [Fact]
    public void Substitute_ListsAndNestedArguments_WritesTheName()
    {
        var step = new Written
        {
            Strings = [Lamp, "other"],
            Nested = new Message { Line = Lamp, Details = [Lamp] },
            NestedItems = [new Message { Line = Lamp }],
        };

        StepArguments.Substitute(step, _values);

        Assert.Equal([Uid, "other"], step.Strings);
        Assert.Equal(Uid, step.Nested.Line);
        Assert.Equal(Uid, Assert.Single(step.Nested.Details).Value);
        Assert.Equal(Uid, Assert.Single(step.NestedItems).Line);
    }

    [Fact]
    public void Substitute_ArgumentsLeftOut_StayLeftOut()
    {
        var step = new Written();

        StepArguments.Substitute(step, _values);

        Assert.Null(step.NoString);
        Assert.Null(step.Text.Value);
        Assert.Null(step.NoText);
        Assert.Null(step.Strings);
        Assert.Null(step.Nested);
    }

    [Fact]
    public void Substitute_NullListItem_StaysNull()
    {
        var step = new Written { Strings = [null, Lamp] };

        StepArguments.Substitute(step, _values);

        Assert.Equal([null, Uid], step.Strings);
    }

    [Fact]
    public void Substitute_ArgumentWithoutASetter_IsFineWithNothingToWrite()
    {
        var step = new Written();

        StepArguments.Substitute(step, _values);

        Assert.Equal(1, step.GetOnly);
    }

    [Fact]
    public void Substitute_OtherArgumentTypes_AreLeftAlone()
    {
        var step = new Written { FileName = Lamp, Keys = new Keys(Lamp), Defines = Lamp, Uses = Lamp };

        StepArguments.Substitute(step, _values);

        Assert.Equal(Lamp, step.FileName.Value);
        Assert.Equal(Lamp, step.Keys.Value);
        Assert.Equal(Lamp, step.Defines.Value);
        Assert.Equal(Lamp, step.Uses.Value);
    }

    [Fact]
    public void Substitute_PropertyThatIsNotAnArgument_IsLeftAlone()
    {
        var step = new Written { NotAnArgument = "${not-defined}" };

        StepArguments.Substitute(step, _values);

        Assert.Equal("${not-defined}", step.NotAnArgument);
    }

    [Fact]
    public void Substitute_NameNotDefined_FailsTheStep()
    {
        var step = new Written { Strings = ["${not-defined}"] };

        var exception = Assert.Throws<StepFailedException>(() => StepArguments.Substitute(step, _values));

        Assert.Equal("The name 'not-defined' isn't defined by an earlier step.", exception.Message);
    }
}
