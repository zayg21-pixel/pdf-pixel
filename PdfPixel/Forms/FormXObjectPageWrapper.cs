using PdfPixel.Models;
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

    private static PdfDictionary ResolveResourceDictionary(IPdfPageInternal originalPage, PdfObject formXObject)
    {
        PdfDictionary? formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
        return formResources ?? originalPage.ResourceDictionary;
    }
}
