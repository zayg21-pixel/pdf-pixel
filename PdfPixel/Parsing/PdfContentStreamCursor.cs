namespace PdfPixel.Parsing;

/// <summary>
/// Positions in a content stream shared by every copy of its <see cref="PdfParseContext"/>, through which the parser and the
/// operators that read the stream directly hand positions to each other: where the current inline image starts, and how much
/// of the stream an operator read past the parser's position.
/// </summary>
internal sealed class PdfContentStreamCursor
{
    /// <summary>
    /// Position where the entries of the current inline image start, after its BI operator, or -1 outside an inline image.
    /// </summary>
    public int InlineImageStart { get; set; } = -1;

    /// <summary>
    /// Position right after the ID operator of the current inline image, where its data starts.
    /// </summary>
    public int InlineImageDataStart { get; set; }

    /// <summary>
    /// Number of bytes past its position the parser skips once, before reading its next token; 0 when none.
    /// </summary>
    public int SkipPositions { get; set; }
}
