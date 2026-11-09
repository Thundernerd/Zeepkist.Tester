using System.IO;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class RunRequestTests
{
    private const string RunJson = """
        {
          "runId": "abc",
          "mods": [
            { "guid": "Some.Mod", "oldGuids": ["net.someone.some.mod"] },
            { "guid": "Other.Mod" }
          ]
        }
        """;

    [Fact]
    public void TheScenarios_AreTheirFilesInOrder_NextToThePlugin()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), """{ "runId": "abc", "scenarios": ["b.ztest.yaml", "shared/a.ztest.yaml"] }""");

        Assert.True(RunRequest.TryRead(folder.Path, ["Zeepkist.exe", "-ztest", "abc"], out var request));

        Assert.Equal(
            [Path.Combine(folder.Path, "scenarios", "b.ztest.yaml"), Path.Combine(folder.Path, "scenarios", "shared", "a.ztest.yaml")],
            request.ScenarioFiles(folder.Path));
    }

    [Fact]
    public void ARunJsonWithoutScenarios_HasNone()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), RunJson);

        Assert.True(RunRequest.TryRead(folder.Path, ["Zeepkist.exe", "-ztest", "abc"], out var request));

        Assert.Empty(request.ScenarioFiles(folder.Path));
        Assert.Empty(request.Tests);
    }

    [Fact]
    public void TheTests_AreWhatItNames_InOrder()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), """{ "runId": "abc", "tests": ["WindowTests", "OtherTests.One"] }""");

        Assert.True(RunRequest.TryRead(folder.Path, ["Zeepkist.exe", "-ztest", "abc"], out var request));

        Assert.Equal(["WindowTests", "OtherTests.One"], request.Tests);
    }

    [Fact]
    public void TheFlag_WithAMatchingRunJson_IsARun()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), RunJson);

        Assert.True(RunRequest.TryRead(folder.Path, ["Zeepkist.exe", "-ztest", "abc"], out var request));

        Assert.Equal("abc", request.RunId);
        Assert.Collection(
            request.Mods,
            mod =>
            {
                Assert.Equal("Some.Mod", mod.Guid);
                Assert.Equal(["net.someone.some.mod"], mod.OldGuids);
            },
            mod =>
            {
                Assert.Equal("Other.Mod", mod.Guid);
                Assert.Empty(mod.OldGuids);
            });
    }

    [Theory]
    [InlineData]
    [InlineData("-ztest")]
    [InlineData("-ztest", "other")]
    [InlineData("-other-flag", "abc")]
    public void WithoutTheFlagAndItsRunId_ThereIsNoRun(params string[] args)
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), RunJson);

        Assert.False(RunRequest.TryRead(folder.Path, args, out _));
    }

    [Fact]
    public void WithARunJsonForAnotherRun_ThereIsNoRequest()
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), RunJson);

        Assert.False(RunRequest.TryRead(folder.Path, ["-ztest", "other"], out var request));

        Assert.Null(request);
    }

    [Fact]
    public void WithoutARunJson_ThereIsNoRun()
    {
        using var folder = new TemporaryFolder();

        Assert.False(RunRequest.TryRead(folder.Path, ["-ztest", "abc"], out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"runId\": ")]
    [InlineData("[]")]
    public void WithARunJsonThatCantBeRead_ThereIsNoRun(string text)
    {
        using var folder = new TemporaryFolder();
        File.WriteAllText(Path.Combine(folder.Path, "run.json"), text);

        Assert.False(RunRequest.TryRead(folder.Path, ["-ztest", "abc"], out _));
    }
}
