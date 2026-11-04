using System.IO;
using TNRD.Zeepkist.Tester.Cli;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class ProgramTests
{
    [Fact]
    public void Run_PrintsUsageAndSucceeds()
    {
        var output = new StringWriter();

        var exitCode = Program.Run(output);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: ztest", output.ToString());
    }
}
