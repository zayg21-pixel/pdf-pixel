namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// A half-open range of samples along one axis.
/// </summary>
public readonly struct PdfImageRowSampleRange
{
    /// <summary>
    /// Initializes a range from <paramref name="start"/> up to, not including, <paramref name="end"/>.
    /// </summary>
    public PdfImageRowSampleRange(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the first sample of the range.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the sample after the last of the range.
    /// </summary>
    public int End { get; }

    /// <summary>
    /// Gets the number of samples of the range.
    /// </summary>
    public int Count => End - Start;
}
