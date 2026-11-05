namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Uses a named value of kind <typeparamref name="T"/>, which an earlier step of the scenario defined.
/// </summary>
public readonly struct Ref<T>
    where T : class
{
    public const string Pattern = Name<T>.Pattern;

    public Ref(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static implicit operator Ref<T>(string value)
    {
        return new Ref<T>(value);
    }

    /// <summary>
    /// Fails the step, with a <see cref="StepFailedException"/> naming the name, if the scenario has no such value of this kind.
    /// </summary>
    public T Get(IStepContext context)
    {
        return context.Values.Get<T>(Value);
    }

    public override string ToString()
    {
        return Value;
    }
}
