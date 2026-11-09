using System.Collections;
using System.Reflection;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class TestDescriptorRuleTests
{
    private const string Here = "TNRD.Zeepkist.Tester.Tests.TestDescriptorRuleTests";

    [Theory]
    [InlineData(nameof(WrongSignatures.Static))]
    [InlineData(nameof(WrongSignatures.ReturnsNothing))]
    [InlineData(nameof(WrongSignatures.TakesNothing))]
    [InlineData(nameof(WrongSignatures.TakesTwo))]
    [InlineData("Private")]
    public void BuildTest_AMethodOfAnotherShape_Fails(string method)
    {
        var error = Assert.Throws<StepDefinitionException>(() => Build(typeof(WrongSignatures), method));

        Assert.Equal($"[InGameTest] {Here}+WrongSignatures.{method} has to be public IEnumerator {method}(IStepContext context), not static or generic.", error.Message);
    }

    [Fact]
    public void BuildTest_InAClassThatCantBeMade_Fails()
    {
        var error = Assert.Throws<StepDefinitionException>(() => Build(typeof(NoEmptyConstructor), nameof(NoEmptyConstructor.Test)));

        Assert.Equal($"[InGameTest] {Here}+NoEmptyConstructor.Test is in a class that can't be made: it has to be a concrete, non-generic class with a public constructor that takes nothing.", error.Message);
    }

    [Fact]
    public void BuildTests_TwoClassesOfOneNameWithOneTest_Fails()
    {
        var first = typeof(First.Twin).GetMethod(nameof(First.Twin.Test));
        var second = typeof(Second.Twin).GetMethod(nameof(Second.Twin.Test));

        var error = Assert.Throws<StepDefinitionException>(() => new StepDescriptorBuilder().BuildTests([first, second]));

        Assert.Equal($"{Here}+First+Twin and {Here}+Second+Twin both have the test 'Twin.Test'. Rename one of them.", error.Message);
    }

    [Fact]
    public void IsSelectedBy_ItsClassOrItsName_AndNothingElse()
    {
        var test = new StepDescriptorBuilder().BuildTest(typeof(First.Twin).GetMethod(nameof(First.Twin.Test)));

        Assert.True(test.IsSelectedBy("Twin"));
        Assert.True(test.IsSelectedBy("Twin.Test"));
        Assert.False(test.IsSelectedBy("Twin.Tes"));
        Assert.False(test.IsSelectedBy("twin"));
        Assert.False(test.IsSelectedBy("First"));
    }

    private static TestDescriptor Build(System.Type type, string method)
    {
        return new StepDescriptorBuilder().BuildTest(type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
    }

    public sealed class WrongSignatures
    {
        [InGameTest]
        public static IEnumerator Static(IStepContext context)
        {
            yield break;
        }

        [InGameTest]
        public void ReturnsNothing(IStepContext context)
        {
        }

        [InGameTest]
        public IEnumerator TakesNothing()
        {
            yield break;
        }

        [InGameTest]
        public IEnumerator TakesTwo(IStepContext context, int other)
        {
            yield break;
        }

        [InGameTest]
        private IEnumerator Private(IStepContext context)
        {
            yield break;
        }
    }

    public sealed class NoEmptyConstructor
    {
        public NoEmptyConstructor(int value)
        {
        }

        [InGameTest]
        public IEnumerator Test(IStepContext context)
        {
            yield break;
        }
    }

    public static class First
    {
        public sealed class Twin
        {
            [InGameTest]
            public IEnumerator Test(IStepContext context)
            {
                yield break;
            }
        }
    }

    public static class Second
    {
        public sealed class Twin
        {
            [InGameTest]
            public IEnumerator Test(IStepContext context)
            {
                yield break;
            }
        }
    }
}
