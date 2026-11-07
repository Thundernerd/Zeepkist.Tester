namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A block a step placed, by its UID, which <c>${name}</c> writes.
/// </summary>
[ValueKind(Kind, Substitute = nameof(Uid))]
public sealed class BlockValue
{
    public const string Kind = "block";

    public BlockValue(string uid)
    {
        Uid = uid;
    }

    public string Uid { get; }
}
