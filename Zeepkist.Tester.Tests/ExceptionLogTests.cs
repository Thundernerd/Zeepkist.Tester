using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ExceptionLogTests
{
    private const string Trace = "ZeepSDK.UI.UIApi.RemoveTooltip (UnityEngine.GameObject gameObject) (at ZeepSDK/UI/UIApi.cs:223)\n"
        + "ZeepSDK.UI.Tooltipper.OnDestroy () (at ZeepSDK/UI/Tooltipper.cs:19)\n";

    [Fact]
    public void AnException_KeepsItsMessageAndFirstStackLine()
    {
        var log = new ExceptionLog();
        log.StartScenario("smoke");

        log.Record("NullReferenceException: Object reference not set", Trace);

        var logged = Assert.Single(log.Logged);
        Assert.Equal("smoke", logged.Scenario);
        Assert.Equal("NullReferenceException: Object reference not set", logged.Message);
        Assert.Equal("ZeepSDK.UI.UIApi.RemoveTooltip (UnityEngine.GameObject gameObject) (at ZeepSDK/UI/UIApi.cs:223)", logged.Frame);
        Assert.Equal(1, logged.Count);
    }

    [Fact]
    public void TheSameException_IsCountedOnce()
    {
        var log = new ExceptionLog();

        log.Record("NullReferenceException: a", Trace);
        log.Record("NullReferenceException: a", Trace);
        log.Record("NullReferenceException: b", Trace);

        Assert.Collection(
            log.Logged,
            logged => Assert.Equal(2, logged.Count),
            logged => Assert.Equal("NullReferenceException: b", logged.Message));
    }

    [Fact]
    public void TheSameException_InAnotherScenario_IsListedThere()
    {
        var log = new ExceptionLog();
        log.StartScenario("smoke");
        log.Record("NullReferenceException: a", Trace);

        log.StartScenario("test-mode");
        log.Record("NullReferenceException: a", Trace);

        Assert.Collection(
            log.Logged,
            logged => Assert.Equal("smoke", logged.Scenario),
            logged => Assert.Equal("test-mode", logged.Scenario));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    public void AnExceptionWithoutAStack_HasNoFrame(string trace)
    {
        var log = new ExceptionLog();

        log.Record("Exception: a", trace);

        Assert.Equal("", Assert.Single(log.Logged).Frame);
    }

    [Fact]
    public void TakeSinceCheck_HasTheExceptionsSinceTheLastTake()
    {
        var log = new ExceptionLog();
        log.Record("Exception: a", Trace);
        log.Record("Exception: a", Trace);

        var first = log.TakeSinceCheck();
        log.Record("Exception: b", Trace);
        var second = log.TakeSinceCheck();

        Assert.Equal(2, Assert.Single(first).Count);
        Assert.Equal("Exception: b", Assert.Single(second).Message);
        Assert.Empty(log.TakeSinceCheck());
        Assert.Equal(2, log.Logged.Count);
    }

    [Fact]
    public void TakeSinceCheck_StartsAgainWithEachScenario()
    {
        var log = new ExceptionLog();
        log.Record("Exception: a", Trace);

        log.StartScenario("smoke");

        Assert.Empty(log.TakeSinceCheck());
    }

    [Fact]
    public void AnException_ReadsAsItsMessageAtItsFrame_WithItsCount()
    {
        var log = new ExceptionLog();
        log.Record("Exception: a", "Foo.Bar ()\n");
        log.Record("Exception: a", "Foo.Bar ()\n");
        log.Record("Exception: b", "");

        Assert.Equal(["2× Exception: a at Foo.Bar ()", "Exception: b"], log.Logged.ConvertAll(logged => logged.ToString()));
    }
}
