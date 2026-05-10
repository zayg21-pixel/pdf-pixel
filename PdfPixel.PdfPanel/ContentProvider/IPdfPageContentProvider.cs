using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

public enum UpdatedContentType
{
    Content,
    Annotations
}

public class PdfContentPictures
{
    public ContentLocker<SKPicture> Content { get; set; }

    public ContentLocker<SKPicture> Annotations { get; set; }
}

public class PageUpdatedArgs
{
    public PageUpdatedArgs(int pageNumber, PdfContentPictures contentPictures, UpdatedContentType updatedContentType)
    {
        PageNumber = pageNumber;
        ContentPictures = contentPictures;
        UpdatedContentType = updatedContentType;
    }

    public int PageNumber { get; }

    public PdfContentPictures ContentPictures { get; }

    public UpdatedContentType UpdatedContentType { get; }
}

public interface IPdfPageContentProvider : IDisposable
{
    object DocumentLocker { get; }

    Action<PageUpdatedArgs> OnPageUpdated { get; set; }

    PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber);

    int GetPagesCount();

    PdfContentPictures GetExistingContentPictures(int pageNumber);

    void UpdateContent(UpdateContentRequest request);

    PdfPanelPageInfo GetPageInfo(int pageNumber);
}
