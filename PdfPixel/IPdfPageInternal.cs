using PdfPixel.Objects;
using PdfPixel.Resources;
using PdfPixel.Streams;
using PdfPixel.Transparency;
using System.Collections.Generic;

namespace PdfPixel;

/// <summary>
/// Internal view of a PDF page, exposing resources and low-level objects used by parsers and renderers.
/// </summary>
internal interface IPdfPageInternal : IPdfPage, IPdfContentHost
{
    /// <summary>
    /// Page resources snapshot used to resolve inheritable attributes.
    /// </summary>
    PdfPageResources PageResources { get; }

    /// <summary>
    /// Reference of the underlying /Page object, or <see langword="null"/> when it is a direct object.
    /// </summary>
    PdfReference? PageReference { get; }

    /// <summary>
    /// Stream sources named by the page's /Contents entry, in the order they are to be read.
    /// </summary>
    IReadOnlyList<PdfObjectStream> ContentStreams { get; }

    /// <summary>
    /// Page-level transparency group (/Group entry in the page dict), or null when absent.
    /// </summary>
    PdfTransparencyGroup? TransparencyGroup { get; }
}
