using TNRD.Zeepkist.Tester.Scenarios;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// One thing <c>ztest check</c> found. It prints as <c>file:line:col: error|warning: message</c>.
/// </summary>
internal sealed class CheckProblem
{
    public CheckProblem(SourceLocation location, bool isError, string message)
    {
        Location = location;
        IsError = isError;
        Message = message;
    }

    public SourceLocation Location { get; }

    public bool IsError { get; }

    public string Message { get; }

    public override string ToString()
    {
        return $"{Location}: {(IsError ? "error" : "warning")}: {Message}";
    }
}
