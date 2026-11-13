using System.Text.Encodings.Web;
using System.Text.Json;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// How the commands write JSON files for people to read and edit.
/// </summary>
internal static class ReadableJson
{
    /// <summary>
    /// Indented, with <c>\n</c> line ends. A file for people to read, and no page to put it in, so backticks and quotes
    /// stay as they are.
    /// </summary>
    public static JsonSerializerOptions Options(int indentSize)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            NewLine = "\n",
            IndentSize = indentSize,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }
}
