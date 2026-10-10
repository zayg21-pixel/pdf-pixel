namespace PdfPixel.Models;

/// <summary>
/// Content host of a nested content stream: a form XObject, soft mask group, tiling pattern cell
/// or Type 3 glyph description.
/// </summary>
internal sealed class PdfContentHost : IPdfContentHost
{
    /// <summary>
    /// Initializes a new instance for a nested content stream.
    /// </summary>
    /// <param name="document">Owning document.</param>
    /// <param name="resourceDictionary">Resource dictionary the stream's names resolve against.</param>
    /// <param name="structParents">Structural parent key (/StructParents) of the stream, or null when absent.</param>
    public PdfContentHost(IPdfDocumentInternal document, PdfDictionary resourceDictionary, int? structParents)
    {
        Document = document;
        ResourceDictionary = resourceDictionary;
        StructParents = structParents;
        Cache = new PdfContentHostCache(this, document, resourceDictionary);
    }

    /// <inheritdoc/>
    public PdfContentHostCache Cache { get; }

    /// <inheritdoc/>
    public int? StructParents { get; }

    /// <inheritdoc/>
    public PdfDictionary ResourceDictionary { get; }

    /// <inheritdoc/>
    public IPdfDocumentInternal Document { get; }
}
