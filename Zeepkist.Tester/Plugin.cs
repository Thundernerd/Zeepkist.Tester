using BepInEx;

namespace TNRD.Zeepkist.Tester;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(ZeepSdkInfo.PluginGuid, ZeepSdkInfo.Version)]
public class Plugin : BaseUnityPlugin
{
}
