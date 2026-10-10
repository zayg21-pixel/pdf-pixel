namespace PdfPixel.Models;

/// <summary>
/// Object a content stream is rendered against: a page, form XObject, tiling pattern or Type 3 font,
/// providing the resources and structural parent key the stream's operators resolve.
/// </summary>
internal interface IPdfContentHost
{
    /// <summary>
    /// Resource cache providing name-based lookups.
    /// </summary>
    PdfContentHostCache Cache { get; }

    /// <summary>
    /// Structural parent key (/StructParents) of the content streams, or null when absent.
    /// </summary>
    int? StructParents { get; }

    /// <summary>
    /// Resolved resource dictionary (never null).
    /// </summary>
    PdfDictionary ResourceDictionary { get; }

    /// <summary>
    /// Owning document instance.
    /// </summary>
    IPdfDocumentInternal Document { get; }
}
