using TNRD.Zeepkist.Tester.Sdk;
using Xunit;
using static TNRD.Zeepkist.Tester.Tests.TestSteps;
using BlockValue = TNRD.Zeepkist.Tester.Tests.TestSteps.BlockValue;

namespace TNRD.Zeepkist.Tester.Tests;

public class ScenarioValuesTests
{
    private readonly Context _context = new Context();

    [Fact]
    public void Get_AfterSet_GivesTheValueThatNameDefined()
    {
        var lamp = new BlockValue { Uid = "uid-1" };
        var name = new Name<BlockValue>("lamp");
        var reference = new Ref<BlockValue>("lamp");

        name.Set(_context, lamp);

        Assert.Same(lamp, reference.Get(_context));
    }

    [Fact]
    public void Set_NameAlreadyDefined_FailsTheStepNamingIt()
    {
        var name = new Name<BlockValue>("lamp");
        name.Set(_context, new BlockValue());

        var exception = Assert.Throws<StepFailedException>(() => name.Set(_context, new BlockValue()));

        Assert.Equal("The name 'lamp' is already defined, as a block.", exception.Message);
    }

    [Fact]
    public void Set_NameAlreadyDefinedAsAnotherKind_FailsTheStepToo()
    {
        new Name<WindowValue>("lamp").Set(_context, new WindowValue());

        var exception = Assert.Throws<StepFailedException>(() => new Name<BlockValue>("lamp").Set(_context, new BlockValue()));

        Assert.Equal("The name 'lamp' is already defined, as a window.", exception.Message);
    }

    [Fact]
    public void Set_NullValue_FailsTheStepNamingIt()
    {
        var exception = Assert.Throws<StepFailedException>(() => new Name<BlockValue>("lamp").Set(_context, null));

        Assert.Equal("The name 'lamp' has to be defined with a value, not null.", exception.Message);
    }

    [Fact]
    public void Set_NameLeftOut_FailsTheStep()
    {
        var exception = Assert.Throws<StepFailedException>(() => default(Name<BlockValue>).Set(_context, new BlockValue()));

        Assert.Equal("The step defines a named value, but its name was left out.", exception.Message);
    }

    [Fact]
    public void Get_NameLeftOut_FailsTheStep()
    {
        var exception = Assert.Throws<StepFailedException>(() => default(Ref<BlockValue>).Get(_context));

        Assert.Equal("The step uses a named value, but its name was left out.", exception.Message);
    }

    [Fact]
    public void Get_NameNotDefined_FailsTheStepNamingIt()
    {
        var exception = Assert.Throws<StepFailedException>(() => new Ref<BlockValue>("lamp").Get(_context));

        Assert.Equal("The name 'lamp' isn't defined by an earlier step.", exception.Message);
    }

    [Fact]
    public void Get_NameOfAnotherKind_FailsTheStepNamingBothKinds()
    {
        new Name<WindowValue>("lamp").Set(_context, new WindowValue());

        var exception = Assert.Throws<StepFailedException>(() => new Ref<BlockValue>("lamp").Get(_context));

        Assert.Equal("The name 'lamp' is a window, not a block.", exception.Message);
    }

    [Fact]
    public void Get_OnAnotherScenariosValues_DoesNotFindTheName()
    {
        new Name<BlockValue>("lamp").Set(_context, new BlockValue());

        Assert.Throws<StepFailedException>(() => new Ref<BlockValue>("lamp").Get(new Context()));
    }

    [Fact]
    public void Substitute_DefinedName_WritesItsSubstituteMember()
    {
        Define("lamp", "uid-1");
        Define("switch_2", "uid-2");

        var text = _context.Values.Substitute("toggler(targets: placed(\"${lamp}\", \"${switch_2}\"))");

        Assert.Equal("toggler(targets: placed(\"uid-1\", \"uid-2\"))", text);
    }

    [Theory]
    [InlineData("no names", "no names")]
    [InlineData("", "")]
    [InlineData("$${lamp}", "${lamp}")]
    [InlineData("$${", "${")]
    [InlineData("$${lamp} ${lamp}", "${lamp} uid-1")]
    [InlineData("$$${lamp}", "$${lamp}")]
    [InlineData("$ $$ $5 {lamp} }", "$ $$ $5 {lamp} }")]
    public void Substitute_Escapes_WriteALiteral(string text, string written)
    {
        Define("lamp", "uid-1");

        Assert.Equal(written, _context.Values.Substitute(text));
    }

    [Fact]
    public void Substitute_ValueThatLooksLikeAName_IsNotSubstitutedAgain()
    {
        Define("lamp", "${other}");

        Assert.Equal("${other}", _context.Values.Substitute("${lamp}"));
    }

    [Fact]
    public void Substitute_NameNotDefined_FailsTheStepNamingIt()
    {
        var exception = Assert.Throws<StepFailedException>(() => _context.Values.Substitute("a ${lamp} b"));

        Assert.Equal("The name 'lamp' isn't defined by an earlier step.", exception.Message);
    }

