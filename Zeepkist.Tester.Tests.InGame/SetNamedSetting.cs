using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using TNRD.Zeepkist.Tester.States;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Changes a block's setting by its name in this plugin's list, as a mod's step does with <c>context.Blocks</c>.</summary>
[Step("set-named-setting")]
[Requires(typeof(InEditor))]
public sealed class SetNamedSetting : Step
{
    /// <summary>The block.</summary>
    [Arg]
    public required Ref<BlockValue> Block { get; set; }

    /// <summary>The setting's name, such as a lamp's spotlight.</summary>
    [Arg]
    public required string Setting { get; set; }

    /// <summary>Its value.</summary>
    [Arg]
    public required string Value { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var type = central.undoRedo.allBlocksDictionary[Block.Get(context).Uid].blockID;
        var key = NamedBlocks.KeyOf(type, Setting) ?? throw new StepFailedException($"{Block} has no setting called {Setting}.");
        yield return context.Blocks.SetSetting(Block, key, Value);
    }
}
