using System.Collections;
using System.Globalization;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Checks what a named block holds under a setting's key.</summary>
[Step("expect-block-setting")]
[Requires(typeof(InEditor))]
public sealed class ExpectBlockSetting : Step
{
    /// <summary>The block.</summary>
    [Arg]
    public required Ref<BlockValue> Block { get; set; }

    /// <summary>The setting's key.</summary>
    [Arg]
    public required string Setting { get; set; }

    /// <summary>What it should hold.</summary>
    [Arg]
    public required string Value { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var properties = central.undoRedo.allBlocksDictionary[Block.Get(context).Uid].customBlockEditProperties;
        var actual = properties.GetIntDictionaryValue(Setting).ToString(CultureInfo.InvariantCulture);
        if (actual == Value)
            context.Pass($"{Setting} is {actual}", Value, actual);
        else
            context.Fail($"{Setting} is {actual}", Value, actual);
    }
}
