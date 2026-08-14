using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;

namespace PdfPixel.Forms;

/// <summary>
/// Wrapper page that exposes a Form XObject's own /Resources dictionary if present,
/// otherwise falls back to the original page's resources. Per PDF spec a Form XObject
/// either supplies its complete resource dictionary or inherits the current one; no merging.
/// Geometry (MediaBox, CropBox, Rotation) is inherited unchanged from the original page.
/// </summary>
internal class FormXObjectPageWrapper : PdfPage
{
    public FormXObjectPageWrapper(IPdfPageInternal originalPage, PdfObject formXObject)
        : base(
            originalPage.PageNumber,
            originalPage.PageLabel,
            originalPage.Document,
            formXObject,
            originalPage.PageResources,
            ResolveResourceDictionary(originalPage, formXObject))
    {
    }

    public FormXObjectPageWrapper(PdfObject formXObject)
        : base(
            0,
            default,
            formXObject.Document,
            formXObject,
            new PdfPageResources(),
            formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey) ?? new PdfDictionary(formXObject.Document))
    {
    }

    /// <summary>
    /// Initializes a wrapper for a content stream whose reference, stream and /Resources have already
    /// been resolved, so the stream's object does not have to be parsed a second time.
    /// </summary>
    /// <param name="document">Owning document.</param>
    /// <param name="contentReference">Reference of the object holding the content stream.</param>
    /// <param name="contentStream">The content stream itself.</param>
    /// <param name="resources">The stream's own /Resources, or null when it declares none.</param>
    public FormXObjectPageWrapper(
        IPdfDocumentInternal document,
        in PdfReference contentReference,
        PdfObjectStream contentStream,
        PdfDictionary? resources)
        : base(
            0,
            default,
            document,
            contentReference,
            [contentStream],
            new PdfPageResources(),
            resources ?? new PdfDictionary(document),
            null)
    {
    }

    private static PdfDictionary ResolveResourceDictionary(IPdfPageInternal originalPage, PdfObject formXObject)
    {
        PdfDictionary? formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
        return formResources ?? originalPage.ResourceDictionary;
    }
}
