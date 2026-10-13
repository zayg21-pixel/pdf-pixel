namespace PdfPixel.Tagging.Model;

/// <summary>
/// A nested content stream enclosing the content it draws.
/// </summary>
public sealed class PdfContentStreamScope : PdfScope
{
    /// <summary>
    /// Initializes a new <see cref="PdfContentStreamScope"/> for a content stream of the specified type.
    /// </summary>
    public PdfContentStreamScope(PdfContentStreamType streamType) => StreamType = streamType;

    /// <summary>
    /// Type of the enclosed content stream.
    /// </summary>
    public PdfContentStreamType StreamType { get; }

    /// <inheritdoc />
    public override PdfScopeType ScopeType => PdfScopeType.ContentStream;

    /// <inheritdoc />
    public override string ToString() => $"ContentStream {StreamType}";
}
