namespace TNRD.Zeepkist.Tester.Sdk;

/// <summary>One move of a drag, in even steps over the frames.</summary>
public sealed class DragMove
{
    /// <summary>Pixels down, or up for a number below 0. With <see cref="Sideways"/>, pixels right, or left below 0.</summary>
    [Arg(Primary = true)]
    public required Pixels By { get; set; }

    /// <summary>Frames the move takes.</summary>
    [Arg(Default = 1)]
    [InRange(1, 600)]
    public int Over { get; set; } = 1;

    /// <summary>Whether it goes right or left, instead of down or up.</summary>
    [Arg(Default = false)]
    public bool Sideways { get; set; }
}
