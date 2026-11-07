using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>A kind of named value, whose <c>${name}</c> writes its text.</summary>
[ValueKind("marker", Substitute = nameof(Text))]
public sealed class Marker
{
    public string Text { get; set; }
}
