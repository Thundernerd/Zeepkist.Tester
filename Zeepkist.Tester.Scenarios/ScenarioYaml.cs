using System;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Reads a scenario file's YAML into nodes, where every scalar stays the text it was written as and knows its line and
/// column.
/// </summary>
internal static class ScenarioYaml
{
    /// <summary>
    /// How deep lists and maps may nest. YamlDotNet reads a nested node by recursing, so a file nested deep enough would
    /// overflow the stack and end the process, the game's included. A scenario is nowhere near this deep.
    /// </summary>
    private const int MostDepth = 100;

    /// <exception cref="ScenarioException">The file isn't valid YAML, or nests too deep.</exception>
    public static YamlStream Load(string file, TextReader reader)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new DepthLimitedParser(file, new Parser(reader)));
        }
        catch (YamlException exception)
        {
            throw new ScenarioException(new SourceLocation(file, exception.Start.Line, exception.Start.Column), Problem(exception));
        }

        return stream;
    }

    /// <summary>
    /// A YamlException's message starts with where it is, as <c>(Line: 3, Col: 5, Idx: 20) - (Line: 3, Col: 5, Idx: 20): </c>.
    /// </summary>
    private static string Problem(YamlException exception)
    {
        const string AfterPlace = "): ";
        var message = exception.Message;
        var place = message.IndexOf(AfterPlace, StringComparison.Ordinal);
        var problem = place < 0 ? message : message.Substring(place + AfterPlace.Length);
        return problem.Contains("flow") ? $"{problem} Inside {{ }} or [ ], quote a value with ${{ or a colon in it." : problem;
    }

    /// <summary>
    /// Stops at the first list or map that's nested deeper than <see cref="MostDepth"/>, before anything recurses into it.
    /// </summary>
    private sealed class DepthLimitedParser : IParser
    {
        private readonly string _file;
        private readonly IParser _parser;
        private int _depth;

        public DepthLimitedParser(string file, IParser parser)
        {
            _file = file;
            _parser = parser;
        }

        public ParsingEvent Current => _parser.Current;

        public bool MoveNext()
        {
            if (!_parser.MoveNext())
                return false;

            var current = _parser.Current;
            switch (current)
            {
                case SequenceStart or MappingStart:
                    if (++_depth > MostDepth)
                    {
                        throw new ScenarioException(new SourceLocation(_file, current.Start.Line, current.Start.Column), $"Lists and maps are nested more than {MostDepth} deep here.");
                    }

                    break;
                case SequenceEnd or MappingEnd:
                    _depth--;
                    break;
            }

            return true;
        }
    }
}
