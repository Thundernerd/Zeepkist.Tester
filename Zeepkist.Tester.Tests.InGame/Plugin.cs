using BepInEx;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// A mod's test plugin as run.sh deploys it, to run scenarios in the real game with a service and hooks of a mod's.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(TesterInfo.Guid)]
[TestPlugin(Namespace = "sample")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        Sdk.Tester.Register(this)
            .Service(new HookCounts())
            .Hooks(new CountingHooks());
    }
}
