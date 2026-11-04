using System.Collections;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests;

/// <summary>
/// Steps that each break one of the registry's rules.
/// </summary>
public static class InvalidSteps
{
    public abstract class Base : Step
    {
        public override IEnumerator Run(IStepContext context)
        {
            yield break;
        }
    }

    [Step("two-primaries")]
    public sealed class TwoPrimaries : Base
    {
        [Arg(Primary = true)]
        public string First { get; set; }

        [Arg(Primary = true)]
        public string Second { get; set; }
    }

    public sealed class NestedTwoPrimaries
    {
        [Arg(Primary = true)]
        public string First { get; set; }

        [Arg(Primary = true)]
        public string Second { get; set; }
    }

    [Step("nested-two-primaries")]
    public sealed class HasNestedTwoPrimaries : Base
    {
        [Arg]
        public List<NestedTwoPrimaries> Items { get; set; }
    }

    [Step("no-namespace")]
    public sealed class NoNamespace : Base
    {
    }

    [Step("empty-namespace", Namespace = "")]
    public sealed class EmptyNamespace : Base
    {
    }

    [Step("PlaceBlock")]
    public sealed class StepNameNotKebab : Base
    {
    }

    [Step("place_block")]
    public sealed class StepNameSnake : Base
    {
    }

    [Step("good", Namespace = "Bad_Namespace")]
    public sealed class NamespaceNotKebab : Base
    {
    }

    [Step("arg-name-not-kebab")]
    public sealed class ArgNameNotKebab : Base
    {
        [Arg(Name = "Some_Name")]
        public string Value { get; set; }
    }

    [Step("same-name", Namespace = "a")]
    public sealed class SameNameOne : Base
    {
    }

    [Step("same-name", Namespace = "a")]
    public sealed class SameNameTwo : Base
    {
    }

    [Step("same-name", Namespace = "b")]
    public sealed class SameNameOtherNamespace : Base
    {
    }

    [Step("unsupported-type")]
    public sealed class UnsupportedType : Base
    {
        [Arg]
        public System.DateTime When { get; set; }
    }

    public sealed class ClassWithoutArgs
    {
        public string Value { get; set; }
    }

    [Step("class-without-args")]
    public sealed class HasClassWithoutArgs : Base
    {
        [Arg]
        public ClassWithoutArgs Value { get; set; }
    }

    public sealed class Recursive
    {
        [Arg]
        public List<Recursive> Children { get; set; }
    }

    [Step("recursive")]
    public sealed class HasRecursive : Base
    {
        [Arg]
        public Recursive Root { get; set; }
    }

    public sealed class NoStepAttribute : Base
    {
    }

    [Step("not-a-step")]
    public sealed class NotAStep
    {
    }
}
