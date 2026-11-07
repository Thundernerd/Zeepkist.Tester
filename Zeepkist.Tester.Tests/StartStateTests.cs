using System.Linq;
using TNRD.Zeepkist.Tester.Cli;
using TNRD.Zeepkist.Tester.Scenarios;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StartStateTests
{
    public static TheoryData<string> Names => [.. StartState.All.Select(state => state.Name)];

    /// <summary>
    /// <c>ztest check</c> holds a state by the name of the start, so that name has to be one a step can require.
    /// </summary>
    [Theory]
    [MemberData(nameof(Names))]
    public void EachStartState_IsAStateTheTesterDeclares_ThatNeedsNoOtherState(string name)
    {
        var state = Assert.Single(OfflineRegistry.Read([]).States, each => each.FullName == name);

        Assert.Null(state.Within);
        Assert.Contains($"start: {name}", state.Hint);
    }
}