    [Fact]
    public void Substitute_KindWithoutASubstituteMember_FailsTheStepNamingIt()
    {
        new Name<WindowValue>("main").Set(_context, new WindowValue());

        var exception = Assert.Throws<StepFailedException>(() => _context.Values.Substitute("${main}"));

        Assert.Equal("${main} can't be written: a window has no Substitute member.", exception.Message);
    }

    [Fact]
    public void Substitute_MemberIsNull_FailsTheStepNamingIt()
    {
        Define("lamp", null);

        var exception = Assert.Throws<StepFailedException>(() => _context.Values.Substitute("${lamp}"));

        Assert.Equal("${lamp} can't be written: the block's Uid is null.", exception.Message);
    }

    [Theory]
    [InlineData("${")]
    [InlineData("${lamp")]
    [InlineData("${}")]
    [InlineData("a ${my lamp} b")]
    public void Substitute_NoNameAfterTheOpening_FailsTheStepSayingHowToEscapeIt(string text)
    {
        Define("lamp", "uid-1");

        var exception = Assert.Throws<StepFailedException>(() => _context.Values.Substitute(text));

        Assert.Equal("A ${ has no name and } after it. Write $${ for a literal ${.", exception.Message);
    }

    [Fact]
    public void AScope_ReadsWhatIsDefinedAroundIt()
    {
        var outer = new ScenarioValues();
        outer.Define("lamp", new BlockValue { Uid = "uid-1" });

        var inner = outer.NewScope();

        Assert.Equal("uid-1", inner.Get<BlockValue>("lamp").Uid);
        Assert.Equal("uid-1", inner.Substitute("${lamp}"));
    }

    [Fact]
    public void AScope_CantDefineANameFromAroundIt_SoNothingIsHidden()
    {
        var outer = new ScenarioValues();
        outer.Define("lamp", new BlockValue());

        var exception = Assert.Throws<StepFailedException>(() => outer.NewScope().Define("lamp", new BlockValue()));

        Assert.Equal("The name 'lamp' is already defined, as a block.", exception.Message);
    }

    [Fact]
    public void WhatAScopeDefines_EndsWithIt_AndTheNextPassCanDefineItAgain()
    {
        var outer = new ScenarioValues();

        outer.NewScope().Define("lamp", new BlockValue());

        Assert.Throws<StepFailedException>(() => outer.Get<BlockValue>("lamp"));
        outer.NewScope().Define("lamp", new BlockValue());
    }

    [Fact]
    public void AText_WritesItsOwnText_AndIsAKindOfItsOwn()
    {
        var values = new ScenarioValues();
        values.Define("h", new TextValue("64"));

        Assert.Equal("64", values.Substitute("${h}"));
        Assert.Equal("The name 'h' is a text, not a block.", Assert.Throws<StepFailedException>(() => values.Get<BlockValue>("h")).Message);
    }

    [Fact]
    public void Define_KindWhoseSubstituteIsntAProperty_FailsTheStepNamingIt()
    {
        var exception = Assert.Throws<StepFailedException>(() => new ScenarioValues().Define("lamp", new InvalidSteps.SubstituteMissing()));

        Assert.Equal($"Value kind Substitute 'Missing' ({typeof(InvalidSteps.SubstituteMissing).FullName}) isn't a public string property of it.", exception.Message);
    }

    [Fact]
    public void Define_KindWhoseSubstituteIsntAString_FailsTheStepNamingIt()
    {
        var exception = Assert.Throws<StepFailedException>(() => new ScenarioValues().Define("lamp", new InvalidSteps.SubstituteNotAString()));

        Assert.Equal($"Value kind Substitute 'Number' ({typeof(InvalidSteps.SubstituteNotAString).FullName}) isn't a public string property of it.", exception.Message);
    }

    [Fact]
    public void Get_AnotherClassOfTheSameKind_FailsTheStepNamingBothClasses()
    {
        var values = new ScenarioValues();
        values.Define("x", new InvalidSteps.LampOne());

        var exception = Assert.Throws<StepFailedException>(() => values.Get<InvalidSteps.LampTwo>("x"));

        Assert.Equal($"{typeof(InvalidSteps.LampOne).FullName} and {typeof(InvalidSteps.LampTwo).FullName} are both the value kind 'lamp'. A kind's name has to be unique across every mod's steps.", exception.Message);
    }

    [Fact]
    public void Get_AnotherClassOfTheSameNameWithoutAKind_IsNotCalledAKindClash()
    {
        var values = new ScenarioValues();
        values.Define("x", new One.Plain());

        var exception = Assert.Throws<StepFailedException>(() => values.Get<Other.Plain>("x"));

        Assert.Equal("The name 'x' is a Plain, not a Plain.", exception.Message);
    }

    private void Define(string name, string uid)
    {
        new Name<BlockValue>(name).Set(_context, new BlockValue { Uid = uid });
    }

    private sealed class Context : BareStepContext
    {
    }

    private static class One
    {
        public sealed class Plain
        {
        }
    }

    private static class Other
    {
        public sealed class Plain
        {
        }
    }
}

