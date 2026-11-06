using System;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class RegistryCheckTests
{
    [Fact]
    public void ACompleteRegistry_HasNoSmells()
    {
        Assert.Empty(Smells([typeof(Enter)], [typeof(NeededAndEntered)]));
    }

    [Fact]
    public void AStateNoStepEnters_IsASmell()
    {
        Assert.Contains("No step enters the state 'mod.never-entered', so the offline check never sees it hold.", Smells([typeof(NeedsNeverEntered)], []));
    }

    [Fact]
    public void AStartState_NeedsNoStepToEnterIt()
    {
        Assert.Empty(Smells([typeof(NeedsEditor)], []));
    }

    [Fact]
    public void AStateNothingUses_IsASmell()
    {
        Assert.Contains("Nothing uses the state 'mod.unused': no step requires it and no state is within it.", Smells([typeof(EntersUnused)], []));
    }

    [Fact]
    public void AStateWithAStateWithinIt_IsUsed()
    {
        Assert.DoesNotContain("Nothing uses the state 'mod.outer': no step requires it and no state is within it.", Smells([typeof(EntersInner)], []));
    }

    [Fact]
    public void AStateAStepNeedsWithoutAHint_IsASmell()
    {
        Assert.Contains("The state 'mod.no-hint' has no Hint, so a step that needs it can't say how to get into it.", Smells([typeof(NeedsNoHint)], []));
    }

    [Fact]
    public void AStepOrArgumentWithoutASummary_IsASmell()
    {
        var smells = Smells([typeof(Undocumented)], [], documented: false);

        Assert.Contains("The step 'mod.undocumented' has no <summary>, which its reference docs come from.", smells);
        Assert.Contains("'count' of mod.undocumented has no <summary>, which its reference docs come from.", smells);
    }

    [Fact]
    public void ADefaultThatDoesntFitItsOwnChecks_IsASmell()
    {
        Assert.Contains(
            "The default doesn't fit: 'count' of mod.bad-default has to be between 1 and 8, not '0'.",
            Smells([typeof(BadDefault)], []));
    }

    [Fact]
    public void ANestedArgument_IsCheckedToo()
    {
        Assert.Contains(
            "The default doesn't fit: 'count' of 'item' of mod.bad-nested-default has to be between 1 and 8, not '0'.",
            Smells([typeof(BadNestedDefault)], []));
    }

    [Fact]
    public void AGoodDefault_IsNoSmell()
    {
        Assert.Empty(Smells([typeof(GoodDefault)], []));
    }

    private static IReadOnlyList<string> Smells(Type[] steps, Type[] states, bool documented = true)
    {
        var builder = new StepDescriptorBuilder
        {
            TesterAssemblyName = typeof(RegistryCheckTests).Assembly.GetName().Name,
            Summaries = documented ? _ => "Documented." : _ => null,
        };

        return RegistryCheck.Smells(builder.BuildRegistry(steps, states));
    }

    [State("needed-and-entered", Namespace = "mod", Hint = "Add it.")]
    public sealed class NeededAndEntered : CheckSteps.EmptyState
    {
    }

    [State("never-entered", Namespace = "mod", Hint = "Add it.")]
    public sealed class NeverEntered : CheckSteps.EmptyState
    {
    }

    [State("editor", Hint = "Start there.")]
    public sealed class Editor : CheckSteps.EmptyState
    {
    }

    [State("unused", Namespace = "mod")]
    public sealed class Unused : CheckSteps.EmptyState
    {
    }

    [State("outer", Namespace = "mod", Hint = "Add it.")]
    public sealed class Outer : CheckSteps.EmptyState
    {
    }

    [State("inner", Namespace = "mod", Within = typeof(Outer), Hint = "Add it.")]
    public sealed class Inner : CheckSteps.EmptyState
    {
    }

    [State("no-hint", Namespace = "mod")]
    public sealed class NoHint : CheckSteps.EmptyState
    {
    }

    [Step("enter", Namespace = "mod")]
    [Requires(typeof(NeededAndEntered))]
    [Enters(typeof(NeededAndEntered))]
    public sealed class Enter : CheckSteps.EmptyStep
    {
    }

    [Step("needs-never-entered", Namespace = "mod")]
    [Requires(typeof(NeverEntered))]
    public sealed class NeedsNeverEntered : CheckSteps.EmptyStep
    {
    }

    [Step("needs-editor")]
    [Requires(typeof(Editor))]
    public sealed class NeedsEditor : CheckSteps.EmptyStep
    {
    }

    [Step("enters-unused", Namespace = "mod")]
    [Enters(typeof(Unused))]
    public sealed class EntersUnused : CheckSteps.EmptyStep
    {
    }

    [Step("enters-inner", Namespace = "mod")]
    [Enters(typeof(Inner))]
    public sealed class EntersInner : CheckSteps.EmptyStep
    {
    }

    [Step("needs-no-hint", Namespace = "mod")]
    [Requires(typeof(NoHint))]
    [Enters(typeof(NoHint))]
    public sealed class NeedsNoHint : CheckSteps.EmptyStep
    {
    }

    [Step("undocumented", Namespace = "mod")]
    public sealed class Undocumented : CheckSteps.EmptyStep
    {
        [Arg]
        public int Count { get; set; }
    }

    [Step("bad-default", Namespace = "mod")]
    public sealed class BadDefault : CheckSteps.EmptyStep
    {
        [Arg(Default = 0)]
        [InRange(1, 8)]
        public int Count { get; set; }
    }

    public sealed class Item
    {
        [Arg(Default = 0)]
        [InRange(1, 8)]
        public int Count { get; set; }
    }

    [Step("bad-nested-default", Namespace = "mod")]
    public sealed class BadNestedDefault : CheckSteps.EmptyStep
    {
        [Arg]
        public Item Item { get; set; }
    }

    [Step("good-default", Namespace = "mod")]
    public sealed class GoodDefault : CheckSteps.EmptyStep
    {
        [Arg(Default = 2)]
        [InRange(1, 8)]
        public int Count { get; set; }

        [Arg(Default = true)]
        public bool Flag { get; set; }
    }
}
