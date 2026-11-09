using System;
using System.Collections;
using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What one scenario keeps while it runs, which goes when it ends.
/// </summary>
internal class ScenarioContext : RunContext, IScenarioContext
{
    private readonly Dictionary<Type, object> _state;

    public ScenarioContext(RunContext run, string scenarioName)
        : this(run, scenarioName, [])
    {
    }

    /// <param name="run">The run it's part of.</param>
    /// <param name="scenarioName">The running scenario.</param>
    /// <param name="state">What <see cref="Scenario{T}"/> keeps, which each of the scenario's steps shares.</param>
    private protected ScenarioContext(RunContext run, string scenarioName, Dictionary<Type, object> state)
        : base(run.Logger, run.Services)
    {
        ScenarioName = scenarioName;
        _state = state;
    }

    public string ScenarioName { get; }

    public T Scenario<T>()
        where T : class, new()
    {
        if (_state.TryGetValue(typeof(T), out var state))
            return (T)state;

        var made = new T();
        _state.Add(typeof(T), made);
        return made;
    }

    /// <summary>
    /// A context for one step of this scenario, or for a C# test, which runs steps with <paramref name="run"/>.
    /// </summary>
    public StepContext ForStep(IRunHost host, IExceptionLog exceptions, IValues values, Func<Step, IEnumerator> run = null)
    {
        return new StepContext(this, host, exceptions, values, _state, run);
    }
}
