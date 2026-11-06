namespace TNRD.Zeepkist.Tester.Scenarios;

/// <summary>
/// Where something is written in a scenario file.
/// </summary>
internal sealed class SourceLocation
{
    public SourceLocation(string file, long line, long column)
    {
        File = file;
        Line = line;
        Column = column;
    }

    public string File { get; }

    public long Line { get; }

    public long Column { get; }

    public override string ToString()
    {
        return $"{File}:{Line}:{Column}";
    }
}
