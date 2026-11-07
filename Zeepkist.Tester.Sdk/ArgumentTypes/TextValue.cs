namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>
/// A named value that is only text: what a <c>for</c> variable and a <c>with:</c> parameter are.
/// </summary>
[ValueKind(Kind, Substitute = nameof(Text))]
public sealed class TextValue
{
    public const string Kind = "text";

    public TextValue(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
