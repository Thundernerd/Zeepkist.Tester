using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks what's selected in the window's field.</summary>
[Step("expect-field-selection")]
public sealed class ExpectFieldSelection : Step
{
    /// <summary>What should be selected, nothing when left out.</summary>
    [Arg(Primary = true, Default = "")]
    public string Text { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var actual = context.Get<InputWindow>().SelectedText;
        if (actual == Text)
            context.Pass($"'{actual}' is selected in the field", Text, actual);
        else
            context.Fail("Something else is selected in the field", Text, actual);

        yield break;
    }
}
