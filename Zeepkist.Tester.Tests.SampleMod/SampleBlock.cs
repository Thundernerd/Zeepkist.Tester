using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>A kind of named value, as a mod declares it.</summary>
[ValueKind("sample-block", Substitute = nameof(Uid))]
public sealed class SampleBlock
{
    public string Uid { get; set; }
}
