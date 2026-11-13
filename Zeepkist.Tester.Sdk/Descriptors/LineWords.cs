using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// The words of a <c>.zlstest</c> line that <see cref="LineSyntax"/> matched to one argument.
/// </summary>
internal sealed class LineWords
{
    public LineWords(IReadOnlyList<string> words, string text)
    {
        Words = words;
        Text = text;
    }

    public IReadOnlyList<string> Words { get; }

    /// <summary>
    /// The line from the first word to the last, with the spaces between them as written.
    /// </summary>
    public string Text { get; }
}
