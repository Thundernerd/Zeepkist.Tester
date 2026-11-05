using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// Stops the running step and fails it, with this message as what went wrong.
/// </summary>
public sealed class StepFailedException : Exception
{
    public StepFailedException(string message)
        : base(message)
    {
    }
}
