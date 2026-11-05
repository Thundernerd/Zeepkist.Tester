namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Defines a named value of kind <typeparamref name="T"/>, for later steps of the scenario.
/// </summary>
public readonly struct Name<T>
    where T : class
{
    public const string Pattern = "^[A-Za-z0-9_-]+$";

    public Name(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator Name<T>(string value)
    {
        return new Name<T>(value);
    }

    /// <summary>
    /// Fails the step, with a <see cref="StepFailedException"/>, if the scenario already defined this name.
    /// </summary>
    public void Set(IStepContext context, T value)
    {
        context.Values.Define(Value, value);
    }

    public override string ToString()
    {
        return Value;
    }
}
