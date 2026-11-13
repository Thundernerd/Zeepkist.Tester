using System;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// A DLL that can't be read offline, with a message that names it and says why.
/// </summary>
internal sealed class UnreadableAssemblyException : Exception
{
    public UnreadableAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
