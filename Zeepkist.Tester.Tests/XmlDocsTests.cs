using System.IO;
using TNRD.Zeepkist.Tester.Cli;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class XmlDocsTests
{
    [Theory]
    [InlineData("Waits a while.", "Waits a while.")]
    [InlineData("\n            Waits\n            a while.\n        ", "Waits a while.")]
    [InlineData("The function, such as <c>lamp</c>.", "The function, such as `lamp`.")]
    [InlineData("Like <see cref=\"T:Mod.Steps.Wait\"/>.", "Like `Wait`.")]
    [InlineData("Like <see cref=\"P:Mod.Steps.Wait.Seconds\"/>.", "Like `Seconds`.")]
    [InlineData("Like <see cref=\"M:Mod.Steps.Wait.Run(Mod.Context,System.Int32)\"/>.", "Like `Run`.")]
    [InlineData("Like <see cref=\"T:Mod.Named`1\"/>.", "Like `Named`.")]
    [InlineData("Like <see cref=\"M:Mod.Steps.Wait.#ctor(System.Int32)\"/>.", "Like `Wait`.")]
    [InlineData("Uses <paramref name=\"count\"/> and <typeparamref name=\"T\"/>.", "Uses `count` and `T`.")]
    [InlineData("Like <see cref=\"T:Mod.Steps.Wait\">waiting</see>.", "Like waiting.")]
    [InlineData("Or <see langword=\"null\"/>.", "Or `null`.")]
    [InlineData("See <see href=\"https://example.com\">the site</see>.", "See [the site](https://example.com).")]
    [InlineData("See <see href=\"https://example.com\"/>.", "See [https://example.com](https://example.com).")]
    public void Read_Summary_BecomesOneLineOfMarkdown(string summary, string markdown)
    {
        using var folder = new TemporaryFolder();
        var assembly = WriteDocs(folder, "Mod", $"<member name=\"T:Mod.Wait\"><summary>{summary}</summary></member>");

        Assert.Equal(markdown, XmlDocs.Read([assembly])("T:Mod.Wait"));
    }

    [Fact]
    public void Read_MemberWithoutASummary_HasNoDocumentation()
    {
        using var folder = new TemporaryFolder();
        var assembly = WriteDocs(folder, "Mod", "<member name=\"T:Mod.Wait\"><remarks>Not this.</remarks></member>");

        var summaries = XmlDocs.Read([assembly]);

        Assert.Null(summaries("T:Mod.Wait"));
        Assert.Null(summaries("T:Mod.Other"));
    }

    [Fact]
    public void Read_AssemblyWithoutADocFile_HasNoDocumentation()
    {
        using var folder = new TemporaryFolder();

        Assert.Null(XmlDocs.Read([Path.Combine(folder.Path, "Mod.dll")])("T:Mod.Wait"));
    }

    [Fact]
    public void Read_SeveralAssemblies_FindsTheMembersOfEach()
    {
        using var folder = new TemporaryFolder();
        var first = WriteDocs(folder, "First", "<member name=\"T:First.Step\"><summary>One.</summary></member>");
        var second = WriteDocs(folder, "Second", "<member name=\"T:Second.Step\"><summary>Two.</summary></member>");

        var summaries = XmlDocs.Read([first, second]);

        Assert.Equal("One.", summaries("T:First.Step"));
        Assert.Equal("Two.", summaries("T:Second.Step"));
    }

    private static string WriteDocs(TemporaryFolder folder, string assemblyName, string members)
    {
        File.WriteAllText(Path.Combine(folder.Path, $"{assemblyName}.xml"), $"<doc><members>{members}</members></doc>");
        return Path.Combine(folder.Path, $"{assemblyName}.dll");
    }
}
