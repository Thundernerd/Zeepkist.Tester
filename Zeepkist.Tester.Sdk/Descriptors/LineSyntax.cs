using System;
using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A <see cref="LineSyntaxAttribute"/>'s pattern, read, which matches what follows a step's name on a <c>.zlstest</c> line.
/// </summary>
internal sealed class LineSyntax
{
    private readonly IReadOnlyList<Element> _elements;

    private LineSyntax(IReadOnlyList<Element> elements)
    {
        _elements = elements;
    }

    /// <summary>
    /// The arguments it names, in the order it names them.
    /// </summary>
    public IReadOnlyList<string> Names => [.. Captures(_elements).Select(capture => capture.Name)];

    /// <summary>
    /// A step's own <see cref="StepDescriptor.LineSyntax"/>, or else its primary argument as the rest of the line when there's any, or else nothing.
    /// </summary>
    /// <exception cref="FormatException">The step's pattern can't be read, which the descriptor builder doesn't let happen.</exception>
    public static LineSyntax For(StepDescriptor step)
    {
        if (step.LineSyntax != null)
            return Parse(step.LineSyntax);

        var primary = step.Arguments.FirstOrDefault(argument => argument.IsPrimary);
        return new LineSyntax(primary == null ? [] : [new Optional([new Capture(primary.Name, true)])]);
    }

    /// <exception cref="FormatException">The pattern can't be read. The message says why, to follow its pattern.</exception>
    public static LineSyntax Parse(string pattern)
    {
        // [ and ] are words of their own, written against the words they enclose or not
        var words = pattern.Replace("[", " [ ").Replace("]", " ] ").Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var position = 0;
        var elements = ReadElements(words, ref position);
        if (position < words.Length)
            throw new FormatException("has a ] without its [");

        var twice = Captures(elements).GroupBy(capture => capture.Name).FirstOrDefault(group => group.Count() > 1);
        if (twice != null)
            throw new FormatException($"names '{twice.Key}' twice");

        return new LineSyntax(elements);
    }

    /// <summary>
    /// Matches the words of <paramref name="text"/>, trying the longest run for each <c>&lt;name...&gt;</c> first.
    /// </summary>
    /// <returns>Each argument it names and matched, with its words and the text they span. Null when the text doesn't match.</returns>
    public IReadOnlyDictionary<string, LineWords> Match(string text)
    {
        var words = Words(text);
        var matched = new List<(string Name, int Start, int End)>();
        if (!Match(_elements, 0, words, 0, matched, end => end == words.Count))
            return null;

        return matched.ToDictionary(
            each => each.Name,
            each => new LineWords(
                [.. words.Skip(each.Start).Take(each.End - each.Start).Select(word => word.Text)],
                text.Substring(words[each.Start].Offset, words[each.End - 1].Offset + words[each.End - 1].Text.Length - words[each.Start].Offset)));
    }

    /// <summary>
    /// Reads up to the end of the words or a ], which it leaves at <paramref name="position"/>.
    /// </summary>
    private static List<Element> ReadElements(string[] words, ref int position)
    {
        var elements = new List<Element>();
        while (position < words.Length && words[position] != "]")
        {
            var word = words[position++];
            if (word == "[")
            {
                elements.Add(new Optional(ReadElements(words, ref position)));
                if (position == words.Length)
                    throw new FormatException("has a [ without its ]");

                position++;
            }
            else if (word.StartsWith("<", StringComparison.Ordinal) || word.EndsWith(">", StringComparison.Ordinal))
            {
                elements.Add(ReadCapture(word));
            }
            else
            {
                elements.Add(new Literal(word));
            }
        }

        return elements;
    }

    private static Capture ReadCapture(string word)
    {
        if (!word.StartsWith("<", StringComparison.Ordinal) || !word.EndsWith(">", StringComparison.Ordinal) || word.Length < 3)
            throw new FormatException($"has '{word}', where an argument is written as <name> or <name...>");

        var name = word.Substring(1, word.Length - 2);
        var rest = name.EndsWith("...", StringComparison.Ordinal);
        name = rest ? name.Substring(0, name.Length - 3) : name;
        if (name.Length == 0)
            throw new FormatException($"has '{word}', where an argument is written as <name> or <name...>");

        return new Capture(name, rest);
    }

    private static IEnumerable<Capture> Captures(IEnumerable<Element> elements)
    {
        foreach (var element in elements)
        {
            if (element is Capture capture)
                yield return capture;
            else if (element is Optional optional)
            {
                foreach (var inner in Captures(optional.Elements))
                    yield return inner;
            }
        }
    }

    private static bool Match(IReadOnlyList<Element> elements, int index, IReadOnlyList<Word> words, int word, List<(string Name, int Start, int End)> matched, Func<int, bool> then)
    {
        if (index == elements.Count)
            return then(word);

        switch (elements[index])
        {
            case Literal literal:
                return word < words.Count && words[word].Text == literal.Text && Match(elements, index + 1, words, word + 1, matched, then);
            case Capture capture:
                for (var count = capture.Rest ? words.Count - word : Math.Min(1, words.Count - word); count > 0; count--)
                {
                    matched.Add((capture.Name, word, word + count));
                    if (Match(elements, index + 1, words, word + count, matched, then))
                        return true;

                    matched.RemoveAt(matched.Count - 1);
                }

                return false;
            default:
                var optional = (Optional)elements[index];
                var before = matched.Count;
                if (Match(optional.Elements, 0, words, word, matched, end => Match(elements, index + 1, words, end, matched, then)))
                    return true;

                matched.RemoveRange(before, matched.Count - before);
                return Match(elements, index + 1, words, word, matched, then);
        }
    }

    private static List<Word> Words(string text)
    {
        var words = new List<Word>();
        for (var start = 0; start < text.Length;)
        {
            if (text[start] == ' ')
            {
                start++;
                continue;
            }

            var end = text.IndexOf(' ', start);
            end = end < 0 ? text.Length : end;
            words.Add(new Word(text.Substring(start, end - start), start));
            start = end;
        }

        return words;
    }

    private abstract class Element
    {
    }

    private sealed class Literal : Element
    {
        public Literal(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    private sealed class Capture : Element
    {
        public Capture(string name, bool rest)
        {
            Name = name;
            Rest = rest;
        }

        public string Name { get; }

        /// <summary>
        /// Whether it takes one or more words, instead of one.
        /// </summary>
        public bool Rest { get; }
    }

    private sealed class Optional : Element
    {
        public Optional(IReadOnlyList<Element> elements)
        {
            Elements = elements;
        }

        public IReadOnlyList<Element> Elements { get; }
    }

    private readonly struct Word
    {
        public Word(string text, int offset)
        {
            Text = text;
            Offset = offset;
        }

        public string Text { get; }

        public int Offset { get; }
    }
}
