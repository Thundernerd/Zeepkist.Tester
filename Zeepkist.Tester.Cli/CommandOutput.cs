using System.IO;
using TNRD.Zeepkist.Tester.Scenarios;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// How every command writes a problem: <c>error: message</c> or <c>warning: message</c>, after <c>file:line:col: </c> when
/// it's in a file.
/// </summary>
internal static class CommandOutput
{
    public static void WriteError(this TextWriter output, string problem)
    {
        output.WriteLine(Line(null, ProblemSeverity.Error, problem));
    }

    public static void WriteError(this TextWriter output, SourceLocation location, string problem)
    {
        output.WriteLine(Line(location, ProblemSeverity.Error, problem));
    }

    public static void WriteWarning(this TextWriter output, string problem)
    {
        output.WriteLine(Line(null, ProblemSeverity.Warning, problem));
    }

    /// <summary>
    /// Writes <paramref name="problem"/> as an error, for a command that stops at it.
    /// </summary>
    /// <returns>The exit code for it, 1.</returns>
    public static int Fail(this TextWriter output, string problem)
    {
        output.WriteError(problem);
        return 1;
    }

    /// <summary>
    /// The line for a problem, null <paramref name="location"/> when it isn't in a file.
    /// </summary>
    public static string Line(SourceLocation location, ProblemSeverity severity, string problem)
    {
        var label = severity == ProblemSeverity.Error ? "error" : "warning";
        return location == null ? $"{label}: {problem}" : $"{location}: {label}: {problem}";
    }
}
