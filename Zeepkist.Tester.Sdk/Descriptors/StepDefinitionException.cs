using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A step or argument class breaks one of the registry's rules. The message says which, and where.
/// </summary>
public sealed class StepDefinitionException : Exception
{
    public StepDefinitionException(string message)
        : base(message)
    {
    }
}
