using System.Reflection;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class SdkAssemblyTests
{
    [Fact]
    public void TheNet472Sdk_LoadsOnTheTestRuntime()
    {
        var sdk = Assembly.Load("Zeepkist.Tester.Sdk");

        Assert.Equal("Zeepkist.Tester.Sdk", sdk.GetName().Name);
    }
}
