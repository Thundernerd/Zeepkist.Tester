namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// An error fails the check. A warning doesn't, because it comes from a model that's only as good as what steps declare.
/// </summary>
internal enum CheckSeverity
{
    Error,
    Warning,
}
