using System.IO;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class BuildCheckTests
{
    private static readonly string _pluginsFolder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Sideloaded", "Plugins"));
    private static readonly ModUnderTest _mod = new() { Guid = "Some.Mod", OldGuids = ["net.someone.some.mod"] };
    private static readonly LoadedPlugin _deployed = new("Some.Mod", "Some Mod", Path.Combine(_pluginsFolder, "Some.Mod", "Some.Mod.dll"));

    [Fact]
    public void TheModLoadedFromWhereItWasDeployed_WithNoOtherCopy_HasNoProblem()
    {
        Assert.Null(BuildCheck.Problem(_mod, _pluginsFolder, [_deployed], []));
    }

    [Fact]
    public void AModThatIsntLoaded_IsAProblem()
    {
        var problem = BuildCheck.Problem(_mod, _pluginsFolder, [], []);

        Assert.Equal("Some.Mod isn't loaded", problem);
    }

    [Fact]
    public void AModLoadedFromElsewhere_IsAProblem_NamingBothFolders()
    {
        var elsewhere = Path.Combine(_pluginsFolder, "mod.io", "12345");
        var loaded = new LoadedPlugin("Some.Mod", "Some Mod", Path.Combine(elsewhere, "Some.Mod.dll"));

        var problem = BuildCheck.Problem(_mod, _pluginsFolder, [loaded], []);

        Assert.Equal($"Some.Mod loaded from {elsewhere}, not the deployed build in {Path.Combine(_pluginsFolder, "Some.Mod")}", problem);
    }

    [Fact]
    public void ACopyUnderAnOldGuid_IsAProblem()
    {
        var old = new LoadedPlugin("net.someone.some.mod", "Some Mod Before", "/old/Some.Mod.dll");

        var problem = BuildCheck.Problem(_mod, _pluginsFolder, [_deployed, old], []);

        Assert.Equal("Another copy of Some.Mod loaded too: /old/Some.Mod.dll", problem);
    }

    [Fact]
    public void ACopyWithTheSameNameUnderAnotherGuid_IsAProblem()
    {
        var other = new LoadedPlugin("Someone.Elses.Fork", "Some Mod", "/fork/Fork.dll");

        var problem = BuildCheck.Problem(_mod, _pluginsFolder, [_deployed, other], []);

        Assert.Equal("Another copy of Some.Mod loaded too: /fork/Fork.dll", problem);
    }

    [Fact]
    public void OtherMods_AreNoProblem()
    {
        var other = new LoadedPlugin("ZeepSDK", "ZeepSDK", "/mods/ZeepSDK.dll");

        Assert.Null(BuildCheck.Problem(_mod, _pluginsFolder, [_deployed, other], []));
    }

    [Fact]
    public void ACopyBepInExSkipped_IsAProblem()
    {
        string[] log =
        [
            "[Info   :   BepInEx] Loading [ZeepSDK 2.7.6]",
            "[Warning:   BepInEx] Skipping [Some Mod 1.0.0] because a newer version exists (Some Mod 1.1.0)",
        ];

        var problem = BuildCheck.Problem(_mod, _pluginsFolder, [_deployed], log);

        Assert.Equal("BepInEx found another copy: Skipping [Some Mod 1.0.0] because a newer version exists (Some Mod 1.1.0)", problem);
    }

    [Fact]
    public void AnotherMod_WhoseNameStartsTheSame_BeingSkipped_IsNoProblem()
    {
        string[] log = ["[Warning:   BepInEx] Skipping [Some Mod Extras 1.0.0] because a newer version exists (Some Mod Extras 1.1.0)"];

        Assert.Null(BuildCheck.Problem(_mod, _pluginsFolder, [_deployed], log));
    }
}
