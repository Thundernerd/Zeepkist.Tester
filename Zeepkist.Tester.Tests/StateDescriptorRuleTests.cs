using System;
using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StateDescriptorRuleTests
{
    private const string Invalid = "TNRD.Zeepkist.Tester.Tests.InvalidStates";

    [Fact]
    public void BuildState_NoStateAttribute_Fails()
    {
        AssertStateFails(typeof(InvalidStates.NoStateAttribute), $"{Invalid}+NoStateAttribute has no [State] attribute.");
    }

    [Fact]
    public void BuildState_NameNotInKebabCase_Fails()
    {
        AssertStateFails(typeof(InvalidStates.NameNotKebab), $"State name 'Bad_Name' ({Invalid}+NameNotKebab) isn't in kebab case, such as 'place-block'.");
    }

    [Fact]
    public void BuildState_NamespaceNotInKebabCase_Fails()
    {
        AssertStateFails(typeof(InvalidStates.NamespaceNotKebab), $"Namespace 'Bad_Namespace' ({Invalid}+NamespaceNotKebab) isn't in kebab case, such as 'place-block'.");
    }

    [Fact]
    public void BuildState_OutsideTheTesterWithoutANamespace_Fails()
    {
        AssertStateFails(
            typeof(InvalidStates.NoNamespace),
            $"State 'no-namespace' ({Invalid}+NoNamespace) has no namespace, which is reserved for the tester's own states. Set Namespace on [State] or [TestPlugin].");
    }

    [Fact]
    public void BuildState_InTheTestersOwnAssembly_MayHaveNoNamespace()
    {
        var builder = new StepDescriptorBuilder { TesterAssemblyName = typeof(InvalidStates).Assembly.GetName().Name };

        Assert.Equal("no-namespace", builder.BuildState(typeof(InvalidStates.NoNamespace)).FullName);
    }

    [Fact]
    public void BuildState_NotAnIState_Fails()
    {
        AssertStateFails(typeof(InvalidStates.NotAState), $"{Invalid}+NotAState has [State] but doesn't implement IState.");
    }

    [Theory]
    [InlineData(typeof(InvalidStates.Abstract), "Abstract")]
    [InlineData(typeof(InvalidStates.TakesArguments), "TakesArguments")]
    public void BuildState_TheHarnessCannotMakeOne_Fails(Type stateType, string typeName)
    {
        AssertStateFails(stateType, $"{Invalid}+{typeName} has [State] but can't be made: it has to be a concrete class with a public constructor that takes nothing.");
    }

    [Fact]
    public void BuildState_WithinAnUnknownState_Fails()
    {
        AssertStateFails(typeof(InvalidStates.WithinUnknown), $"Within on {Invalid}+WithinUnknown names {Invalid}+NoStateAttribute, which has no [State] attribute.");
    }

    [Fact]
    public void BuildState_WithinItself_Fails()
    {
        AssertStateFails(typeof(InvalidStates.WithinItself), $"{Invalid}+WithinItself is within itself, through {Invalid}+WithinItself.");
    }

    [Fact]
    public void BuildState_CycleInWithin_Fails()
    {
        AssertStateFails(typeof(InvalidStates.CycleOne), $"{Invalid}+CycleOne is within itself, through {Invalid}+CycleThree.");
    }

    [Fact]
    public void BuildState_WithinACycleItIsNotPartOf_NamesTheCycle()
    {
        AssertStateFails(typeof(InvalidStates.IntoCycle), $"{Invalid}+CycleOne is within itself, through {Invalid}+CycleThree.");
    }

    [Theory]
    [InlineData(typeof(InvalidStates.RequiresUnknown), "Requires", "RequiresUnknown")]
    [InlineData(typeof(InvalidStates.RequiresAnyUnknown), "RequiresAny", "RequiresAnyUnknown")]
    [InlineData(typeof(InvalidStates.EntersUnknown), "Enters", "EntersUnknown")]
    [InlineData(typeof(InvalidStates.LeavesUnknown), "Leaves", "LeavesUnknown")]
    public void Build_StepNamesAnUnknownState_Fails(Type stepType, string attribute, string typeName)
    {
        AssertStepFails(stepType, $"[{attribute}] on {Invalid}+{typeName} names {Invalid}+NoStateAttribute, which has no [State] attribute.");
    }

    [Theory]
    [InlineData(typeof(InvalidStates.RequiresNothing), "Requires", "RequiresNothing")]
    [InlineData(typeof(InvalidStates.RequiresAnyOfNothing), "RequiresAny", "RequiresAnyOfNothing")]
    public void Build_StepNamesNoStates_Fails(Type stepType, string attribute, string typeName)
    {
        AssertStepFails(stepType, $"[{attribute}] on {Invalid}+{typeName} names no states.");
    }

    [Fact]
    public void Build_StepNamesABrokenState_Fails()
    {
        AssertStepFails(typeof(InvalidStates.RequiresBroken), $"{Invalid}+CycleOne is within itself, through {Invalid}+CycleThree.");
    }

    [Fact]
    public void BuildRegistry_SameNameInOneNamespace_Fails()
    {
        var exception = Assert.Throws<StepDefinitionException>(
            () => new StepDescriptorBuilder().BuildRegistry([], [typeof(InvalidStates.Good), typeof(InvalidStates.SameNameAsGood)]));

        Assert.Equal($"{Invalid}+Good and {Invalid}+SameNameAsGood are both the state 'zls.good'.", exception.Message);
    }

    [Fact]
    public void BuildRegistry_SameNameThroughAStepsWithin_Fails()
    {
        var exception = Assert.Throws<StepDefinitionException>(
            () => new StepDescriptorBuilder().BuildRegistry([typeof(InvalidStates.RequiresSameName)], [typeof(InvalidStates.Good)]));

        Assert.Equal($"{Invalid}+Good and {Invalid}+SameNameAsGood are both the state 'zls.good'.", exception.Message);
    }

    [Fact]
    public void BuildRegistry_StateAStepNamesAndAlsoListed_IsThereOnce()
    {
        var registry = new StepDescriptorBuilder().BuildRegistry([typeof(InvalidStates.RequiresGood)], [typeof(InvalidStates.Good)]);

        Assert.Equal(["zls.good"], registry.States.Select(state => state.FullName));
    }

    private static void AssertStateFails(Type stateType, string message)
    {
        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildState(stateType));

        Assert.Equal(message, exception.Message);
    }

    private static void AssertStepFails(Type stepType, string message)
    {
        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().Build(stepType));

        Assert.Equal(message, exception.Message);
    }
}
