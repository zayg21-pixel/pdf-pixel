namespace PdfPixel.Parsing;

/// <summary>
/// Specifies the kind of data a <see cref="PdfParser"/> reads.
/// </summary>
internal enum PdfParserMode
{
    /// <summary>
    /// Content stream: bare tokens are read as operators.
    /// </summary>
    ContentStream,

    /// <summary>
    /// File structure: bare tokens are read as keywords.
    /// </summary>
    FileStructure
}
