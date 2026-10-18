namespace PdfPixel.Parsing;

/// <summary>
/// Specifies what a <see cref="PdfParser"/> reads.
/// </summary>
internal enum PdfParserMode
{
    /// <summary>
    /// Content stream: bare tokens are operators; no references, no decryption.
    /// </summary>
    ContentStream,

    /// <summary>
    /// Indirect object of the file body: bare tokens are keywords; references, decrypted strings and streams.
    /// </summary>
    IndirectObject,

    /// <summary>
    /// Object stream contents: bare tokens are keywords; references, no decryption.
    /// </summary>
    ObjectStream,

    /// <summary>
    /// Cross-reference table, stream and trailer: bare tokens are keywords; references, no decryption.
    /// </summary>
    CrossReference,

    /// <summary>
    /// Recovery scan of the file body: bare tokens are keywords; references, no decryption.
    /// </summary>
    Recovery
}
