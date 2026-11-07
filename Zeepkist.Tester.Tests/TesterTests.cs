using System.Linq;
using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class TesterTests
{
    [Fact]
    public void Registering_KeepsWhatThePluginGave_ForTheHarness()
    {
        var service = new object();

        var registration = Sdk.Tester.Register("a plugin").Service(service).Service<string>("text");

        Assert.Contains(registration, Sdk.Tester.Registered);
        Assert.Equal("a plugin", registration.PluginName);
        Assert.Same(service, registration.Services.Single(entry => entry.Key == typeof(object)).Value);
        Assert.Equal("text", registration.Services.Single(entry => entry.Key == typeof(string)).Value);
    }

    [Fact]
    public void TheTestersGuid_IsThePlugins()
    {
        Assert.Equal(MyPluginInfo.PLUGIN_GUID, TesterInfo.Guid);
    }
}
