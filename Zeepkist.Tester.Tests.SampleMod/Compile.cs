using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

[Step("compile")]
public sealed class Compile : Step
{
    public Compile()
    {
    }

    [SetsRequiredMembers]
    public Compile(string function)
    {
        Function = function;
    }

    [Arg(Primary = true)]
    public required string Function { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
