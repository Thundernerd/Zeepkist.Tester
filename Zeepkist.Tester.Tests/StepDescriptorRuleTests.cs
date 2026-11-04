using System;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.Tests.SampleMod;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepDescriptorRuleTests
{
    private const string InvalidStepsType = "TNRD.Zeepkist.Tester.Tests.InvalidSteps";

    [Fact]
    public void Build_TwoPrimaryArguments_Fails()
    {
        AssertFails(typeof(InvalidSteps.TwoPrimaries), $"{InvalidStepsType}+TwoPrimaries has more than one Primary argument: 'first' and 'second'.");
    }

    [Fact]
    public void Build_TwoPrimaryArgumentsInANestedClass_Fails()
    {
        AssertFails(typeof(InvalidSteps.HasNestedTwoPrimaries), $"{InvalidStepsType}+NestedTwoPrimaries has more than one Primary argument: 'first' and 'second'.");
    }

    [Theory]
    [InlineData(typeof(InvalidSteps.NoNamespace), "no-namespace", "NoNamespace")]
    [InlineData(typeof(InvalidSteps.EmptyNamespace), "empty-namespace", "EmptyNamespace")]
    public void Build_StepOutsideTheTesterWithoutANamespace_Fails(Type stepType, string name, string typeName)
    {
        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().Build(stepType));

        Assert.Equal(
            $"Step '{name}' ({InvalidStepsType}+{typeName}) has no namespace, which is reserved for the tester's own steps. Set Namespace on [Step] or [TestPlugin].",
            exception.Message);
    }

    [Fact]
    public void Build_StepInTheTestersOwnAssembly_MayHaveNoNamespace()
    {
        var descriptor = AsTester().Build(typeof(InvalidSteps.NoNamespace));

        Assert.Equal(string.Empty, descriptor.Namespace);
    }

    [Fact]
    public void Build_StepWithoutANamespace_UsesTheOneOfItsAssemblysTestPlugin()
    {
        var descriptor = new StepDescriptorBuilder().Build(typeof(Compile));

        Assert.Equal("sample.compile", descriptor.FullName);
    }

    [Fact]
    public void Build_StepWithANamespace_KeepsItOverItsAssemblysTestPlugin()
    {
        var descriptor = new StepDescriptorBuilder().Build(typeof(OwnNamespace));

        Assert.Equal("other.own-namespace", descriptor.FullName);
    }

    [Fact]
    public void Build_RequiredPropertyOnNet472_IsRequired()
    {
        var descriptor = new StepDescriptorBuilder().Build(typeof(Compile));

        Assert.True(Assert.Single(descriptor.Arguments).IsRequired);
    }

    [Theory]
    [InlineData(typeof(InvalidSteps.StepNameNotKebab), "PlaceBlock", "StepNameNotKebab")]
    [InlineData(typeof(InvalidSteps.StepNameSnake), "place_block", "StepNameSnake")]
    public void Build_StepNameNotInKebabCase_Fails(Type stepType, string name, string typeName)
    {
        AssertFails(stepType, $"Step name '{name}' ({InvalidStepsType}+{typeName}) isn't in kebab case, such as 'place-block'.");
    }

    [Fact]
    public void Build_NamespaceNotInKebabCase_Fails()
    {
        AssertFails(typeof(InvalidSteps.NamespaceNotKebab), $"Namespace 'Bad_Namespace' ({InvalidStepsType}+NamespaceNotKebab) isn't in kebab case, such as 'place-block'.");
    }

    [Fact]
    public void Build_ArgumentNameNotInKebabCase_Fails()
    {
        AssertFails(typeof(InvalidSteps.ArgNameNotKebab), $"Argument name 'Some_Name' ({InvalidStepsType}+ArgNameNotKebab) isn't in kebab case, such as 'place-block'.");
    }

    [Fact]
    public void Build_UnsupportedArgumentType_Fails()
    {
        AssertFails(typeof(InvalidSteps.UnsupportedType), $"Argument 'when' ({InvalidStepsType}+UnsupportedType) has the type System.DateTime, which isn't an argument type.");
    }

    [Fact]
    public void Build_ClassWithoutArgProperties_Fails()
    {
        AssertFails(typeof(InvalidSteps.HasClassWithoutArgs), $"Argument 'value' ({InvalidStepsType}+HasClassWithoutArgs) has the type {InvalidStepsType}+ClassWithoutArgs, which isn't an argument type.");
    }

    [Fact]
    public void Build_NestedClassContainingItself_Fails()
    {
        AssertFails(typeof(InvalidSteps.HasRecursive), $"{InvalidStepsType}+Recursive contains itself through argument 'children'.");
    }

    [Fact]
    public void Build_TypeWithoutStepAttribute_Fails()
    {
        AssertFails(typeof(InvalidSteps.NoStepAttribute), $"{InvalidStepsType}+NoStepAttribute has no [Step] attribute.");
    }

    [Fact]
    public void Build_StepAttributeOnANonStep_Fails()
    {
        AssertFails(typeof(InvalidSteps.NotAStep), $"{InvalidStepsType}+NotAStep has [Step] but doesn't derive from Step.");
    }

    [Fact]
    public void BuildAll_SameNameInOneNamespace_Fails()
    {
        var exception = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildAll([typeof(InvalidSteps.SameNameOne), typeof(InvalidSteps.SameNameTwo)]));

        Assert.Equal($"{InvalidStepsType}+SameNameOne and {InvalidStepsType}+SameNameTwo are both the step 'a.same-name'.", exception.Message);
    }

    [Fact]
    public void BuildAll_SameNameInDifferentNamespaces_BuildsBoth()
    {
        var descriptors = new StepDescriptorBuilder().BuildAll([typeof(InvalidSteps.SameNameOne), typeof(InvalidSteps.SameNameOtherNamespace)]);

        Assert.Equal(2, descriptors.Count);
    }

    private static void AssertFails(Type stepType, string message)
    {
        var exception = Assert.Throws<StepDefinitionException>(() => AsTester().Build(stepType));

        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    /// The builder as if this assembly were the tester's, so only the rule under test fails.
    /// </summary>
    private static StepDescriptorBuilder AsTester()
    {
        return new StepDescriptorBuilder { TesterAssemblyName = typeof(InvalidSteps).Assembly.GetName().Name };
    }
}
