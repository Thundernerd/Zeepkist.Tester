using TNRD.Zeepkist.Tester.Sdk;
using Xunit;

namespace TNRD.Zeepkist.Tester.Tests;

public class SubstitutionTests
{
    [Theory]
    [InlineData("no names", new string[0])]
    [InlineData("${a}", new[] { "a" })]
    [InlineData("${a-1} and ${b_2} and ${a-1}", new[] { "a-1", "b_2", "a-1" })]
    [InlineData("$${a}", new string[0])]
    [InlineData("$$${a}", new string[0])]
    [InlineData("$${a} ${b}", new[] { "b" })]
    public void NamesIn_FindsEachName_ButNotAnEscapedOne(string text, string[] names)
    {
        Assert.Equal(names, Substitution.NamesIn(text));
    }

    [Theory]
    [InlineData("${")]
    [InlineData("${a")]
    [InlineData("${}")]
    [InlineData("a ${my lamp} b")]
    public void NamesIn_OpeningWithoutANameAfterIt_FailsTheStepSayingHowToEscapeIt(string text)
    {
        var exception = Assert.Throws<StepFailedException>(() => Substitution.NamesIn(text));

        Assert.Equal("A ${ has no name and } after it. Write $${ for a literal ${.", exception.Message);
    }

    [Fact]
    public void Replace_WritesEachNamesValue_AndEachEscapeAsALiteral()
    {
        var text = Substitution.Replace("$${a} ${a} ${b}", name => name.ToUpperInvariant());

        Assert.Equal("${a} A B", text);
    }

    [Fact]
    public void Replace_AValueThatLooksLikeAName_IsNotReplacedAgain()
    {
        Assert.Equal("${b}", Substitution.Replace("${a}", _ => "${b}"));
    }

    [Fact]
    public void ANamedValuesPattern_AllowsWhatTheScanCallsAName()
    {
        Assert.Equal("^[A-Za-z0-9_-]+$", Name<object>.Pattern);
    }
}
