namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Per-edge values of a layout attribute, in the order of the before, after, start and end edges.
/// </summary>
public readonly struct PdfStructureSides<T>
{
    internal PdfStructureSides(T before, T after, T start, T end)
    {
        Before = before;
        After = after;
        Start = start;
        End = end;
    }

    internal PdfStructureSides(T all)
        : this(all, all, all, all)
    {
    }

    /// <summary>
    /// Value for the before edge.
    /// </summary>
    public T Before { get; }

    /// <summary>
    /// Value for the after edge.
    /// </summary>
    public T After { get; }

    /// <summary>
    /// Value for the start edge.
    /// </summary>
    public T Start { get; }

    /// <summary>
    /// Value for the end edge.
    /// </summary>
    public T End { get; }
}
