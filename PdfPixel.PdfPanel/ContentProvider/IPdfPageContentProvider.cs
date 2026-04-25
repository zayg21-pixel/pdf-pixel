using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public interface IPdfPageContentProvider : IDisposable
{
    SemaphoreSlim DocumentLocker { get; }

    PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber);

    int GetPagesCount();

    void RefreshCache(RefreshCacheRequest request);

    ContentLocker<SKPicture> GetExistingContent(int pageNumber);

    ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber);

    void UpdateContent(UpdateContentRequest request);

    PdfPanelPageInfo GetPageInfo(int pageNumber);
}
