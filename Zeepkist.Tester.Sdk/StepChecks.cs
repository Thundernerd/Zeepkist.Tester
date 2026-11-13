namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Shorthands for what a step's checks record.
/// </summary>
public static class StepChecks
{
    /// <summary>
    /// Records a check: <see cref="IStepContext.Pass"/> when <paramref name="held"/>, and <see cref="IStepContext.Fail"/>
    /// otherwise, with the same <paramref name="detail"/>, <paramref name="expected"/> and <paramref name="actual"/>.
    /// </summary>
    public static void Check(this IStepContext context, bool held, string detail, string expected, string actual)
    {
        if (held)
            context.Pass(detail, expected, actual);
        else
            context.Fail(detail, expected, actual);
    }
}
