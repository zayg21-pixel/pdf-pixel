using PdfRender.Models;
using PdfRender.Text;

namespace PdfRender.Forms;

/// <summary>
/// Wrapper page that exposes a Form XObject's own /Resources dictionary if present,
/// otherwise falls back to the original page's resources. Per PDF spec a Form XObject
/// either supplies its complete resource dictionary or inherits the current one; no merging.
/// Geometry (MediaBox, CropBox, Rotation) is inherited unchanged from the original page.
/// </summary>
internal class FormXObjectPageWrapper : PdfPage
{
    private readonly PdfObject _resourcePageObject;
    private readonly PdfDictionary _resourceDictionary;
    private readonly PdfPage _originalPage;
    private readonly PdfPageCache _pageCache;

    public FormXObjectPageWrapper(PdfPage originalPage, PdfObject formXObject)
        : base(originalPage.PageNumber, originalPage.Document, originalPage.PageObject, originalPage.PageResources)
    {
        _originalPage = originalPage;

        // Inline former CreateResourcePageObject logic.
        var formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
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
        : base(0, formXObject.Document, formXObject, new PdfPageResources())
    {
        var formResources = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
        _resourcePageObject = formXObject;
        _resourceDictionary = formResources;
        _pageCache = new PdfPageCache(this);
    }

    public override PdfObject PageObject => _resourcePageObject;

    public override PdfDictionary ResourceDictionary => _resourceDictionary;

    internal override PdfPageCache Cache => _pageCache;
}