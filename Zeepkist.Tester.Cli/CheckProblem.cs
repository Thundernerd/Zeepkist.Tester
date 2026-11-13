using TNRD.Zeepkist.Tester.Scenarios;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// One thing <c>ztest check</c> found. It prints as <c>file:line:col: error|warning: message</c>.
/// </summary>
internal sealed class CheckProblem
{
    public CheckProblem(SourceLocation location, CheckSeverity severity, string message)
    {
        Location = location;
        Severity = severity;
        Message = message;
    }

    public SourceLocation Location { get; }

    public CheckSeverity Severity { get; }

    public string Message { get; }

    public override string ToString()
    {
        return CommandOutput.Line(Location, Severity, Message);
    }
}
