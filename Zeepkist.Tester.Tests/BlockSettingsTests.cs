using System.Collections.Generic;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class BlockSettingsTests
{
    private readonly Dictionary<string, int> _whole = new Dictionary<string, int> { ["xbs"] = 0 };
    private readonly Dictionary<string, float> _numbers = new Dictionary<string, float> { ["xfl"] = 1.5f };
    private readonly Dictionary<string, string> _text = new Dictionary<string, string> { ["xtx"] = "old" };

    private BlockSettings Settings => new BlockSettings(_whole, _numbers, _text);

    [Fact]
    public void AWholeNumberSetting_TakesAWholeNumber()
    {
        var written = Settings.Write("xbs", "2", out var problem);

        Assert.Null(problem);
        Assert.Equal("2", written);
        Assert.Equal(2, _whole["xbs"]);
        Assert.Equal("2", Settings.Read("xbs"));
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    public void AWholeNumberSetting_TakesTrueOrFalse_AsTheGameStoresThem(string value, int stored)
    {
        Settings.Write("xbs", value, out var problem);

        Assert.Null(problem);
        Assert.Equal(stored, _whole["xbs"]);
    }

    [Fact]
    public void ADecimalSetting_TakesADecimal_WhateverTheCulture()
    {
        var written = Settings.Write("xfl", "2.25", out var problem);

        Assert.Null(problem);
        Assert.Equal("2.25", written);
        Assert.Equal(2.25f, _numbers["xfl"]);
    }

    [Fact]
    public void ATextSetting_TakesAnyText()
    {
        var written = Settings.Write("xtx", "new", out var problem);

        Assert.Null(problem);
        Assert.Equal("new", written);
        Assert.Equal("new", _text["xtx"]);
    }

    [Theory]
    [InlineData("xbs", "soon", "xbs takes a whole number, true or false, not 'soon'")]
    [InlineData("xfl", "soon", "xfl takes a number, not 'soon'")]
    public void AValueThatDoesntFit_ChangesNothing_AndSaysWhy(string key, string value, string why)
    {
        var written = Settings.Write(key, value, out var problem);

        Assert.Null(written);
        Assert.Equal(why, problem);
        Assert.Equal(0, _whole["xbs"]);
        Assert.Equal(1.5f, _numbers["xfl"]);
    }

    [Fact]
    public void ASettingTheBlockDoesntHave_SaysWhichItHas()
    {
        var written = Settings.Write("nope", "1", out var problem);

        Assert.Null(written);
        Assert.Equal("The block has no setting 'nope'. It has xbs, xfl, xtx.", problem);
    }

    [Fact]
    public void ABlockWithNoSettings_SaysSo()
    {
        var written = new BlockSettings(null, null, null).Write("nope", "1", out var problem);

        Assert.Null(written);
        Assert.Equal("The block has no setting 'nope'. It has none.", problem);
    }

    [Fact]
    public void ASettingLeftOut_ReadsAsNull()
    {
        Assert.Null(new BlockSettings(null, null, null).Read("xbs"));
    }
}
