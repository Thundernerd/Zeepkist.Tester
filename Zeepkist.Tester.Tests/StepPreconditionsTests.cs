using System;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;
using static TNRD.Zeepkist.Tester.Tests.TestSteps;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepPreconditionsTests
{
    private readonly StatesContext _context = new StatesContext();

    [Fact]
    public void Run_RequiredStateHolds_RunsTheStep()
    {
        Met(typeof(InEditor), typeof(WindowOpen), typeof(ScriptSelected));

        var (yielded, failed) = Run(new Compile());

        Assert.Null(failed);
        Assert.Equal(["ran"], yielded);
        Assert.Equal([typeof(Compile)], _context.Ran);
    }

    [Fact]
    public void Run_RequiredStateDoesNotHold_FailsWithoutRunningTheStep()
    {
        Met(typeof(InEditor), typeof(WindowOpen));

        var (yielded, failed) = Run(new Compile());

        var exception = Assert.IsType<StepFailedException>(failed);
        Assert.Equal("zls.compile needs zls.script-selected: Add zls.new-script or zls.select-script first.", exception.Message);
        Assert.Empty(yielded);
        Assert.Empty(_context.Ran);
    }

    [Fact]
    public void Run_NoRequirements_RunsTheStep()
    {
        var (_, failed) = Run(new CloseWindow());

        Assert.Null(failed);
        Assert.Equal([typeof(CloseWindow)], _context.Ran);
    }

    [Fact]
    public void Run_SeveralRequiredStates_NeedsAllOfThem()
    {
        Met(typeof(InEditor));

        var (_, failed) = Run(new OpenWindow());

        Assert.Equal("zls.open-window needs zls.no-hint.", failed?.Message);
        Assert.Empty(_context.Ran);
    }

    [Fact]
    public void Run_StateWithoutAHint_EndsAfterItsName()
    {
        var (_, failed) = Run(new NeedsNoHint());

        Assert.Equal("zls.no-hint needs zls.no-hint.", failed?.Message);
    }

    [Theory]
    [InlineData(typeof(ScriptSelected))]
    [InlineData(typeof(FieldFocused))]
    public void Run_RequiresAny_OneOfThemIsEnough(Type met)
    {
        Met(typeof(InEditor), typeof(WindowOpen), met);

        var (_, failed) = Run(new NeedsEither());

        Assert.Null(failed);
        Assert.Equal([typeof(NeedsEither)], _context.Ran);
    }

    [Fact]
    public void Run_RequiresAny_NoneHolds_NamesThemAll()
    {
        Met(typeof(InEditor), typeof(WindowOpen));

        var (_, failed) = Run(new NeedsEither());

        Assert.Equal(
            "zls.needs-either needs zls.script-selected or zls.field-focused: Add zls.new-script or zls.select-script first. Add zls.caret-at-end first.",
            failed?.Message);
        Assert.Empty(_context.Ran);
    }

    [Fact]
    public void Run_RequiresAndRequiresAny_NeedsBoth()
    {
        Met(typeof(WindowOpen), typeof(ScriptSelected));

        var (_, failed) = Run(new NeedsEither());

        Assert.Equal("zls.needs-either needs zls.editor: Start with start: editor.", failed?.Message);
    }

    [Fact]
    public void Run_TheStateItIsWithinWasLeft_DoesNotHoldEvenIfItSaysSo()
    {
        Met(typeof(InEditor), typeof(ScriptSelected));

        var (_, failed) = Run(new Compile());

        Assert.Equal("zls.compile needs zls.window-open: Add zls.open-window first.", failed?.Message);
        Assert.Empty(_context.Ran);
    }

    [Fact]
    public void Run_SeveralStatesLeft_NamesTheOutermostOne()
    {
        var (_, failed) = Run(new Compile());

        Assert.Equal("zls.compile needs zls.editor: Start with start: editor.", failed?.Message);
    }

    [Fact]
    public void Run_RequiresAny_TheStateTheyAreWithinWasLeft_NamesItOnce()
    {
        Met(typeof(InEditor), typeof(ScriptSelected), typeof(FieldFocused));

        var (_, failed) = Run(new NeedsEither());

        Assert.Equal("zls.needs-either needs zls.window-open: Add zls.open-window first.", failed?.Message);
    }

    private void Met(params Type[] states)
    {
        _context.Met.UnionWith(states);
    }

    private (List<object> Yielded, Exception Failed) Run(Step step)
    {
        Exception failed = null;
        var routine = GuardedRoutine.Run(StepPreconditions.Run(step, _context), exception => failed = exception);

        var yielded = new List<object>();
        while (routine.MoveNext())
            yielded.Add(routine.Current);

        return (yielded, failed);
    }
}
