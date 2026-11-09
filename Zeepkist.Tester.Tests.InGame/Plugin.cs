using BepInEx;
using TNRD.Zeepkist.Tester.Sdk;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// A mod's test plugin as run.sh deploys it, to run scenarios in the real game with a service and hooks of a mod's.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(TesterInfo.Guid)]
[BepInDependency(ZeepSdkInfo.PluginGuid, ZeepSdkInfo.Version)]
[TestPlugin(Namespace = "sample")]
public class Plugin : BaseUnityPlugin
{
    private readonly InputWindow _window = new InputWindow();

    private void Awake()
    {
        UIApi.AddZeepGUIDrawer(_window);
        Sdk.Tester.Register(this)
            .Service(new HookCounts())
            .Service(_window)
            .Service(new UguiButton())
            .Hooks(new CountingHooks());
    }
}
