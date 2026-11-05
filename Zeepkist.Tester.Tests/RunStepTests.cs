using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class RunStepTests
{
    [Fact]
    public void AStepOutsideAScenario_ReadsAsItsNameAndOutcome()
    {
        Assert.Equal("right build: Some.Mod PASS", RunStep.Pass("right build: Some.Mod").ToString());
    }

    [Fact]
    public void AFailedStep_ReadsWithItsDetail()
    {
        Assert.Equal("right build FAIL Some.Mod isn't loaded", RunStep.Fail("right build", "Some.Mod isn't loaded").ToString());
    }

    [Fact]
    public void AStepInAScenario_ReadsWithItsPlace_AndACheckWithWhatItCompared()
    {
        var step = new RunStep { Scenario = "smoke", Line = 3, Name = "expect-blocks 3", Passed = false, Detail = "wrong count", Expected = "3", Actual = "2" };

        Assert.Equal("smoke:3 expect-blocks 3 FAIL wrong count (expected: 3, actual: 2)", step.ToString());
    }
}
