using System.Collections.Generic;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class StepChecksTests
{
    private readonly Recording _context = new Recording();

    [Fact]
    public void Check_ThatHolds_PassesWithWhatItSaw()
    {
        _context.Check(true, "3 blocks", "3", "3");

        Assert.Equal(["pass: 3 blocks | 3 | 3"], _context.Calls);
    }

    [Fact]
    public void Check_ThatDoesntHold_FailsWithWhatItSaw()
    {
        _context.Check(false, "2 blocks", "3", "2");

        Assert.Equal(["fail: 2 blocks | 3 | 2"], _context.Calls);
    }

    private sealed class Recording : BareStepContext
    {
        public List<string> Calls { get; } = [];

        public override void Pass(string detail, string expected, string actual)
        {
            Calls.Add($"pass: {detail} | {expected} | {actual}");
        }

        public override void Fail(string detail, string expected = null, string actual = null)
        {
            Calls.Add($"fail: {detail} | {expected} | {actual}");
        }
    }
}
