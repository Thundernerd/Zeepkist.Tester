using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>
/// A mod's assembly as the tests need it: on net472, so <c>required</c> comes from the SDK's polyfills, and with its own <see cref="TestPluginAttribute"/>.
/// </summary>
[TestPlugin(Namespace = "sample")]
public sealed class SamplePlugin
{
}
