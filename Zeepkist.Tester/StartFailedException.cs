using System;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The game didn't get to where a scenario starts. Unlike a failed step, this leaves it somewhere unknown.
/// </summary>
internal sealed class StartFailedException : Exception
{
    public StartFailedException(string message)
        : base(message)
    {
    }
}
