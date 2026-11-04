using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>
/// Its constructors are there to compile on net472: <c>required</c> with <see cref="SetsRequiredMembersAttribute"/>.
/// </summary>
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
