using System;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// How the step was written on one line of a <c>.zlstest</c> file, for <c>ztest convert</c> to read such a line into its arguments.
/// </summary>
/// <remarks>
/// Words are separated by spaces. <c>&lt;name&gt;</c> is one word of the argument <c>name</c>, <c>&lt;name...&gt;</c> is one or more words of it,
/// <c>[ … ]</c> can be left out, and any other word is written as it is: <c>&lt;function&gt; [as &lt;as&gt;]</c>. A step without one takes its
/// primary argument as the rest of the line, or nothing when it has none.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LineSyntaxAttribute : Attribute
{
    public LineSyntaxAttribute(string pattern)
    {
        Pattern = pattern;
    }

    public string Pattern { get; }
}
