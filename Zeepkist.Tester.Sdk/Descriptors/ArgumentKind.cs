namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The fixed set of types an argument can have.
/// </summary>
public enum ArgumentKind
{
    String,
    Text,
    Int,
    Float,
    Bool,
    Seconds,
    Pixels,
    Keys,
    FileName,

    /// <summary>
    /// Defines a named value.
    /// </summary>
    Name,

    /// <summary>
    /// Uses a named value.
    /// </summary>
    Ref,

    Enum,
    List,
    Object,
}
