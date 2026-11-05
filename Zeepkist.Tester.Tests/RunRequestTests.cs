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
