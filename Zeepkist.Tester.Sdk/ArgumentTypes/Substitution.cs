using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// How a scenario's text writes a named value: <c>${name}</c> stands for it, <c>$${</c> is a literal <c>${</c>, and any other <c>${</c> is an error.
/// The harness and <c>ztest check</c> both read text through this.
/// </summary>
public static class Substitution
{
    /// <summary>
    /// What a name is made of, which <see cref="Name{T}.Pattern"/> is built from too.
    /// </summary>
    internal const string NameCharacters = "[A-Za-z0-9_-]+";

    // In order: an escape, a name, and a ${ that's neither
    private static readonly Regex _form = new Regex(@"\$\$\{|\$\{(?<name>" + NameCharacters + @")\}|\$\{");

    /// <summary>
    /// The names of each <c>${name}</c> in the text, in order, as often as they're written.
    /// </summary>
    /// <exception cref="StepFailedException">A <c>${</c> has no name and <c>}</c> after it.</exception>
    public static IReadOnlyList<string> NamesIn(string text)
    {
        var names = new List<string>();
        Replace(text, name =>
        {
            names.Add(name);
            return string.Empty;
        });
        return names;
    }

    /// <summary>
    /// Writes each <c>${name}</c> as what <paramref name="valueOf"/> gives for the name, and each <c>$${</c> as a literal <c>${</c>.
    /// </summary>
    /// <exception cref="StepFailedException">A <c>${</c> has no name and <c>}</c> after it.</exception>
    public static string Replace(string text, Func<string, string> valueOf)
    {
        return _form.Replace(text, match =>
        {
            if (match.Value == "$${")
                return "${";

            var name = match.Groups["name"];
            return name.Success
                ? valueOf(name.Value)
                : throw new StepFailedException("A ${ has no name and } after it. Write $${ for a literal ${.");
        });
    }
}
