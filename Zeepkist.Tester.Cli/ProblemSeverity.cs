namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// An error fails the command. A warning doesn't: from <c>ztest check</c>, it comes from a model that's only as good as
/// what steps declare.
/// </summary>
internal enum ProblemSeverity
{
    Error,
    Warning,
}
