namespace PdfPixel.Models;

/// <summary>
/// Internal view of a PDF page, exposing resources and low-level objects used by parsers and renderers.
/// </summary>
internal interface IPdfPageInternal : IPdfPage
{
    /// <summary>
    /// Lazy per-page resource cache providing name-based lookups.
    /// Created on first access to avoid unnecessary allocations for pages that do not need caching.
    /// </summary>
    PdfPageCache Cache { get; }

    /// <summary>
    /// Page resources snapshot used to resolve inheritable attributes.
    /// </summary>
    PdfPageResources PageResources { get; }

    /// <summary>
    /// Underlying /Page object supplying dictionary entries and content references.
    /// </summary>
    PdfObject PageObject { get; }

    /// <summary>
    /// Resolved resource dictionary for this page (never null).
    /// </summary>
    PdfDictionary ResourceDictionary { get; }

    /// <summary>
    /// Owning document instance.
    /// </summary>
    IPdfDocumentInternal Document { get; }
}
