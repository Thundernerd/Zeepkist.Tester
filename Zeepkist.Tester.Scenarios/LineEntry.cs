namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// A step, a comment or a blank line of a <c>.zlstest</c> file.
/// </summary>
internal sealed class LineEntry
{
    private LineEntry(StepNode step, string comment)
    {
        Step = step;
        Comment = comment;
    }

    public static LineEntry Blank { get; } = new LineEntry(null, null);

    /// <summary>
    /// Null for a comment or a blank line.
    /// </summary>
    public StepNode Step { get; }

    /// <summary>
    /// What follows the <c>#</c>, or null for a step or a blank line.
    /// </summary>
    public string Comment { get; }

    public static LineEntry ForStep(StepNode step)
    {
        return new LineEntry(step, null);
    }

    public static LineEntry ForComment(string comment)
    {
        return new LineEntry(null, comment);
    }
}
