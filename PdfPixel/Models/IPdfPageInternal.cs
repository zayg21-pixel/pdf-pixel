using PdfPixel.Streams;
using PdfPixel.Transparency.Model;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Internal view of a PDF page, exposing resources and low-level objects used by parsers and renderers.
/// </summary>
internal interface IPdfPageInternal : IPdfPage
{
    /// <summary>
    /// Per-page resource cache providing name-based lookups.
    /// Created on first access to avoid unnecessary allocations for pages that do not need caching.
    /// </summary>
    PdfPageCache Cache { get; }

    /// <summary>
    /// Page resources snapshot used to resolve inheritable attributes.
    /// </summary>
    PdfPageResources PageResources { get; }

    /// <summary>
    /// Reference of the underlying /Page object.
    /// </summary>
    PdfReference PageReference { get; }

    /// <summary>
    /// Stream sources named by the page's /Contents entry, in the order they are to be read.
    /// </summary>
    IReadOnlyList<PdfObjectStream> ContentStreams { get; }

    /// <summary>
    /// Resolved resource dictionary for this page (never null).
    /// </summary>
    PdfDictionary ResourceDictionary { get; }

    /// <summary>
    /// Owning document instance.
    /// </summary>
    IPdfDocumentInternal Document { get; }

    /// <summary>
    /// Page-level transparency group (/Group entry in the page dict), or null when absent.
    /// </summary>
    PdfTransparencyGroup? TransparencyGroup { get; }
}
