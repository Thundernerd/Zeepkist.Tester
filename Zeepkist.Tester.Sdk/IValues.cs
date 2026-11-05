namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The named values of one scenario. Each mistake in using them fails the step, with a <see cref="StepFailedException"/>.
/// </summary>
public interface IValues
{
    /// <summary>
    /// Fails the step if the scenario already defined <paramref name="name"/>.
    /// </summary>
    public void Define<T>(string name, T value)
        where T : class;

    /// <summary>
    /// Fails the step if the scenario hasn't defined <paramref name="name"/>, or defined it as another kind.
    /// </summary>
    public T Get<T>(string name)
        where T : class;

    /// <summary>
    /// Writes each <c>${name}</c> as its value's <see cref="ValueKindAttribute.Substitute"/> member, and each <c>$${</c> as a literal <c>${</c>.
    /// Fails the step for a name that isn't defined, a kind with no such member, or a <c>${</c> without a name and <c>}</c> after it.
    /// </summary>
    public string Substitute(string text);
}
