using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class GuardedRoutineTests
{
    [Fact]
    public void WhatTheRoutineYields_ComesThrough_WithNestedRoutinesFlattened()
    {
        var yielded = Drain(GuardedRoutine.Run(Outer(), _ => Assert.Fail("nothing threw")));

        Assert.Equal(["outer 1", "inner", "outer 2"], yielded);
    }

    [Fact]
    public void AnException_StopsTheRoutine_AndGoesToFailed()
    {
        Exception failed = null;

        var yielded = Drain(GuardedRoutine.Run(Throws(), exception => failed = exception));

        Assert.Equal(["before"], yielded);
        Assert.Equal("boom", failed?.Message);
    }

    [Fact]
    public void AnExceptionInANestedRoutine_StopsTheOuterOneToo()
    {
        Exception failed = null;

        var yielded = Drain(GuardedRoutine.Run(CallsThrows(), exception => failed = exception));

        Assert.Equal(["before"], yielded);
        Assert.Equal("boom", failed?.Message);
    }

    private static List<object> Drain(IEnumerator routine)
    {
        List<object> yielded = [];
        while (routine.MoveNext())
            yielded.Add(routine.Current);

        return yielded;
    }

    private static IEnumerator Outer()
    {
        yield return "outer 1";
        yield return Inner();
        yield return "outer 2";
    }

    private static IEnumerator Inner()
    {
        yield return "inner";
    }

    private static IEnumerator Throws()
    {
        yield return "before";
        throw new InvalidOperationException("boom");
    }

    private static IEnumerator CallsThrows()
    {
        yield return Throws();
        yield return "after";
    }
}
