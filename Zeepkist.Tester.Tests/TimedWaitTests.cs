using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class TimedWaitTests
{
    [Fact]
    public void AConditionThatHolds_EndsTheWaitWithoutAFrame()
    {
        var wait = TimedWait.Until(() => true, "it", 5, () => 0);

        Assert.False(wait.MoveNext());
    }

    [Fact]
    public void TheWait_LastsUntilTheConditionHolds()
    {
        var holds = false;
        var wait = TimedWait.Until(() => holds, "it", 5, () => 0);

        Assert.True(wait.MoveNext());
        Assert.True(wait.MoveNext());
        holds = true;
        Assert.False(wait.MoveNext());
    }

    [Fact]
    public void AConditionThatNeverHolds_GivesUpSayingWhatItWaitedFor()
    {
        var now = 10f;
        var wait = TimedWait.Until(() => false, "the main menu", 5, () => now);

        Assert.True(wait.MoveNext());
        now = 15f;
        Assert.True(wait.MoveNext());
        now = 15.1f;
        var failed = Assert.Throws<StartFailedException>(() => wait.MoveNext());

        Assert.Equal("the main menu: not reached after 5 seconds", failed.Message);
    }
}
