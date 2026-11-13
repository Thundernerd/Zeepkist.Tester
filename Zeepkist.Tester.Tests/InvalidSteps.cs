using System;
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

    [Step("bad-pattern", Namespace = "zls")]
    public sealed class BadPattern : Base
    {
        [Arg]
        [Pattern("[")]
        public string Text { get; set; }
    }

    [Step("repeat", Namespace = "zls")]
    public sealed class NamedLikeAControlNode : Base
    {
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

    [Step("same-argument-name")]
    public sealed class SameArgumentName : Base
    {
        [Arg(Name = "value")]
        public string First { get; set; }

        [Arg(Name = "value")]
        public string Second { get; set; }
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
        public DateTime When { get; set; }
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

    public sealed class NoValueKind
    {
    }

    [Step("name-without-kind")]
    public sealed class NameWithoutKind : Base
    {
        [Arg]
        public Name<NoValueKind> Value { get; set; }
    }

    [Step("ref-without-kind")]
    public sealed class RefWithoutKind : Base
    {
        [Arg]
        public Ref<NoValueKind> Value { get; set; }
    }

    [ValueKind("Not_Kebab")]
    public sealed class KindNotKebab
    {
    }

    [Step("kind-not-kebab")]
    public sealed class HasKindNotKebab : Base
    {
        [Arg]
        public Name<KindNotKebab> Value { get; set; }
    }

    [ValueKind("no-member", Substitute = "Missing")]
    public sealed class SubstituteMissing
    {
    }

    [Step("substitute-missing")]
    public sealed class HasSubstituteMissing : Base
    {
        [Arg]
        public Name<SubstituteMissing> Value { get; set; }
    }

    [ValueKind("number-member", Substitute = nameof(Number))]
    public sealed class SubstituteNotAString
    {
        public int Number { get; set; }
    }

    [Step("substitute-not-a-string")]
    public sealed class HasSubstituteNotAString : Base
    {
        [Arg]
        public Name<SubstituteNotAString> Value { get; set; }
    }

    [ValueKind("lamp")]
    public sealed class LampOne
    {
    }

    [ValueKind("lamp")]
    public sealed class LampTwo
    {
    }

    [Step("defines-lamp-one", Namespace = "a")]
    public sealed class DefinesLampOne : Base
    {
        [Arg]
        public Name<LampOne> Value { get; set; }
    }

    [Step("uses-lamp-two", Namespace = "b")]
    public sealed class UsesLampTwo : Base
    {
        [Arg]
        public List<Ref<LampTwo>> Values { get; set; }
    }

    [Step("uses-lamp-one", Namespace = "b")]
    public sealed class UsesLampOne : Base
    {
        [Arg]
        public Ref<LampOne> Value { get; set; }
    }

    [ValueKind("lamp")]
    public class LampBase
    {
    }

    [ValueKind("dimmable-lamp")]
    public sealed class DimmableLamp : LampBase
    {
    }

    [Step("uses-dimmable-lamp", Namespace = "b")]
    public sealed class UsesDimmableLamp : Base
    {
        [Arg]
        public Ref<DimmableLamp> Value { get; set; }
    }

    public sealed class NoStepAttribute : Base
    {
    }

    [Step("not-a-step")]
    public sealed class NotAStep
    {
    }

    [Step("line-syntax-unknown-argument")]
    [LineSyntax("<function> as <name>")]
    public sealed class LineSyntaxUnknownArgument : Base
    {
        [Arg(Primary = true)]
        public string Function { get; set; }
    }

    [Step("line-syntax-unclosed")]
    [LineSyntax("<function> [as <function>")]
    public sealed class LineSyntaxUnclosed : Base
    {
        [Arg(Primary = true)]
        public string Function { get; set; }
    }

    [Step("line-syntax-object")]
    [LineSyntax("<move>")]
    public sealed class LineSyntaxObject : Base
    {
        [Arg(Primary = true)]
        public TestSteps.DragMove Move { get; set; }
    }

    [Step("line-syntax-twice")]
    [LineSyntax("<function> <function>")]
    public sealed class LineSyntaxTwice : Base
    {
        [Arg(Primary = true)]
        public string Function { get; set; }
    }
}
