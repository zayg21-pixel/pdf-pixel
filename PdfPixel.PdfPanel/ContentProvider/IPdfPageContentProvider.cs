using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public interface IPdfPageContentProvider : IDisposable
{
    SemaphoreSlim DocumentLocker { get; }

    int GetPagesCount();

    void RefreshCache(IEnumerable<int> pagesToStore, CancellationTokenSource cancellationTokenSource);

    ContentLocker<SKPicture> GetExistingContent(int pageNumber);

    ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber);

    void UpdateContent(ContentProviderRequest request);

    PdfPanelPageInfo GetPageInfo(int pageNumber);
}
