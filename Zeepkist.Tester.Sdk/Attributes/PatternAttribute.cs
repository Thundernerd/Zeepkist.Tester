using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A regular expression a string argument's value has to match.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class PatternAttribute : Attribute
{
    public PatternAttribute(string pattern)
    {
        Pattern = pattern;
    }

    public string Pattern { get; }
}
