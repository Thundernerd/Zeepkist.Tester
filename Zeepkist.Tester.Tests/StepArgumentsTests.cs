using TNRD.Zeepkist.Tester.Sdk;
using Xunit;
using static TNRD.Zeepkist.Tester.Tests.TestSteps;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepArgumentsTests
{
    private const string Written = "${lamp}";
    private const string Uid = "uid-1";

    private readonly ScenarioValues _values = new ScenarioValues();

    public StepArgumentsTests()
    {
        _values.Define("lamp", new BlockValue { Uid = Uid });
    }

    [Fact]
    public void Substitute_StringAndTextArguments_WritesTheName()
    {
        var step = new Written { String = Written, Text = "a\n${lamp}\nb", MaybeText = Written };

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
            Strings = [Written, "other"],
            Nested = new Message { Line = Written, Details = [Written] },
            NestedItems = [new Message { Line = Written }],
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
    public void Substitute_OtherArgumentTypes_AreLeftAlone()
    {
        var step = new Written { FileName = Written, Keys = new Keys(Written), Defines = Written, Uses = Written };

        StepArguments.Substitute(step, _values);

        Assert.Equal(Written, step.FileName.Value);
        Assert.Equal(Written, step.Keys.Value);
        Assert.Equal(Written, step.Defines.Value);
        Assert.Equal(Written, step.Uses.Value);
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
