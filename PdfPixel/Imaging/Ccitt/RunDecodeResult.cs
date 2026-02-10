namespace PdfPixel.Imaging.Ccitt;

/// <summary>
/// Represents the result of a run-length decoding operation.
/// </summary>
internal readonly struct RunDecodeResult
{
    public RunDecodeResult(int length, bool hasTerminating, bool isEndOfLine)
    {
        Length = length;
        HasTerminating = hasTerminating;
        IsEndOfLine = isEndOfLine;
    }

    /// <summary>
    /// Accumulated run length (sum of make-ups + terminating) or 0 if EOL
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// True if a terminating code (0..63) closed the run.
    /// </summary>
    public bool HasTerminating { get; }

    /// <summary>
    /// True if EOL code encountered (no pixels implied).
    /// </summary>
    public bool IsEndOfLine { get; }
}
