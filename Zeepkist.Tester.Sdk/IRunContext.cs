using BepInEx.Logging;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// What's there for a whole run.
/// </summary>
public interface IRunContext
{
    public ManualLogSource Logger { get; }

    /// <summary>
    /// What a test plugin gave the harness with <see cref="TestPluginRegistration.Service"/>, such as its window driver.
    /// Fails the step, with a <see cref="StepFailedException"/>, if no test plugin did.
    /// </summary>
    public T Get<T>()
        where T : class;
}
