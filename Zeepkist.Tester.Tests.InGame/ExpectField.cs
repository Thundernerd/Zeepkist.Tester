using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks the text in the window's field.</summary>
[Step("expect-field")]
public sealed class ExpectField : Step
{
    /// <summary>What it should say, nothing when left out.</summary>
    [Arg(Primary = true, Default = "")]
    public string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var actual = context.Get<InputWindow>().Text;
        if (actual == Text)
            context.Pass($"The field says '{actual}'", Text, actual);
        else
            context.Fail("The field says something else", Text, actual);

        yield break;
    }
}
