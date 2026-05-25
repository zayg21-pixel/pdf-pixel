using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Forms;

/// <summary>
/// Wrapper page that exposes a Form XObject's own /Resources dictionary if present,
/// otherwise falls back to the original page's resources. Per PDF spec a Form XObject
/// either supplies its complete resource dictionary or inherits the current one; no merging.
/// Geometry (MediaBox, CropBox, Rotation) is inherited unchanged from the original page.
/// </summary>
internal class FormXObjectPageWrapper : PdfPage, IPdfPageInternal
{
    private readonly PdfObject _resourcePageObject;
    private readonly PdfDictionary _resourceDictionary;
    private readonly IPdfPageInternal _originalPage;
    private readonly PdfPageCache _pageCache;

    public FormXObjectPageWrapper(IPdfPageInternal originalPage, PdfObject formXObject)
        : base(originalPage.PageNumber, originalPage.PageLabel, originalPage.Document, originalPage.PageObject, originalPage.PageResources)
    {
        _originalPage = originalPage;

        // Inline former CreateResourcePageObject logic.
        PdfDictionary formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
        if (formResources == null)
        {
            _resourcePageObject = originalPage.PageObject;
            _resourceDictionary = originalPage.ResourceDictionary;
            _pageCache = originalPage.Cache;
        }
        else
        {
            _resourcePageObject = formXObject;
            _resourceDictionary = formResources;
            _pageCache = new PdfPageCache(this);
        }
    }

    public FormXObjectPageWrapper(PdfObject formXObject)
        : base(0, default, formXObject.Document, formXObject, new PdfPageResources())
    {
        PdfDictionary formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
        _resourcePageObject = formXObject;
        _resourceDictionary = formResources;
        _pageCache = new PdfPageCache(this);
    }

    PdfObject IPdfPageInternal.PageObject => _resourcePageObject;

    PdfDictionary IPdfPageInternal.ResourceDictionary => _resourceDictionary;

    PdfPageCache IPdfPageInternal.Cache => _pageCache;
}
